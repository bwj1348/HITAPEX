using System.Collections.Concurrent;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

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

    public event Action<UsbDeviceInfo>? DeviceConnected;
    public event Action<UsbDeviceInfo>? DeviceDisconnected;
    public event Action<UsbDeviceInfo, byte[]>? RawDataReceived;
    public event Action<UsbDeviceInfo, string>? DeviceError;

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

    public bool IsRunning => _isRunning;

    public UsbSerialManager()
    {
        _logger = new DeviceLogger();

        _discovery = new UsbDeviceDiscovery(_logger);
        _discovery.DeviceArrived += OnDeviceArrived;
        _discovery.DeviceRemoved += OnDeviceRemoved;
    }

    public void RegisterTargetDevice(VidPidPair pair)
    {
        _discovery.AddTargetDevice(pair);
    }

    public void RegisterTargetDevices(IEnumerable<VidPidPair> pairs)
    {
        _discovery.AddTargetDevices(pairs);
    }

    public void UnregisterTargetDevice(VidPidPair pair)
    {
        _discovery.RemoveTargetDevice(pair);
    }

    public IReadOnlyCollection<VidPidPair> GetRegisteredDevices()
    {
        return _discovery.GetTargetDevices();
    }

    public void Start()
    {
        lock (_startStopLock)
        {
            if (_isRunning || _disposed)
                return;

            _isRunning = true;

            _logger.Log(DeviceEventType.DiscoveryStarted, "", "USB串口管理器启动");

            var existingDevices = _discovery.DiscoverDevices();
            foreach (var device in existingDevices)
            {
                OnDeviceArrived(device);
            }

            _discovery.StartHotplugMonitoring();
        }
    }

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

    private void OnChannelRawDataReceived(DeviceSerialChannel channel, byte[] data)
    {
        RawDataReceived?.Invoke(channel.DeviceInfo, data);
    }

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
