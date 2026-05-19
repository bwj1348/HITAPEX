namespace HITAPEX.Models.Usb;

public class DeviceLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public DeviceEventType EventType { get; init; }
    public string DeviceKey { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public Exception? Exception { get; init; }

    public override string ToString()
    {
        var detail = Detail != null ? $" | {Detail}" : "";
        var ex = Exception != null ? $" | Exception: {Exception.Message}" : "";
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{EventType}] [{DeviceKey}] {Message}{detail}{ex}";
    }
}
