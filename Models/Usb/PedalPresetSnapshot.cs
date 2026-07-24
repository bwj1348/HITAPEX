using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace HITAPEX.Models.Usb;

/// <summary>踏板参数完整快照，用于预设存取与修改比对</summary>
public class PedalPresetSnapshot
{
    // ── 离合轴 ──
    /// <summary>离合曲线类型</summary>
    [JsonPropertyName("clutchCurveType")]
    public int ClutchCurveType { get; set; } = 1;

    /// <summary>离合轴方向</summary>
    [JsonPropertyName("clutchDirection")]
    public byte ClutchDirection { get; set; }

    /// <summary>离合曲线点1 Y 坐标</summary>
    [JsonPropertyName("clutchPoint1Y")]
    public byte ClutchPoint1Y { get; set; }

    /// <summary>离合曲线点1 X 坐标</summary>
    [JsonPropertyName("clutchPoint1X")]
    public byte ClutchPoint1X { get; set; }

    /// <summary>离合曲线点2 Y 坐标</summary>
    [JsonPropertyName("clutchPoint2Y")]
    public byte ClutchPoint2Y { get; set; }

    /// <summary>离合曲线点2 X 坐标</summary>
    [JsonPropertyName("clutchPoint2X")]
    public byte ClutchPoint2X { get; set; }

    /// <summary>离合曲线点3 Y 坐标</summary>
    [JsonPropertyName("clutchPoint3Y")]
    public byte ClutchPoint3Y { get; set; }

    /// <summary>离合曲线点3 X 坐标</summary>
    [JsonPropertyName("clutchPoint3X")]
    public byte ClutchPoint3X { get; set; }

    /// <summary>离合曲线点4 Y 坐标</summary>
    [JsonPropertyName("clutchPoint4Y")]
    public byte ClutchPoint4Y { get; set; }

    /// <summary>离合曲线点4 X 坐标</summary>
    [JsonPropertyName("clutchPoint4X")]
    public byte ClutchPoint4X { get; set; }

    /// <summary>离合前部死区</summary>
    [JsonPropertyName("clutchDeadZoneFront")]
    public byte ClutchDeadZoneFront { get; set; }

    /// <summary>离合后部死区</summary>
    [JsonPropertyName("clutchDeadZoneRear")]
    public byte ClutchDeadZoneRear { get; set; }

    // ── 刹车轴 ──
    /// <summary>刹车曲线类型</summary>
    [JsonPropertyName("brakeCurveType")]
    public int BrakeCurveType { get; set; } = 1;

    /// <summary>刹车轴方向</summary>
    [JsonPropertyName("brakeDirection")]
    public byte BrakeDirection { get; set; }

    /// <summary>刹车曲线点1 Y 坐标</summary>
    [JsonPropertyName("brakePoint1Y")]
    public byte BrakePoint1Y { get; set; }

    /// <summary>刹车曲线点1 X 坐标</summary>
    [JsonPropertyName("brakePoint1X")]
    public byte BrakePoint1X { get; set; }

    /// <summary>刹车曲线点2 Y 坐标</summary>
    [JsonPropertyName("brakePoint2Y")]
    public byte BrakePoint2Y { get; set; }

    /// <summary>刹车曲线点2 X 坐标</summary>
    [JsonPropertyName("brakePoint2X")]
    public byte BrakePoint2X { get; set; }

    /// <summary>刹车曲线点3 Y 坐标</summary>
    [JsonPropertyName("brakePoint3Y")]
    public byte BrakePoint3Y { get; set; }

    /// <summary>刹车曲线点3 X 坐标</summary>
    [JsonPropertyName("brakePoint3X")]
    public byte BrakePoint3X { get; set; }

    /// <summary>刹车曲线点4 Y 坐标</summary>
    [JsonPropertyName("brakePoint4Y")]
    public byte BrakePoint4Y { get; set; }

    /// <summary>刹车曲线点4 X 坐标</summary>
    [JsonPropertyName("brakePoint4X")]
    public byte BrakePoint4X { get; set; }

    /// <summary>刹车前部死区</summary>
    [JsonPropertyName("brakeDeadZoneFront")]
    public byte BrakeDeadZoneFront { get; set; }

    /// <summary>刹车后部死区</summary>
    [JsonPropertyName("brakeDeadZoneRear")]
    public byte BrakeDeadZoneRear { get; set; }

    // ── 油门轴 ──
    /// <summary>油门曲线类型</summary>
    [JsonPropertyName("throttleCurveType")]
    public int ThrottleCurveType { get; set; } = 1;

    /// <summary>油门轴方向</summary>
    [JsonPropertyName("throttleDirection")]
    public byte ThrottleDirection { get; set; }

    /// <summary>油门曲线点1 Y 坐标</summary>
    [JsonPropertyName("throttlePoint1Y")]
    public byte ThrottlePoint1Y { get; set; }

    /// <summary>油门曲线点1 X 坐标</summary>
    [JsonPropertyName("throttlePoint1X")]
    public byte ThrottlePoint1X { get; set; }

    /// <summary>油门曲线点2 Y 坐标</summary>
    [JsonPropertyName("throttlePoint2Y")]
    public byte ThrottlePoint2Y { get; set; }

    /// <summary>油门曲线点2 X 坐标</summary>
    [JsonPropertyName("throttlePoint2X")]
    public byte ThrottlePoint2X { get; set; }

