namespace HITAPEX.Models.Usb;

public class UsbDeviceInfo
{
    public string DeviceId { get; init; } = string.Empty;
    public string PortName { get; init; } = string.Empty;
    public int Vid { get; init; }
    public int Pid { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public DeviceConnectionState State { get; set; } = DeviceConnectionState.Disconnected;
    public DateTime? LastConnectedTime { get; set; }
    public int ReconnectAttempts { get; set; }

    private long _totalBytesReceived;
    public long TotalBytesReceived
    {
        get => Interlocked.Read(ref _totalBytesReceived);
        set => Interlocked.Exchange(ref _totalBytesReceived, value);
    }

    internal void IncrementBytesReceived(long delta) => Interlocked.Add(ref _totalBytesReceived, delta);

    public string DeviceKey => $"{Vid:X4}:{Pid:X4}_{PortName}";

    public override string ToString()
        => $"[{DeviceKey}] {Name} ({Description})";
}
