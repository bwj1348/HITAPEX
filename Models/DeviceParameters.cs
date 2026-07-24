namespace HITAPEX.Models;

/// <summary>
/// 设备基础参数，包含力反馈、阻尼、温度保护、响应曲线等通用设置。
/// </summary>
public class BaseParameters
{
    /// <summary>力反馈强度 (0-100)</summary>
    public double ForceFeedback { get; set; } = 75;

    /// <summary>路面细节等级 (0-100)，数值越高路面纹理反馈越丰富</summary>
    public double DetailLevel { get; set; } = 60;

    /// <summary>阻尼等级 (0-100)，数值越高方向盘回中阻力越大</summary>
    public double DampingLevel { get; set; } = 40;

    /// <summary>温度警告阈值（摄氏度），超过此值触发警告</summary>
    public double TempWarning { get; set; } = 60;

    /// <summary>温度降额阈值（摄氏度），超过此值降低输出功率</summary>
    public double TempThrottle { get; set; } = 70;

    /// <summary>最大转速（RPM）</summary>
    public double MaxRpm { get; set; } = 3000;

    /// <summary>响应曲线类型索引 (0=线性, 1=柔和, 2=激进)</summary>
    public int ResponseCurve { get; set; } = 0;

    /// <summary>工作模式 (0=正常, 1=ECO节能, 2=竞技)</summary>
    public int WorkMode { get; set; } = 0;

    /// <summary>深拷贝当前基础参数对象</summary>
    public BaseParameters Clone()
    {
        return new BaseParameters
        {
            ForceFeedback = ForceFeedback,
            DetailLevel = DetailLevel,
            DampingLevel = DampingLevel,
            TempWarning = TempWarning,
            TempThrottle = TempThrottle,
            MaxRpm = MaxRpm,
            ResponseCurve = ResponseCurve,
            WorkMode = WorkMode
        };
    }

    /// <summary>将另一基础参数对象的值复制到当前对象</summary>
    public void Apply(BaseParameters other)
    {
        ForceFeedback = other.ForceFeedback;
        DetailLevel = other.DetailLevel;
        DampingLevel = other.DampingLevel;
        TempWarning = other.TempWarning;
        TempThrottle = other.TempThrottle;
        MaxRpm = other.MaxRpm;
        ResponseCurve = other.ResponseCurve;
        WorkMode = other.WorkMode;
    }
}

/// <summary>
/// 方向盘参数，包含转角、灵敏度、死区、振动及按键映射等转向相关设置。
/// </summary>
public class SteeringWheelParameters
{
    /// <summary>方向盘最大旋转角度 (270-1080度)</summary>
    public double RotationAngle { get; set; } = 900;

    /// <summary>转向灵敏度 (0-100)</summary>
    public double Sensitivity { get; set; } = 50;

    /// <summary>转向死区 (0-100)，中心区域不响应输入的范围</summary>
    public double DeadZone { get; set; } = 5;

    /// <summary>方向盘阻尼 (0-100)</summary>
    public double Damping { get; set; } = 30;

    /// <summary>振动强度 (0-100)</summary>
    public double Vibration { get; set; } = 70;

    /// <summary>路面反馈强度 (0-100)</summary>
    public double RoadFeedback { get; set; } = 60;

    /// <summary>按键映射表，键为物理按键编号，值为功能名称</summary>
    public Dictionary<int, string> ButtonMappings { get; set; } = new();

    /// <summary>初始化方向盘参数，加载默认按键映射</summary>
    public SteeringWheelParameters()
    {
        InitializeDefaultButtonMappings();
    }

    /// <summary>初始化默认按键映射（升档/降档/手刹/视角/菜单等）</summary>
    private void InitializeDefaultButtonMappings()
    {
        ButtonMappings[1] = "ShiftUp";
        ButtonMappings[2] = "ShiftDown";
        ButtonMappings[3] = "Handbrake";
        ButtonMappings[4] = "Camera";
        ButtonMappings[5] = "Menu";
        ButtonMappings[6] = "Replay";
        ButtonMappings[7] = "TC";
        ButtonMappings[8] = "ABS";
        ButtonMappings[9] = "Engine";
        ButtonMappings[10] = "Lights";
        ButtonMappings[11] = "Wipers";
        ButtonMappings[12] = "Custom";
    }

    /// <summary>深拷贝当前方向盘参数对象（含按键映射）</summary>
    public SteeringWheelParameters Clone()
    {
        return new SteeringWheelParameters
        {
            RotationAngle = RotationAngle,
            Sensitivity = Sensitivity,
            DeadZone = DeadZone,
            Damping = Damping,
            Vibration = Vibration,
            RoadFeedback = RoadFeedback,
            ButtonMappings = new Dictionary<int, string>(ButtonMappings)
        };
    }

