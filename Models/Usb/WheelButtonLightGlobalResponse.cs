namespace HITAPEX.Models.Usb;

/// <summary>设备上报的面盘按键灯全局属性参数（协议 0x2106 Get 响应）</summary>
public class WheelButtonLightGlobalResponse
{
    /// <summary>按键LED灯模式 (0=单独颜色常亮, 1=统一颜色常亮)</summary>
    public byte LedMode { get; set; }

    /// <summary>按键灯全局亮度 (0-100)</summary>
    public byte Brightness { get; set; }

    /// <summary>按键灯统一颜色R分量</summary>
    public byte ColorR { get; set; }

    /// <summary>按键灯统一颜色G分量</summary>
    public byte ColorG { get; set; }

    /// <summary>按键灯统一颜色B分量</summary>
    public byte ColorB { get; set; }
}
