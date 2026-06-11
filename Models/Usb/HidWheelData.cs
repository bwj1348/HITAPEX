namespace HITAPEX.Models.Usb;

/// <summary>
/// 面盘设备 HID 上报数据，对应通信协议第7节"面盘USB上报HID数据"格式
/// </summary>
public class HidWheelData
{
    public byte ReportId { get; init; }
    /// <summary>X 轴数据（保留）</summary>
    public ushort X { get; init; }
    /// <summary>Y 轴数据（保留）</summary>
    public ushort Y { get; init; }
    /// <summary>右下拨片数据 0-65535</summary>
    public ushort RightBottomPaddle { get; init; }
    /// <summary>左下拨片数据 0-65535</summary>
    public ushort LeftBottomPaddle { get; init; }
    /// <summary>Z 轴数据（保留）</summary>
    public ushort Z { get; init; }
    /// <summary>RZ 轴数据（保留）</summary>
    public ushort Rz { get; init; }
    /// <summary>方向键，8 个方向，0 表示释放</summary>
    public byte Dpad { get; init; }
    /// <summary>按键位图（bytes 14-21），每位对应一个按键：bit0=按键1, … bit7=按键8</summary>
    public byte[] ButtonBits { get; init; } = new byte[8];

    /// <summary>判断指定物理按键是否被按下（1-based: 1-64）</summary>
    public bool IsButtonPressed(int buttonIndex)
    {
        if (buttonIndex < 1 || buttonIndex > 64) return false;
        int byteIdx = (buttonIndex - 1) / 8;
        int bitIdx = (buttonIndex - 1) % 8;
        return byteIdx < ButtonBits.Length && (ButtonBits[byteIdx] & (1 << bitIdx)) != 0;
    }

    public static HidWheelData? Parse(byte[] data)
    {
        if (data == null || data.Length < 22 || data[0] != 0x01)
            return null;

        var buttonBits = new byte[8];
        Array.Copy(data, 14, buttonBits, 0, 8);

        return new HidWheelData
        {
            ReportId = data[0],
            X = BitConverter.ToUInt16(data, 1),
            Y = BitConverter.ToUInt16(data, 3),
            RightBottomPaddle = BitConverter.ToUInt16(data, 5),
            LeftBottomPaddle = BitConverter.ToUInt16(data, 7),
            Z = BitConverter.ToUInt16(data, 9),
            Rz = BitConverter.ToUInt16(data, 11),
            Dpad = data[13],
            ButtonBits = buttonBits,
        };
    }
}
