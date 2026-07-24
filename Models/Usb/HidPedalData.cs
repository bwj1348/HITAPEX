namespace HITAPEX.Models.Usb;

/// <summary>
/// 踏板设备 HID 上报数据，对应 PEDAAL_DATA_T 结构体
/// </summary>
public class HidPedalData
{
    /// <summary>HID 报告 ID</summary>
    public byte ReportId { get; init; }

    /// <summary>X 轴数据（保留通道）</summary>
    public ushort X { get; init; }

    /// <summary>Y 轴数据（保留通道）</summary>
    public ushort Y { get; init; }

    /// <summary>油门轴数据 0-65535</summary>
    public ushort Gas { get; init; }

    /// <summary>刹车轴数据 0-65535</summary>
    public ushort Brake { get; init; }

    /// <summary>离合轴数据 0-65535</summary>
    public ushort Clutch { get; init; }

    /// <summary>RZ 轴数据（保留通道）</summary>
    public ushort Rz { get; init; }

    /// <summary>用户自定义轴数据（8 个通道）</summary>
    public ushort[] User { get; init; } = new ushort[8];

    /// <summary>油门位置百分比 0-100</summary>
    public double GasPercent => Gas / 65535.0 * 100.0;

    /// <summary>刹车位置百分比 0-100</summary>
    public double BrakePercent => Brake / 65535.0 * 100.0;

    /// <summary>离合位置百分比 0-100</summary>
    public double ClutchPercent => Clutch / 65535.0 * 100.0;

    /// <summary>从原始 HID 数据包解析踏板数据</summary>
    /// <param name="data">HID 原始数据缓冲区</param>
    /// <returns>解析成功返回 HidPedalData 实例，失败返回 null</returns>
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
