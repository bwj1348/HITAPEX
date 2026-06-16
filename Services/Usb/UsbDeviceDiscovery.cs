using System.Management;
using System.Runtime.Versioning;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

[SupportedOSPlatform("windows")]
public class UsbDeviceDiscovery : IDisposable
{
    private readonly DeviceLogger _logger;
    private readonly HashSet<VidPidPair> _targetDevices = new();
    private ManagementEventWatcher? _arrivalWatcher;
    private ManagementEventWatcher? _removalWatcher;
    private CancellationTokenSource? _pollCts;
    private bool _disposed;

    public event Action<UsbDeviceInfo>? DeviceArrived;
    public event Action<UsbDeviceInfo>? DeviceRemoved;

    public UsbDeviceDiscovery(DeviceLogger logger)
    {
        _logger = logger;
    }

    public void AddTargetDevice(VidPidPair pair)
    {
        _targetDevices.Add(pair);
        _logger.Log(DeviceEventType.DiscoveryStarted, "", $"添加目标设备: {pair}");
    }

    public void AddTargetDevices(IEnumerable<VidPidPair> pairs)
    {
        foreach (var pair in pairs)
            AddTargetDevice(pair);
    }

    public void RemoveTargetDevice(VidPidPair pair)
    {
        _targetDevices.Remove(pair);
    }

    public void ClearTargetDevices()
    {
        _targetDevices.Clear();
    }

    public IReadOnlyCollection<VidPidPair> GetTargetDevices() => _targetDevices.ToList().AsReadOnly();

