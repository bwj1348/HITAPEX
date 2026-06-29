# TelemetrySDK DLL 接口文档

> **当前版本**: `v2.0.0`  |  **最后更新**: 2026-06-29  |  **兼容性**: ⚠️ **破坏性变更**（详见下方修订历史）
> 客户端从 v1.x 升级到 v2.0 **必须迁移代码**，迁移指南见同目录 `Update_Notes_v2.0.md`

## 📝 修订历史

| 版本 | 日期 | 类型 | 主要内容 |
|------|------|------|---------|
| **v2.0.0** | 2026-06-28 | ⚠️ **破坏性变更** | ① **结构体重排**：`NormalizedData` 在 `raceFlag` 之后新增第三批 17 个字段（clutch / 圈速×5 / 胎温内中外核 ×4 / 胎压 / 胎磨 / 刹车温度 / 排名 / 水温 / 油温 / 涡轮），`validFlags` 偏移由 136B 移至 288B，`_reserved` 由 376B 缩至 224B；② **ValidFlags 扩展**：新增 bit 28-44（共 17 位）；③ **`tyreWear` 值域变更**：从 `0-1` 浮点改为 `0-100` 递增百分比；④ **新增数据健康层**：`SanitizeNormalizedData` 在 SDK 内部对超界 / NaN / Inf 值自动置 0（不再返回 `-1` 哨兵）；⑤ **滑移数据**：从"待定"转为正式版。**C# 端必须迁移**，详见 `Update_Notes_v2.0.md` |
| v1.0.0 | 2026-06-12 | 初始版本 | 首次发布：5 个 C 接口、31 款游戏支持、第一二批参数（bit 0-27） |

---

## 📋 文档概述

本文档为 `TelemetrySDK.dll` 的 C# 调用接口说明。

- **文件名**: `TelemetrySDK.dll`（C++17 / MSVC 2022 / Windows x64 / `extern "C"` 导出）
- **定位**: 把 20+ 款赛车游戏的异构遥测协议统一成 `NormalizedData`，客户端只需对接一套数据结构
- **核心机制**: `validFlags` 位掩码标记每个字段是否被当前游戏支持/投递，客户端按位判断有效性

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
| 2483190 | `GAME_FORZA_HORIZON_6` | Forza Horizon 6 | UDP (1024) | FH45Adapter | 端口与FH4/5不同 |
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

### NormalizedData 结构体（C++ 定义 · 权威契约）

`NormalizedData` 是所有游戏统一的输出数据格式，也是 DLL 实际导出的**二进制契约**——字段顺序、内存偏移、类型、512 字节固定大小全部由此定义。C#/Python/Rust 等任何客户端语言的结构体都只是对这个契约的一种**绑定**。

- **对齐**：`#pragma pack(push, 1)` 强制 1 字节对齐（C# 端对应 `Pack = 1`）
- **大小**：固定 512 字节，当前已用 288 字节，末尾 `_reserved[224]` 预留扩展
- **数组顺序**：4 元素轮胎/刹车数组统一 `[0]=FL, [1]=FR, [2]=RL, [3]=RR`
- 每字段注释格式：**含义 · 单位 · 合法范围 · 对应的 `VALID_*` 宏**（C# 端为同名 `ValidFlags.Xxx` 常量）

> **⚠️ v2.0 破坏性变更**：字段顺序相比 v1.x 调整——`raceFlag` 之后新增第三批 17 字段，`validFlags` 字段位置由 v1.x 的 136B 偏移处**移至 288B 偏移处**。客户端必须同步更新结构体定义，否则内存错位、读到垃圾数据。