    /// <summary>将另一方向盘参数对象的值复制到当前对象</summary>
    public void Apply(SteeringWheelParameters other)
    {
        RotationAngle = other.RotationAngle;
        Sensitivity = other.Sensitivity;
        DeadZone = other.DeadZone;
        Damping = other.Damping;
        Vibration = other.Vibration;
        RoadFeedback = other.RoadFeedback;
        ButtonMappings = new Dictionary<int, string>(other.ButtonMappings);
    }
}

/// <summary>
/// 踏板参数，包含油门、刹车、离合三条轴的灵敏度、死区和曲线设置。
/// </summary>
public class PedalParameters
{
    // ── 油门轴 ──
    /// <summary>油门灵敏度 (0-100)</summary>
    public double ThrottleSensitivity { get; set; } = 80;

    /// <summary>油门死区 (0-100)，踩下初期不响应的范围</summary>
    public double ThrottleDeadZone { get; set; } = 2;

    /// <summary>油门响应曲线类型索引 (0=线性, 1=柔和, 2=激进)</summary>
    public int ThrottleCurve { get; set; } = 0;

    // ── 刹车轴 ──
    /// <summary>刹车灵敏度 (0-100)</summary>
    public double BrakeSensitivity { get; set; } = 90;

    /// <summary>刹车死区 (0-100)</summary>
    public double BrakeDeadZone { get; set; } = 1;

    /// <summary>刹车压力点模拟强度 (0-100)，模拟液压刹车阻力感</summary>
    public double BrakePressure { get; set; } = 70;

    /// <summary>ABS 振动强度 (0-100)</summary>
    public double AbsVibration { get; set; } = 50;

    /// <summary>刹车响应曲线类型索引 (0=线性, 1=柔和, 2=激进)</summary>
    public int BrakeCurve { get; set; } = 0;

    // ── 离合轴 ──
    /// <summary>离合灵敏度 (0-100)</summary>
    public double ClutchSensitivity { get; set; } = 70;

    /// <summary>离合咬合点位置 (0-100)，半联动位置</summary>
    public double ClutchBitePoint { get; set; } = 50;

    /// <summary>深拷贝当前踏板参数对象</summary>
    public PedalParameters Clone()
    {
        return new PedalParameters
        {
            ThrottleSensitivity = ThrottleSensitivity,
            ThrottleDeadZone = ThrottleDeadZone,
            ThrottleCurve = ThrottleCurve,
            BrakeSensitivity = BrakeSensitivity,
            BrakeDeadZone = BrakeDeadZone,
            BrakePressure = BrakePressure,
            AbsVibration = AbsVibration,
            BrakeCurve = BrakeCurve,
            ClutchSensitivity = ClutchSensitivity,
            ClutchBitePoint = ClutchBitePoint
        };
    }

    /// <summary>将另一踏板参数对象的值复制到当前对象</summary>
    public void Apply(PedalParameters other)
    {
        ThrottleSensitivity = other.ThrottleSensitivity;
        ThrottleDeadZone = other.ThrottleDeadZone;
        ThrottleCurve = other.ThrottleCurve;
        BrakeSensitivity = other.BrakeSensitivity;
        BrakeDeadZone = other.BrakeDeadZone;
        BrakePressure = other.BrakePressure;
        AbsVibration = other.AbsVibration;
        BrakeCurve = other.BrakeCurve;
        ClutchSensitivity = other.ClutchSensitivity;
        ClutchBitePoint = other.ClutchBitePoint;
    }
}

/// <summary>
/// 设备参数集，聚合基础参数、方向盘参数和踏板参数，提供统一的存取接口。
/// </summary>
/// <remarks>
/// 用于参数预设的保存/加载和"修改比对"逻辑，
/// 所有参数子集通过 Clone/Apply 模式支持高效复制与回滚。
/// </remarks>
public class DeviceParametersSet
{
    /// <summary>基础通用参数</summary>
    public BaseParameters Base { get; set; } = new();

    /// <summary>方向盘专项参数</summary>
    public SteeringWheelParameters SteeringWheel { get; set; } = new();

    /// <summary>踏板专项参数</summary>
    public PedalParameters Pedal { get; set; } = new();

    /// <summary>深拷贝整个设备参数集（含所有子参数对象）</summary>
    public DeviceParametersSet Clone()
    {
        return new DeviceParametersSet
        {
            Base = Base.Clone(),
            SteeringWheel = SteeringWheel.Clone(),
            Pedal = Pedal.Clone()
        };
    }

    /// <summary>将另一参数集的所有值复制到当前对象</summary>
    public void Apply(DeviceParametersSet other)
    {
        Base.Apply(other.Base);
        SteeringWheel.Apply(other.SteeringWheel);
        Pedal.Apply(other.Pedal);
    }

    /// <summary>将所有参数重置为出厂默认值</summary>
    public void ResetToDefaults()
    {
        Base = new BaseParameters();
        SteeringWheel = new SteeringWheelParameters();
        Pedal = new PedalParameters();
    }
}
