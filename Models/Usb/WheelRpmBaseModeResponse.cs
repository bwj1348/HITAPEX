namespace HITAPEX.Models.Usb;

/// <summary>设备上报的面盘转速灯基础模式属性参数（协议 0x2103 Get 响应）</summary>
public class WheelRpmBaseModeResponse
{
    /// <summary>转速灯基础模式 (0=恒亮, 1=呼吸, 2=彩色循环)</summary>
    public byte BaseMode { get; set; }

    /// <summary>转速灯基础模式显示速度 (0-5)</summary>
    public byte BaseSpeed { get; set; }

    /// <summary>12个LED灯颜色，每个灯3字节RGB。索引0-11对应LED1-LED12</summary>
    public byte[][] LedColors { get; set; } = new byte[12][];

    public WheelRpmBaseModeResponse()
    {
        for (int i = 0; i < 12; i++)
            LedColors[i] = new byte[3];
    }
}
