using System.Runtime.InteropServices;
using System.Text;

namespace HITAPEX.Services;

/// <summary>
/// TelemetrySDK.dll 的 C# P/Invoke 封装（SDK v1.0.0，10 个导出函数）。
/// 结构体/枚举/常量定义与《TelemetrySDK_API_Document.md》4.4 / 8.2 / 10.3 / 附录 A 逐行对应。
/// </summary>
public static class TelemetryAPI
{
    #region DLL Imports

    /// <summary>初始化并启动指定游戏的遥测数据采集（运行中重复调用安全：内部先停旧会话再启新会话）</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool StartTelemetry(int gameId);

    /// <summary>获取最新的归一化遥测数据。true = 会话存在；不代表数据已到达（数据到达性走 GetTelemetryStatus）</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool GetTelemetryData(ref NormalizedData outData);

    /// <summary>停止遥测数据采集并释放资源（幂等）</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void StopTelemetry();

    /// <summary>获取 SDK 版本号（主*10000 + 次*100 + 修订，1.0.0 → 10000）</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetSDKVersion();

    /// <summary>获取当前游戏声明的支持字段掩码（启动时快照；未启动返回 0）</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong GetSupportedFlags();

    /// <summary>配置 UDP 监听端口与 fan-out 转发（须在 StartTelemetry 之前调用；仅 UDP 适配器有效）</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool SetUDPSettings(int gameId, ref TelemetryUDPSettings settings);

    /// <summary>清除指定游戏的 UDP 配置、恢复默认（settings = null）</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool SetUDPSettings(int gameId, IntPtr settings);

    /// <summary>健康层运行时开关（默认 true=生产行为；false=旁路范围校验与 NaN 清洗，开发诊断用）</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetRangeCheckEnabled([MarshalAs(UnmanagedType.U1)] bool enabled);

    /// <summary>最近一次 StartTelemetry 失败码（成功即清 0；GetTelemetryData 空转不写错误）</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetLastTelemetryError();

    /// <summary>获取失败码对应的中文错误消息（UTF-8）。buf=null 或 bufSize&lt;=0 时为探长模式，仅返回所需长度</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetLastTelemetryErrorMessage(byte[]? buf, int bufSize);

    /// <summary>SDK/连接状态快照（每帧现算；未启动时返回 true + 全默认字段）</summary>
    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool GetTelemetryStatus(ref TelemetryStatus status);

    #endregion

    #region Data Structures

