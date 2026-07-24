using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HITAPEX.Models.Usb;
using Microsoft.Win32.SafeHandles;

namespace HITAPEX.Services.Usb;

/// <summary>
/// Windows HID 设备服务 —— IHidService 的实现，独立于串口连接管理 HID 设备的数据读取。
/// 通过 setupapi.dll 枚举系统 HID 设备，为匹配的踏板/基座/面盘建立读取通道，
/// 解析 HID 报告后通过事件向外抛出解码数据。
/// </summary>
/// <remarks>
/// 线程模型：DevicePollLoop 每 2 秒扫描一次新设备；每个设备有独立的 ReadLoop 异步循环（5ms 间隔）。
/// 与串口连接（UsbSerialManager）并行运行，互不干扰。
/// HID 数据仅来自踏板(Pedal, reportId=0x01)、基座(Base, reportId=0x11)和面盘(Wheel, reportId=0x01)。
/// </remarks>
public class HidService : IHidService
{
    private readonly ConcurrentDictionary<string, HidChannel> _channels = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>HID 设备连接事件</summary>
    public event Action<UsbDeviceInfo>? HDeviceConnected;
    /// <summary>HID 设备断开事件</summary>
    public event Action<UsbDeviceInfo>? HDeviceDisconnected;
    /// <summary>踏板 HID 数据到达事件</summary>
    public event Action<UsbDeviceInfo, HidPedalData>? PedalDataReceived;
    /// <summary>基座 HID 数据到达事件</summary>
    public event Action<UsbDeviceInfo, HidBaseData>? BaseDataReceived;
    /// <summary>面盘 HID 数据到达事件（面盘直连 USB 时）</summary>
    public event Action<UsbDeviceInfo, HidWheelData>? WheelDataReceived;

    /// <summary>当前已连接的 HID 设备列表</summary>
    public IReadOnlyList<UsbDeviceInfo> ConnectedHidDevices =>
        _channels.Values
            .Where(c => c.State == DeviceConnectionState.Connected)
            .Select(c => c.DeviceInfo)
            .ToList().AsReadOnly();

    /// <summary>是否正在运行</summary>
    public bool IsRunning => !_disposed && _cts != null && !_cts.IsCancellationRequested;

