using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace HITAPEX.Models.Usb;

/// <summary>面盘参数完整快照，用于预设存取与修改比对</summary>
/// <remarks>
/// 包含面盘全局设置、14个可调按键参数、转速灯配置和拨片设置。
/// 通过 ParametersEqual 方法逐字段比对，用于检测设备下发参数是否与本地预设一致。
/// </remarks>
public class WheelPresetSnapshot
{
    // ── 全局设置 ──
    /// <summary>是否启用全局按键颜色</summary>
    [JsonPropertyName("keyColorEnabled")]
    public bool KeyColorEnabled { get; set; } = true;

    /// <summary>全局按键颜色索引 (0=红,1=橙,2=黄,3=绿,4=青,5=蓝,6=紫,7=白)</summary>
    [JsonPropertyName("globalKeyColor")]
    public int GlobalKeyColor { get; set; }

    /// <summary>是否显示按键编号</summary>
    [JsonPropertyName("showKeyNumber")]
    public bool ShowKeyNumber { get; set; } = true;

    /// <summary>按键灯亮度 (0-100)</summary>
    [JsonPropertyName("keyBrightness")]
    public int KeyBrightness { get; set; } = 80;

    /// <summary>转速灯亮度 (0-100)</summary>
    [JsonPropertyName("rpmBrightness")]
    public int RpmBrightness { get; set; } = 80;

    /// <summary>睡眠灯光时间下拉索引 (0=5分钟,1=10分钟,2=15分钟,3=30分钟,4=60分钟,5=从不)</summary>
    [JsonPropertyName("sleepLightDuration")]
    public int SleepLightDuration { get; set; }

    /// <summary>待机灯效类型</summary>
    [JsonPropertyName("standbyLightEffect")]
    public int StandbyLightEffect { get; set; }

    /// <summary>待机灯效闪烁速度（档位）</summary>
    [JsonPropertyName("globalFlashSpeed")]
    public int GlobalFlashSpeed { get; set; }

    // ── 14个可调圆形按键参数（B1,B2,B3,B6,B7,B8,B9,B11,B12,B13,B16,B17,B18,B19） ──
    /// <summary>按键灯颜色索引 (0=红,1=橙,2=黄,3=绿,4=青,5=蓝,6=紫,7=白，8=无)</summary>
    [JsonPropertyName("buttonColors")]
    public int[] ButtonColors { get; set; } = Enumerable.Repeat(0, 14).ToArray();

    /// <summary>是否启用遥测功能</summary>
    [JsonPropertyName("buttonTelemetryEnabled")]
    public bool[] ButtonTelemetryEnabled { get; set; } = new bool[14];

    /// <summary>遥测灯效 (0=常亮,1=闪烁)</summary>
    [JsonPropertyName("buttonTelemetryLightEffect")]
    public int[] ButtonTelemetryLightEffect { get; set; } = Enumerable.Repeat(0, 14).ToArray();

    /// <summary>遥测功能类型索引</summary>
    [JsonPropertyName("buttonTelemetryFunc")]
    public int[] ButtonTelemetryFunc { get; set; } = Enumerable.Repeat(0, 14).ToArray();

    /// <summary>按键灯触发遥测颜色索引</summary>
    [JsonPropertyName("buttonTelemetryTriggerColor")]
    public int[] ButtonTelemetryTriggerColor { get; set; } = Enumerable.Repeat(0, 14).ToArray();

    /// <summary>触发遥测时闪烁速度档位</summary>
    [JsonPropertyName("buttonSpeeds")]
    public int[] ButtonSpeeds { get; set; } = Enumerable.Repeat(0, 14).ToArray();

    // ── 转速灯 ──
    /// <summary>12个转速灯颜色索引 (0=红,1=橙,2=黄,3=绿,4=青,5=蓝,6=紫,7=白，8=无)</summary>
    [JsonPropertyName("rpmColors")]
    public int[] RpmColors { get; set; } = new int[12];

    /// <summary>触发转速灯时的转速百分比 (0-100)</summary>
    [JsonPropertyName("rpmValues")]
    public double[] RpmValues { get; set; } = Enumerable.Repeat(0.0, 12).ToArray();

    /// <summary>触发爆闪的转速百分比 (0-100)</summary>
    [JsonPropertyName("rpmCapValue")]
    public double RpmCapValue { get; set; } = 100;

    /// <summary>曲线类型</summary>
    [JsonPropertyName("rpmCurveType")]
    public int RpmCurveType { get; set; }

    /// <summary>转速灯显示模式 (0=百分比, 1=转速)</summary>
    [JsonPropertyName("rpmDisplayMode")]
    public int RpmDisplayMode { get; set; }

    /// <summary>单个转速灯灯光模式</summary>
    [JsonPropertyName("rpmLightMode")]
    public int RpmLightMode { get; set; }

