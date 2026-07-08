using System.Runtime.InteropServices;

namespace HITAPEX.Services;

/// <summary>
/// TelemetrySDK.dll 的 C# P/Invoke 封装。
/// 提供 5 个导出函数和 NormalizedData 结构体定义。
/// </summary>
public static class TelemetryAPI
{
    #region DLL Imports

    /// <summary>初始化并启动指定游戏的遥测数据采集</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool StartTelemetry(int gameId);

    /// <summary>获取最新的归一化遥测数据</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool GetTelemetryData(ref NormalizedData outData);

    /// <summary>停止遥测数据采集并释放资源</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void StopTelemetry();

    /// <summary>获取 SDK 版本号</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetSDKVersion();

    /// <summary>获取当前游戏支持的遥测字段掩码</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong GetSupportedFlags();

    #endregion

    #region Data Structures

    /// <summary>
    /// 归一化遥测数据结构体（Pack=1，固定 512 字节）。
    /// 对应 C++ 端 NormalizedData v2.0，所有游戏统一使用此格式输出。
    /// validFlags 偏移为 288B，_reserved 为 224B。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NormalizedData
    {
        // ---- 基础参数 ----
        public float speed;         // 速度 · km/h · [0, 500]
        public float rpm;           // 引擎当前转速 · RPM · [0, 25000]
        public float maxRpm;        // 引擎最大转速 · RPM · [0, 25000]
        public int gear;            // 档位：-1=R倒挡, 0=N空挡, 1-10=前进 · [-1, 10]
        public float throttle;      // 油门踏板 · 0-1 归一化 · [0, 1]
        public float brake;         // 刹车踏板 · 0-1 归一化 · [0, 1]
        public float steer;         // 转向 · -1=左满舵, +1=右满舵 · [-1, 1]

        // ---- 状态标志（bool）----
        [MarshalAs(UnmanagedType.U1)] public bool isPitLimiterActive;   // 维修区限速器
        [MarshalAs(UnmanagedType.U1)] public bool isTcActive;           // TC 牵引力控制激活
        [MarshalAs(UnmanagedType.U1)] public bool isAbsActive;          // ABS 防抱死激活
        [MarshalAs(UnmanagedType.U1)] public bool isDrsAvailable;       // DRS 可用
        [MarshalAs(UnmanagedType.U1)] public bool isDrsActive;          // DRS 激活

        // ---- 轮胎滑移（[0]=FL, [1]=FR, [2]=RL, [3]=RR）----
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] slipRatio;     // 纵向滑移率 · [-2, 5]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] slipAngle;     // 滑移角 · 弧度 · [-π, π]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] combinedSlip;  // 总滑移正交合成 · [-2, 5]

        // ---- ERS / 混合动力 ----
        public float ersCharge;              // ERS 电量 · 0-1 · [0, 1]
        public int ersDeployMode;            // ERS 部署档位索引 · [-1, 20]
        [MarshalAs(UnmanagedType.U1)] public bool isErsActive;     // ERS 工作中
        public int ersRecoveryLevel;         // ERS 回收级别 · 百分比 · [-1, 100]

        // ---- 发动机状态 ----
        [MarshalAs(UnmanagedType.U1)] public bool isEngineRunning;   // 发动机运行
        [MarshalAs(UnmanagedType.U1)] public bool isIgnitionOn;       // 点火开启
        public int enginePowerMode;           // 发动机动力档位索引 · [-1, 20]

        // ---- TC / ABS 档位 ----
        public int tcLevel;                  // TC 档位 · 0=关, 1+=级别 · [-1, 20]
        public int absLevel;                 // ABS 档位 · 0=关, 1+=级别 · [-1, 20]
        public int tcCutLevel;               // TC 削减档位 · 0=关, 1+=级别 · [-1, 20]

        // ---- 燃油 ----
        public float fuelRemaining;          // 剩余燃油 · 升 · [0, 5000]
        public float fuelRemainingPct;       // 剩余燃油百分比 · 0-1 · [0, 1]

        // ---- 赛事旗语 ----
        public int raceFlag;                 // 当前旗帜 · FlagType 枚举 · [0, 10]

        // ============ v2.0 新增：第三批参数（bit 28-44）============

        // ---- 离合 ----
        public float clutch;                 // 离合踏板 · 0-1 归一化 · [0, 1]

        // ---- 圈速计时 ----
        public int currentLap;               // 当前圈数 · 1 起计 · [0, 999]
        public int totalLaps;                // 赛事总圈数 · [0, 999]
        public float currentLapTime;         // 当前圈已用时间 · 秒 · [0, 100000]
        public float lastLapTime;            // 上一圈用时 · 秒 · [0, 100000]
        public float bestLapTime;            // 个人最佳圈时 · 秒 · [0, 100000]