```cpp
#pragma pack(push, 1)
struct NormalizedData {
    // ---- 基础参数 ----
    float speed;            // 速度 · km/h · [0, 500] · VALID_SPEED
    float rpm;              // 引擎转速 · RPM · [0, 25000] · VALID_RPM
    float maxRpm;           // 引擎最大转速 · RPM · [0, 25000] · VALID_MAX_RPM
    int   gear;             // 档位 · -1=R倒挡, 0=N空挡, 1-10=前进 · [-1, 10] · VALID_GEAR
    float throttle;         // 油门踏板 · 0-1 归一化 · [0, 1] · VALID_THROTTLE
    float brake;            // 刹车踏板 · 0-1 归一化 · [0, 1] · VALID_BRAKE
    float steer;            // 转向 · -1=左满舵, +1=右满舵 · [-1, 1] · VALID_STEER

    // ---- 状态标志（bool）----
    bool isPitLimiterActive;   // 维修区限速器 · VALID_PIT_LIMITER
    bool isTcActive;           // TC 牵引力控制激活 · VALID_TC_ACTIVE
    bool isAbsActive;          // ABS 防抱死激活 · VALID_ABS_ACTIVE
    bool isDrsAvailable;       // DRS 可用 · VALID_DRS_AVAILABLE
    bool isDrsActive;          // DRS 激活 · VALID_DRS_ACTIVE

    // ---- 轮胎滑移（[0]=FL, [1]=FR, [2]=RL, [3]=RR）----
    float slipRatio[4];        // 纵向滑移率 · 无量纲 · [-2, 5] · VALID_SLIP_RATIO
    float slipAngle[4];        // 滑移角 · 弧度 · [-π, π] · VALID_SLIP_ANGLE
    float combinedSlip[4];     // 总滑移正交合成 · 无量纲 · [-2, 5] · VALID_COMBINED_SLIP

    // ---- ERS / 混合动力 ----
    float ersCharge;           // ERS 电量 · 0-1 · [0, 1] · VALID_ERS_CHARGE
    int   ersDeployMode;       // ERS 部署档位索引 · [-1, 20] · VALID_ERS_DEPLOY
    bool  isErsActive;         // ERS 工作中 · VALID_ERS_ACTIVE
    int   ersRecoveryLevel;    // ERS 回收级别 · 百分比 · [-1, 100] · VALID_ERS_RECOVERY

    // ---- 发动机状态 ----
    bool isEngineRunning;      // 发动机运行 · VALID_ENGINE_RUNNING
    bool isIgnitionOn;         // 点火开启 · VALID_IGNITION
    int  enginePowerMode;      // 发动机动力档位索引 · [-1, 20] · VALID_ENGINE_POWER

    // ---- TC / ABS 档位 ----
    int tcLevel;               // TC 档位 · 0=关, 1+=级别 · [-1, 20] · VALID_TC_LEVEL
    int absLevel;              // ABS 档位 · 0=关, 1+=级别 · [-1, 20] · VALID_ABS_LEVEL
    int tcCutLevel;            // TC 削减档位 · 0=关, 1+=级别 · [-1, 20] · VALID_TC_CUT

    // ---- 燃油 ----
    float fuelRemaining;       // 剩余燃油 · 升 · [0, 5000] · VALID_FUEL
    float fuelRemainingPct;    // 剩余燃油百分比 · 0-1 · [0, 1] · VALID_FUEL_PCT

    // ---- 赛事旗语 ----
    int raceFlag;              // 当前旗帜 · FlagType 枚举 · [0, 10] · VALID_RACE_FLAG

    // ============ v2.0 新增：第三批参数（bit 28-44）============

    // ---- 离合 ----
    float clutch;              // 离合踏板 · 0-1 归一化 · [0, 1] · VALID_CLUTCH

    // ---- 圈速计时 ----
    int   currentLap;          // 当前圈数 · 1 起计 · [0, 999] · VALID_CURRENT_LAP_NUM
    int   totalLaps;           // 赛事总圈数 · [0, 999] · VALID_TOTAL_LAPS
    float currentLapTime;      // 当前圈已用时间 · 秒 · [0, 100000] · VALID_CURRENT_LAP
    float lastLapTime;         // 上一圈用时 · 秒 · [0, 100000] · VALID_LAST_LAP
    float bestLapTime;         // 个人最佳圈时 · 秒 · [0, 100000] · VALID_BEST_LAP

    // ---- 胎面温度（[0]=FL, [1]=FR, [2]=RL, [3]=RR，°C）----
    float tyreTempInner[4];    // 胎面内侧温度 · °C · [-50, 200] · VALID_TYRE_TEMP_INNER
    float tyreTempMiddle[4];   // 胎面中间温度 · °C · [-50, 200] · VALID_TYRE_TEMP_MIDDLE
    float tyreTempOuter[4];    // 胎面外侧温度 · °C · [-50, 200] · VALID_TYRE_TEMP_OUTER
    float tyreCoreTemp[4];     // 轮胎核心温度 · °C · [-50, 200] · VALID_TYRE_CORE_TEMP

    // ---- 胎压 / 胎磨 / 刹车温度（[0]=FL, [1]=FR, [2]=RL, [3]=RR）----
    float tyrePressure[4];     // 胎压 · kPa · [0, 500] · VALID_TYRE_PRESSURE
    float tyreWear[4];         // 胎磨损百分比 · 0=全新, 100=全磨 · [0, 100] · VALID_TYRE_WEAR
    float brakeTemp[4];        // 刹车温度 · °C · [-50, 2000] · VALID_BRAKE_TEMP

    // ---- 排名 / 发动机温度 / 涡轮 ----
    int   position;            // 车手排名 · 1 起计 · [0, 999] · VALID_POSITION
    float waterTemp;           // 冷却水温 · °C · [-40, 200] · VALID_WATER_TEMP
    float oilTemp;             // 机油温度 · °C · [-40, 200] · VALID_OIL_TEMP
    float turboPressure;       // 涡轮增压压力 · bar · [-5, 5] · VALID_TURBO_PRESSURE

    // ============ 字段有效性掩码（v2.0 移至 288B 偏移处）============
    uint64_t validFlags;       // 每位对应一个字段，置 1 = 该字段有有效数据（见 VALID_* 宏）

    // ---- 预留空间：结构体固定 512 字节，当前已用 288 字节，剩余 224 字节 ----
    unsigned char _reserved[224];
};
#pragma pack(pop)
```