    /// <summary>单个转速灯爆闪灯光颜色模式</summary>
    [JsonPropertyName("rpmStrobeMode")]
    public int RpmStrobeMode { get; set; }

    /// <summary>爆闪颜色索引，12灯统一 (0=红,1=橙,2=黄,3=绿,4=青,5=蓝,6=紫,7=白)</summary>
    [JsonPropertyName("rpmStrobeColor")]
    public int RpmStrobeColor { get; set; }

    /// <summary>触发爆闪时闪烁速度档位</summary>
    [JsonPropertyName("rpmSpeed")]
    public int RpmSpeed { get; set; }

    /// <summary>单个转速灯基础灯光模式</summary>
    [JsonPropertyName("rpmBaseLightMode")]
    public int RpmBaseLightMode { get; set; }

    /// <summary>单个转速灯基础灯光闪烁速度档位</summary>
    [JsonPropertyName("rpmBaseLightSpeed")]
    public int RpmBaseLightSpeed { get; set; }

    /// <summary>是否启用转速灯遥测模式</summary>
    [JsonPropertyName("rpmTelemetryEnabled")]
    public bool RpmTelemetryEnabled { get; set; }

    // ── 拨片 ──
    /// <summary>离合拨片模式 (0=合成轴, 1=独立轴, 2=按键)</summary>
    [JsonPropertyName("clutchMode")]
    public int ClutchMode { get; set; }

    /// <summary>合成轴模式下离合点位置 (0-100)</summary>
    [JsonPropertyName("clutchPointValue")]
    public double ClutchPointValue { get; set; } = 50;

    /// <summary>
    /// 逐字段比较面盘参数是否一致，同时输出差异日志。
    /// 不参与比较的字段：
    ///   - RpmCurveType: RPM 弹窗纯 UI 概念，设备不会存储/返回此值
    ///   - ShowKeyNumber: 按键编号显隐开关，纯 UI 概念，设备不会存储
    /// 注意：GlobalKeyColor 始终比较（0x2106协议始终返回设备存储的统一颜色，与当前模式无关）
    /// </summary>
    /// <param name="other">要比较的另一面盘预设快照</param>
    /// <returns>所有字段均一致返回 true，否则返回 false</returns>
    public bool ParametersEqual(WheelPresetSnapshot other)
    {
        var diffs = new List<string>();

        void Check(string name, object? a, object? b)
        {
            if (!Equals(a, b))
                diffs.Add($"{name}: device={a}, preset={b}");
        }
        void CheckSeq<T>(string name, T[] a, T[] b)
        {
            if (!a.SequenceEqual(b))
            {
                var mismatchIdxs = new List<int>();
                for (int i = 0; i < a.Length; i++)
                    if (!Equals(a[i], b[i]))
                        mismatchIdxs.Add(i);
                diffs.Add($"{name}: mismatch at indices [{string.Join(",", mismatchIdxs)}] device={string.Join(",", a)}, preset={string.Join(",", b)}");
            }
        }

        // ── 全局设置 ──
        Check("KeyColorEnabled", KeyColorEnabled, other.KeyColorEnabled);
        // GlobalKeyColor: 0x2106 协议始终存储统一颜色，无条件比较
        Check("GlobalKeyColor", GlobalKeyColor, other.GlobalKeyColor);
        // ShowKeyNumber: 纯 UI 概念，跳过
        Check("KeyBrightness", KeyBrightness, other.KeyBrightness);
        Check("RpmBrightness", RpmBrightness, other.RpmBrightness);
        Check("SleepLightDuration", SleepLightDuration, other.SleepLightDuration);
        Check("StandbyLightEffect", StandbyLightEffect, other.StandbyLightEffect);
        Check("GlobalFlashSpeed", GlobalFlashSpeed, other.GlobalFlashSpeed);

        // ── 14个可调按键 ──
        CheckSeq("ButtonColors", ButtonColors, other.ButtonColors);
        CheckSeq("ButtonTelemetryEnabled", ButtonTelemetryEnabled, other.ButtonTelemetryEnabled);
        CheckSeq("ButtonTelemetryLightEffect", ButtonTelemetryLightEffect, other.ButtonTelemetryLightEffect);
        CheckSeq("ButtonTelemetryFunc", ButtonTelemetryFunc, other.ButtonTelemetryFunc);
        CheckSeq("ButtonTelemetryTriggerColor", ButtonTelemetryTriggerColor, other.ButtonTelemetryTriggerColor);
        CheckSeq("ButtonSpeeds", ButtonSpeeds, other.ButtonSpeeds);

        // ── 转速灯 ──
        CheckSeq("RpmColors", RpmColors, other.RpmColors);
        CheckSeq("RpmValues", RpmValues, other.RpmValues);
        Check("RpmCapValue", RpmCapValue, other.RpmCapValue);
        // RpmCurveType 不参与比较
        Check("RpmDisplayMode", RpmDisplayMode, other.RpmDisplayMode);
        Check("RpmLightMode", RpmLightMode, other.RpmLightMode);
        Check("RpmStrobeMode", RpmStrobeMode, other.RpmStrobeMode);
        Check("RpmStrobeColor", RpmStrobeColor, other.RpmStrobeColor);
        Check("RpmSpeed", RpmSpeed, other.RpmSpeed);
        Check("RpmBaseLightMode", RpmBaseLightMode, other.RpmBaseLightMode);
        Check("RpmBaseLightSpeed", RpmBaseLightSpeed, other.RpmBaseLightSpeed);
        Check("RpmTelemetryEnabled", RpmTelemetryEnabled, other.RpmTelemetryEnabled);

        // ── 拨片 ──
        Check("ClutchMode", ClutchMode, other.ClutchMode);
        Check("ClutchPointValue", ClutchPointValue, other.ClutchPointValue);

        if (diffs.Count > 0)
        {
            Debug.WriteLine($"[WheelPresetSnapshot.ParametersEqual] 发现 {diffs.Count} 处不一致:");
            foreach (var d in diffs)
                Debug.WriteLine($"  {d}");
            return false;
        }
        else
        {
            Debug.WriteLine("[WheelPresetSnapshot.ParametersEqual] 参数完全一致");
            return true;
        }
    }

