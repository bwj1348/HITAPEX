using System.Collections.Concurrent;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

/// <summary>
/// USB 串口管理器 —— IUsbSerialManager 的实现，负责目标设备的发现、连接、断开、重连和数据收发。
/// 通过 UsbDeviceDiscovery 进行 WMI 热插拔监控，每个设备分配独立的 DeviceSerialChannel 进行串口通信。
/// </summary>
/// <remarks>
/// 线程安全性：设备集合使用 ConcurrentDictionary，启停通过 _startStopLock 保护。
/// 自动重连：连接失败时以指数退避（1s → 2s → 4s → 8s → 16s）最多重试 5 次。
/// 错误恢复：串口异常时自动触发重连流程。
/// </remarks>
public class UsbSerialManager : IUsbSerialManager
{
    private readonly DeviceLogger _logger;
    private readonly UsbDeviceDiscovery _discovery;
    private readonly ConcurrentDictionary<string, DeviceSerialChannel> _channels = new();
    private readonly ConcurrentDictionary<string, UsbDeviceInfo> _devices = new();
    private readonly object _startStopLock = new();
    private bool _disposed;
    private volatile bool _isRunning;

    private const int DefaultBaudRate = 115200;
    private const int MaxReconnectAttempts = 5;
    private const int ReconnectBaseDelayMs = 1000;
    private const int ReconnectMaxDelayMs = 30000;

    /// <summary>设备首次连接成功时触发</summary>
    public event Action<UsbDeviceInfo>? DeviceConnected;

    /// <summary>设备断开连接时触发</summary>
    public event Action<UsbDeviceInfo>? DeviceDisconnected;

    /// <summary>从设备收到原始数据时触发</summary>
    public event Action<UsbDeviceInfo, byte[]>? RawDataReceived;

    /// <summary>设备发生错误时触发</summary>
    public event Action<UsbDeviceInfo, string>? DeviceError;

    /// <summary>当前已连接的设备列表（仅 State == Connected）</summary>
    public IReadOnlyList<UsbDeviceInfo> ConnectedDevices
    {
        get
        {
            var connected = _devices.Values
                .Where(d => d.State == DeviceConnectionState.Connected)
                .ToList();
            return connected;
        }
    }

    /// <summary>管理器是否正在运行</summary>
    public bool IsRunning => _isRunning;

    /// <summary>初始化 USB 串口管理器</summary>
    public UsbSerialManager()
    {
        _logger = new DeviceLogger();

        _discovery = new UsbDeviceDiscovery(_logger);
        _discovery.DeviceArrived += OnDeviceArrived;
        _discovery.DeviceRemoved += OnDeviceRemoved;
    }

    /// <summary>注册一个目标设备 VID/PID</summary>
    public void RegisterTargetDevice(VidPidPair pair)
    {
        _discovery.AddTargetDevice(pair);
    }

    /// <summary>批量注册目标设备</summary>
    public void RegisterTargetDevices(IEnumerable<VidPidPair> pairs)
    {
        _discovery.AddTargetDevices(pairs);
    }

    /// <summary>注销一个目标设备</summary>
    public void UnregisterTargetDevice(VidPidPair pair)
    {
        _discovery.RemoveTargetDevice(pair);
    }

    /// <summary>获取当前注册的所有目标设备</summary>
    public IReadOnlyCollection<VidPidPair> GetRegisteredDevices()
    {
        return _discovery.GetTargetDevices();
    }

    /// <summary>
    /// 启动管理器 —— 扫描已连接的设备并开始热插拔监控。
    /// </summary>
    public void Start()
    {
        lock (_startStopLock)
        {
            if (_isRunning || _disposed)
                return;

            _isRunning = true;

            _logger.Log(DeviceEventType.DiscoveryStarted, "", "USB串口管理器启动");

            // 先扫描当前已连接的设备
            var existingDevices = _discovery.DiscoverDevices();
            foreach (var device in existingDevices)
            {
                OnDeviceArrived(device);
            }

            // 启动 WMI 热插拔监控（失败时自动降级为轮询）
            _discovery.StartHotplugMonitoring();
        }
    }

