namespace HITAPEX.Models.Usb;

/// <summary>
/// USB 设备连接状态枚举。
/// </summary>
public enum DeviceConnectionState
{
    /// <summary>未连接</summary>
    Disconnected,
    /// <summary>正在连接</summary>
    Connecting,
    /// <summary>已连接</summary>
    Connected,
    /// <summary>正在断开</summary>
    Disconnecting,
    /// <summary>正在重连</summary>
    Reconnecting,
    /// <summary>连接错误</summary>
    Error
}
