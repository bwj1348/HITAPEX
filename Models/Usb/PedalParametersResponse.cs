namespace HITAPEX.Models.Usb;

/// <summary>设备上报的脚踏板属性参数（协议 0x2110 Get 响应）</summary>
public class PedalParametersResponse
{
    // ── 离合轴 ──
    public byte ClutchDirection { get; set; }
    public byte ClutchPoint1Y { get; set; }
    public byte ClutchPoint1X { get; set; }
    public byte ClutchPoint2Y { get; set; }
    public byte ClutchPoint2X { get; set; }
    public byte ClutchPoint3Y { get; set; }
    public byte ClutchPoint3X { get; set; }
    public byte ClutchPoint4Y { get; set; }
    public byte ClutchPoint4X { get; set; }
    public byte ClutchDeadZoneFront { get; set; }
    public byte ClutchDeadZoneRear { get; set; }

    // ── 刹车轴 ──
    public byte BrakeDirection { get; set; }
    public byte BrakePoint1Y { get; set; }
    public byte BrakePoint1X { get; set; }
    public byte BrakePoint2Y { get; set; }
    public byte BrakePoint2X { get; set; }
    public byte BrakePoint3Y { get; set; }
    public byte BrakePoint3X { get; set; }
    public byte BrakePoint4Y { get; set; }
    public byte BrakePoint4X { get; set; }
    public byte BrakeDeadZoneFront { get; set; }
    public byte BrakeDeadZoneRear { get; set; }

    // ── 油门轴 ──
    public byte ThrottleDirection { get; set; }
    public byte ThrottlePoint1Y { get; set; }
    public byte ThrottlePoint1X { get; set; }
    public byte ThrottlePoint2Y { get; set; }
    public byte ThrottlePoint2X { get; set; }
    public byte ThrottlePoint3Y { get; set; }
    public byte ThrottlePoint3X { get; set; }
    public byte ThrottlePoint4Y { get; set; }
    public byte ThrottlePoint4X { get; set; }
    public byte ThrottleDeadZoneFront { get; set; }
    public byte ThrottleDeadZoneRear { get; set; }
}
