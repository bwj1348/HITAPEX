# TelemetrySDK DLL 接口文档

## 📋 文档概述

本文档为 `TelemetrySDK.dll` 的完整接口说明文档，面向C#开发者提供P/Invoke调用指南。

**DLL信息**
- **文件名**: `TelemetrySDK.dll`
- **开发语言**: C++ (C++17标准)
- **编译器**: MSVC (Visual Studio 2022)
- **目标平台**: Windows x64
- **导出方式**: `__declspec(dllexport)` + `extern "C"`

**主要功能**
- 支持 31 款赛车/模拟驾驶游戏的遥测数据获取
- 统一的数据输出格式（`NormalizedData`），便于上层应用处理
- 字段有效性掩码（`validFlags`），按位判断各字段是否有效
- 基于共享内存和UDP通信的高性能数据采集
- 简单易用的C接口，支持C# P/Invoke调用
- 内存对齐采用 `#pragma pack(push, 1)` 确保C++与C#的P/Invoke互操作正确

---

## 🎮 支持的游戏列表

| GameId 值 (Steam App ID) | 枚举名称 | 游戏名称 | 通信方式 | 适配器 | 说明 |
|----------|---------|---------|----------|--------|------|
| 244210 | `GAME_ASSETTO_CORSA` | Assetto Corsa | 混合(共享内存+UDP) | ACAdapter | 3块共享内存 + UDP请求-响应 |
| 805550 | `GAME_ACC` | Assetto Corsa Competizione | 共享内存 | ACCAdapter | 3块共享内存 |
| 3917090 | `GAME_ACRALLY` | Assetto Corsa Rally | 共享内存 | ACCAdapter | 与ACC完全兼容 |
| 3058630 | `GAME_AC_EVO` | Assetto Corsa EVO | 共享内存 | ACEvoAdapter | 3块共享内存 |
| 1692250 | `GAME_F1_2022` | F1 22 | UDP (20777) | F122Adapter | |
| 2108330 | `GAME_F1_2023` | F1 23 | UDP (20777) | F123Adapter | |
| 2488620 | `GAME_F1_2024` | F1 24 | UDP (20777) | F124Adapter | |
| 3059520 | `GAME_F1_2025` | F1 25 | UDP (20777) | F125Adapter | |
| 1293830 | `GAME_FORZA_HORIZON_4` | Forza Horizon 4 | UDP (1024) | FH45Adapter | FH4/FH5共用 |
| 1551360 | `GAME_FORZA_HORIZON_5` | Forza Horizon 5 | UDP (1024) | FH45Adapter | FH4/FH5共用 |
| 2483190 | `GAME_FORZA_HORIZON_6` | Forza Horizon 6 | UDP (20440) | FH45Adapter | 端口与FH4/5不同 |
| 2440510 | `GAME_FORZA_MOTORSPORT` | Forza Motorsport 2023 | UDP (1024) | FM2023Adapter | |
| 421020 | `GAME_DIRT_4` | DiRT 4 | UDP (20777) | DiRTAdapter | DiRT系列共用 |
| 690790 | `GAME_DIRT_RALLY_2` | DiRT Rally 2.0 | UDP (20777) | DiRTAdapter | DiRT系列共用 |
| 365960 | `GAME_RF2` | rFactor 2 | 共享内存 | RF2LMUAdapter | 与LMU共用适配器 |
| 2399420 | `GAME_LMU` | Le Mans Ultimate | 共享内存 | RF2LMUAdapter | 与rF2共用适配器 |
| 378860 | `GAME_PCARS2` | Project CARS 2 | 共享内存 (`$pcars2$`) | AMS2PC23Adapter | AMS2/PC2/PC3共用 |
| 958400 | `GAME_PCARS3` | Project CARS 3 | 共享内存 (`$pcars2$`) | AMS2PC23Adapter | AMS2/PC2/PC3共用 |
| 1066890 | `GAME_AMS2` | Automobilista 2 | 共享内存 (`$pcars2$`) | AMS2PC23Adapter | AMS2/PC2/PC3共用 |
| 1004750 | `GAME_WRC_8` | WRC 8 | UDP (20777) | WrcPatchAdapter | WRC系列共用 |
| 1267540 | `GAME_WRC_9` | WRC 9 | UDP (20777) | WrcPatchAdapter | WRC系列共用 |
| 1462810 | `GAME_WRC_10` | WRC 10 | UDP (20777) | WrcPatchAdapter | WRC系列共用 |
| 1953520 | `GAME_WRC_GENERATIONS` | WRC Generations | UDP (20777) | WRCGAdapter | 原生UDP遥测 |
| 1849250 | `GAME_EA_WRC` | EA Sports WRC | UDP (26666) | EAWRCAdapter | |
| 266410 | `GAME_IRACING` | iRacing | 共享内存 | IRacingAdapter | 动态变量系统 |
| 211500 | `GAME_R3E` | RaceRoom Racing Experience | 共享内存 (`$R3E`) | R3EAdapter | |
| 284160 | `GAME_BEAMNG` | BeamNG.drive | UDP (30000) | LFSBeamNGAdapter | OutGauge协议，与LFS共用 |
| 227300 | `GAME_SCS_ETS2` | Euro Truck Simulator 2 | 共享内存 | SCSAdapter | 与ATS共用 |
| 270880 | `GAME_SCS_ATS` | American Truck Simulator | 共享内存 | SCSAdapter | 与ETS2共用 |
| 22 | `GAME_RBR` | Richard Burns Rally | UDP (30000) | RBRAdapter | 非Steam游戏，自定义ID |
| 25 | `GAME_LFS` | Live for Speed | UDP (30000) | LFSBeamNGAdapter | 非Steam游戏，自定义ID |

