namespace HITAPEX.Models;

public class BaseParameters
{
    public double ForceFeedback { get; set; } = 75;
    public double DetailLevel { get; set; } = 60;
    public double DampingLevel { get; set; } = 40;
    public double TempWarning { get; set; } = 60;
    public double TempThrottle { get; set; } = 70;
    public double MaxRpm { get; set; } = 3000;
    public int ResponseCurve { get; set; } = 0;
    public int WorkMode { get; set; } = 0;

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

public class SteeringWheelParameters
{
    public double RotationAngle { get; set; } = 900;
    public double Sensitivity { get; set; } = 50;
    public double DeadZone { get; set; } = 5;
    public double Damping { get; set; } = 30;
    public double Vibration { get; set; } = 70;
    public double RoadFeedback { get; set; } = 60;
    public Dictionary<int, string> ButtonMappings { get; set; } = new();

    public SteeringWheelParameters()
    {
        InitializeDefaultButtonMappings();
    }

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

public class PedalParameters
{
    public double ThrottleSensitivity { get; set; } = 80;
    public double ThrottleDeadZone { get; set; } = 2;
    public int ThrottleCurve { get; set; } = 0;

    public double BrakeSensitivity { get; set; } = 90;
    public double BrakeDeadZone { get; set; } = 1;
    public double BrakePressure { get; set; } = 70;
    public double AbsVibration { get; set; } = 50;
    public int BrakeCurve { get; set; } = 0;

    public double ClutchSensitivity { get; set; } = 70;
    public double ClutchBitePoint { get; set; } = 50;

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

public class DeviceParametersSet
{
    public BaseParameters Base { get; set; } = new();
    public SteeringWheelParameters SteeringWheel { get; set; } = new();
    public PedalParameters Pedal { get; set; } = new();

    public DeviceParametersSet Clone()
    {
        return new DeviceParametersSet
        {
            Base = Base.Clone(),
            SteeringWheel = SteeringWheel.Clone(),
            Pedal = Pedal.Clone()
        };
    }

    public void Apply(DeviceParametersSet other)
    {
        Base.Apply(other.Base);
        SteeringWheel.Apply(other.SteeringWheel);
        Pedal.Apply(other.Pedal);
    }

    public void ResetToDefaults()
    {
        Base = new BaseParameters();
        SteeringWheel = new SteeringWheelParameters();
        Pedal = new PedalParameters();
    }
}