        // ---- 胎面温度（[0]=FL, [1]=FR, [2]=RL, [3]=RR，°C）----
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreTempInner;   // 胎面内侧温度 · [-50, 200]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreTempMiddle;  // 胎面中间温度 · [-50, 200]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreTempOuter;   // 胎面外侧温度 · [-50, 200]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreCoreTemp;    // 轮胎核心温度 · [-50, 200]

        // ---- 胎压 / 胎磨 / 刹车温度（[0]=FL, [1]=FR, [2]=RL, [3]=RR）----
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyrePressure;    // 胎压 · kPa · [0, 500]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreWear;        // 胎磨损 · 0=全新, 100=全磨 · [0, 100]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] brakeTemp;       // 刹车温度 · °C · [-50, 2000]

        // ---- 排名 / 发动机温度 / 涡轮 ----
        public int position;                 // 车手排名 · 1 起计 · [0, 999]
        public float waterTemp;              // 冷却水温 · °C · [-40, 200]
        public float oilTemp;                // 机油温度 · °C · [-40, 200]
        public float turboPressure;          // 涡轮增压压力 · bar · [-5, 5]

        // ============ 字段有效性掩码（v2.0 移至 288B 偏移处）============
        public ulong validFlags;

        // ---- 预留空间：结构体固定 512 字节，当前已用 288 字节，剩余 224 字节 ----
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 224)]
        public byte[] _reserved;
    }

    #endregion

    #region GameId 枚举

    /// <summary>支持的游戏 ID（Steam 游戏使用 Steam App ID，非 Steam 游戏使用自定义 ID）</summary>
    public enum GameId : int
    {
        Unknown = 0,

        // Assetto Corsa 系列
        AssettoCorsa = 244210,
        ACC = 805550,
        ACRally = 3917090,
        AC_Evo = 3058630,

        // F1 系列
        F1_2022 = 1692250,
        F1_2023 = 2108330,
        F1_2024 = 2488620,
        F1_2025 = 3059520,

        // Forza 系列
        ForzaMotorsport = 2440510,
        ForzaHorizon4 = 1293830,
        ForzaHorizon5 = 1551360,
        ForzaHorizon6 = 2483190,

        // DiRT 系列
        DiRT_4 = 421020,
        DiRT_Rally_2 = 690790,

        // rFactor / LMU 系列
        rFactor2 = 365960,
        LMU = 2399420,

        // Project CARS / AMS2 系列
        PCARS2 = 378860,
        PCARS3 = 958400,
        AMS2 = 1066890,

        // WRC 系列
        WRC_8 = 1004750,
        WRC_9 = 1267540,
        WRC_10 = 1462810,
        WRC_Generations = 1953520,
        EA_WRC = 1849250,

        // 其他竞速
        iRacing = 266410,
        R3E = 211500,
        BeamNG = 284160,

        // 模拟驾驶
        SCS_ETS2 = 227300,
        SCS_ATS = 270880,

        // 非 Steam 游戏（自定义 ID）
        RBR = 22,
        LFS = 25,
    }

    #endregion

    #region ValidFlags 字段有效性掩码常量

    /// <summary>字段有效性位掩码常量，按位与 validFlags 判断对应字段是否有效</summary>
    public static class ValidFlags
    {
        // ---- 第一批：基础驾驶（bit 0-14）----
        public const ulong Speed          = 1UL << 0;   // speed
        public const ulong Rpm            = 1UL << 1;   // rpm
        public const ulong MaxRpm         = 1UL << 2;   // maxRpm
        public const ulong Gear           = 1UL << 3;   // gear
        public const ulong Throttle       = 1UL << 4;   // throttle
        public const ulong Brake          = 1UL << 5;   // brake
        public const ulong Steer          = 1UL << 6;   // steer
        public const ulong PitLimiter     = 1UL << 7;   // isPitLimiterActive
        public const ulong TcActive       = 1UL << 8;   // isTcActive
        public const ulong AbsActive      = 1UL << 9;   // isAbsActive
        public const ulong DrsAvailable   = 1UL << 10;  // isDrsAvailable
        public const ulong DrsActive      = 1UL << 11;  // isDrsActive
        public const ulong SlipRatio      = 1UL << 12;  // slipRatio[4]
        public const ulong SlipAngle      = 1UL << 13;  // slipAngle[4]
        public const ulong CombinedSlip   = 1UL << 14;  // combinedSlip[4]

        // ---- 第二批：辅助系统（bit 15-27）----
        public const ulong ErsCharge      = 1UL << 15;  // ersCharge
        public const ulong ErsDeploy      = 1UL << 16;  // ersDeployMode
        public const ulong ErsActive      = 1UL << 17;  // isErsActive
        public const ulong ErsRecovery    = 1UL << 18;  // ersRecoveryLevel
        public const ulong EngineRunning  = 1UL << 19;  // isEngineRunning
        public const ulong Ignition       = 1UL << 20;  // isIgnitionOn
        public const ulong EnginePower    = 1UL << 21;  // enginePowerMode
        public const ulong TcLevel        = 1UL << 22;  // tcLevel
        public const ulong AbsLevel       = 1UL << 23;  // absLevel
        public const ulong TcCut          = 1UL << 24;  // tcCutLevel
        public const ulong Fuel           = 1UL << 25;  // fuelRemaining
        public const ulong FuelPct        = 1UL << 26;  // fuelRemainingPct
        public const ulong RaceFlag       = 1UL << 27;  // raceFlag

        // ---- 第三批：竞赛深度（bit 28-44，v2.0 新增）----
        public const ulong Clutch           = 1UL << 28;  // clutch
        public const ulong CurrentLapNum    = 1UL << 29;  // currentLap
        public const ulong TotalLaps        = 1UL << 30;  // totalLaps
        public const ulong CurrentLapTime   = 1UL << 31;  // currentLapTime
        public const ulong LastLap          = 1UL << 32;  // lastLapTime
        public const ulong BestLap          = 1UL << 33;  // bestLapTime
        public const ulong TyreTempInner    = 1UL << 34;  // tyreTempInner[4]
        public const ulong TyreTempMiddle   = 1UL << 35;  // tyreTempMiddle[4]
        public const ulong TyreTempOuter    = 1UL << 36;  // tyreTempOuter[4]
        public const ulong TyreCoreTemp     = 1UL << 37;  // tyreCoreTemp[4]
        public const ulong TyrePressure     = 1UL << 38;  // tyrePressure[4]
        public const ulong TyreWear         = 1UL << 39;  // tyreWear[4]
        public const ulong BrakeTemp        = 1UL << 40;  // brakeTemp[4]
        public const ulong Position         = 1UL << 41;  // position
        public const ulong WaterTemp        = 1UL << 42;  // waterTemp
        public const ulong OilTemp          = 1UL << 43;  // oilTemp
        public const ulong TurboPressure    = 1UL << 44;  // turboPressure
    }

    #endregion

    #region FlagType 枚举

    /// <summary>统一旗帜枚举</summary>
    public enum FlagType
    {
        None      = 0,   // 无旗/绿旗（正常比赛）
        Blue      = 1,   // 蓝旗（让车）
        Yellow    = 2,   // 黄旗（危险/减速）
        Black     = 3,   // 黑旗（取消资格）
        White     = 4,   // 白旗（慢车）
        Checkered = 5,   // 方格旗（比赛结束）
        Penalty   = 6,   // 处罚旗
        Orange    = 7,   // 橙旗（机械故障）
        Red       = 8,   // 红旗（比赛暂停）
        SC        = 9,   // 安全车
        VSC       = 10,  // 虚拟安全车
    }

    #endregion

    #region 辅助方法

    /// <summary>将挡位值转换为显示字符串（v2.0：-1=R, 0=N, 1-10=前进挡）</summary>
    public static string GetGearName(int gear) =>
        gear switch
        {
            -1 => "R",
            0 => "N",
            >= 1 and <= 10 => gear.ToString(),
            _ => "?"
        };

    /// <summary>判断遥测数据是否有效（游戏在赛道上）</summary>
    public static bool IsDataValid(NormalizedData data) =>
        data.rpm > 0.0f || data.speed > 0.0f;

    /// <summary>创建已初始化内联数组的 NormalizedData 实例（v2.0：_reserved = 224）</summary>
    public static NormalizedData CreateNormalizedData()
    {
        return new NormalizedData
        {
            slipRatio = new float[4],
            slipAngle = new float[4],
            combinedSlip = new float[4],
            tyreTempInner = new float[4],
            tyreTempMiddle = new float[4],
            tyreTempOuter = new float[4],
            tyreCoreTemp = new float[4],
            tyrePressure = new float[4],
            tyreWear = new float[4],
            brakeTemp = new float[4],
            _reserved = new byte[224]
        };
    }

    /// <summary>检查 validFlags 中指定字段是否有效</summary>
    public static bool HasFlag(NormalizedData data, ulong flag) =>
        (data.validFlags & flag) != 0;

    #endregion
}