---

## 🔧 API接口详解

### 1. StartTelemetry
**功能描述**: 初始化并启动指定游戏的遥测数据采集

**C++ 声明**:
```cpp
DLLEXPORT bool StartTelemetry(int gameId);
```

**C# P/Invoke 定义**:
```csharp
[DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.U1)]
public static extern bool StartTelemetry(int gameId);
```

**参数说明**:
- `gameId` (int): 游戏ID，对应上面的支持游戏列表中的 `GameId 值 (Steam App ID)`
  - 例如：`StartTelemetry(805550)` 启动ACC适配器
  - 例如：`StartTelemetry(266410)` 启动iRacing适配器
  - 例如：`StartTelemetry(3059520)` 启动F1 2025适配器

**返回值**:
- `true`: 成功启动适配器
- `false`: 启动失败（游戏未运行、游戏ID不支持、初始化失败等）

**注意事项**:
- 重复调用会自动先停止当前适配器，再启动新的适配器（安全切换）
- 成功后会自动设置当前游戏支持的字段掩码，可通过 `GetSupportedFlags()` 查询
- 确保目标游戏正在运行且处于活动状态
- 某些游戏需要在赛道上（不在菜单界面）才能成功初始化
- 共用适配器的游戏（如FH4/FH5、DiRT系列、AMS2/PC2/PC3）内部会自动处理差异

---

### 2. GetTelemetryData
**功能描述**: 获取最新的归一化遥测数据，并附带字段有效性掩码

**C++ 声明**:
```cpp
DLLEXPORT bool GetTelemetryData(NormalizedData* outData);
```

**C# P/Invoke 定义**:
```csharp
[DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
public static extern bool GetTelemetryData(ref NormalizedData outData);
```

**参数说明**:
- `outData` (ref NormalizedData): 传引用的数据结构，函数会填充最新数据
  - 注意：在C#中必须使用 `ref` 关键字传递结构体引用

**返回值**:
- `true`: 数据获取成功，`outData` 中包含有效数据
- `false`: 获取失败（适配器未启动或传入空指针）

**调用频率建议**:
- 建议60Hz（每16ms调用一次）
- 部分游戏支持更高频率（如iRacing支持60-120Hz）
- 避免过高频率导致CPU占用过大

---

### 3. StopTelemetry
**功能描述**: 停止遥测数据采集并释放资源

**C++ 声明**:
```cpp
DLLEXPORT void StopTelemetry();
```

**C# P/Invoke 定义**:
```csharp
[DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
public static extern void StopTelemetry();
```

**参数说明**: 无

**返回值**: 无

**注意事项**:
- 退出应用前必须调用，否则可能导致资源泄漏
- 可以安全地重复调用（幂等操作）
- 停止后会重置字段掩码，`GetSupportedFlags()` 返回 0

---

### 4. GetSDKVersion
**功能描述**: 获取SDK版本号，供调用者检查DLL版本是否匹配

**C++ 声明**:
```cpp
DLLEXPORT int GetSDKVersion();
```

**C# P/Invoke 定义**:
```csharp
[DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
public static extern int GetSDKVersion();
```

**返回值**: SDK版本号（整数，当前版本返回 `1`）

---

### 5. GetSupportedFlags
**功能描述**: 获取当前游戏支持的遥测字段掩码，在 `StartTelemetry` 成功后调用

**C++ 声明**:
```cpp
DLLEXPORT uint64_t GetSupportedFlags();
```

**C# P/Invoke 定义**:
```csharp
[DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
public static extern ulong GetSupportedFlags();
```

**返回值**: 字段有效性位掩码，每位对应一个字段，置1表示该字段有有效数据。详见下方 `ValidFlags` 常量定义。

**注意事项**:
- 必须在 `StartTelemetry` 成功后调用，否则返回 0
- 与 `GetTelemetryData` 返回的 `outData.validFlags` 值一致
- 可在启动后一次性查询，无需每帧调用

---

## 📊 数据结构定义

### NormalizedData 结构体
这是所有游戏统一的输出数据格式。使用1字节对齐（`Pack=1`）确保P/Invoke互操作正确。
**结构体固定大小为 512 字节**，其中当前已用字段占 136 字节，末尾预留 376 字节供后续扩展。

