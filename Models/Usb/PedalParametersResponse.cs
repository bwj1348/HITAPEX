namespace HITAPEX.Models.Usb;

/// <summary>设备上报的脚踏板属性参数（协议 0x2110 Get 响应）</summary>
/// <remarks>
/// 包含离合、刹车、油门三条轴的完整参数：
/// 方向、四段曲线控制点、前后死区。
/// </remarks>
public class PedalParametersResponse
{
    // ── 离合轴 ──
    /// <summary>离合轴方向</summary>
    public byte ClutchDirection { get; set; }
    /// <summary>离合曲线点1 Y 坐标</summary>
    public byte ClutchPoint1Y { get; set; }
    /// <summary>离合曲线点1 X 坐标</summary>
    public byte ClutchPoint1X { get; set; }
    /// <summary>离合曲线点2 Y 坐标</summary>
    public byte ClutchPoint2Y { get; set; }
    /// <summary>离合曲线点2 X 坐标</summary>
    public byte ClutchPoint2X { get; set; }
    /// <summary>离合曲线点3 Y 坐标</summary>
    public byte ClutchPoint3Y { get; set; }
    /// <summary>离合曲线点3 X 坐标</summary>
    public byte ClutchPoint3X { get; set; }
    /// <summary>离合曲线点4 Y 坐标</summary>
    public byte ClutchPoint4Y { get; set; }
    /// <summary>离合曲线点4 X 坐标</summary>
    public byte ClutchPoint4X { get; set; }
    /// <summary>离合前部死区</summary>
    public byte ClutchDeadZoneFront { get; set; }
    /// <summary>离合后部死区</summary>
    public byte ClutchDeadZoneRear { get; set; }

    // ── 刹车轴 ──
    /// <summary>刹车轴方向</summary>
    public byte BrakeDirection { get; set; }
    /// <summary>刹车曲线点1 Y 坐标</summary>
    public byte BrakePoint1Y { get; set; }
    /// <summary>刹车曲线点1 X 坐标</summary>
    public byte BrakePoint1X { get; set; }
    /// <summary>刹车曲线点2 Y 坐标</summary>
    public byte BrakePoint2Y { get; set; }
    /// <summary>刹车曲线点2 X 坐标</summary>
    public byte BrakePoint2X { get; set; }
    /// <summary>刹车曲线点3 Y 坐标</summary>
    public byte BrakePoint3Y { get; set; }
    /// <summary>刹车曲线点3 X 坐标</summary>
    public byte BrakePoint3X { get; set; }
    /// <summary>刹车曲线点4 Y 坐标</summary>
    public byte BrakePoint4Y { get; set; }
    /// <summary>刹车曲线点4 X 坐标</summary>
    public byte BrakePoint4X { get; set; }
    /// <summary>刹车前部死区</summary>
    public byte BrakeDeadZoneFront { get; set; }
    /// <summary>刹车后部死区</summary>
    public byte BrakeDeadZoneRear { get; set; }

    // ── 油门轴 ──
    /// <summary>油门轴方向</summary>
    public byte ThrottleDirection { get; set; }
    /// <summary>油门曲线点1 Y 坐标</summary>
    public byte ThrottlePoint1Y { get; set; }
    /// <summary>油门曲线点1 X 坐标</summary>
    public byte ThrottlePoint1X { get; set; }
    /// <summary>油门曲线点2 Y 坐标</summary>
    public byte ThrottlePoint2Y { get; set; }
    /// <summary>油门曲线点2 X 坐标</summary>
    public byte ThrottlePoint2X { get; set; }
    /// <summary>油门曲线点3 Y 坐标</summary>
    public byte ThrottlePoint3Y { get; set; }
    /// <summary>油门曲线点3 X 坐标</summary>
    public byte ThrottlePoint3X { get; set; }
    /// <summary>油门曲线点4 Y 坐标</summary>
    public byte ThrottlePoint4Y { get; set; }
    /// <summary>油门曲线点4 X 坐标</summary>
    public byte ThrottlePoint4X { get; set; }
    /// <summary>油门前部死区</summary>
    public byte ThrottleDeadZoneFront { get; set; }
    /// <summary>油门后部死区</summary>
    public byte ThrottleDeadZoneRear { get; set; }
}