### C# 绑定要点

将上方 C++ 契约翻译到 C# 时，遵循以下规则即可保证 P/Invoke 内存对齐一致（其他语言照此类推）：

| C++ (契约) | C# 绑定 | 说明 |
|-----------|---------|------|
| `float` / `int` | `float` / `int` | 同名同尺寸 |
| `bool` | `bool` + `[MarshalAs(UnmanagedType.U1)]` | 强制 1 字节 |
| `float[N]` | `float[]` + `[MarshalAs(UnmanagedType.ByValArray, SizeConst = N)]` | 内联数组 |
| `uint64_t` | `ulong` | 8 字节 |
| `unsigned char[224]` | `byte[]` + `[MarshalAs(UnmanagedType.ByValArray, SizeConst = 224)]` | 预留字节 |
| 结构体整体 | `[StructLayout(LayoutKind.Sequential, Pack = 1)]` | 1 字节对齐 |
| `GetTelemetryData(NormalizedData*)` | `GetTelemetryData(ref NormalizedData outData)` | 用 `ref` 传引用 |

> 现成的 C# 绑定定义见仓库 `SDKTester/TelemetryAPI.cs`，可直接复用。

> ⚠️ **v2.0 行为变化**：`tyreWear[4]` 值域由 `0-1` 浮点改为 `0-100` 递增百分比（0=全新，100=完全磨损）。原客户端代码若曾做 `*100` 显示，**必须去掉 `*100`**。

---

### 客户端使用要点

1. **有效性唯一看 `validFlags`**：用 `(data.validFlags & ValidFlags.Xxx) != 0` 判断字段是否被当前游戏支持/投递。
2. **不要假设无效字段的数值**：未支持的字段是 SDK 内部填充值，**不承诺是 0、-1 或任何特定值**。客户端 UI 自己定显示策略（常见做法：显示 "—" / "N/A" / 隐藏控件）。
3. **`== 0` 不是无效判据**：空挡 `gear=0`、未踩踏板 `throttle=0`、无旗 `raceFlag=0`、起始圈 `currentLap=0` 都是合法业务值。
4. **`== -1` 不是无效判据**：倒挡 `gear=-1`、左满舵 `steer=-1.0` 都是合法业务值。
5. **数值范围有保证**：SDK 内部 `SanitizeNormalizedData` 已过滤 NaN/Inf/超界值（详见下方「字段数值范围与 SanitizeNormalizedData」）。
6. **数组顺序统一**：4 元素轮胎/刹车数组都是 `[0]=FL, [1]=FR, [2]=RL, [3]=RR`。

---

### 字段数值范围与 SanitizeNormalizedData

SDK 内部 `SanitizeNormalizedData` 在每次 `GetTelemetryData()` 调用中对所有数值字段做边界校验——**NaN / Inf / 超出物理极限的值一律置 0**。客户端可依赖此约定：只要字段在结构体内联注释的范围内（且 `validFlags` 标记有效），数值就是物理可信的。

**3 条关键约定**：

1. **范围用物理极限**，不是工作范围（例：水温工作 90-115°C，但范围设 [-40, 200] 留冗余不误杀合法极值；F1 碳盘刹车工作 700-1000°C，范围设 [-50, 2000]）。
2. **0 是合法值**：`gear=0` 空挡 / `throttle=0` 未踩 / `raceFlag=0` 无旗 / `currentLap=0` 起始——都不能用 `== 0` 判无效。
3. **下限含 -1 的字段**（`gear` / `steer` / `slipRatio` / `slipAngle` / `combinedSlip` / `turboPressure` / 各档位索引类）—— -1 既可能是合法值（倒挡、左满舵），也可能是适配器内部哨兵，Sanitize 无法区分，**这些字段尤其要靠 `validFlags` 判断有效性**。

