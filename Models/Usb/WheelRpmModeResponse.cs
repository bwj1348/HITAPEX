namespace HITAPEX.Models.Usb;

/// <summary>设备上报的面盘转速灯模式等属性参数（协议 0x2105 Get 响应）</summary>
public class WheelRpmModeResponse
{
    /// <summary>转速灯亮度 (0-100)</summary>
    public byte Brightness { get; set; }

    /// <summary>遥测模式 (0=遥测模式, 1=关闭遥测/基础模式)</summary>
    public byte TelemetryOff { get; set; }

    /// <summary>转速灯光模式 (0=序列, 1=扩散, 2=汇聚)</summary>
    public byte LightMode { get; set; }

    /// <summary>转速灯爆闪颜色模式 (0=与转速灯颜色一致, 1=自定义, 2=关闭)</summary>
    public byte StrobeMode { get; set; }

    /// <summary>转速灯爆闪速度 (0-5)</summary>
    public byte StrobeSpeed { get; set; }

    /// <summary>转速灯爆闪自定义颜色R分量 (0-255)</summary>
    public byte StrobeColorR { get; set; }

    /// <summary>转速灯爆闪自定义颜色G分量 (0-255)</summary>
    public byte StrobeColorG { get; set; }

    /// <summary>转速灯爆闪自定义颜色B分量 (0-255)</summary>
    public byte StrobeColorB { get; set; }

    /// <summary>爆闪触发值 (0-100)</summary>
    public byte StrobeTriggerValue { get; set; }
}
