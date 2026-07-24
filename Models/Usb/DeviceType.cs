namespace HITAPEX.Models.Usb;

/// <summary>
/// USB 设备类型枚举，标识外设的物理类别。
/// </summary>
public enum DeviceType
{
    /// <summary>未知设备类型</summary>
    Unknown = 0,
    /// <summary>基座</summary>
    Base = 1,
    /// <summary>踏板</summary>
    Pedal = 2,
    /// <summary>排挡</summary>
    Shifter = 3,
    /// <summary>手刹</summary>
    Handbrake = 4,
    /// <summary>序列挡</summary>
    Sequential = 5,
    /// <summary>面盘（方向盘盘面）</summary>
    Wheel = 6
}