    public IReadOnlyList<UsbDeviceInfo> DiscoverDevices()
    {
        var found = new List<UsbDeviceInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"SELECT DeviceID, Name, PNPDeviceID, Description
                  FROM Win32_PnPEntity
                  WHERE PNPClass = 'Ports' AND Name LIKE '%(COM%'");

            foreach (var obj in searcher.Get())
            {
                var pnpDeviceId = obj["PNPDeviceID"]?.ToString() ?? "";
                var name = obj["Name"]?.ToString() ?? "";
                var description = obj["Description"]?.ToString() ?? "";
                var deviceId = obj["DeviceID"]?.ToString() ?? "";

                if (!TryParseVidPid(pnpDeviceId, out int vid, out int pid))
                    continue;

                var pair = new VidPidPair(vid, pid);
                if (!_targetDevices.Contains(pair))
                    continue;

                var portName = ExtractComPort(name);
                if (string.IsNullOrEmpty(portName))
                    continue;

                var deviceInfo = new UsbDeviceInfo
                {
                    DeviceId = deviceId,
                    PortName = portName,
                    Vid = vid,
                    Pid = pid,
                    Name = name,
                    Description = description,
                    SerialNumber = ExtractSerialNumber(pnpDeviceId)
                };

                _logger.Log(DeviceEventType.VidPidMatched, deviceInfo.DeviceKey,
                    $"发现匹配设备: {name}", $"VID={vid:X4}, PID={pid:X4}, Port={portName}");

                found.Add(deviceInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(DeviceEventType.DiscoveryCompleted, "", "设备发现过程异常", null, ex);
        }

        _logger.Log(DeviceEventType.DiscoveryCompleted, "",
            $"设备发现完成，共发现 {found.Count} 个匹配设备");

        return found;
    }

    public void StartHotplugMonitoring(int pollIntervalMs = 2000)
    {
        StopHotplugMonitoring();

        try
        {
            var scope = new ManagementScope(@"\\.\root\CIMV2");
            var qArrival = new WqlEventQuery(
                "SELECT * FROM __InstanceCreationEvent WITHIN 2 " +
                "WHERE TargetInstance ISA 'Win32_PnPEntity' AND TargetInstance.PNPClass = 'Ports'");

            _arrivalWatcher = new ManagementEventWatcher(scope, qArrival);
            _arrivalWatcher.EventArrived += OnDeviceArrived;
            _arrivalWatcher.Start();

            var qRemoval = new WqlEventQuery(
                "SELECT * FROM __InstanceDeletionEvent WITHIN 2 " +
                "WHERE TargetInstance ISA 'Win32_PnPEntity' AND TargetInstance.PNPClass = 'Ports'");

            _removalWatcher = new ManagementEventWatcher(scope, qRemoval);
            _removalWatcher.EventArrived += OnDeviceRemoved;
            _removalWatcher.Start();
        }
        catch (Exception ex)
        {
            _logger.Log(DeviceEventType.DiscoveryStarted, "",
                "WMI热插拔监控启动失败，降级为轮询模式", null, ex);

            StartPollingFallback(pollIntervalMs);
        }
    }

    private void StartPollingFallback(int intervalMs)
    {
        _pollCts = new CancellationTokenSource();
        var knownDevices = new HashSet<string>();

        _ = Task.Run(async () =>
        {
            while (!_pollCts.Token.IsCancellationRequested)
            {
                try
                {
                    var current = DiscoverDevices();
                    var currentKeys = new HashSet<string>(current.Select(d => d.PortName));

                    foreach (var device in current)
                    {
                        if (knownDevices.Add(device.PortName))
                            DeviceArrived?.Invoke(device);
                    }

                    var removed = knownDevices.Where(k => !currentKeys.Contains(k)).ToList();
                    foreach (var port in removed)
                    {
                        knownDevices.Remove(port);
                        DeviceRemoved?.Invoke(new UsbDeviceInfo { PortName = port });
                    }
                }
                catch { }

                await Task.Delay(intervalMs, _pollCts.Token);
            }
        }, _pollCts.Token);
    }

    public void StopHotplugMonitoring()
    {
        _arrivalWatcher?.Stop();
        _arrivalWatcher?.Dispose();
        _arrivalWatcher = null;

        _removalWatcher?.Stop();
        _removalWatcher?.Dispose();
        _removalWatcher = null;

        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollCts = null;
    }

    private void OnDeviceArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var target = e.NewEvent["TargetInstance"] as ManagementBaseObject;
            var pnpId = target?["PNPDeviceID"]?.ToString() ?? "";
            var name = target?["Name"]?.ToString() ?? "";

            if (!TryParseVidPid(pnpId, out int vid, out int pid))
                return;

            if (!_targetDevices.Contains(new VidPidPair(vid, pid)))
                return;

            var portName = ExtractComPort(name);
            if (string.IsNullOrEmpty(portName))
                return;

            // 延迟等待驱动完全加载（异步延迟，不阻塞 WMI 事件线程）
            Task.Delay(500).Wait();

            var deviceInfo = new UsbDeviceInfo
            {
                DeviceId = target?["DeviceID"]?.ToString() ?? "",
                PortName = portName,
                Vid = vid,
                Pid = pid,
                Name = name,
                Description = target?["Description"]?.ToString() ?? "",
                SerialNumber = ExtractSerialNumber(pnpId)
            };

            _logger.Log(DeviceEventType.DeviceConnected, deviceInfo.DeviceKey,
                $"设备插入: {name}", $"Port={portName}");

            DeviceArrived?.Invoke(deviceInfo);
        }
        catch (Exception ex)
        {
            _logger.Log(DeviceEventType.DeviceConnectFailed, "", "处理设备插入事件异常", null, ex);
        }
    }

    private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var target = e.NewEvent["TargetInstance"] as ManagementBaseObject;
            var pnpId = target?["PNPDeviceID"]?.ToString() ?? "";
            var name = target?["Name"]?.ToString() ?? "";

            if (!TryParseVidPid(pnpId, out int vid, out int pid))
                return;

            var portName = ExtractComPort(name);
            if (string.IsNullOrEmpty(portName))
                return;

            var deviceInfo = new UsbDeviceInfo
            {
                PortName = portName,
                Vid = vid,
                Pid = pid,
                Name = name
            };

            _logger.Log(DeviceEventType.DeviceDisconnected, deviceInfo.DeviceKey,
                $"设备拔出: {name}", $"Port={portName}");

            DeviceRemoved?.Invoke(deviceInfo);
        }
        catch (Exception ex)
        {
            _logger.Log(DeviceEventType.DeviceDisconnected, "", "处理设备拔出事件异常", null, ex);
        }
    }

    private static bool TryParseVidPid(string pnpDeviceId, out int vid, out int pid)
    {
        vid = 0;
        pid = 0;

        if (string.IsNullOrEmpty(pnpDeviceId))
            return false;

        var upper = pnpDeviceId.ToUpperInvariant();
        var vidIndex = upper.IndexOf("VID_", StringComparison.Ordinal);
        var pidIndex = upper.IndexOf("PID_", StringComparison.Ordinal);

        if (vidIndex < 0 || pidIndex < 0)
            return false;

        var vidStr = upper.Substring(vidIndex + 4, 4);
        var pidStr = upper.Substring(pidIndex + 4, 4);

        return int.TryParse(vidStr, System.Globalization.NumberStyles.HexNumber, null, out vid) &&
               int.TryParse(pidStr, System.Globalization.NumberStyles.HexNumber, null, out pid);
    }

    private static string ExtractComPort(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";

        var start = name.LastIndexOf("(COM", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return "";

        var end = name.IndexOf(')', start);
        if (end < 0)
            return "";

        return name.Substring(start + 1, end - start - 1);
    }

    private static string ExtractSerialNumber(string pnpDeviceId)
    {
        if (string.IsNullOrEmpty(pnpDeviceId))
            return "";

        var lastBackslash = pnpDeviceId.LastIndexOf('\\');
        if (lastBackslash < 0 || lastBackslash >= pnpDeviceId.Length - 1)
            return "";

        var serial = pnpDeviceId[(lastBackslash + 1)..];
        return serial.Length > 32 ? serial[..32] : serial;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopHotplugMonitoring();
    }
}
