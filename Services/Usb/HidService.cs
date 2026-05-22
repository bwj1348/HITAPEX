using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HITAPEX.Models.Usb;
using Microsoft.Win32.SafeHandles;

namespace HITAPEX.Services.Usb;

/// <summary>
/// Windows HID 设备服务，独立于串口连接管理 HID 设备的数据读取。
/// </summary>
public class HidService : IHidService
{
    private readonly ConcurrentDictionary<string, HidChannel> _channels = new();
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public event Action<UsbDeviceInfo, HidPedalData>? PedalDataReceived;
    public event Action<UsbDeviceInfo, HidBaseData>? BaseDataReceived;

    public IReadOnlyList<UsbDeviceInfo> ConnectedHidDevices =>
        _channels.Values
            .Where(c => c.State == DeviceConnectionState.Connected)
            .Select(c => c.DeviceInfo)
            .ToList().AsReadOnly();

    public bool IsRunning => !_disposed && _cts != null && !_cts.IsCancellationRequested;

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

    public void Stop()
    {
        if (_cts == null)
            return;

        _cts.Cancel();

        foreach (var kvp in _channels.ToList())
        {
            if (_channels.TryRemove(kvp.Key, out var channel))
            {
                channel.Dispose();
            }
        }

        _cts.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void DevicePollLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var discoveredDevices = DiscoverHidDevices();

                // 连接新发现的设备
                foreach (var (vid, pid, path) in discoveredDevices)
                {
                    var key = $"HID_{vid:X4}:{pid:X4}";
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

                        // 为每个通道启动独立读取循环
                        _ = Task.Run(() => ReadLoop(channel, descriptor.DeviceType, token), token);
                    }
                    else
                    {
                        channel.Dispose();
                    }
                }

                // 清理已失效的通道
                foreach (var kvp in _channels.ToList())
                {
                    if (kvp.Value.State == DeviceConnectionState.Error ||
                        kvp.Value.State == DeviceConnectionState.Disconnected)
                    {
                        if (_channels.TryRemove(kvp.Key, out var ch))
                            ch.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HID] 设备轮询异常: {ex.Message}");
            }

            try { Task.Delay(2000, token).Wait(token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void ReadLoop(HidChannel channel, DeviceType deviceType, CancellationToken token)
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

            try { Task.Delay(5, token).Wait(token); }
            catch (OperationCanceledException) { break; }
        }
    }

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
        }
    }

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

/// <summary>单个 HID 设备的读取通道</summary>
internal class HidChannel : IDisposable
{
    private SafeFileHandle? _handle;
    private int _reportSize;

    public UsbDeviceInfo DeviceInfo { get; }
    public string DevicePath { get; }
    public DeviceConnectionState State { get; set; } = DeviceConnectionState.Disconnected;

    public HidChannel(UsbDeviceInfo deviceInfo, string devicePath)
    {
        DeviceInfo = deviceInfo;
        DevicePath = devicePath;
    }

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

    public byte[]? Read()
    {
        if (_handle == null || State != DeviceConnectionState.Connected)
            return null;

        var buffer = new byte[Math.Max(_reportSize, 64)];
        try
        {
            if (HidNative.ReadFile(_handle, buffer, (uint)buffer.Length,
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

        return null;
    }

    public void Dispose()
    {
        State = DeviceConnectionState.Disconnected;
        _handle?.Close();
        _handle?.Dispose();
        _handle = null;
    }
}