**C++ 定义**:
```cpp
#pragma pack(push, 1)
struct NormalizedData {
    // ---- 基础参数 ----
    float speed;       // 速度，单位：km/h
    float rpm;          // 引擎当前转速 (RPM)
    float maxRpm;       // 引擎最大转速 (RPM)
    int gear;           // 挡位：0为空挡(N)，1-8为前进挡，-1为倒挡(R)
    float throttle;     // 油门踏板开度：0.0 - 1.0
    float brake;        // 刹车踏板开度：0.0 - 1.0
    float steer;        // 转向角度：-1.0(左) 到 1.0(右)

    // ---- 状态标志 ----
    bool isPitLimiterActive;     // 维修区限速器激活状态
    bool isTcActive;             // 牵引力控制系统(TC)激活状态
    bool isAbsActive;            // 防抱死制动系统(ABS)激活状态
    bool isDrsAvailable;         // 可调尾翼系统(DRS)是否可用
    bool isDrsActive;            // 可调尾翼系统(DRS)激活状态

    // ---- 轮胎滑移数据 (数组大小4，顺序: 0=FL, 1=FR, 2=RL, 3=RR) ----
    float slipRatio[4];      // 纵向滑移率 (-1.0 to +inf)
    float slipAngle[4];      // 滑移角 (弧度，带正负号)
    float combinedSlip[4];   // 总滑移 (正交合成值)

    // ---- ERS/混合动力系统 (不支持时填充默认值) ----
    float ersCharge;            // ERS电量百分比 0.0-1.0（无ERS系统=-1.0）
    int ersDeployMode;          // ERS部署档位索引（-1=不支持, 0+=实际档位索引）
    bool isErsActive;           // ERS是否正在工作（false=未工作/不支持）
    int ersRecoveryLevel;       // ERS回收级别（-1=不支持, 0-100百分比）

    // ---- 发动机状态系统 (不支持时填充默认值) ----
    bool isEngineRunning;      // 发动机是否正在运行（false=未运行/不支持）
    bool isIgnitionOn;         // 点火开关是否开启（false=未点火/不支持）
    int enginePowerMode;       // 发动机动力档位（-1=不支持, 0+=实际档位索引）

    // ---- 牵引力控制/ABS档位系统 (不支持时填充默认值) ----
    int tcLevel;        // TC牵引力控制档位（-1=不支持, 0=关闭, 1+=实际档位级别）
    int absLevel;       // ABS防抱死制动档位（-1=不支持, 0=关闭, 1+=实际档位级别）
    int tcCutLevel;     // TC削减档位（-1=不支持, 0=关闭, 1+=实际削减级别）

    // ---- 燃油系统 (不支持时填充默认值) ----
    float fuelRemaining;      // 剩余燃油量（升），-1.0=不支持
    float fuelRemainingPct;   // 剩余燃油百分比 0.0-1.0，-1.0=不支持

    // ---- 赛事旗语系统 (不支持时填充默认值) ----
    int raceFlag;     // 当前旗帜状态（FlagType枚举值）

    // ---- 字段有效性掩码（每位对应一个字段，置1表示该字段有有效数据）----
    uint64_t validFlags;   // 详见 ValidFlags 常量定义

    // ---- 预留空间：结构体固定512字节，当前已用136字节，剩余376字节供后续扩展 ----
    unsigned char _reserved[376];
};
#pragma pack(pop)
```

