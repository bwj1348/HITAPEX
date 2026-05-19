using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

public interface IUsbSerialManager : IDisposable
{
    event Action<UsbDeviceInfo>? DeviceConnected;
    event Action<UsbDeviceInfo>? DeviceDisconnected;
    event Action<UsbDeviceInfo, byte[]>? RawDataReceived;
    event Action<DeviceLogEntry>? LogEntryAdded;
    event Action<UsbDeviceInfo, string>? DeviceError;

    IReadOnlyList<UsbDeviceInfo> ConnectedDevices { get; }
    bool IsRunning { get; }

    void RegisterTargetDevice(VidPidPair pair);
    void RegisterTargetDevices(IEnumerable<VidPidPair> pairs);
    void UnregisterTargetDevice(VidPidPair pair);
    IReadOnlyCollection<VidPidPair> GetRegisteredDevices();

    void Start();
    void Stop();
    bool ConnectDevice(UsbDeviceInfo deviceInfo);
    void DisconnectDevice(UsbDeviceInfo deviceInfo);
    void DisconnectAll();

    bool SendToDevice(string deviceKey, byte[] data);

    IReadOnlyList<DeviceLogEntry> GetRecentLogs(int count = 100);
    void SetLoggingEnabled(bool enabled);
}
