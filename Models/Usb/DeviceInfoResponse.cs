namespace HITAPEX.Models.Usb;

public class DeviceInfoResponse
{
    public DeviceType DeviceType { get; set; }
    public int UsbSpeed { get; set; }
    public int NormalFirmwareVersion { get; set; }
    public int BootFirmwareVersion { get; set; }

    // 基座特有字段 — 上报连接的踏板/面盘信息
    public int WheelConnectionStatus { get; set; }    // offset 9
    public int WheelNormalFwVersion { get; set; }     // offset 10-11
    public int WheelBootFwVersion { get; set; }       // offset 12-13
    public int PedalConnectionStatus { get; set; }    // offset 14
    public int PedalNormalFwVersion { get; set; }     // offset 15-16
    public int PedalBootFwVersion { get; set; }       // offset 17-18

    public string VersionString => $"v{NormalFirmwareVersion >> 8}.{NormalFirmwareVersion & 0xFF}";

    public string PedalVersionString => $"v{PedalNormalFwVersion >> 8}.{PedalNormalFwVersion & 0xFF}";

    /// <summary>踏板是否通过基座连接（基座状态字节非零即为已连接）</summary>
    public bool IsPedalConnected => PedalConnectionStatus != 0x00;

    /// <summary>踏板个数: 0=2踏板, 1=3踏板（来自设备信息回复 offset 30）</summary>
    public int PedalCount { get; set; }

    /// <summary>是否为三踏板模式（含离合）</summary>
    public bool HasThreePedals => PedalCount == 1;

    public override string ToString()
        => $"DeviceInfo: Type={DeviceType}, USB={UsbSpeed}, FW={VersionString}, BootFW=v{BootFirmwareVersion >> 8}.{BootFirmwareVersion & 0xFF}";
}