    /// <summary>停止管理器 —— 断开所有设备并停止热插拔监控</summary>
    public void Stop()
    {
        lock (_startStopLock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _discovery.StopHotplugMonitoring();
            DisconnectAll();

            _logger.Log(DeviceEventType.DiscoveryCompleted, "", "USB串口管理器已停止");
        }
    }

    /// <summary>
    /// 设备插入事件处理 —— 新设备直接创建通道并连接；已知设备若未连接则尝试重新连接。
    /// </summary>
    private void OnDeviceArrived(UsbDeviceInfo deviceInfo)
    {
        var key = deviceInfo.DeviceKey;

        if (_devices.ContainsKey(key))
        {
            if (_devices[key].State != DeviceConnectionState.Connected &&
                _devices[key].State != DeviceConnectionState.Connecting)
            {
                ConnectDevice(_devices[key]);
            }
            return;
        }

        _devices[key] = deviceInfo;
        ConnectDevice(deviceInfo);
    }

    /// <summary>
    /// 设备拔出事件处理 —— 移除通道并取消事件订阅，触发 DeviceDisconnected。
    /// </summary>
    private void OnDeviceRemoved(UsbDeviceInfo deviceInfo)
    {
        var key = deviceInfo.DeviceKey;

        if (_channels.TryRemove(key, out var channel))
        {
            channel.RawDataReceived -= OnChannelRawDataReceived;
            channel.ErrorOccurred -= OnChannelError;
            channel.StateChanged -= OnChannelStateChanged;
            channel.Dispose();
        }

        if (_devices.TryRemove(key, out var device))
        {
            device.State = DeviceConnectionState.Disconnected;
            DeviceDisconnected?.Invoke(device);
        }
    }

    /// <summary>
    /// 连接到指定设备（创建 DeviceSerialChannel 并打开串口）。
    /// 首次连接失败将自动触发指数退避重连（最多 5 次）。
    /// </summary>
    public bool ConnectDevice(UsbDeviceInfo deviceInfo)
    {
        var key = deviceInfo.DeviceKey;
        deviceInfo.State = DeviceConnectionState.Connecting;

        var channel = new DeviceSerialChannel(deviceInfo, _logger);
        channel.RawDataReceived += OnChannelRawDataReceived;
        channel.ErrorOccurred += OnChannelError;
        channel.StateChanged += OnChannelStateChanged;

        if (channel.Connect(DefaultBaudRate))
        {
            _channels.AddOrUpdate(key, channel, (_, old) =>
            {
                old.Dispose();
                return channel;
            });

            _devices[key] = deviceInfo;
            DeviceConnected?.Invoke(deviceInfo);
            return true;
        }

        _logger.Log(DeviceEventType.DeviceConnectFailed, key,
            "设备首次连接失败，将尝试自动重连");
        _ = TryReconnectAsync(deviceInfo, channel);
        return false;
    }

    /// <summary>断开指定设备连接</summary>
    public void DisconnectDevice(UsbDeviceInfo deviceInfo)
    {
        var key = deviceInfo.DeviceKey;

        if (_channels.TryRemove(key, out var channel))
        {
            channel.RawDataReceived -= OnChannelRawDataReceived;
            channel.ErrorOccurred -= OnChannelError;
            channel.StateChanged -= OnChannelStateChanged;
            channel.Disconnect();
            channel.Dispose();
        }

        if (_devices.TryGetValue(key, out var device))
        {
            device.State = DeviceConnectionState.Disconnected;
            DeviceDisconnected?.Invoke(device);
        }
    }

    /// <summary>断开所有已连接的设备</summary>
    public void DisconnectAll()
    {
        foreach (var key in _channels.Keys.ToList())
        {
            if (_channels.TryRemove(key, out var channel))
            {
                channel.RawDataReceived -= OnChannelRawDataReceived;
                channel.ErrorOccurred -= OnChannelError;
                channel.StateChanged -= OnChannelStateChanged;
                channel.Dispose();
            }
        }

        foreach (var device in _devices.Values)
        {
            device.State = DeviceConnectionState.Disconnected;
        }
    }