**不校验的字段**：所有 `bool`（天然 0/1）、`validFlags`（位掩码）、`_reserved[224]`（预留字节）。

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
    public const ulong RaceFlag       = 1UL << 27;  // raceFlag

    // 第三批参数（bit 28-44，v2.0 新增）
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
```

---

### 各游戏支持的字段

不同游戏支持的 `VALID_*` 位差异很大（例如 ACC 24 位、ACRally 5/17 第三批、WRC 8/9/10 仅基础速度+转速+档位）。

**完整支持矩阵不在本文档**，请参考外部维护的「TelemetrySDK 遥测支持表」。

运行时验证方式：
- 启动后一次性查询：`ulong flags = GetSupportedFlags();`
- 每帧动态查询：`data.validFlags`

源码层声明：`include/Core/GameSupportTable.h`

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

## ⚠️ 重要注意事项

### 1. 线程安全
- DLL内部使用了全局状态管理
- **不要从多个线程同时调用API**
- 建议在单个线程中进行所有API调用
- UDP类适配器内部使用后台接收线程+互斥锁，数据读取是线程安全的

### 2. 资源管理
- 必须在退出应用前调用 `StopTelemetry()`
- 使用 `try...finally` 确保资源释放
- 重复调用 `StopTelemetry()` 是安全的（幂等操作）

### 3. 数据有效性
- 游戏未运行时，`GetTelemetryData` 返回 false
- 某些游戏在菜单界面时数据可能无效
- 推荐合理性检查：`rpm > 0 || speed > 0` 表示在赛道上
- `validFlags` 可通过 `GetTelemetryData` 每帧获取，也可通过 `GetSupportedFlags()` 一次性查询

### 4. 性能考虑
- 建议调用频率：60Hz (每16ms)
- 避免过高频率导致CPU占用过大
- 可以根据游戏支持的刷新率动态调整

### 5. 内存对齐
- 所有跨语言数据结构使用 `#pragma pack(push, 1)` 强制1字节对齐
- C#端使用 `[StructLayout(LayoutKind.Sequential, Pack = 1)]` 匹配
- `bool` 字段在C#中使用 `[MarshalAs(UnmanagedType.U1)]` 确保为1字节
- `NormalizedData` 结构体固定 512 字节，当前已用 288 字节，末尾 `_reserved[224]` 为预留扩展空间
- 后续新增数据字段时减小 `_reserved` 大小，具体扩展策略与字段物理顺序约定见 `NormalizedData.h` 源码与 `TelemetryAPI.h` 的 `VALID_*` 位定义

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

| 现象 | 解决方案 |
|------|---------|
| **StartTelemetry 返回 false** | ① 确认游戏正在运行；② 确认不在主菜单（需在赛道上）；③ 检查游戏内遥测设置已启用；④ 尝试管理员权限运行；⑤ 确认 GameId 值正确 |
| **GetTelemetryData 全是 0** | ① 确认已进入比赛模式；② 某些游戏需车辆启动后才有数据；③ 检查是否成功调用 StartTelemetry；④ 共享内存游戏需完全加载赛道 |
| **找不到 TelemetrySDK.dll** | ① DLL 与 C# 可执行文件同目录；② 或放入 PATH；③ 检查 DLL 为 x64（与 C# 程序匹配） |
| **数据值异常/乱码** | ① C# `NormalizedData` 必须 `Pack=1`；② bool 字段加 `[MarshalAs(UnmanagedType.U1)]`；③ 数组字段加 `[MarshalAs(UnmanagedType.ByValArray, SizeConst=N)]`；④ C# 项目目标平台 x64 |

---

## 📦 DLL 交付与环境要求

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
TelemetrySDK_API_Document.md  // 本文档
```

无需安装任何额外运行时、驱动或依赖库，将 DLL 放置到调用方进程的工作目录或系统 PATH 中即可加载。

---

## 📝 版本信息

- **当前版本**: 2.0.0
- **最后更新**: 2026-06-29
- **C++标准**: C++17
- **编译器**: MSVC 2022
- **目标平台**: Windows x64
- **支持游戏数**: 31 款（枚举值数；其中 WRC 8/9/10 协议数据极有限，仅速度+转速+档位）
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