**C# 定义**:
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NormalizedData
{
    // ---- 基础参数 ----
    public float speed;           // 速度，单位：km/h
    public float rpm;             // 引擎当前转速 (RPM)
    public float maxRpm;          // 引擎最大转速 (RPM)
    public int gear;              // 挡位：0=空挡(N)，1-8=前进挡，-1=倒挡(R)
    public float throttle;         // 油门踏板开度：0.0 - 1.0
    public float brake;            // 刹车踏板开度：0.0 - 1.0
    public float steer;            // 转向角度：-1.0(左) 到 1.0(右)

    // ---- 状态标志 ----
    [MarshalAs(UnmanagedType.U1)]
    public bool isPitLimiterActive;   // 维修区限速器激活状态
    [MarshalAs(UnmanagedType.U1)]
    public bool isTcActive;           // 牵引力控制系统(TC)激活状态
    [MarshalAs(UnmanagedType.U1)]
    public bool isAbsActive;          // 防抱死制动系统(ABS)激活状态
    [MarshalAs(UnmanagedType.U1)]
    public bool isDrsAvailable;        // 可调尾翼系统(DRS)是否可用
    [MarshalAs(UnmanagedType.U1)]
    public bool isDrsActive;           // 可调尾翼系统(DRS)激活状态

    // ---- 轮胎滑移数据 (数组大小4，顺序: 0=FL, 1=FR, 2=RL, 3=RR) ----
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] slipRatio;         // 纵向滑移率 (-1.0 to +inf)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] slipAngle;         // 滑移角 (弧度，带正负号)
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] combinedSlip;      // 总滑移 (正交合成值)

    // ---- ERS/混合动力系统 ----
    public float ersCharge;           // ERS电量百分比 0.0-1.0（无ERS系统=-1.0）
    public int ersDeployMode;         // ERS部署档位索引（-1=不支持）
    [MarshalAs(UnmanagedType.U1)]
    public bool isErsActive;          // ERS是否正在工作
    public int ersRecoveryLevel;      // ERS回收级别（-1=不支持, 0-100百分比）

    // ---- 发动机状态系统 ----
    [MarshalAs(UnmanagedType.U1)]
    public bool isEngineRunning;      // 发动机是否正在运行
    [MarshalAs(UnmanagedType.U1)]
    public bool isIgnitionOn;         // 点火开关是否开启
    public int enginePowerMode;       // 发动机动力档位（-1=不支持）

    // ---- TC/ABS档位系统 ----
    public int tcLevel;               // TC牵引力控制档位（-1=不支持, 0=关闭）
    public int absLevel;              // ABS防抱死制动档位（-1=不支持, 0=关闭）
    public int tcCutLevel;            // TC削减档位（-1=不支持, 0=关闭）

    // ---- 燃油系统 ----
    public float fuelRemaining;       // 剩余燃油量（升），-1.0=不支持
    public float fuelRemainingPct;    // 剩余燃油百分比 0.0-1.0，-1.0=不支持

    // ---- 赛事旗语系统 ----
    public int raceFlag;              // 当前旗帜状态（FlagType枚举值）

    // ---- 字段有效性掩码（详见 ValidFlags 常量定义）----
    public ulong validFlags;

    // ---- 预留空间：结构体固定512字节，供后续扩展 ----
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 376)]
    public byte[] _reserved;
}
```

**字段说明**:
- `validFlags` 字段由 SDK 自动设置，调用方通过按位与（`&`）判断对应字段是否有效
- 返回 `-1.0`（float）或 `-1`（int）的字段表示该游戏不支持该数据通道
- 轮胎数据数组索引约定：`[0]=FL(前左)`, `[1]=FR(前右)`, `[2]=RL(后左)`, `[3]=RR(后右)`
- **滑移数据（slipRatio / slipAngle / combinedSlip）尚处于待定状态**：各游戏对滑移的定义、单位、精度差异较大，当前输出的滑移值仅供参考，后续版本将统一标准化

---

### FlagType 枚举

```csharp
/// <summary>统一旗帜枚举</summary>
public enum FlagType
{
    NONE       = 0,    // 无旗/绿旗（正常比赛）
    BLUE       = 1,    // 蓝旗（让车）
    YELLOW     = 2,    // 黄旗（危险/减速）
    BLACK      = 3,    // 黑旗（取消资格）
    WHITE      = 4,    // 白旗（慢车）
    CHECKERED  = 5,    // 方格旗（比赛结束）
    PENALTY    = 6,    // 处罚旗
    ORANGE     = 7,    // 橙旗（机械故障）
    RED        = 8,    // 红旗（比赛暂停）
    SC         = 9,    // 安全车
    VSC        = 10,   // 虚拟安全车
}
```

---

### ValidFlags 字段有效性掩码常量

每个常量占一个 bit 位，用于按位检查 `NormalizedData.validFlags` 中对应字段是否有效。

**C++ 宏定义**（`TelemetryAPI.h`）:
```cpp
#define VALID_SPEED             (1ULL << 0)   // speed
#define VALID_RPM               (1ULL << 1)   // rpm
#define VALID_MAX_RPM           (1ULL << 2)   // maxRpm
#define VALID_GEAR              (1ULL << 3)   // gear
#define VALID_THROTTLE          (1ULL << 4)   // throttle
#define VALID_BRAKE             (1ULL << 5)   // brake
#define VALID_STEER             (1ULL << 6)   // steer
#define VALID_PIT_LIMITER       (1ULL << 7)   // isPitLimiterActive
#define VALID_TC_ACTIVE         (1ULL << 8)   // isTcActive
#define VALID_ABS_ACTIVE        (1ULL << 9)   // isAbsActive
#define VALID_DRS_AVAILABLE     (1ULL << 10)  // isDrsAvailable
#define VALID_DRS_ACTIVE        (1ULL << 11)  // isDrsActive
#define VALID_SLIP_RATIO        (1ULL << 12)  // slipRatio[4]
#define VALID_SLIP_ANGLE        (1ULL << 13)  // slipAngle[4]
#define VALID_COMBINED_SLIP     (1ULL << 14)  // combinedSlip[4]
#define VALID_ERS_CHARGE         (1ULL << 15)  // ersCharge
#define VALID_ERS_DEPLOY        (1ULL << 16)  // ersDeployMode
#define VALID_ERS_ACTIVE         (1ULL << 17)  // isErsActive
#define VALID_ERS_RECOVERY      (1ULL << 18)  // ersRecoveryLevel
#define VALID_ENGINE_RUNNING     (1ULL << 19)  // isEngineRunning
#define VALID_IGNITION           (1ULL << 20)  // isIgnitionOn
#define VALID_ENGINE_POWER       (1ULL << 21)  // enginePowerMode
#define VALID_TC_LEVEL           (1ULL << 22)  // tcLevel
#define VALID_ABS_LEVEL          (1ULL << 23)  // absLevel
#define VALID_TC_CUT             (1ULL << 24)  // tcCutLevel
#define VALID_FUEL               (1ULL << 25)  // fuelRemaining
#define VALID_FUEL_PCT           (1ULL << 26)  // fuelRemainingPct
#define VALID_RACE_FLAG          (1ULL << 27)  // raceFlag
```

**C# 常量定义**（`TelemetryAPI.cs`）:
```csharp
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
    public const ulong RaceFlag      = 1UL << 27;  // raceFlag
}
```

**各游戏支持的字段速查**:

| 游戏 | 速度 | 转速 | 最大转速 | 档位 | 油门 | 刹车 | 转向 | 旗语 | 其他支持字段 |
|------|:----:|:----:|:-------:|:----:|:----:|:----:|:----:|:----:|------|
| AC | Y | Y | Y | Y | Y | Y | Y | Y | DRS, ERS全套, combinedSlip, fuel |
| ACC | Y | Y | Y | Y | Y | Y | Y | Y | TC/ABS档位, 滑移全套, 发动机全套, fuel |
| AC Rally | Y | Y | - | Y | Y | Y | Y | - | 滑移全套, 发动机运行/点火 |
| AC EVO | Y | Y | Y | Y | Y | Y | Y | Y | DRS, ERS全套, TC/ABS档位, 发动机运行/点火, fuel |
| F1 22/23 | Y | Y | Y | Y | Y | Y | Y | Y | DRS, TC/ABS档位, combinedSlip, ERS三件, 发动机全套, fuel |
| F1 24 | Y | Y | Y | Y | Y | Y | Y | Y | DRS, TC/ABS档位, ERS三件, 发动机全套, fuel |
| F1 25 | Y | Y | Y | Y | Y | Y | Y | Y | DRS, TC/ABS档位, 滑移全套, ERS三件, 发动机全套, fuel |
| FM/FH4/5/6 | Y | Y | Y | Y | Y | Y | Y | - | 滑移全套, fuelPct |
| RF2/LMU | Y | Y | Y | Y | Y | Y | Y | Y | TC/ABS触发, 发动机运行/点火, fuel, ERS电量+激活 |
| DiRT 4/DR2 | Y | Y | Y | Y | Y | Y | Y | - | 仅基础七项 |
| EA WRC | Y | Y | Y | Y | Y | Y | Y | - | ABS触发 |
| iRacing | Y | Y | Y | Y | Y | Y | Y | Y | TC/ABS触发+档位, slipRatio, 发动机运行/点火, fuel, ERS电量+激活 |
| R3E | Y | Y | Y | Y | Y | Y | Y | Y | DRS, TC/ABS触发+档位, 发动机全套, fuel |
| AMS2 | Y | Y | Y | Y | Y | Y | Y | Y | DRS, TC/ABS触发+档位, 发动机运行/点火, fuel, ERS三件 |
| PCARS2 | Y | Y | Y | Y | Y | Y | Y | Y | ABS触发, 发动机运行/点火, fuel, ERS电量+激活 |
| PCARS3 | Y | Y | Y | Y | Y | Y | Y | - | ABS触发, 发动机运行/点火 |
| RBR | Y | Y | - | Y | Y | Y | Y | - | 发动机运行/点火 |
| ETS2/ATS | Y | Y | Y | Y | Y | Y | Y | - | fuel, 发动机运行/点火 |
| LFS/BeamNG | Y | Y | - | Y | Y | Y | - | - | fuelPct, 发动机运行/点火 |
| WRC 8/9/10 | Y | Y | Y | Y | - | - | - | - | 仅速度+转速+档位 |
| WRC Gen | Y | Y | Y | Y | Y | Y | - | - | combinedSlip, 发动机运行/点火 |

> 完整掩码见 `include/Core/GameSupportTable.h` 源码

---

### GameId 枚举

```csharp
/// <summary>支持的游戏ID枚举（Steam游戏使用Steam App ID，非Steam游戏使用自定义ID）</summary>
public enum GameId : int
{
    Unknown = 0,