    /// <summary>启动 HID 设备发现与轮询</summary>
    public void Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HidService));

        if (_cts != null)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        // 定期发现设备并维护通道
        _ = Task.Run(() => DevicePollLoop(token), token);
    }

    /// <summary>停止 HID 设备发现和所有读取通道</summary>
    public void Stop()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        if (cts == null)
            return;

        cts.Cancel();

        foreach (var kvp in _channels.ToList())
        {
            if (_channels.TryRemove(kvp.Key, out var channel))
            {
                channel.Dispose();
            }
        }

        // 延迟销毁 CancellationTokenSource，避免后台任务访问已释放的 Token
        Task.Delay(1000).ContinueWith(_ => cts.Dispose(), TaskScheduler.Default);
    }

    /// <summary>释放所有资源</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    /// <summary>
    /// 设备轮询循环 —— 每 2 秒扫描一次 HID 设备列表，自动连接新设备并检测断开的设备。
    /// </summary>
    private void DevicePollLoop(CancellationToken token)
    {
        var knownDeviceKeys = new HashSet<string>();

        while (!token.IsCancellationRequested)
        {
            try
            {
                var discoveredDevices = DiscoverHidDevices();
                var currentKeys = new HashSet<string>();

                // 连接新发现的设备
                foreach (var (vid, pid, path) in discoveredDevices)
                {
                    var key = $"HID_{vid:X4}:{pid:X4}";
                    currentKeys.Add(key);

                    if (_channels.ContainsKey(key))
                        continue;

                    var descriptor = DeviceRegistry.FindByVidPid(vid, pid);
                    if (descriptor == null || !descriptor.IsNormalMode(vid, pid))
                        continue;

                    var deviceInfo = new UsbDeviceInfo
                    {
                        Vid = vid,
                        Pid = pid,
                        PortName = key,
                        Name = descriptor.ModelName,
                        Description = $"HID {path}",
                    };

                    var channel = new HidChannel(deviceInfo, path);
                    if (channel.Connect())
                    {
                        _channels[key] = channel;
                        Debug.WriteLine($"[HID] 已连接: {descriptor.ModelName} ({key})");

                        // 触发连接事件
                        HDeviceConnected?.Invoke(deviceInfo);

                        // 为每个通道启动独立异步读取循环
                        _ = ReadLoop(channel, descriptor.DeviceType, token);
                    }
                    else
                    {
                        channel.Dispose();
                    }
                }

                // 检测断开连接的设备
                foreach (var kvp in _channels.ToList())
                {
                    if (kvp.Value.State == DeviceConnectionState.Error ||
                        kvp.Value.State == DeviceConnectionState.Disconnected)
                    {
                        if (_channels.TryRemove(kvp.Key, out var ch))
                        {
                            HDeviceDisconnected?.Invoke(ch.DeviceInfo);
                            ch.Dispose();
                        }
                    }
                }

                // 检测物理拔出的设备（设备列表中消失了）
                var removedKeys = knownDeviceKeys.Where(k => !currentKeys.Contains(k)).ToList();
                foreach (var key in removedKeys)
                {
                    knownDeviceKeys.Remove(key);
                    if (_channels.TryRemove(key, out var ch))
                    {
                        Debug.WriteLine($"[HID] 设备已断开: {ch.DeviceInfo.Name} ({key})");
                        HDeviceDisconnected?.Invoke(ch.DeviceInfo);
                        ch.Dispose();
                    }
                }

                // 更新已知设备集合
                foreach (var k in currentKeys)
                    knownDeviceKeys.Add(k);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HID] 设备轮询异常: {ex.Message}");
            }

            try { Task.Delay(2000, token).Wait(token); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    /// <summary>
    /// 单个设备的异步读取循环 —— 持续调用 HidChannel.Read()，解析数据后触发对应事件。
    /// 间隔 5ms，错误时将通道状态设为 Error 并退出。
    /// </summary>
    private async Task ReadLoop(HidChannel channel, DeviceType deviceType, CancellationToken token)
    {
        while (!token.IsCancellationRequested &&
               channel.State == DeviceConnectionState.Connected)
        {
            try
            {
                var data = channel.Read();
                if (data != null && data.Length > 0)
                {
                    ProcessData(channel.DeviceInfo, deviceType, data);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HID] 读取异常 [{channel.DeviceInfo.PortName}]: {ex.Message}");
                channel.State = DeviceConnectionState.Error;
                break;
            }

            try { await Task.Delay(5, token); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    /// <summary>
    /// 根据设备类型和 HID 报告 ID 解析原始数据，触发对应的解码数据事件。
    /// - 踏板(DeviceType.Pedal) + reportId=0x01 → HidPedalData → PedalDataReceived
    /// - 基座(DeviceType.Base) + reportId=0x11 → HidBaseData → BaseDataReceived
    /// - 面盘(DeviceType.Wheel) + reportId=0x01 → HidWheelData → WheelDataReceived
    /// </summary>
    private void ProcessData(UsbDeviceInfo device, DeviceType deviceType, byte[] data)
    {
        if (data.Length == 0) return;

        var reportId = data[0];

        switch (deviceType)
        {
            case DeviceType.Pedal when reportId == 0x01:
                var pedalData = HidPedalData.Parse(data);
                if (pedalData != null)
                    PedalDataReceived?.Invoke(device, pedalData);
                break;

            case DeviceType.Base when reportId == 0x11:
                var baseData = HidBaseData.Parse(data);
                if (baseData != null)
                    BaseDataReceived?.Invoke(device, baseData);
                break;

            case DeviceType.Wheel when reportId == 0x01:
                var wheelData = HidWheelData.Parse(data);
                if (wheelData != null)
                    WheelDataReceived?.Invoke(device, wheelData);
                break;
        }
    }

    /// <summary>
    /// 枚举系统 HID 设备列表 —— 通过 SetupDi* API 遍历所有 HID GUID 的设备接口，
    /// 过滤出注册表中匹配的 VID/PID 设备。
    /// </summary>
    private static List<(int vid, int pid, string path)> DiscoverHidDevices()
    {
        var result = new List<(int vid, int pid, string path)>();

        var hidGuid = HidNative.HidGuid;
        var deviceInfoSet = HidNative.SetupDiGetClassDevs(
            ref hidGuid, null, IntPtr.Zero,
            HidNative.DigcfPresent | HidNative.DigcfDeviceInterface);

        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
            return result;

        try
        {
            var interfaceData = new HidNative.SpDeviceInterfaceData();
            interfaceData.Size = Marshal.SizeOf(interfaceData);

            for (uint i = 0;
                HidNative.SetupDiEnumDeviceInterfaces(
                    deviceInfoSet, IntPtr.Zero, ref hidGuid, i, ref interfaceData);
                i++)
            {
                uint requiredSize;
                HidNative.SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet, ref interfaceData, IntPtr.Zero, 0,
                    out requiredSize, IntPtr.Zero);

                var detailDataPtr = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    Marshal.WriteInt32(detailDataPtr, Environment.Is64BitProcess ? 8 : 6);
                    if (HidNative.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet, ref interfaceData, detailDataPtr,
                        requiredSize, out _, IntPtr.Zero))
                    {
                        var devicePath = Marshal.PtrToStringAuto(
                            detailDataPtr + 4); // skip cbSize (4 bytes)
                        if (devicePath != null)
                        {
                            var (vid, pid) = GetVidPidFromDevice(devicePath);
                            if (vid > 0)
                                result.Add((vid, pid, devicePath!));
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detailDataPtr);
                }
            }
        }
        finally
        {
            HidNative.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return result;
    }

    /// <summary>通过设备路径打开 HID 设备句柄并读取 VID/PID 属性</summary>
    private static (int vid, int pid) GetVidPidFromDevice(string devicePath)
    {
        using var handle = HidNative.CreateFile(
            devicePath, 0, HidNative.FileShareRead | HidNative.FileShareWrite,
            IntPtr.Zero, HidNative.OpenExisting, 0, IntPtr.Zero);

        if (handle.IsInvalid)
            return (0, 0);

        var attrs = new HidNative.HidAttributes { Size = Marshal.SizeOf<HidNative.HidAttributes>() };
        if (HidNative.HidD_GetAttributes(handle, ref attrs))
            return (attrs.VendorId, attrs.ProductId);

        return (0, 0);
    }
}

/// <summary>
/// 单个 HID 设备的读取通道 —— 封装设备句柄的打开、HID 报告大小查询和同步读取。
/// 使用 ArrayPool 减少内存分配。
/// </summary>
internal class HidChannel : IDisposable
{
    private SafeFileHandle? _handle;
    /// <summary>HID 输入报告字节长度（由 HidP_GetCaps 获取）</summary>
    private int _reportSize;

    /// <summary>设备信息</summary>
    public UsbDeviceInfo DeviceInfo { get; }
    /// <summary>设备路径（如 \\?\hid#vid_xxxx&pid_xxxx#...）</summary>
    public string DevicePath { get; }
    /// <summary>当前连接状态</summary>
    public DeviceConnectionState State { get; set; } = DeviceConnectionState.Disconnected;

    /// <summary>
    /// 初始化 HID 通道。
    /// </summary>
    /// <param name="deviceInfo">设备信息</param>
    /// <param name="devicePath">HID 设备路径</param>
    public HidChannel(UsbDeviceInfo deviceInfo, string devicePath)
    {
        DeviceInfo = deviceInfo;
        DevicePath = devicePath;
    }

    /// <summary>
    /// 打开 HID 设备句柄并查询输入报告长度。
    /// 使用 GenericRead 权限和 FILE_SHARE_READ | FILE_SHARE_WRITE。
    /// </summary>
    /// <returns>是否成功打开</returns>
    public bool Connect()
    {
        try
        {
            _handle = HidNative.CreateFile(
                DevicePath, HidNative.GenericRead, HidNative.FileShareRead | HidNative.FileShareWrite,
                IntPtr.Zero, HidNative.OpenExisting, 0, IntPtr.Zero);

            if (_handle.IsInvalid)
                return false;

            // 获取报告大小
            if (HidNative.HidD_GetPreparsedData(_handle, out var preparsedData))
            {
                try
                {
                    var caps = new HidNative.HidpCaps();
                    if (HidNative.HidP_GetCaps(preparsedData, out caps))
                    {
                        _reportSize = caps.InputReportByteLength;
                    }
                }
                finally
                {
                    HidNative.HidD_FreePreparsedData(preparsedData);
                }
            }

            State = DeviceConnectionState.Connected;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 同步读取 HID 输入报告。使用 ArrayPool 租用缓冲区，读取完成后拷贝结果并归还。
    /// </summary>
    /// <returns>读取到的原始字节数组（失败或超时返回 null）</returns>
    public byte[]? Read()
    {
        if (_handle == null || State != DeviceConnectionState.Connected)
            return null;

        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(_reportSize, 64));
        try
        {
            if (HidNative.ReadFile(_handle, buffer, (uint)Math.Max(_reportSize, 64),
                    out uint bytesRead, IntPtr.Zero))
            {
                if (bytesRead > 0)
                {
                    var result = new byte[bytesRead];
                    Array.Copy(buffer, result, bytesRead);
                    return result;
                }
            }
        }
        catch
        {
            State = DeviceConnectionState.Error;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return null;
    }

    /// <summary>释放 HID 设备句柄</summary>
    public void Dispose()
    {
        State = DeviceConnectionState.Disconnected;
        _handle?.Close();
        _handle?.Dispose();
        _handle = null;
    }
}
