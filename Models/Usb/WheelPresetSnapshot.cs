using System.Linq;
using System.Text.Json.Serialization;

namespace HITAPEX.Models.Usb;

/// <summary>面盘参数完整快照，用于预设存取与修改比对</summary>
public class WheelPresetSnapshot
{
    // ── 全局设置 ──
    [JsonPropertyName("keyColorEnabled")]//是否启用全局按键颜色
    public bool KeyColorEnabled { get; set; } = true;

    [JsonPropertyName("globalKeyColor")]//全局按键颜色索引 (0=红,1=橙,2=黄,3=绿,4=青,5=蓝,6=紫,7=白)
    public int GlobalKeyColor { get; set; }

    [JsonPropertyName("showKeyNumber")]//是否显示按键编号
    public bool ShowKeyNumber { get; set; } = true;

    [JsonPropertyName("keyBrightness")]//按键灯亮度
    public int KeyBrightness { get; set; } = 80;

    [JsonPropertyName("rpmBrightness")]//转速灯亮度
    public int RpmBrightness { get; set; } = 80;

    /// <summary>睡眠灯光时间下拉索引 (0=5分钟,1=10分钟,2=15分钟,3=30分钟,4=60分钟,5=从不)</summary>
    [JsonPropertyName("sleepLightDuration")]
    public int SleepLightDuration { get; set; }

    [JsonPropertyName("standbyLightEffect")]//待机灯效
    public int StandbyLightEffect { get; set; }

    [JsonPropertyName("globalFlashSpeed")]//待机灯效闪烁速度
    public int GlobalFlashSpeed { get; set; }

    // ── 14个可调圆形按键参数（B1,B2,B3,B6,B7,B8,B9,B11,B12,B13,B16,B17,B18,B19） ──
    [JsonPropertyName("buttonColors")]//按键灯颜色索引 (0=红,1=橙,2=黄,3=绿,4=青,5=蓝,6=紫,7=白，8=无)
    public int[] ButtonColors { get; set; } = Enumerable.Repeat(0, 14).ToArray();

    [JsonPropertyName("buttonTelemetryEnabled")]//是否启用遥测功能
    public bool[] ButtonTelemetryEnabled { get; set; } = new bool[14];

    [JsonPropertyName("buttonTelemetryLightEffect")]//遥测灯效 (0=常亮,1=闪烁)
    public int[] ButtonTelemetryLightEffect { get; set; } = Enumerable.Repeat(0, 14).ToArray();

    [JsonPropertyName("buttonTelemetryFunc")]//遥测功能类型索引
    public int[] ButtonTelemetryFunc { get; set; } = Enumerable.Repeat(0, 14).ToArray();

    [JsonPropertyName("buttonTelemetryTriggerColor")]//按键灯触发遥测颜色索引
    public int[] ButtonTelemetryTriggerColor { get; set; } = Enumerable.Repeat(0, 14).ToArray();

    [JsonPropertyName("buttonSpeeds")]//触发遥测时闪烁速度档位
    public int[] ButtonSpeeds { get; set; } = Enumerable.Repeat(0, 14).ToArray();

    // ── 转速灯 ──
    [JsonPropertyName("rpmColors")]//12个转速灯颜色索引 (0=红,1=橙,2=黄,3=绿,4=青,5=蓝,6=紫,7=白，8=无)
    public int[] RpmColors { get; set; } = new int[12];

    [JsonPropertyName("rpmValues")]//触发转速灯时的转速百分比
    public double[] RpmValues { get; set; } = Enumerable.Repeat(0.0, 12).ToArray();

    [JsonPropertyName("rpmCapValue")]//触发爆闪的转速百分比
    public double RpmCapValue { get; set; } = 100;

    [JsonPropertyName("rpmCurveType")]//曲线类型
    public int RpmCurveType { get; set; }

    [JsonPropertyName("rpmDisplayMode")]//转速灯显示模式 （0=百分比,1=转速）
    public int RpmDisplayMode { get; set; }

    [JsonPropertyName("rpmLightMode")]//单个转速灯灯光模式
    public int RpmLightMode { get; set; }

    [JsonPropertyName("rpmStrobeMode")]//单个转速灯爆闪灯光颜色模式
    public int RpmStrobeMode { get; set; }

    /// <summary>爆闪颜色索引，12灯统一 (0=红,1=橙,2=黄,3=绿,4=青,5=蓝,6=紫,7=白)</summary>
    [JsonPropertyName("rpmStrobeColor")]
    public int RpmStrobeColor { get; set; }

    [JsonPropertyName("rpmSpeed")]//触发爆闪时闪烁速度档位
    public int RpmSpeed { get; set; }

    [JsonPropertyName("rpmBaseLightMode")]//单个转速灯基础灯光模式
    public int RpmBaseLightMode { get; set; }

    [JsonPropertyName("rpmBaseLightSpeed")]//单个转速灯基础灯光闪烁速度档位
    public int RpmBaseLightSpeed { get; set; }

    [JsonPropertyName("rpmTelemetryEnabled")]//是否启用转速灯遥测模式
    public bool RpmTelemetryEnabled { get; set; }

    // ── 拨片 ──
    [JsonPropertyName("clutchMode")]//离合拨片模式 （0=合成轴，1=独立轴，2=按键）
    public int ClutchMode { get; set; }

    [JsonPropertyName("clutchPointValue")]//合成轴模式下离合点位置
    public double ClutchPointValue { get; set; } = 50;

    /// <summary>逐字段比较面盘参数是否一致</summary>
    public bool ParametersEqual(WheelPresetSnapshot other)
    {
        return KeyColorEnabled == other.KeyColorEnabled
            && GlobalKeyColor == other.GlobalKeyColor
            && ShowKeyNumber == other.ShowKeyNumber
            && KeyBrightness == other.KeyBrightness
            && RpmBrightness == other.RpmBrightness
            && SleepLightDuration == other.SleepLightDuration
            && StandbyLightEffect == other.StandbyLightEffect
            && GlobalFlashSpeed == other.GlobalFlashSpeed
            && ButtonColors.SequenceEqual(other.ButtonColors)
            && ButtonTelemetryEnabled.SequenceEqual(other.ButtonTelemetryEnabled)
            && ButtonTelemetryLightEffect.SequenceEqual(other.ButtonTelemetryLightEffect)
            && ButtonTelemetryFunc.SequenceEqual(other.ButtonTelemetryFunc)
            && ButtonTelemetryTriggerColor.SequenceEqual(other.ButtonTelemetryTriggerColor)
            && ButtonSpeeds.SequenceEqual(other.ButtonSpeeds)
            && RpmColors.SequenceEqual(other.RpmColors)
            && RpmValues.SequenceEqual(other.RpmValues)
            && RpmCapValue == other.RpmCapValue
            && RpmCurveType == other.RpmCurveType
            && RpmDisplayMode == other.RpmDisplayMode
            && RpmLightMode == other.RpmLightMode
            && RpmStrobeMode == other.RpmStrobeMode
            && RpmStrobeColor == other.RpmStrobeColor
            && RpmSpeed == other.RpmSpeed
            && RpmBaseLightMode == other.RpmBaseLightMode
            && RpmBaseLightSpeed == other.RpmBaseLightSpeed
            && RpmTelemetryEnabled == other.RpmTelemetryEnabled
            && ClutchMode == other.ClutchMode
            && ClutchPointValue == other.ClutchPointValue;
    }
}