    /// <summary>
    /// 自动重连流程 —— 指数退避延迟（1s → 2s → 4s → 8s → 16s），最多 5 次尝试。
    /// 成功则触发 DeviceConnected 事件，失败则将设备状态设为 Error。
    /// </summary>
    private async Task TryReconnectAsync(UsbDeviceInfo deviceInfo, DeviceSerialChannel channel)
    {
        var attempts = 0;

        while (attempts < MaxReconnectAttempts && _isRunning && !_disposed)
        {
            attempts++;
            deviceInfo.ReconnectAttempts = attempts;
            deviceInfo.State = DeviceConnectionState.Reconnecting;

            var delayMs = Math.Min(ReconnectBaseDelayMs * (int)Math.Pow(2, attempts - 1), ReconnectMaxDelayMs);

            _logger.Log(DeviceEventType.DeviceReconnecting, deviceInfo.DeviceKey,
                $"第 {attempts}/{MaxReconnectAttempts} 次重连尝试，等待 {delayMs}ms");

            await Task.Delay(delayMs);

            if (!_isRunning || _disposed)
                break;

            if (channel.Connect(DefaultBaudRate))
            {
                _channels.AddOrUpdate(deviceInfo.DeviceKey, channel, (_, old) =>
                {
                    old.Dispose();
                    return channel;
                });

                _devices[deviceInfo.DeviceKey] = deviceInfo;
                DeviceConnected?.Invoke(deviceInfo);
                _logger.Log(DeviceEventType.DeviceRecovered, deviceInfo.DeviceKey, "设备重连成功");
                return;
            }
        }

        _logger.Log(DeviceEventType.DeviceReconnectFailed, deviceInfo.DeviceKey,
            $"设备重连失败，已达最大尝试次数 {MaxReconnectAttempts}");

        deviceInfo.State = DeviceConnectionState.Error;
        channel.Dispose();
    }

    /// <summary>通道原始数据回调 → 转发到 RawDataReceived 事件</summary>
    private void OnChannelRawDataReceived(DeviceSerialChannel channel, byte[] data)
    {
        RawDataReceived?.Invoke(channel.DeviceInfo, data);
    }

    /// <summary>
    /// 通道错误回调 —— 触发 DeviceError 事件，若非正在重连则自动启动重连流程。
    /// </summary>
    private void OnChannelError(DeviceSerialChannel channel, string error)
    {
        var deviceInfo = channel.DeviceInfo;
        DeviceError?.Invoke(deviceInfo, error);

        if (_isRunning && !_disposed &&
            deviceInfo.State != DeviceConnectionState.Reconnecting)
        {
            _logger.Log(DeviceEventType.DeviceReconnecting, deviceInfo.DeviceKey,
                $"设备异常，自动触发重连: {error}");

            _channels.TryRemove(deviceInfo.DeviceKey, out _);
            channel.RawDataReceived -= OnChannelRawDataReceived;
            channel.ErrorOccurred -= OnChannelError;
            channel.StateChanged -= OnChannelStateChanged;

            var newChannel = new DeviceSerialChannel(deviceInfo, _logger);
            newChannel.RawDataReceived += OnChannelRawDataReceived;
            newChannel.ErrorOccurred += OnChannelError;
            newChannel.StateChanged += OnChannelStateChanged;

            _ = TryReconnectAsync(deviceInfo, newChannel);
        }
    }

    private void OnChannelStateChanged(DeviceSerialChannel channel,
        DeviceConnectionState oldState, DeviceConnectionState newState)
    {
        _devices[channel.DeviceInfo.DeviceKey] = channel.DeviceInfo;
    }

    public bool SendToDevice(string deviceKey, byte[] data)
    {
        if (_channels.TryGetValue(deviceKey, out var channel))
            return channel.Send(data);
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _discovery.Dispose();
    }
}
