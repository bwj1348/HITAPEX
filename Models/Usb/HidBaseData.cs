namespace HITAPEX.Models.Usb;

/// <summary>
/// 基座设备 HID 上报数据，对应通信协议第6节"基座上报HID数据"格式
/// </summary>
public class HidBaseData
{
    /// <summary>HID 报告 ID</summary>
    public byte ReportId { get; init; }

    /// <summary>转向数据 0-65535，归中值为 0x8000</summary>
    public ushort Steering { get; init; }

    /// <summary>Y 轴数据（保留通道）</summary>
    public ushort Y { get; init; }

    /// <summary>左拨片轴数据 0-65535</summary>
    public ushort LeftPaddle { get; init; }

    /// <summary>油门轴数据 0-65535</summary>
    public ushort Throttle { get; init; }

    /// <summary>刹车轴数据 0-65535</summary>
    public ushort Brake { get; init; }

    /// <summary>离合轴数据 0-65535</summary>
    public ushort Clutch { get; init; }

    /// <summary>右拨片轴数据 0-65535</summary>
    public ushort RightPaddle { get; init; }

    /// <summary>滑块轴数据 0-65535</summary>
    public ushort Slider { get; init; }

    /// <summary>方向键 0-8，0 表示释放</summary>
    public byte DirectionKeys1 { get; init; }

    /// <summary>按键 1-128 位掩码（bytes 18-33）</summary>
    public byte[] ButtonBits { get; init; } = new byte[16];

    /// <summary>方向键2 0-8，0 表示释放</summary>
    public byte DirectionKeys2 { get; init; }

    /// <summary>从原始 HID 数据包解析基座数据</summary>
    /// <param name="data">HID 原始数据缓冲区</param>
    /// <returns>解析成功返回 HidBaseData 实例，失败返回 null</returns>
    public static HidBaseData? Parse(byte[] data)
    {
        if (data == null || data.Length < 42 || data[0] != 0x11)
            return null;

        var buttonBits = new byte[16];
        Array.Copy(data, 18, buttonBits, 0, 16);

        return new HidBaseData
        {
            ReportId = data[0],
            Steering = BitConverter.ToUInt16(data, 1),
            Y = BitConverter.ToUInt16(data, 3),
            LeftPaddle = BitConverter.ToUInt16(data, 5),
            Throttle = BitConverter.ToUInt16(data, 7),
            Brake = BitConverter.ToUInt16(data, 9),
            Clutch = BitConverter.ToUInt16(data, 11),
            RightPaddle = BitConverter.ToUInt16(data, 13),
            Slider = BitConverter.ToUInt16(data, 15),
            DirectionKeys1 = data[17],
            ButtonBits = buttonBits,
            DirectionKeys2 = data[34],
        };
    }
}
