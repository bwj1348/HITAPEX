namespace HITAPEX.Models.Usb;

/// <summary>
/// 踏板设备 HID 上报数据，对应 PEDAAL_DATA_T 结构体
/// </summary>
public class HidPedalData
{
    public byte ReportId { get; init; }
    public ushort X { get; init; }
    public ushort Y { get; init; }
    public ushort Gas { get; init; }
    public ushort Brake { get; init; }
    public ushort Clutch { get; init; }
    public ushort Rz { get; init; }
    public ushort[] User { get; init; } = new ushort[8];

    /// <summary>油门位置百分比 0-100</summary>
    public double GasPercent => Gas / 65535.0 * 100.0;
    /// <summary>刹车位置百分比 0-100</summary>
    public double BrakePercent => Brake / 65535.0 * 100.0;
    /// <summary>离合位置百分比 0-100</summary>
    public double ClutchPercent => Clutch / 65535.0 * 100.0;

    public static HidPedalData? Parse(byte[] data)
    {
        if (data == null || data.Length < 29 || data[0] != 0x01)
            return null;

        return new HidPedalData
        {
            ReportId = data[0],
            X = BitConverter.ToUInt16(data, 1),
            Y = BitConverter.ToUInt16(data, 3),
            Gas = BitConverter.ToUInt16(data, 5),
            Brake = BitConverter.ToUInt16(data, 7),
            Clutch = BitConverter.ToUInt16(data, 9),
            Rz = BitConverter.ToUInt16(data, 11),
            User = new ushort[]
            {
                BitConverter.ToUInt16(data, 13),
                BitConverter.ToUInt16(data, 15),
                BitConverter.ToUInt16(data, 17),
                BitConverter.ToUInt16(data, 19),
                BitConverter.ToUInt16(data, 21),
                BitConverter.ToUInt16(data, 23),
                BitConverter.ToUInt16(data, 25),
                BitConverter.ToUInt16(data, 27),
            }
        };
    }
}
