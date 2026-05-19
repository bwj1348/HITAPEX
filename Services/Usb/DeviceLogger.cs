using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

public class DeviceLogger
{
    private readonly ConcurrentQueue<DeviceLogEntry> _logEntries = new();
    private readonly string _logFilePath;
    private readonly object _fileLock = new();
    private bool _isEnabled = true;

    public event Action<DeviceLogEntry>? LogEntryAdded;

    public int MaxInMemoryEntries { get; set; } = 1000;

    public DeviceLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, $"usb_device_{DateTime.Now:yyyyMMdd}.log");
    }

    public void Log(DeviceEventType eventType, string deviceKey, string message, string? detail = null, Exception? ex = null)
    {
        if (!_isEnabled) return;

        var entry = new DeviceLogEntry
        {
            Timestamp = DateTime.Now,
            EventType = eventType,
            DeviceKey = deviceKey,
            Message = message,
            Detail = detail,
            Exception = ex
        };

        _logEntries.Enqueue(entry);
        while (_logEntries.Count > MaxInMemoryEntries)
            _logEntries.TryDequeue(out _);

        LogEntryAdded?.Invoke(entry);
        WriteToFile(entry);
        Debug.WriteLine(entry.ToString());
    }

    private void WriteToFile(DeviceLogEntry entry)
    {
        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath, entry.ToString() + Environment.NewLine);
            }
        }
        catch
        {
            // 日志写入失败不应影响主流程
        }
    }

    public IReadOnlyList<DeviceLogEntry> GetRecentEntries(int count = 100)
    {
        return _logEntries.TakeLast(count).ToList().AsReadOnly();
    }

    public void Clear()
    {
        _logEntries.Clear();
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
    }
}