    // Assetto Corsa 系列
    AssettoCorsa = 244210,        // Assetto Corsa
    ACC = 805550,                // Assetto Corsa Competizione
    ACRally = 3917090,           // Assetto Corsa Rally（与ACC兼容）
    AC_Evo = 3058630,            // Assetto Corsa EVO

    // F1 系列
    F1_2022 = 1692250,            // F1 22
    F1_2023 = 2108330,            // F1 23
    F1_2024 = 2488620,            // F1 24
    F1_2025 = 3059520,            // F1 25

    // Forza 系列
    ForzaMotorsport = 2440510,    // Forza Motorsport 2023
    ForzaHorizon4 = 1293830,     // Forza Horizon 4
    ForzaHorizon5 = 1551360,     // Forza Horizon 5
    ForzaHorizon6 = 2483190,     // Forza Horizon 6

    // DiRT 系列
    DiRT_4 = 421020,             // DiRT 4
    DiRT_Rally_2 = 690790,        // DiRT Rally 2.0

    // rFactor / LMU 系列
    rFactor2 = 365960,            // rFactor 2
    LMU = 2399420,                // Le Mans Ultimate

    // Project CARS / AMS2 系列
    PCARS2 = 378860,             // Project CARS 2
    PCARS3 = 958400,             // Project CARS 3
    AMS2 = 1066890,              // Automobilista 2

    // 拉力赛 (WRC 系列)
    WRC_8 = 1004750,              // WRC 8
    WRC_9 = 1267540,             // WRC 9
    WRC_10 = 1462810,            // WRC 10
    WRC_Generations = 1953520,   // WRC Generations
    EA_WRC = 1849250,             // EA Sports WRC

    // 其他竞速
    iRacing = 266410,             // iRacing
    R3E = 211500,                // RaceRoom Racing Experience
    BeamNG = 284160,             // BeamNG.drive

    // 模拟驾驶
    SCS_ETS2 = 227300,            // Euro Truck Simulator 2
    SCS_ATS = 270880,             // American Truck Simulator

