using System.Text.Json.Serialization;

namespace HITAPEX.Models.Usb;

/// <summary>踏板参数完整快照，用于预设存取与修改比对</summary>
public class PedalPresetSnapshot
{
    // ── 离合轴 ──
    [JsonPropertyName("clutchCurveType")]
    public int ClutchCurveType { get; set; } = 1;

    [JsonPropertyName("clutchDirection")]
    public byte ClutchDirection { get; set; }

    [JsonPropertyName("clutchPoint1Y")]
    public byte ClutchPoint1Y { get; set; }

    [JsonPropertyName("clutchPoint1X")]
    public byte ClutchPoint1X { get; set; }

    [JsonPropertyName("clutchPoint2Y")]
    public byte ClutchPoint2Y { get; set; }

    [JsonPropertyName("clutchPoint2X")]
    public byte ClutchPoint2X { get; set; }

    [JsonPropertyName("clutchPoint3Y")]
    public byte ClutchPoint3Y { get; set; }

    [JsonPropertyName("clutchPoint3X")]
    public byte ClutchPoint3X { get; set; }

    [JsonPropertyName("clutchPoint4Y")]
    public byte ClutchPoint4Y { get; set; }

    [JsonPropertyName("clutchPoint4X")]
    public byte ClutchPoint4X { get; set; }

    [JsonPropertyName("clutchDeadZoneFront")]
    public byte ClutchDeadZoneFront { get; set; }

    [JsonPropertyName("clutchDeadZoneRear")]
    public byte ClutchDeadZoneRear { get; set; }

    // ── 刹车轴 ──
    [JsonPropertyName("brakeCurveType")]
    public int BrakeCurveType { get; set; } = 1;

    [JsonPropertyName("brakeDirection")]
    public byte BrakeDirection { get; set; }

    [JsonPropertyName("brakePoint1Y")]
    public byte BrakePoint1Y { get; set; }

    [JsonPropertyName("brakePoint1X")]
    public byte BrakePoint1X { get; set; }

    [JsonPropertyName("brakePoint2Y")]
    public byte BrakePoint2Y { get; set; }

    [JsonPropertyName("brakePoint2X")]
    public byte BrakePoint2X { get; set; }

    [JsonPropertyName("brakePoint3Y")]
    public byte BrakePoint3Y { get; set; }

    [JsonPropertyName("brakePoint3X")]
    public byte BrakePoint3X { get; set; }

    [JsonPropertyName("brakePoint4Y")]
    public byte BrakePoint4Y { get; set; }

    [JsonPropertyName("brakePoint4X")]
    public byte BrakePoint4X { get; set; }

    [JsonPropertyName("brakeDeadZoneFront")]
    public byte BrakeDeadZoneFront { get; set; }

    [JsonPropertyName("brakeDeadZoneRear")]
    public byte BrakeDeadZoneRear { get; set; }

    // ── 油门轴 ──
    [JsonPropertyName("throttleCurveType")]
    public int ThrottleCurveType { get; set; } = 1;

    [JsonPropertyName("throttleDirection")]
    public byte ThrottleDirection { get; set; }

    [JsonPropertyName("throttlePoint1Y")]
    public byte ThrottlePoint1Y { get; set; }

    [JsonPropertyName("throttlePoint1X")]
    public byte ThrottlePoint1X { get; set; }

    [JsonPropertyName("throttlePoint2Y")]
    public byte ThrottlePoint2Y { get; set; }

    [JsonPropertyName("throttlePoint2X")]
    public byte ThrottlePoint2X { get; set; }

    [JsonPropertyName("throttlePoint3Y")]
    public byte ThrottlePoint3Y { get; set; }

    [JsonPropertyName("throttlePoint3X")]
    public byte ThrottlePoint3X { get; set; }

    [JsonPropertyName("throttlePoint4Y")]
    public byte ThrottlePoint4Y { get; set; }

    [JsonPropertyName("throttlePoint4X")]
    public byte ThrottlePoint4X { get; set; }

    [JsonPropertyName("throttleDeadZoneFront")]
    public byte ThrottleDeadZoneFront { get; set; }

    [JsonPropertyName("throttleDeadZoneRear")]
    public byte ThrottleDeadZoneRear { get; set; }

    /// <summary>逐字段比较踏板参数是否一致（不包含曲线类型，设备下发时曲线类型固定为自定义）</summary>
    public bool ParametersEqual(PedalPresetSnapshot other)
    {
        return ClutchDirection == other.ClutchDirection
            && ClutchPoint1Y == other.ClutchPoint1Y && ClutchPoint1X == other.ClutchPoint1X
            && ClutchPoint2Y == other.ClutchPoint2Y && ClutchPoint2X == other.ClutchPoint2X
            && ClutchPoint3Y == other.ClutchPoint3Y && ClutchPoint3X == other.ClutchPoint3X
            && ClutchPoint4Y == other.ClutchPoint4Y && ClutchPoint4X == other.ClutchPoint4X
            && ClutchDeadZoneFront == other.ClutchDeadZoneFront && ClutchDeadZoneRear == other.ClutchDeadZoneRear
            && BrakeDirection == other.BrakeDirection
            && BrakePoint1Y == other.BrakePoint1Y && BrakePoint1X == other.BrakePoint1X
            && BrakePoint2Y == other.BrakePoint2Y && BrakePoint2X == other.BrakePoint2X
            && BrakePoint3Y == other.BrakePoint3Y && BrakePoint3X == other.BrakePoint3X
            && BrakePoint4Y == other.BrakePoint4Y && BrakePoint4X == other.BrakePoint4X
            && BrakeDeadZoneFront == other.BrakeDeadZoneFront && BrakeDeadZoneRear == other.BrakeDeadZoneRear
            && ThrottleDirection == other.ThrottleDirection
            && ThrottlePoint1Y == other.ThrottlePoint1Y && ThrottlePoint1X == other.ThrottlePoint1X
            && ThrottlePoint2Y == other.ThrottlePoint2Y && ThrottlePoint2X == other.ThrottlePoint2X
            && ThrottlePoint3Y == other.ThrottlePoint3Y && ThrottlePoint3X == other.ThrottlePoint3X
            && ThrottlePoint4Y == other.ThrottlePoint4Y && ThrottlePoint4X == other.ThrottlePoint4X
            && ThrottleDeadZoneFront == other.ThrottleDeadZoneFront && ThrottleDeadZoneRear == other.ThrottleDeadZoneRear;
    }
}
