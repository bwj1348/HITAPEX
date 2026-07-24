namespace HITAPEX.Models.Usb;

/// <summary>
/// USB 设备事件类型枚举，用于设备发现、连接状态变化和数据传输等事件通知。
/// </summary>
public enum DeviceEventType
{
    /// <summary>设备已连接</summary>
    DeviceConnected,
    /// <summary>设备已断开</summary>
    DeviceDisconnected,
    /// <summary>设备连接失败</summary>
    DeviceConnectFailed,
    /// <summary>设备正在重连</summary>
    DeviceReconnecting,
    /// <summary>设备重连失败</summary>
    DeviceReconnectFailed,
    /// <summary>设备已恢复</summary>
    DeviceRecovered,
    /// <summary>收到原始数据</summary>
    RawDataReceived,
    /// <summary>数据发送失败</summary>
    DataSendFailed,
    /// <summary>串口错误</summary>
    SerialError,
    /// <summary>设备发现开始</summary>
    DiscoveryStarted,
    /// <summary>设备发现完成</summary>
    DiscoveryCompleted,
    /// <summary>VID/PID 匹配成功</summary>
    VidPidMatched,
    /// <summary>VID/PID 不匹配</summary>
    VidPidNotMatched
}