    // 非 Steam 游戏（自定义ID）
    RBR = 22,                     // Richard Burns Rally
    LFS = 25,                     // Live for Speed
}
```

---

### 各游戏适配器配置参考

部分适配器支持通过配置结构体自定义行为。以下为高级用法，通过C++直接使用适配器类时可配置（C# DLL接口不直接暴露这些配置，SDK内部已为每个游戏设置好默认值）。

#### 通信方式分类

| 通信方式 | 游戏 | 默认端口/内存名 |
|----------|------|----------------|
| 共享内存 | AC, ACC, AC Rally, AC EVO, iRacing, rF2, LMU, R3E, AMS2, PC2, PC3, ETS2, ATS | 见各适配器说明 |
| UDP | F1系列(2022-2025), Forza系列, DiRT系列, EA WRC, RBR, LFS, BeamNG | 见各适配器说明 |
| 混合(共享内存+UDP) | Assetto Corsa (经典版) | SM: `Local\acpmf_*`, UDP: 9996端口 |

#### 各适配器详细信息

| 适配器 | 命名空间 | 可配置项 |
|--------|---------|---------|
| ACAdapter | `TelemetryAdapters::AC` | 共享内存名称、启用物理/图形/静态/UDP数据 |
| ACCAdapter | `TelemetryAdapters::ACC` | 共享内存名称、启用物理/图形/静态数据 |
| ACEvoAdapter | `TelemetryAdapters::ACEvo` | 共享内存名称、启用物理/图形/静态数据、损伤归一化、悬挂绝对值、离地高度检测 |
| F122Adapter | `TelemetryAdapters::F122` | UDP端口(20777)、超时时间、数据验证、处理所有包类型 |
| F123Adapter | `TelemetryAdapters::F123` | 同F122 |
| F124Adapter | `TelemetryAdapters::F124` | 同F122 |
| F125Adapter | `TelemetryAdapters::F125` | 同F122 |
| FH45Adapter | `TelemetryAdapters::FH45` | UDP端口(1024/20440)、超时时间、单位转换、数据验证、控制输入归一化 |
| FM2023Adapter | `TelemetryAdapters::FM2023` | UDP端口(1024)、超时时间、单位转换、数据验证、控制输入归一化 |
| DiRTAdapter | `TelemetryAdapters::DiRT` | UDP端口(20777)、超时时间、单位转换、数据验证、调试输出 |
| EAWRCAdapter | `TelemetryAdapters::EAWRC` | UDP端口(26666)、超时时间、单位转换、数据验证、处理所有包类型 |
| RF2LMUAdapter | `TelemetryAdapters::RF2LMU` | 共享内存名称、LMU模式、单位转换、数据验证、平滑输入、内存读取TC/ABS |
| AMS2PC23Adapter | `TelemetryAdapters::AMS2PC23` | 共享内存名称(`$pcars2$`) |
| IRacingAdapter | `TelemetryAdapters::iRacing` | 共享内存名称、启用物理/会话数据、自动重连、超时(16ms/60Hz)、速度单位、转向归一化 |
| R3EAdapter | `TelemetryAdapters::R3E` | 共享内存名称(`$R3E`) |
| RBRAdapter | `TelemetryAdapters::RBR` | UDP端口(30000)、超时时间 |
| LFSBeamNGAdapter | `TelemetryAdapters::LFSBeamNG` | UDP端口(30000)、超时时间、调试输出 |
| SCSAdapter | `TelemetryAdapters::SCS` | 共享内存名称(`Local\cwyxSCSTelemetry`)、内存大小(32KB) |

---

## 🚀 完整C#调用示例

### 基础调用示例
```csharp
using System;
using System.Runtime.InteropServices;

public static class TelemetryAPI
{
    #region DLL Imports

    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool StartTelemetry(int gameId);

    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool GetTelemetryData(ref NormalizedData outData);

    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void StopTelemetry();

    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetSDKVersion();

    [DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong GetSupportedFlags();

    #endregion

    #region Data Structures

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NormalizedData
    {
        // 基础参数
        public float speed;
        public float rpm;
        public float maxRpm;
        public int gear;
        public float throttle;
        public float brake;
        public float steer;

        // 状态标志
        [MarshalAs(UnmanagedType.U1)] public bool isPitLimiterActive;
        [MarshalAs(UnmanagedType.U1)] public bool isTcActive;
        [MarshalAs(UnmanagedType.U1)] public bool isAbsActive;
        [MarshalAs(UnmanagedType.U1)] public bool isDrsAvailable;
        [MarshalAs(UnmanagedType.U1)] public bool isDrsActive;

        // 轮胎滑移数据 (0=FL, 1=FR, 2=RL, 3=RR)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] slipRatio;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] slipAngle;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] combinedSlip;

        // ERS/混合动力系统
        public float ersCharge;
        public int ersDeployMode;
        [MarshalAs(UnmanagedType.U1)] public bool isErsActive;
        public int ersRecoveryLevel;

        // 发动机状态
        [MarshalAs(UnmanagedType.U1)] public bool isEngineRunning;
        [MarshalAs(UnmanagedType.U1)] public bool isIgnitionOn;
        public int enginePowerMode;

        // TC/ABS档位
        public int tcLevel;
        public int absLevel;
        public int tcCutLevel;

        // 燃油系统
        public float fuelRemaining;
        public float fuelRemainingPct;

        // 旗语系统
        public int raceFlag;

        // 字段有效性掩码
        public ulong validFlags;

        // 预留空间（结构体固定512字节）
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 376)]
        public byte[] _reserved;
    }

    public enum GameId : int
    {
        Unknown = 0,
        AssettoCorsa = 244210, ACC = 805550, ACRally = 3917090, AC_Evo = 3058630,
        F1_2022 = 1692250, F1_2023 = 2108330, F1_2024 = 2488620, F1_2025 = 3059520,
        ForzaMotorsport = 2440510, ForzaHorizon4 = 1293830, ForzaHorizon5 = 1551360, ForzaHorizon6 = 2483190,
        DiRT_4 = 421020, DiRT_Rally_2 = 690790,
        rFactor2 = 365960, LMU = 2399420,
        PCARS2 = 378860, PCARS3 = 958400, AMS2 = 1066890,
        WRC_8 = 1004750, WRC_9 = 1267540, WRC_10 = 1462810, WRC_Generations = 1953520,
        EA_WRC = 1849250, iRacing = 266410, R3E = 211500, BeamNG = 284160,
        SCS_ETS2 = 227300, SCS_ATS = 270880,
        RBR = 22, LFS = 25
    }

    #endregion

    #region 辅助方法

    public static string GetGearName(int gear) =>
        gear switch
        {
            -1 => "R", 0 => "N", 1 => "1", 2 => "2", 3 => "3",
            4 => "4", 5 => "5", 6 => "6", 7 => "7", 8 => "8", _ => "?"
        };

    public static bool IsDataValid(NormalizedData data) =>
        data.rpm > 0.0f || data.speed > 0.0f;

    public static string FormatData(NormalizedData data) =>
        $"速度: {data.speed:F1} km/h | " +
        $"转速: {data.rpm:F0} RPM ({data.maxRpm:F0}) | " +
        $"档位: {GetGearName(data.gear)} | " +
        $"油门: {data.throttle * 100:F0}% | " +
        $"刹车: {data.brake * 100:F0}% | " +
        $"转向: {data.steer * 100:F0}%";

    #endregion
}
```

### 实时数据循环示例
```csharp
public class Program
{
    public static void Main()
    {
        // 启动F1 2025遥测
        if (!TelemetryAPI.StartTelemetry((int)TelemetryAPI.GameId.F1_2025))
        {
            Console.WriteLine("启动失败，请确保F1 2025正在运行且在赛道上");
            Console.ReadKey();
            return;
        }

        // 查询支持的字段掩码
        ulong flags = TelemetryAPI.GetSupportedFlags();
        bool hasSlip = (flags & TelemetryAPI.ValidFlags.CombinedSlip) != 0;
        Console.WriteLine($"开始采集数据（滑移数据: {(hasSlip ? "支持" : "不支持")}），按ESC键停止...");
        Console.WriteLine("================================");

        try
        {
            bool running = true;
            while (running)
            {
                var data = new TelemetryAPI.NormalizedData();
                TelemetryAPI.GetTelemetryData(ref data);

                if (TelemetryAPI.IsDataValid(data))
                {
                    // 根据字段有效性掩码决定显示内容
                    string slipInfo = "";
                    if (hasSlip)
                        slipInfo = $" | 滑移FL: {data.combinedSlip[0]:F2}";
                    Console.WriteLine(TelemetryAPI.FormatData(data) + slipInfo);
                }

                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    running = false;

                Thread.Sleep(16); // ~60Hz
            }
        }
        finally
        {
            TelemetryAPI.StopTelemetry();
        }
    }
}
```

### 高级用法：使用 TelemetryManager（事件驱动）
```csharp
using var manager = new TelemetryManager();
manager.OnStarted += (game) => Console.WriteLine($"已连接: {game}");
manager.OnInvalidData += (_) => Console.WriteLine("等待游戏数据...");

