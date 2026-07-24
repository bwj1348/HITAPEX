namespace HITAPEX.Models.Usb;

/// <summary>
/// USB 设备运行时信息，包含设备标识、端口、连接状态和通信统计。
/// </summary>
/// <remarks>
/// 设备连接成功后会创建一个 UsbDeviceInfo 实例来跟踪设备运行状况，
/// 包括连接状态变迁、重连尝试次数和收发字节统计。
/// </remarks>
public class UsbDeviceInfo
{
    /// <summary>设备唯一标识符（通常为设备路径或系统 ID）</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>串口名称（如 COM3）</summary>
    public string PortName { get; init; } = string.Empty;

    /// <summary>USB 供应商 ID（Vendor ID）</summary>
    public int Vid { get; init; }

    /// <summary>USB 产品 ID（Product ID）</summary>
    public int Pid { get; init; }

    /// <summary>设备显示名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>设备描述信息</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>设备序列号</summary>
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>当前连接状态</summary>
    public DeviceConnectionState State { get; set; } = DeviceConnectionState.Disconnected;

    /// <summary>最近一次连接成功的时间</summary>
    public DateTime? LastConnectedTime { get; set; }

    /// <summary>累计重连尝试次数</summary>
    public int ReconnectAttempts { get; set; }

    /// <summary>已接收字节总数（线程安全）</summary>
    private long _totalBytesReceived;

    /// <summary>获取或设置已接收字节总数（线程安全操作）</summary>
    public long TotalBytesReceived
    {
        get => Interlocked.Read(ref _totalBytesReceived);
        set => Interlocked.Exchange(ref _totalBytesReceived, value);
    }

    /// <summary>原子性地增加已接收字节计数</summary>
    internal void IncrementBytesReceived(long delta) => Interlocked.Add(ref _totalBytesReceived, delta);

    /// <summary>设备唯一键，格式为 "VID:PID_端口名"</summary>
    public string DeviceKey => $"{Vid:X4}:{Pid:X4}_{PortName}";

    /// <summary>返回设备简要信息字符串</summary>
    public override string ToString()
        => $"[{DeviceKey}] {Name} ({Description})";
}