    /// <summary>校验参数值是否在合法范围内，返回错误消息列表（空列表表示通过）</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        void InRange(string name, int value, int min, int max)
        {
            if (value < min || value > max)
                errors.Add($"{name}: {value}, 允许范围 [{min}, {max}]");
        }

        void CheckArrayLen(string name, Array arr, int expected)
        {
            if (arr.Length != expected)
                errors.Add($"{name}: 数组长度 {arr.Length}, 期望 {expected}");
        }

        // 全局设置
        InRange("GlobalKeyColor", GlobalKeyColor, 0, 8);
        InRange("KeyBrightness", KeyBrightness, 0, 100);
        InRange("RpmBrightness", RpmBrightness, 0, 100);
        InRange("SleepLightDuration", SleepLightDuration, 0, 5);
        InRange("StandbyLightEffect", StandbyLightEffect, 0, 1);
        InRange("GlobalFlashSpeed", GlobalFlashSpeed, 0, 5);

        // 14 个按键参数
        CheckArrayLen("ButtonColors", ButtonColors, 14);
        CheckArrayLen("ButtonTelemetryEnabled", ButtonTelemetryEnabled, 14);
        CheckArrayLen("ButtonTelemetryLightEffect", ButtonTelemetryLightEffect, 14);
        CheckArrayLen("ButtonTelemetryFunc", ButtonTelemetryFunc, 14);
        CheckArrayLen("ButtonTelemetryTriggerColor", ButtonTelemetryTriggerColor, 14);
        CheckArrayLen("ButtonSpeeds", ButtonSpeeds, 14);

        for (int i = 0; i < 14; i++)
        {
            InRange($"ButtonColors[{i}]", ButtonColors[i], 0, 8);
            InRange($"ButtonTelemetryLightEffect[{i}]", ButtonTelemetryLightEffect[i], 0, 1);
            InRange($"ButtonTelemetryFunc[{i}]", ButtonTelemetryFunc[i], 0, 6);
            InRange($"ButtonTelemetryTriggerColor[{i}]", ButtonTelemetryTriggerColor[i], 0, 8);
            InRange($"ButtonSpeeds[{i}]", ButtonSpeeds[i], 0, 5);
        }

        // 12 个转速灯
        CheckArrayLen("RpmColors", RpmColors, 12);
        CheckArrayLen("RpmValues", RpmValues, 12);

        for (int i = 0; i < 12; i++)
        {
            InRange($"RpmColors[{i}]", RpmColors[i], 0, 8);
            var rpmVal = (int)RpmValues[i];
            InRange($"RpmValues[{i}]", rpmVal, 0, 100);
        }

        InRange("RpmCapValue", (int)RpmCapValue, 0, 100);
        InRange("RpmCurveType", RpmCurveType, 0, 3);
        InRange("RpmDisplayMode", RpmDisplayMode, 0, 1);
        InRange("RpmLightMode", RpmLightMode, 0, 2);
        InRange("RpmStrobeMode", RpmStrobeMode, 0, 2);
        InRange("RpmStrobeColor", RpmStrobeColor, 0, 8);
        InRange("RpmSpeed", RpmSpeed, 0, 5);
        InRange("RpmBaseLightMode", RpmBaseLightMode, 0, 2);
        InRange("RpmBaseLightSpeed", RpmBaseLightSpeed, 0, 5);

        // 拨片
        InRange("ClutchMode", ClutchMode, 0, 2);
        InRange("ClutchPointValue", (int)ClutchPointValue, 0, 100);

        return errors;
    }
}