manager.Start(TelemetryAPI.GameId.iRacing);

// 在游戏循环中
var data = manager.GetLatestData();
if (TelemetryAPI.IsDataValid(data))
{
    Console.WriteLine(TelemetryAPI.FormatData(data));
}

// manager 会在 Dispose 时自动停止
```

`TelemetryManager` 类位于 `SDKTester/TelemetryAPI.cs`，提供事件驱动的生命周期管理：

| 事件 | 触发时机 |
|------|---------|
| `OnStarted` | 适配器启动成功 |
| `OnStartFailed` | 适配器启动失败 |
| `OnStopped` | 适配器已停止 |
| `OnInvalidData` | 获取到的数据无效（游戏未在赛道上） |
| `OnNotRunning` | 未启动适配器时尝试获取数据 |

---

## ⚠️ 重要注意事项

### 1. 线程安全
- DLL内部使用了全局状态管理
- **不要从多个线程同时调用API**
- 建议在单个线程中进行所有API调用
- UDP类适配器内部使用后台接收线程+互斥锁，数据读取是线程安全的

### 2. 资源管理
- 必须在退出应用前调用 `StopTelemetry()`
- 使用 `try...finally` 或 `using`（TelemetryManager）确保资源释放
- 重复调用 `StopTelemetry()` 是安全的（幂等操作）

### 3. 数据有效性
- 游戏未运行时，`GetTelemetryData` 返回 false
- 某些游戏在菜单界面时数据可能无效
- 建议添加数据合理性检查（如 `rpm > 0` 或 `speed > 0` 表示在赛道上）
- 不支持的数据通道返回 `-1.0` 或 `-1`
- 通过 `validFlags` 掩码按位检查各字段是否有效（详见 ValidFlags 常量）
- `validFlags` 可通过 `GetTelemetryData` 每帧获取，也可通过 `GetSupportedFlags()` 一次性查询

### 4. 性能考虑
- 建议调用频率：60Hz (每16ms)
- 避免过高频率导致CPU占用过大
- 可以根据游戏支持的刷新率动态调整

### 5. 内存对齐
- 所有跨语言数据结构使用 `#pragma pack(push, 1)` 强制1字节对齐
- C#端使用 `[StructLayout(LayoutKind.Sequential, Pack = 1)]` 匹配
- `bool` 字段在C#中使用 `[MarshalAs(UnmanagedType.U1)]` 确保为1字节
- `NormalizedData` 结构体固定 512 字节，末尾 `_reserved[384]` 为预留扩展空间
- 后续新增字段时，在 `validFlags` 之后、`_reserved` 之前插入，同时减小 `_reserved` 大小

### 6. 游戏特定注意事项
| 游戏 | 注意事项 |
|------|---------|
| Assetto Corsa | 需要游戏内启用UDP遥测发送功能 |
| ACC | 需要在比赛/自由练习中，菜单界面无数据 |
| iRacing | 需要在赛道上，车库中也有部分数据；支持自动重连 |
| F1系列 | 需要在游戏设置中开启UDP遥测发送（Telemetry设置） |
| Forza系列 | 需要在Forza设置中启用DATA OUT功能 |
| DiRT系列 | 需要在游戏设置中启用UDP遥测 |
| rF2/LMU | 需要游戏正在运行且已加入服务器 |

---

## 🔍 调试与故障排除

### 常见问题

**Q: StartTelemetry 返回 false**
```
解决方案：
1. 确认游戏正在运行
2. 确认游戏不在主菜单界面（需要在赛道上）
3. 检查游戏内遥测设置是否启用
4. 尝试以管理员权限运行C#程序
5. 确认传入的GameId值正确（参考游戏列表中的GameId值）
```