    /// <summary>油门曲线点3 Y 坐标</summary>
    [JsonPropertyName("throttlePoint3Y")]
    public byte ThrottlePoint3Y { get; set; }

    /// <summary>油门曲线点3 X 坐标</summary>
    [JsonPropertyName("throttlePoint3X")]
    public byte ThrottlePoint3X { get; set; }

    /// <summary>油门曲线点4 Y 坐标</summary>
    [JsonPropertyName("throttlePoint4Y")]
    public byte ThrottlePoint4Y { get; set; }

    /// <summary>油门曲线点4 X 坐标</summary>
    [JsonPropertyName("throttlePoint4X")]
    public byte ThrottlePoint4X { get; set; }

    /// <summary>油门前部死区</summary>
    [JsonPropertyName("throttleDeadZoneFront")]
    public byte ThrottleDeadZoneFront { get; set; }

    /// <summary>油门后部死区</summary>
    [JsonPropertyName("throttleDeadZoneRear")]
    public byte ThrottleDeadZoneRear { get; set; }

    /// <summary>逐字段比较踏板参数是否一致（不包含曲线类型，设备下发时曲线类型固定为自定义），同时输出差异日志</summary>
    /// <param name="other">要比较的另一踏板预设快照</param>
    /// <returns>所有字段均一致返回 true，否则返回 false</returns>
    public bool ParametersEqual(PedalPresetSnapshot other)
    {
        var diffs = new List<string>();

        void Check(string name, object? a, object? b)
        {
            if (!Equals(a, b))
                diffs.Add($"{name}: device={a}, preset={b}");
        }

        // ── 离合轴 ──
        Check("ClutchDirection", ClutchDirection, other.ClutchDirection);
        Check("ClutchPoint1Y", ClutchPoint1Y, other.ClutchPoint1Y);
        Check("ClutchPoint1X", ClutchPoint1X, other.ClutchPoint1X);
        Check("ClutchPoint2Y", ClutchPoint2Y, other.ClutchPoint2Y);
        Check("ClutchPoint2X", ClutchPoint2X, other.ClutchPoint2X);
        Check("ClutchPoint3Y", ClutchPoint3Y, other.ClutchPoint3Y);
        Check("ClutchPoint3X", ClutchPoint3X, other.ClutchPoint3X);
        Check("ClutchPoint4Y", ClutchPoint4Y, other.ClutchPoint4Y);
        Check("ClutchPoint4X", ClutchPoint4X, other.ClutchPoint4X);
        Check("ClutchDeadZoneFront", ClutchDeadZoneFront, other.ClutchDeadZoneFront);
        Check("ClutchDeadZoneRear", ClutchDeadZoneRear, other.ClutchDeadZoneRear);

        // ── 刹车轴 ──
        Check("BrakeDirection", BrakeDirection, other.BrakeDirection);
        Check("BrakePoint1Y", BrakePoint1Y, other.BrakePoint1Y);
        Check("BrakePoint1X", BrakePoint1X, other.BrakePoint1X);
        Check("BrakePoint2Y", BrakePoint2Y, other.BrakePoint2Y);
        Check("BrakePoint2X", BrakePoint2X, other.BrakePoint2X);
        Check("BrakePoint3Y", BrakePoint3Y, other.BrakePoint3Y);
        Check("BrakePoint3X", BrakePoint3X, other.BrakePoint3X);
        Check("BrakePoint4Y", BrakePoint4Y, other.BrakePoint4Y);
        Check("BrakePoint4X", BrakePoint4X, other.BrakePoint4X);
        Check("BrakeDeadZoneFront", BrakeDeadZoneFront, other.BrakeDeadZoneFront);
        Check("BrakeDeadZoneRear", BrakeDeadZoneRear, other.BrakeDeadZoneRear);

        // ── 油门轴 ──
        Check("ThrottleDirection", ThrottleDirection, other.ThrottleDirection);
        Check("ThrottlePoint1Y", ThrottlePoint1Y, other.ThrottlePoint1Y);
        Check("ThrottlePoint1X", ThrottlePoint1X, other.ThrottlePoint1X);
        Check("ThrottlePoint2Y", ThrottlePoint2Y, other.ThrottlePoint2Y);
        Check("ThrottlePoint2X", ThrottlePoint2X, other.ThrottlePoint2X);
        Check("ThrottlePoint3Y", ThrottlePoint3Y, other.ThrottlePoint3Y);
        Check("ThrottlePoint3X", ThrottlePoint3X, other.ThrottlePoint3X);
        Check("ThrottlePoint4Y", ThrottlePoint4Y, other.ThrottlePoint4Y);
        Check("ThrottlePoint4X", ThrottlePoint4X, other.ThrottlePoint4X);
        Check("ThrottleDeadZoneFront", ThrottleDeadZoneFront, other.ThrottleDeadZoneFront);
        Check("ThrottleDeadZoneRear", ThrottleDeadZoneRear, other.ThrottleDeadZoneRear);

        if (diffs.Count > 0)
        {
            Debug.WriteLine($"[PedalPresetSnapshot.ParametersEqual] 发现 {diffs.Count} 处不一致:");
            foreach (var d in diffs)
                Debug.WriteLine($"  {d}");
            return false;
        }
        else
        {
            Debug.WriteLine("[PedalPresetSnapshot.ParametersEqual] 参数完全一致");
            return true;
        }
    }
}
