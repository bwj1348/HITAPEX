namespace HITAPEX.Models.Usb;

/// <summary>设备上报的面盘睡眠和拨片属性参数（协议 0x2108 Get 响应）</summary>
public class WheelSleepAndPaddleResponse
{
    /// <summary>按键灯光睡眠时间 (0=从不, 1=5分钟, 2=10分钟, 3=15分钟, 4=30分钟, 5=60分钟)</summary>
    public byte SleepTime { get; set; }

    /// <summary>睡眠灯效 (0=关闭, 1=呼吸)</summary>
    public byte SleepEffect { get; set; }

    /// <summary>睡眠灯效速度 (0-5)</summary>
    public byte SleepEffectSpeed { get; set; }

    /// <summary>离合拨片模式 (0=独立轴, 1=合成轴, 2=按键)</summary>
    public byte ClutchPaddleMode { get; set; }

    /// <summary>双离合咬合点 (0-100)</summary>
    public byte ClutchBitePoint { get; set; }
}
