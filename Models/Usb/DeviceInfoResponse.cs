namespace HITAPEX.Models.Usb;

/// <summary>
/// 设备信息响应，对应设备上报的配置信息（协议 0x2010 Get 响应）。
/// 包含设备类型、USB 速率、固件版本及连接的子设备信息（面盘/踏板）。
/// </summary>
public class DeviceInfoResponse
{
    /// <summary>设备类型</summary>
    public DeviceType DeviceType { get; set; }

    /// <summary>USB 速率</summary>
    public int UsbSpeed { get; set; }

    /// <summary>正常运行固件版本号（主版本号 << 8 | 次版本号）</summary>
    public int NormalFirmwareVersion { get; set; }

    /// <summary>Bootloader 固件版本号（主版本号 << 8 | 次版本号）</summary>
    public int BootFirmwareVersion { get; set; }

    // ── 基座特有字段 — 上报连接的踏板/面盘信息 ──

    /// <summary>面盘连接状态（offset 9）</summary>
    public int WheelConnectionStatus { get; set; }

    /// <summary>面盘正常运行固件版本号（offset 10-11）</summary>
    public int WheelNormalFwVersion { get; set; }

    /// <summary>面盘 Bootloader 固件版本号（offset 12-13）</summary>
    public int WheelBootFwVersion { get; set; }

    /// <summary>踏板连接状态（offset 14）</summary>
    public int PedalConnectionStatus { get; set; }

    /// <summary>踏板正常运行固件版本号（offset 15-16）</summary>
    public int PedalNormalFwVersion { get; set; }

    /// <summary>踏板 Bootloader 固件版本号（offset 17-18）</summary>
    public int PedalBootFwVersion { get; set; }

    /// <summary>基座固件版本字符串，如 "v1.0"</summary>
    public string VersionString => $"v{NormalFirmwareVersion >> 8}.{NormalFirmwareVersion & 0xFF}";

    /// <summary>踏板固件版本字符串，如 "v1.0"</summary>
    public string PedalVersionString => $"v{PedalNormalFwVersion >> 8}.{PedalNormalFwVersion & 0xFF}";

    /// <summary>踏板是否通过基座连接（基座状态字节非零即为已连接）</summary>
    public bool IsPedalConnected => PedalConnectionStatus != 0x00;

    /// <summary>踏板个数: 0=2踏板, 1=3踏板（来自设备信息回复 offset 30）</summary>
    public int PedalCount { get; set; }

    /// <summary>是否为三踏板模式（含离合）</summary>
    public bool HasThreePedals => PedalCount == 1;

    /// <summary>返回设备信息摘要字符串</summary>
    public override string ToString()
        => $"DeviceInfo: Type={DeviceType}, USB={UsbSpeed}, FW={VersionString}, BootFW=v{BootFirmwareVersion >> 8}.{BootFirmwareVersion & 0xFF}";
}