**Q: GetTelemetryData 返回的值都是0**
```
解决方案：
1. 确认游戏已进入比赛模式
2. 某些游戏需要车辆启动后才有数据
3. 检查是否成功调用了StartTelemetry
4. 部分共享内存游戏需要游戏完全加载赛道后才有数据
```

**Q: 找不到TelemetrySDK.dll**
```
解决方案：
1. 确认DLL文件与C#可执行文件在同一目录
2. 或者将DLL放入系统PATH环境变量目录
3. 检查DLL是否为x64架构（与C#程序匹配）
```

**Q: 数据值异常或显示乱码**
```
解决方案：
1. 确认C#中的NormalizedData结构体使用了 Pack=1 对齐
2. 确认bool字段使用了 [MarshalAs(UnmanagedType.U1)]
3. 确认数组字段使用了 [MarshalAs(UnmanagedType.ByValArray, SizeConst=N)]
4. 确认C#项目目标平台为 x64（Any CPU可能会导致问题）
```

---

## 📦 DLL文件信息

**编译输出文件**:
```
build/Release/
├── TelemetrySDK.dll      # 主DLL文件（分发此文件）
├── TelemetrySDK.lib      # 导入库（C#不需要）
└── TelemetrySDK.exp      # 导出符号文件（调试用）
```

**依赖项**:
- Windows系统库（自动包含）
- `ws2_32.lib` (Windows Socket库，用于UDP通信)
- 无需额外的Visual C++ Redistributable（已静态链接 /MT）

**分发清单**:
```
分发给你的C#项目：
✅ TelemetrySDK.dll
❌ TelemetrySDK.lib (C#不需要)
❌ 测试程序 (C#不需要)
❌ .h 头文件 (C#不需要，本文档已包含所有必要信息)
```

---

## 🏗️ 项目架构

```
TelemetrySDK/
├── include/
│   ├── TelemetryAPI.h              ← DLL导出接口 (5个函数 + GameId枚举 + VALID_*掩码)
│   ├── ITelemetryAdapter.h          ← 适配器抽象接口 (Initialize/Update/Shutdown)
│   ├── NormalizedData.h             ← 统一输出数据结构 + FlagType枚举
│   ├── Core/
│   │   ├── GameSupportTable.h        ← 游戏→validFlags 静态查表
│   │   ├── SharedMemory.h            ← Windows共享内存封装
│   │   └── UDPConnection.h           ← UDP Socket封装
│   └── Adapters/                    ← 20个游戏适配器 (每个包含适配器类+数据结构)
├── src/
│   ├── TelemetrySDK.cpp             ← DLL入口 + 适配器工厂 (GameId→Adapter映射) + flags集成
│   ├── Core/                        ← SharedMemory.cpp, UDPConnection.cpp
│   └── Adapters/                    ← 20个适配器的实现
└── SDKTester/                       ← C# WPF测试应用
    └── TelemetryAPI.cs              ← C# P/Invoke封装 + TelemetryManager
```

**适配器接口 (ITelemetryAdapter)**:
```cpp
class ITelemetryAdapter {
public:
    virtual ~ITelemetryAdapter() = default;
    virtual bool Initialize() = 0;           // 连接数据源
    virtual void Update(NormalizedData& outData) = 0;  // 读取+转换数据
    virtual void Shutdown() = 0;            // 释放资源
};
```

---

## 🖥️ DLL 交付与环境要求

### 运行环境

| 项目 | 说明 |
|------|------|
| **DLL 架构** | x64（调用方进程必须为 x64，否则无法加载） |
| **运行时依赖** | 无，CRT 已静态链接（`/MT`），无需附带 MSVC 运行时 DLL |
| **操作系统** | 仅 Windows（内部使用 Windows 共享内存 API 和 Winsock） |
| **字节序** | 小端序（x86/x64 默认） |

### 调用约定与内存对齐

| 项目 | 说明 |
|------|------|
| **调用约定** | `__cdecl`，C# 端须声明 `CallingConvention.Cdecl` |
| **结构体对齐** | `Pack = 1`（C++ 端 `#pragma pack(push, 1)`，C# 端 `[StructLayout(LayoutKind.Sequential, Pack = 1)]`） |
| **NormalizedData 大小** | 固定 512 字节，C# 端结构体须保持一致 |

### 通信说明

- 部分适配器通过 UDP 接收游戏数据（如 F1 系列、iRacing 等），需确保防火墙放行对应端口
- 共享内存类适配器无需额外网络配置
- DLL 不主动监听端口，仅连接游戏已开放的通信通道

### 部署清单

交付时仅需提供以下文件：

```
TelemetrySDK.dll          // 核心动态链接库
TelemetrySDK_API_Documentation.md  // 本文档
```

无需安装任何额外运行时、驱动或依赖库，将 DLL 放置到调用方进程的工作目录或系统 PATH 中即可加载。

---

## 📝 版本信息

- **当前版本**: 0.1.0
- **最后更新**: 2026-06-12
- **C++标准**: C++17
- **编译器**: MSVC 2022
- **目标平台**: Windows x64
- **支持游戏数**: 31 款
- **导出API数**: 5 个（StartTelemetry / GetTelemetryData / StopTelemetry / GetSDKVersion / GetSupportedFlags）

---

## 📞 技术支持

如有问题或建议，请参考：
- 项目代码：`D:\project\chengyou\TelemetryData\telemetry\TelemetrySDK`
- C++源码：`src/TelemetrySDK.cpp`
- 接口定义：`include/TelemetryAPI.h`
- C#封装：`SDKTester/TelemetryAPI.cs`
- 数据结构：`include/NormalizedData.h`
- 支持表：`include/Core/GameSupportTable.h`
