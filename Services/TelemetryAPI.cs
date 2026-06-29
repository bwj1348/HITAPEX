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
    /// 对应 C++ 端 NormalizedData，所有游戏统一使用此格式输出。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NormalizedData
    {
        // ---- 基础参数 ----
        public float speed;         // 速度，单位：km/h
        public float rpm;           // 引擎当前转速 (RPM)
        public float maxRpm;        // 引擎最大转速 (RPM)
        public int gear;            // 挡位：0=空挡(N)，1-8=前进挡，-1=倒挡(R)
        public float throttle;      // 油门踏板开度：0.0 - 1.0
        public float brake;         // 刹车踏板开度：0.0 - 1.0
        public float steer;         // 转向角度：-1.0(左) 到 1.0(右)

        // ---- 状态标志 ----
        [MarshalAs(UnmanagedType.U1)]
        public bool isPitLimiterActive;
        [MarshalAs(UnmanagedType.U1)]
        public bool isTcActive;
        [MarshalAs(UnmanagedType.U1)]
        public bool isAbsActive;
        [MarshalAs(UnmanagedType.U1)]
        public bool isDrsAvailable;
        [MarshalAs(UnmanagedType.U1)]
        public bool isDrsActive;

        // ---- 轮胎滑移数据 (0=FL, 1=FR, 2=RL, 3=RR) ----
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] slipRatio;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] slipAngle;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] combinedSlip;

        // ---- ERS/混合动力系统 ----
        public float ersCharge;          // ERS电量百分比 0.0-1.0（无ERS系统=-1.0）
        public int ersDeployMode;        // ERS部署档位索引（-1=不支持）
        [MarshalAs(UnmanagedType.U1)]
        public bool isErsActive;
        public int ersRecoveryLevel;     // ERS回收级别（-1=不支持, 0-100百分比）

        // ---- 发动机状态系统 ----
        [MarshalAs(UnmanagedType.U1)]
        public bool isEngineRunning;
        [MarshalAs(UnmanagedType.U1)]
        public bool isIgnitionOn;
        public int enginePowerMode;      // 发动机动力档位（-1=不支持）

        // ---- 牵引力控制/ABS 档位系统 ----
        public int tcLevel;              // TC牵引力控制档位（-1=不支持, 0=关闭）
        public int absLevel;             // ABS防抱死制动档位（-1=不支持, 0=关闭）
        public int tcCutLevel;           // TC削减档位（-1=不支持, 0=关闭）

        // ---- 燃油系统 ----
        public float fuelRemaining;      // 剩余燃油量（升），-1.0=不支持
        public float fuelRemainingPct;   // 剩余燃油百分比 0.0-1.0，-1.0=不支持

        // ---- 赛事旗语系统 ----
        public int raceFlag;             // 当前旗帜状态（FlagType 枚举值）

        // ---- 字段有效性掩码 ----
        public ulong validFlags;

        // ---- 预留空间（结构体固定 512 字节）----
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 376)]
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

    /// <summary>将挡位值转换为显示字符串</summary>
    public static string GetGearName(int gear) =>
        gear switch
        {
            -1 => "R",
            0 => "N",
            >= 1 and <= 8 => gear.ToString(),
            >= 9 and <= 100 => gear.ToString(),
            _ => "?"
        };

    /// <summary>判断遥测数据是否有效（游戏在赛道上）</summary>
    public static bool IsDataValid(NormalizedData data) =>
        data.rpm > 0.0f || data.speed > 0.0f;

    /// <summary>创建已初始化内联数组的 NormalizedData 实例</summary>
    public static NormalizedData CreateNormalizedData()
    {
        return new NormalizedData
        {
            slipRatio = new float[4],
            slipAngle = new float[4],
            combinedSlip = new float[4],
            _reserved = new byte[376]
        };
    }

    /// <summary>检查 validFlags 中指定字段是否有效</summary>
    public static bool HasFlag(NormalizedData data, ulong flag) =>
        (data.validFlags & flag) != 0;

    #endregion
}
