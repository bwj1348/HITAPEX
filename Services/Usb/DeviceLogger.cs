using System.Diagnostics;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Usb;

/// <summary>
/// USB 设备通信日志记录器。
/// 仅在 DEBUG 模式下通过 Debug.WriteLine 输出日志，Release 模式下完全不产生任何开销。
/// </summary>
public class DeviceLogger
{
    private bool _isEnabled = true;

    public DeviceLogger()
    {
    }

    public void Log(DeviceEventType eventType, string deviceKey, string message, string? detail = null, Exception? ex = null)
    {
        if (!_isEnabled) return;

        var detailStr = detail != null ? $" | {detail}" : "";
        var exStr = ex != null ? $" | Exception: {ex.Message}" : "";
        Debug.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{eventType}] [{deviceKey}] {message}{detailStr}{exStr}");
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
    }
}
