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
}
