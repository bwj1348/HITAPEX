namespace HITAPEX.Models.Usb;

/// <summary>设备上报的面盘转速灯转速指示属性参数（协议 0x2104 Get 响应）</summary>
public class WheelRpmIndicatorResponse
{
    /// <summary>触发转速模式 (0=百分比, 1=转速RPM)</summary>
    public byte TriggerMode { get; set; }

    /// <summary>12个LED的触发转速值，百分比模式时0-100，RPM模式时0-65535</summary>
    public ushort[] TriggerValues { get; set; } = new ushort[12];

    /// <summary>12个LED灯颜色，每个灯3字节RGB。索引0-11对应LED1-LED12</summary>
    public byte[][] LedColors { get; set; } = new byte[12][];

    /// <summary>初始化 12 个 LED 的触发值和颜色数组</summary>
    public WheelRpmIndicatorResponse()
    {
        for (int i = 0; i < 12; i++)
            LedColors[i] = new byte[3];
    }
}