    /// <summary>
    /// 归一化遥测数据结构体（Pack=1，固定 512 字节，SDK v1.0.0 布局）。
    /// 字段物理顺序 = VALID 位号（0-45），分组：驾驶输入 → 车辆动态 → 轮胎 → 动力系统 → 辅助系统 → 赛事状态 → 元信息。
    /// 轮胎/刹车数组顺序：[0]=FL, [1]=FR, [2]=RL, [3]=RR。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NormalizedData
    {
        // —— 驾驶输入（bit 0-5）——
        public float throttle;                                   // 0.0-1.0
        public float brake;                                      // 0.0-1.0
        public float steer;                                      // -1.0(左) ~ 1.0(右)
        public float clutch;                                     // 0.0-1.0，1=踩到底（与油门/刹车同向）
        public int gear;                                         // 0=N, 1+=前进, -1=R1, -2=R2…
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
        public string gearLabel;                                 // "4H"/"R1" 等，空串=""

        // —— 车辆动态（bit 6-9）——
        public float speed;                                      // km/h（恒非负）
        public float rpm;
        public float maxRpm;
        public float shiftRpm;                                   // 建议换挡转速

        // —— 轮胎（bit 10-18；[0]=FL [1]=FR [2]=RL [3]=RR）——
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] tyrePressure;                             // kPa
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] tyreCoreTemp;                             // °C
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] tyreTempInner;                            // °C 靠车体中心一侧
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] tyreTempMiddle;                           // °C 胎面中心
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] tyreTempOuter;                            // °C 胎体外侧
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] tyreWear;                                 // 0-100 递增%
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] brakeTemp;                                // °C
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U1)]
        public bool[] isWheelLocked;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.U1)]
        public bool[] isWheelSlipping;

        // —— 动力系统（bit 19-29）——
        public float fuelRemaining;                              // 升
        public float fuelRemainingPct;                           // 0.0-1.0
        public float waterTemp;                                  // °C
        public float oilTemp;                                    // °C
        public float turboPressure;                              // bar
        public float ersCharge;                                  // 0.0-1.0
        public int ersDeployMode;                                // 档位索引（随游戏）
        public int ersRecoveryLevel;                             // 0-100
        [MarshalAs(UnmanagedType.U1)] public bool isEngineRunning;
        [MarshalAs(UnmanagedType.U1)] public bool isIgnitionOn;
        public int enginePowerMode;                              // 发动机模式（Engine Map）档位索引

        // —— 辅助系统（bit 30-38）——
        [MarshalAs(UnmanagedType.U1)] public bool isPitLimiterActive;
        [MarshalAs(UnmanagedType.U1)] public bool isInPits;
        [MarshalAs(UnmanagedType.U1)] public bool isTcActive;
        public int tcLevel;                                      // 0=关, 1+=级别
        public int tcCutLevel;                                   // 0=关, 1+=级别
        [MarshalAs(UnmanagedType.U1)] public bool isAbsActive;
        public int absLevel;                                     // 0=关, 1+=级别
        [MarshalAs(UnmanagedType.U1)] public bool isDrsAvailable;
        [MarshalAs(UnmanagedType.U1)] public bool isDrsActive;

        // —— 赛事状态（bit 39-45）——
        public int currentLap;                                   // 1 起计
        public int totalLaps;
        public int position;                                     // 1 起计
        public float currentLapTime;                             // 秒
        public float lastLapTime;                                // 秒
        public float bestLapTime;                                // 秒

        // —— 旗语（bit 45 共用一位；绿旗居首；可同帧并发多旗）——
        [MarshalAs(UnmanagedType.U1)] public bool isGreenFlag;
        [MarshalAs(UnmanagedType.U1)] public bool isBlueFlag;
        [MarshalAs(UnmanagedType.U1)] public bool isYellowFlag;
        [MarshalAs(UnmanagedType.U1)] public bool isBlackFlag;
        [MarshalAs(UnmanagedType.U1)] public bool isWhiteFlag;
        [MarshalAs(UnmanagedType.U1)] public bool isCheckeredFlag;
        [MarshalAs(UnmanagedType.U1)] public bool isPenaltyFlag;
        [MarshalAs(UnmanagedType.U1)] public bool isOrangeFlag;   // 肉丸旗（机械故障）
        [MarshalAs(UnmanagedType.U1)] public bool isRedFlag;
        [MarshalAs(UnmanagedType.U1)] public bool isScActive;     // 安全车
        [MarshalAs(UnmanagedType.U1)] public bool isVscActive;    // 虚拟安全车

        // —— 元信息 ——
        public ulong validFlags;                                 // 有效性掩码，见 ValidFlags

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 249)]
        public byte[] _reserved;                                 // 263 已用 + 249 = 512
    }

    /// <summary>
    /// SDK/连接状态快照（Pack=4，20 字节）。见 GetTelemetryStatus。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TelemetryStatus
    {
        public int sdkState;        // TEL_SDK_IDLE(0) / TEL_SDK_RUNNING(1)
        public int connState;       // TelemetryConnState 六态
        public int lastError;       // 错误槽透传（同 GetLastTelemetryError）
        public uint dataAgeMs;      // 数据年龄；0xFFFFFFFF = 未知/尚无数据
        public uint staleTimeoutMs; // STALE 判定阈值（默认 5000ms）
    }

    /// <summary>UDP 转发目标（ip 为 IPv4 字符串，null 终止）</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct TelemetryForwardTarget
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
        public string ip;
        public ushort port;
    }

    /// <summary>
    /// UDP 配置（自然对齐，勿加 Pack=1）：listenPort 与 forwardCount 之间有 2 字节 padding，总大小 344 字节。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TelemetryUDPSettings
    {
        public ushort listenPort;                       // 0 = 不覆盖，用适配器默认端口
        public uint forwardCount;                       // 0 = 不转发，最大 8
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public TelemetryForwardTarget[] forwardTargets; // 仅前 forwardCount 个生效
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
        Acevo = 3058630,

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
        Dirt4 = 421020,
        DirtRally2 = 690790,

        // rFactor / LMU
        RF2 = 365960,
        LMU = 2399420,

        // PCARS / AMS2
        PCars2 = 378860,
        PCars3 = 958400,
        AMS2 = 1066890,

        // WRC 系列
        WRC8 = 1004750,
        WRC9 = 1267540,
        WRC10 = 1462810,
        WRCGenerations = 1953520,
        EAWRC = 1849250,

        // 其他竞速
        IRacing = 266410,
        R3E = 211500,
        BeamNG = 284160,

        // 模拟驾驶
        SCSEts2 = 227300,
        SCSAts = 270880,

        // 非 Steam 游戏（自定义 ID）
        RBR = 22,
        LFS = 25,
    }

    #endregion

    #region ValidFlags 字段有效性掩码常量

    /// <summary>字段有效性位掩码常量（bit 号 = 字段物理顺序），按位与 validFlags 判断对应字段是否有效</summary>
    public static class ValidFlags
    {
        public const ulong Throttle       = 1UL << 0;   // throttle
        public const ulong Brake          = 1UL << 1;   // brake
        public const ulong Steer          = 1UL << 2;   // steer
        public const ulong Clutch         = 1UL << 3;   // clutch
        public const ulong Gear           = 1UL << 4;   // gear
        public const ulong GearLabel      = 1UL << 5;   // gearLabel[8]
        public const ulong Speed          = 1UL << 6;   // speed
        public const ulong Rpm            = 1UL << 7;   // rpm
        public const ulong MaxRpm         = 1UL << 8;   // maxRpm
        public const ulong ShiftRpm       = 1UL << 9;   // shiftRpm
        public const ulong TyrePressure   = 1UL << 10;  // tyrePressure[4]
        public const ulong TyreCoreTemp   = 1UL << 11;  // tyreCoreTemp[4]
        public const ulong TyreTempInner  = 1UL << 12;  // tyreTempInner[4]
        public const ulong TyreTempMiddle = 1UL << 13;  // tyreTempMiddle[4]
        public const ulong TyreTempOuter  = 1UL << 14;  // tyreTempOuter[4]
        public const ulong TyreWear       = 1UL << 15;  // tyreWear[4]
        public const ulong BrakeTemp      = 1UL << 16;  // brakeTemp[4]
        public const ulong WheelLocked    = 1UL << 17;  // isWheelLocked[4]（一位覆盖整个数组）
        public const ulong WheelSlipping  = 1UL << 18;  // isWheelSlipping[4]
        public const ulong Fuel           = 1UL << 19;  // fuelRemaining
        public const ulong FuelPct        = 1UL << 20;  // fuelRemainingPct
        public const ulong WaterTemp      = 1UL << 21;  // waterTemp
        public const ulong OilTemp        = 1UL << 22;  // oilTemp
        public const ulong TurboPressure  = 1UL << 23;  // turboPressure
        public const ulong ErsCharge      = 1UL << 24;  // ersCharge
        public const ulong ErsDeploy      = 1UL << 25;  // ersDeployMode
        public const ulong ErsRecovery    = 1UL << 26;  // ersRecoveryLevel
        public const ulong EngineRunning  = 1UL << 27;  // isEngineRunning
        public const ulong Ignition       = 1UL << 28;  // isIgnitionOn
        public const ulong EnginePower    = 1UL << 29;  // enginePowerMode
        public const ulong PitLimiter     = 1UL << 30;  // isPitLimiterActive
        public const ulong InPits         = 1UL << 31;  // isInPits
        public const ulong TcActive       = 1UL << 32;  // isTcActive
        public const ulong TcLevel        = 1UL << 33;  // tcLevel
        public const ulong TcCut          = 1UL << 34;  // tcCutLevel
        public const ulong AbsActive      = 1UL << 35;  // isAbsActive
        public const ulong AbsLevel       = 1UL << 36;  // absLevel
        public const ulong DrsAvailable   = 1UL << 37;  // isDrsAvailable
        public const ulong DrsActive      = 1UL << 38;  // isDrsActive
        public const ulong CurrentLapNum  = 1UL << 39;  // currentLap
        public const ulong TotalLaps      = 1UL << 40;  // totalLaps
        public const ulong Position       = 1UL << 41;  // position
        public const ulong CurrentLap     = 1UL << 42;  // currentLapTime
        public const ulong LastLap        = 1UL << 43;  // lastLapTime
        public const ulong BestLap        = 1UL << 44;  // bestLapTime
        public const ulong RaceFlag       = 1UL << 45;  // 旗语 11 bool 共用一位
    }

    #endregion

    #region 错误码 / 连接状态枚举

    /// <summary>StartTelemetry 失败错误码（仅记录启动失败；成功即清 0）</summary>
    public enum TelemetryErrorCode
    {
        None = 0,             // 无错误（上次启动成功或尚未调用）
        InvalidGameId = 1,    // gameId 不在支持列表
        GameNotRunning = 2,   // 共享内存映射不存在（仅 iRacing 触发；sim 未开）
        UdpPortInUse = 3,     // UDP 监听端口被占用（bind 10048）
        WinsockFailure = 4,   // Winsock 初始化/socket 创建失败
        InitFailed = 5,       // 其他初始化失败
    }

    /// <summary>
    /// 连接状态机六态（sdkState=RUNNING 时的连接/新鲜度）。
    /// 数值为 SDK 实际定义（TelemetryConnState）。
    /// </summary>
    public enum TelemetryConnState
    {
        Idle = 0,          // SDK 未启动
        WaitingData = 1,   // 已启动但从未观察到数据（游戏没在发/写）
        Connected = 2,     // 数据新鲜（dataAgeMs ≤ staleTimeoutMs）
        Stale = 3,         // 超阈值无新数据（游戏退出/暂停/回菜单）
        Disconnected = 4,  // 传输层失效（如 iRacing sim 退出）
        UnknownAge = 5,    // ABI 兼容保留，1.0.0 起实际不可达
    }

    /// <summary>SDK 运行状态（TelemetryStatus.sdkState）</summary>
    public enum TelemetrySdkState
    {
        Idle = 0,     // TEL_SDK_IDLE：未启动
        Running = 1,  // TEL_SDK_RUNNING：会话存在
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 将挡位值转换为显示字符串（v1.0.0：-1=R1、-2=R2、-3=R3、-4=R4 多倒挡；0=N；1+=前进挡）。
    /// </summary>
    public static string GetGearName(int gear) =>
        gear switch
        {
            -1 => "R1",
            -2 => "R2",
            -3 => "R3",
            -4 => "R4",
            0 => "N",
            >= 1 => gear.ToString(),
            _ => "?"
        };

    /// <summary>判断遥测数据是否有效（引擎转速或车速 > 0，视为游戏在赛道上）</summary>
    public static bool IsDataValid(NormalizedData data) =>
        data.rpm > 0.0f || data.speed > 0.0f;

    /// <summary>创建已初始化内联数组的 NormalizedData 实例（_reserved = 249）</summary>
    public static NormalizedData CreateNormalizedData()
    {
        return new NormalizedData
        {
            gearLabel = "",
            tyrePressure = new float[4],
            tyreCoreTemp = new float[4],
            tyreTempInner = new float[4],
            tyreTempMiddle = new float[4],
            tyreTempOuter = new float[4],
            tyreWear = new float[4],
            brakeTemp = new float[4],
            isWheelLocked = new bool[4],
            isWheelSlipping = new bool[4],
            _reserved = new byte[249],
        };
    }

    /// <summary>检查 validFlags 中指定字段是否有效</summary>
    public static bool HasFlag(NormalizedData data, ulong flag) =>
        (data.validFlags & flag) != 0;

    /// <summary>获取最近一次 StartTelemetry 失败的中文错误消息（UTF-8，探长模式取长度后再读内容）</summary>
    public static string GetLastErrorMessage()
    {
        int need = GetLastTelemetryErrorMessage(null, 0);
        if (need <= 0) return string.Empty;

        var buf = new byte[need];
        int written = GetLastTelemetryErrorMessage(buf, need);
        if (written <= 0) return string.Empty;

        // SDK 写入内容含结尾 null，去掉
        int len = written - 1;
        if (len > buf.Length) len = buf.Length;
        return Encoding.UTF8.GetString(buf, 0, len);
    }

    /// <summary>数据年龄是否可判定为新鲜（age == 0xFFFFFFFF = 未知）</summary>
    public static bool IsDataFresh(TelemetryStatus status) =>
        status.connState == (int)TelemetryConnState.Connected &&
        status.dataAgeMs != 0xFFFFFFFF &&
        status.dataAgeMs <= status.staleTimeoutMs;

    #endregion
}
