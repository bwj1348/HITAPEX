namespace HITAPEX.Models.Usb;

/// <summary>设备上报的面盘按键灯单独效果属性参数（协议 0x2107 Get 响应）</summary>
public class WheelButtonLightResponse
{
    /// <summary>按键LED灯索引 (0-25)</summary>
    public byte LedIndex { get; set; }

    /// <summary>常亮时LED灯颜色R分量</summary>
    public byte ColorR { get; set; }

    /// <summary>常亮时LED灯颜色G分量</summary>
    public byte ColorG { get; set; }

    /// <summary>常亮时LED灯颜色B分量</summary>
    public byte ColorB { get; set; }

    /// <summary>按键灯遥测功能 (0=关闭, 1=ABS, 2=TC, 3=DRS可用, 4=DRS开启, 5=抱死, 6=维修区限速, 7=打滑)</summary>
    public byte TelemetryFunc { get; set; }

    /// <summary>按键灯遥测闪烁速度或常亮 (0-5=闪烁, 0xFF=常亮)</summary>
    public byte FlashSpeed { get; set; }

    /// <summary>按键灯遥测显示颜色R分量</summary>
    public byte TelemetryColorR { get; set; }

    /// <summary>按键灯遥测显示颜色G分量</summary>
    public byte TelemetryColorG { get; set; }

    /// <summary>按键灯遥测显示颜色B分量</summary>
    public byte TelemetryColorB { get; set; }
}
