# TelemetrySDK API 文档

> **SDK 版本 1.0.0**（首个正式版）
> 本文档为交付文档，与 `TelemetrySDK.dll` 同版本；C# 侧全部结构体 / 枚举 / 常量定义在文档内可直接粘贴（见 4.4 / 8.2 / 10.3 / 附录 A）。

## 修订历史

| 文档轮次 | SDK 版本 | 日期 | 说明 |
|---|---|---|---|
| 第四轮（交付终轮） | 1.0.0 | 2026-09-05 | 首个正式版（优化阶段 0-4 + SHM 新鲜度统一化收官，无新增导出 API、ABI 不变）：SHM 新鲜度全游戏接入（R3E/AMS2/SCS/RF2/LMU 补齐，全部适配器提供 dataAge，UNKNOWN_AGE 实际不可达、枚举保留为 ABI 兼容）；dataAgeMs 语义按传输方式分家 + 暂停语义差异说明；StartTelemetry 补充 iRacing 游戏进程前置条件与重试模式。交付前调用方视角审视追加：勘误 §3.2 返回值语义（true ≠ 数据已到达）与 §3.5 掩码静/动态表述（GetSupportedFlags 为启动快照、iRacing 实时以 validFlags 为准）；新增 8.1 UDP 默认监听端口表、10.5 恢复语义与重入安全、附录 A C# 声明全集 |
| 第三轮 | 0.4.0 | 2026-09-03 | 链路健壮性（无新增导出 API，ABI 不变）：UDP 断连恢复——recvfrom 瞬时错误不再永久断流（旧版一次错误即数据冻结），恢复发包自动自愈；SHM 新鲜度采样——AC/ACC/ACRally/ACEvo（physics packetId）与 iRacing（tickCount）脱离 UNKNOWN_AGE，游戏退出/冻结可感知（STALE）；WAITING_DATA 语义扩展到 SHM 空映射。状态机六态不变 |
| 第二轮 | 0.3.0 | 2026-09-02 | 新增错误处理三 API：GetLastTelemetryError / GetLastTelemetryErrorMessage（中文 UTF-8 消息）/ GetTelemetryStatus（SDK/连接状态机 + 数据新鲜度）。第 10 节占位替换为正式内容。数据契约（NormalizedData 布局 / validFlags 位表）与 0.2.0 一致，无破坏性变更 |
| 第一轮 | 0.2.0 | 2026-09-02 | 契约冻结后整体重写：字段七段分组重排（位号 0-45 跟排）、滑移三参数/raceFlag/test1-8 删除、旗语 11 独立 bool、支持掩码表按实测矩阵刷真、新增 GetSDKVersion / SetUDPSettings / SetRangeCheckEnabled 三个 API。旧 2.0.0 版次文档作废（布局已全面变更，其中 C# 定义不可再引用） |
| — | 0.1.0 | 2026-09-01 | 阶段 0 基础设施：版本号体系（宏单一来源 + .rc 资源）、post-build 拷 DLL |
| （历史） | — | 2026-06-29 | 旧版文档 v2.0.0（已废弃） |

---

## 1. 概述

**一句话定位**：把 20+ 款赛车模拟游戏的异构遥测协议（共享内存 / UDP）统一成 `NormalizedData`（512 字节），编译为 `TelemetrySDK.dll`（C 接口），供 C# 或任何支持 C ABI 的语言调用。

**数据流**：游戏遥测源 →（共享内存读取 / UDP 接收线程）→ 适配器归一化（字段映射 + 单位换算 + 派生判定）→ 健康层校验 → `GetTelemetryData` 输出。

**核心机制**：`validFlags` 位掩码标记每个字段是否被当前游戏支持/投递——客户端判断字段有效性的**唯一权威**。

**支持矩阵**：62 参数 × 31 游戏的支持与否**不在本文档内**，唯一权威是《遥测支持》表——随包交付 `遥测支持.csv` 快照，线上文档同步维护、随时可查最新版（实测定稿）。

---

## 2. 快速开始（C#）

### 2.1 部署

- `TelemetrySDK.dll`（x64）放到客户端可执行文件同目录；平台位数必须一致。
- DLL 静态链接 VC 运行库（`/MT`），无需安装 VC_redist，无其他依赖文件。

### 2.2 启动自检（推荐模式）

客户端启动时先调 `GetSDKVersion()` 并显示在 UI（如标题栏），一举两得：

1. P/Invoke 全链路自检——能出版本号即说明 DLL 加载、导出函数、调用约定全部正常；
2. **陈旧 DLL 探测器**——显示值与预期不符 = 手动拷 DLL 忘了，一眼可判。

```csharp
try
{
    int v = TelemetryAPI.GetSDKVersion();            // 1.0.0 → 10000
    string ver = $"{v / 10000}.{(v / 100) % 100}.{v % 100}";
    Title = $"TelemetrySDK {ver}";
}
catch (DllNotFoundException)
{
    // WPF 无全局异常处理，不兜底会白屏闪退——必须 try/catch 并给出提示
    MessageBox.Show("未找到 TelemetrySDK.dll");
}
```

### 2.3 最小调用链

```csharp
// 1. 启动（以 F1 25 为例）
if (!TelemetryAPI.StartTelemetry((int)GameId.F1_2025))
    return; // 启动失败：gameId 非法 / 数据源初始化失败

// 2. 每帧轮询（建议 30-60Hz，与 UI 帧率一致即可）
var data = new NormalizedData();
if (TelemetryAPI.GetTelemetryData(ref data))
{
    if ((data.validFlags & ValidFlags.Speed) != 0)      // 先判位再用值
        speedText.Text = data.speed.ToString("F1");
    if ((data.validFlags & ValidFlags.TyrePressure) != 0)
        flText.Text = data.tyrePressure[0].ToString("F1"); // [0]=FL
}

// 3. 退出前停止（幂等，可重复调用）
TelemetryAPI.StopTelemetry();
```

切换游戏 = `StopTelemetry()` → `StartTelemetry(新gameId)`。同一时刻只有一个会话。运行中直接再调 `StartTelemetry`（同游戏或换游戏）也是安全的——内部会自动停止旧会话后切换，手动 Stop 只是更显式（详见 10.5）。

---

## 3. API 详解（10 个导出函数）

全部 `extern "C"` + `__cdecl`，Windows x64。

### 3.1 StartTelemetry

```c
bool StartTelemetry(int gameId);
```

传入 `GameId`，内部创建对应适配器并连接数据源。`true` = 初始化成功（**不代表立即有数据**——数据在游戏开始发送/写入后到达，新鲜度走 3.10 GetTelemetryStatus）。`false` = 失败，失败原因用 `GetLastTelemetryError()` 查询（5 类错误码 + 中文消息，见第 10 节）。**运行中重复调用是安全的**——内部先自动停止当前会话再启动新会话，不会报错或泄漏资源（详见 10.5）。

**iRacing 前置条件**（全 SDK 唯一）：iRacing 要求游戏进程已在运行才能启动成功——sim 未开时返回 `false + TEL_ERR_GAME_NOT_RUNNING`。这是协议本质约束（共享内存与数据有效事件由 sim 进程创建），不是缺陷。客户端处理模式：收到 `GAME_NOT_RUNNING` → 提示用户启动游戏 → 开好后**重调一次 `StartTelemetry`** 即恢复，不要弹致命错误死框（它是"环境未就绪"可重试恢复）。其余游戏 `StartTelemetry` 必成功，游戏后开靠 `WAITING_DATA → CONNECTED` 自动接上，无需重试。

### 3.2 GetTelemetryData

```c
bool GetTelemetryData(NormalizedData* outData);
```

每帧调用，SDK 将最新归一化数据（含健康层校验）填入 `outData`，同时写入 `validFlags`。**返回值**：`true` = SDK 会话存在（已 StartTelemetry 且未 Stop）；`false` = 未启动或 `outData` 为空指针。**`true` 不代表数据已到达**——数据未到（WAITING_DATA / STALE）时适配器静默返回，`outData` 保持调用方上次传入的内容（首次调用即复用实例的初值），`validFlags` 仍为该游戏支持掩码。数据到达性一律走 3.10 GetTelemetryStatus，两者职责分离。

C# 侧传 `ref NormalizedData`；建议固定一个结构体实例反复复用（`new` 即零初始化），不要每帧 new。

### 3.3 StopTelemetry

```c
void StopTelemetry();
```

销毁适配器、释放底层连接。幂等。未启动时调用也安全。

### 3.4 GetSDKVersion

```c
int GetSDKVersion();
```

返回版本号 int 编码：`主*10000 + 次*100 + 修订`（1.0.0 → 10000）。推荐用法见 2.2 启动自检。

### 3.5 GetSupportedFlags

```c
uint64_t GetSupportedFlags();
```

返回当前游戏**声明支持**的字段掩码（`StartTelemetry` 成功后调用；**未启动 / Start 失败后返回 0**）。与 `validFlags` 的区别：

| | 语义 | 时机 |
|---|---|---|
| `GetSupportedFlags()` | 该游戏**能力面**（协议层能提供什么）的**启动时快照** | StartTelemetry 成功时取一次，之后不变 |
| `data.validFlags` | 支持掩码的**逐帧写入**——iRacing 为运行时动态重算（换车/换会话时重扫变量表），其余游戏每帧查静态表、值恒等于快照 | 每帧随 `GetTelemetryData` 写入 |

两点注意：

1. **`validFlags` 与数据到达性无关**——它只声明"该游戏协议层支持这些字段"，本帧数据是否真的到达走 3.10 GetTelemetryStatus（返回值语义见 3.2）；
2. **iRacing 特例**：支持的变量集合随车型变化（Ferrari 296 有 ERS、MX-5 Cup 没有），SDK 在换车/换会话时自动更新掩码。**实时能力面以 `data.validFlags` 为准**——若 StartTelemetry 时 sim 还停在主菜单，`GetSupportedFlags()` 快照可能偏小，进入驾驶后 `data.validFlags` 会更新为完整掩码。其余游戏两者恒等。

### 3.6 SetUDPSettings

```c
bool SetUDPSettings(int gameId, const TelemetryUDPSettings* settings);
```

UDP 监听端口自定义 + 原始包 fan-out 转发（详见第 8 节）。要点：

- 仅适用于 UDP 适配器（F1 系列 / Forza / DiRT / EA WRC / WRC Generations / LFS / BeamNG / RBR）；共享内存游戏传参返回 `false`；
- **必须在 `StartTelemetry` 之前调用**；修改配置需 Stop/Start 后生效（无运行中热更新）；
- `settings = nullptr` 表示清除该游戏配置、恢复默认；
- 配置按 gameId 持久保存在 SDK 内部表，**跨 Start/Stop 会话有效**。

### 3.7 SetRangeCheckEnabled

```c
void SetRangeCheckEnabled(bool enabled);
```

健康层运行时开关，默认 `true`（生产行为）。`false` = 开发测试旁路——连 NaN/Inf 清洗一起跳过，直出适配器原始真值（超界值正是单位换算错误的诊断信号，置 0 会销毁线索）。可运行中随时切换，对之后的每次 `GetTelemetryData` 立即生效。

### 3.8 GetLastTelemetryError

```c
int GetLastTelemetryError();
```

返回最近一次 `StartTelemetry` 失败码（`TelemetryErrorCode`，见第 10 节错误码表）。成功启动后为 `TEL_ERR_NONE(0)`。**错误槽只记 StartTelemetry 失败**——`GetTelemetryData` 空转不写错误，客户端轮询循环不会覆盖启动诊断信息。

### 3.9 GetLastTelemetryErrorMessage

```c
int GetLastTelemetryErrorMessage(char* buf, int bufSize);
```

返回失败码对应的**中文错误消息**（UTF-8 编码，逐条带"用户该做什么"，可直接弹窗展示）。返回值 = 写入字节数（含结尾 null）：

- `buf = null` 或 `bufSize <= 0`：探长模式，仅返回所需长度，不写入；
- `bufSize` 小于所需：截断写入（保证 null 终止），返回 `bufSize`。

C# 侧用 `byte[]` 接收后 `Encoding.UTF8` 解码（P/Invoke 声明见附录 A；用 `string` 参数封送会乱码）。

### 3.10 GetTelemetryStatus

```c
bool GetTelemetryStatus(TelemetryStatus* outStatus);
```

SDK/连接状态快照（20 字节结构体，字段语义见第 10 节状态机）。**每帧现算、无内部状态变量**；未启动（IDLE）时返回 `true` + 全默认字段；仅 `outStatus = null` 返回 `false`。

与 `GetTelemetryData` 的职责分离：后者的 bool 返回值**语义不变**（适配器断连时 Update 静默返回、outData 保持上一帧数据仍返回 `true`，ABI 兼容）——数据新鲜度/连接状态一律走本接口。

---

## 4. NormalizedData 数据布局

### 4.1 布局总则

- **512 字节固定体积**，`#pragma pack(1)` 逐字节紧凑排列，C# 必须用 `Pack = 1`；
- **字段物理顺序 = VALID 位号（0-45）**，对照第 5 节位表；
- 分组 = 数据流方向：驾驶输入 → 车辆动态 → 轮胎 → 动力系统 → 辅助系统 → 赛事状态 → 元信息；
- **布局已冻结**（2026-09-01 蓝图定稿）：增删/挪位字段 = 破坏性变更（major 版本），新增字段只能吃 `_reserved`；
- 当前用量 263 字节 + `_reserved[249]` = 512。

### 4.2 字段表（位号 = 物理顺序）

| 位段 | 组 | 字段（类型 · 语义） |
|---|---|---|
| 0-5 | 驾驶输入 | 0 throttle(f) 0-1 · 1 brake(f) 0-1 · 2 steer(f) -1左~1右 · 3 clutch(f) 0=松开~1=踩到底 · 4 gear(i) 0=N/1+前进/负=倒档（-1=R1，-2=R2…） · 5 gearLabel(char[8]) |
| 6-9 | 车辆动态 | 6 speed(f) km/h（恒非负） · 7 rpm(f) · 8 maxRpm(f) · 9 shiftRpm(f) 建议换挡转速 |
| 10-18 | 轮胎 | 10 tyrePressure(f[4]) kPa · 11 tyreCoreTemp(f[4]) °C · 12 tyreTempInner(f[4]) · 13 tyreTempMiddle(f[4]) · 14 tyreTempOuter(f[4]) · 15 tyreWear(f[4]) 0-100 递增% · 16 brakeTemp(f[4]) °C · 17 isWheelLocked(bool[4]) · 18 isWheelSlipping(bool[4]) |
| 19-29 | 动力系统 | 19 fuelRemaining(f) 升 · 20 fuelRemainingPct(f) 0-1 · 21 waterTemp(f) °C · 22 oilTemp(f) °C · 23 turboPressure(f) bar · 24 ersCharge(f) 0-1 · 25 ersDeployMode(i) 枚举随游戏见 4.3 · 26 ersRecoveryLevel(i) 0-100 · 27 isEngineRunning(b) · 28 isIgnitionOn(b) · 29 enginePowerMode(i) |
| 30-38 | 辅助系统 | 30 isPitLimiterActive(b) · 31 isInPits(b) · 32 isTcActive(b) · 33 tcLevel(i) · 34 tcCutLevel(i) · 35 isAbsActive(b) · 36 absLevel(i) · 37 isDrsAvailable(b) · 38 isDrsActive(b) |
| 39-45 | 赛事状态 | 39 currentLap(i) 1起计 · 40 totalLaps(i) · 41 position(i) · 42 currentLapTime(f) 秒 · 43 lastLapTime(f) · 44 bestLapTime(f) · 45 旗语 11 bool（共用 VALID_RACE_FLAG 一位） |
| 元信息 | — | validFlags(u64) · _reserved(byte[249]) |

旗语 11 bool 顺序（绿旗居首，其余按 FlagType 枚举序）：`isGreenFlag / isBlueFlag / isYellowFlag / isBlackFlag / isWhiteFlag / isCheckeredFlag / isPenaltyFlag / isOrangeFlag / isRedFlag / isScActive / isVscActive`。可同帧并发多旗。

### 4.3 约定速查

- **单位全 SI**：速度 km/h、温度 °C、压力 kPa、涡轮 bar、圈时 秒、燃油 升；
- **踏板方向统一**：throttle / brake / clutch 均为踏板行程语义，0=松开、1=踩到底；
- **speed 恒非负**：速度幅值，倒车行驶时也是正值、不回负；
- **轮胎/刹车数组顺序**：`[0]=FL, [1]=FR, [2]=RL, [3]=RR`；
- **胎温三层位置**：Inner = 靠车体中心一侧，Middle = 胎面中心，Outer = 胎体外侧；
- `tyreWear` 统一为 **0-100 递增百分比**（0=全新，100=完全磨损）；
- **gear 负值为倒档**：-1=R1、-2=R2、-3=R3（SCS 卡车多倒档）；0=空挡，1+=前进档；
- `gearLabel`：7 字符 + null 终止；SCS 卡车复合档位输出 `"4H"/"R1"/"1L"` 等，其他游戏留空 `""`；
- **ersDeployMode 枚举随游戏**：F1 系列 0=无 / 1=中 / 2=飞行圈 / 3=超车；LMU 为 MGU-K 功率地图档位（0 至最大档）；其余游戏不支持该字段（位未声明）；
- **圈时无效值**：尚无有效圈速时（本圈未完成 / 无历史圈速）输出 0 且有效位仍置位——客户端按 0 / 极小值过滤；
- `isWheelLocked` / `isWheelSlipping`：SDK 派生的判定结果（bool）；滑移原始三参数（slipRatio/slipAngle/combinedSlip）**不在结构体内**；
- 拉力类游戏（ACRally/EAWRC/DiRT/WRCG）的 `isWheelLocked` 仅判定前轮，后两元素恒 false。

### 4.4 C# 结构定义（可直接粘贴）

Pack=1 共 512 字节，字段与 4.2 表逐行对应：

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NormalizedData
{
    // —— 驾驶输入（bit 0-5）——
    public float throttle;                                   // 0.0-1.0
    public float brake;                                      // 0.0-1.0
    public float steer;                                      // -1.0(左) ~ 1.0(右)
    public float clutch;                                     // 0.0-1.0，1=踩到底（与油门/刹车同向）
    public int gear;                                         // 0=N, 1+=前进, -1=R
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
    public string gearLabel;                                 // "4H"/"R1" 等，空串=""

    // —— 车辆动态（bit 6-9）——
    public float speed;                                      // km/h
    public float rpm;
    public float maxRpm;
    public float shiftRpm;                                   // 建议换挡转速

    // —— 轮胎（bit 10-18；[0]=FL [1]=FR [2]=RL [3]=RR）——
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] tyrePressure;                             // kPa
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] tyreCoreTemp;                             // °C
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] tyreTempInner;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] tyreTempMiddle;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] tyreTempOuter;
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
    public int ersDeployMode;                                // 档位索引
    public int ersRecoveryLevel;                             // 0-100
    [MarshalAs(UnmanagedType.U1)] public bool isEngineRunning;
    [MarshalAs(UnmanagedType.U1)] public bool isIgnitionOn;
    public int enginePowerMode;                              // 档位索引

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
    public int position;
    public float currentLapTime;                             // 秒
    public float lastLapTime;
    public float bestLapTime;

    // —— 旗语（bit 45 共用一位；绿旗居首；可同帧并发多旗）——
    [MarshalAs(UnmanagedType.U1)] public bool isGreenFlag;
    [MarshalAs(UnmanagedType.U1)] public bool isBlueFlag;
    [MarshalAs(UnmanagedType.U1)] public bool isYellowFlag;
    [MarshalAs(UnmanagedType.U1)] public bool isBlackFlag;
    [MarshalAs(UnmanagedType.U1)] public bool isWhiteFlag;
    [MarshalAs(UnmanagedType.U1)] public bool isCheckeredFlag;
    [MarshalAs(UnmanagedType.U1)] public bool isPenaltyFlag;
    [MarshalAs(UnmanagedType.U1)] public bool isOrangeFlag;
    [MarshalAs(UnmanagedType.U1)] public bool isRedFlag;
    [MarshalAs(UnmanagedType.U1)] public bool isScActive;    // 安全车
    [MarshalAs(UnmanagedType.U1)] public bool isVscActive;   // 虚拟安全车

    // —— 元信息 ——
    public ulong validFlags;                                 // 有效性掩码，见 ValidFlags

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 249)]
    public byte[] _reserved;                                 // 263 已用 + 249 = 512
}
```

**C# 绑定三陷阱**：

1. 所有 bool 字段（含 `bool[]`）必须 `[MarshalAs(UnmanagedType.U1)]`——默认封送 4 字节 Win32 BOOL，会撑破 512B 布局；
2. 数组字段是引用类型：结构体实例**复用**时内嵌数组同一引用反复覆写没问题，但**不要**在多线程间共享同一实例；
3. `gearLabel` 用 `ByValTStr` 读出 string；写方向（客户端→SDK）本 SDK 无此场景。

---

## 5. validFlags 有效性掩码

### 5.1 位定义（位号 = 字段物理顺序）

位名（C++ 宏 `VALID_XXX` / C# 常量，完整定义见附录 A）与字段对照：

| 位 | 宏 / C# 常量 | 字段 |
|---|---|---|
| 0 | VALID_THROTTLE / Throttle | throttle |
| 1 | VALID_BRAKE / Brake | brake |
| 2 | VALID_STEER / Steer | steer |
| 3 | VALID_CLUTCH / Clutch | clutch |
| 4 | VALID_GEAR / Gear | gear |
| 5 | VALID_GEAR_LABEL / GearLabel | gearLabel[8] |
| 6 | VALID_SPEED / Speed | speed |
| 7 | VALID_RPM / Rpm | rpm |
| 8 | VALID_MAX_RPM / MaxRpm | maxRpm |
| 9 | VALID_SHIFT_RPM / ShiftRpm | shiftRpm |
| 10 | VALID_TYRE_PRESSURE / TyrePressure | tyrePressure[4] |
| 11 | VALID_TYRE_CORE_TEMP / TyreCoreTemp | tyreCoreTemp[4] |
| 12 | VALID_TYRE_TEMP_INNER / TyreTempInner | tyreTempInner[4] |
| 13 | VALID_TYRE_TEMP_MIDDLE / TyreTempMiddle | tyreTempMiddle[4] |
| 14 | VALID_TYRE_TEMP_OUTER / TyreTempOuter | tyreTempOuter[4] |
| 15 | VALID_TYRE_WEAR / TyreWear | tyreWear[4] |
| 16 | VALID_BRAKE_TEMP / BrakeTemp | brakeTemp[4] |
| 17 | VALID_WHEEL_LOCKED / WheelLocked | isWheelLocked[4]（一位覆盖整个数组） |
| 18 | VALID_WHEEL_SLIPPING / WheelSlipping | isWheelSlipping[4]（同上） |
| 19 | VALID_FUEL / Fuel | fuelRemaining |
| 20 | VALID_FUEL_PCT / FuelPct | fuelRemainingPct |
| 21 | VALID_WATER_TEMP / WaterTemp | waterTemp |
| 22 | VALID_OIL_TEMP / OilTemp | oilTemp |
| 23 | VALID_TURBO_PRESSURE / TurboPressure | turboPressure |
| 24 | VALID_ERS_CHARGE / ErsCharge | ersCharge |
| 25 | VALID_ERS_DEPLOY / ErsDeploy | ersDeployMode |
| 26 | VALID_ERS_RECOVERY / ErsRecovery | ersRecoveryLevel |
| 27 | VALID_ENGINE_RUNNING / EngineRunning | isEngineRunning |
| 28 | VALID_IGNITION / Ignition | isIgnitionOn |
| 29 | VALID_ENGINE_POWER / EnginePower | enginePowerMode |
| 30 | VALID_PIT_LIMITER / PitLimiter | isPitLimiterActive |
| 31 | VALID_IN_PITS / InPits | isInPits |
| 32 | VALID_TC_ACTIVE / TcActive | isTcActive |
| 33 | VALID_TC_LEVEL / TcLevel | tcLevel |
| 34 | VALID_TC_CUT / TcCut | tcCutLevel |
| 35 | VALID_ABS_ACTIVE / AbsActive | isAbsActive |
| 36 | VALID_ABS_LEVEL / AbsLevel | absLevel |
| 37 | VALID_DRS_AVAILABLE / DrsAvailable | isDrsAvailable |
| 38 | VALID_DRS_ACTIVE / DrsActive | isDrsActive |
| 39 | VALID_CURRENT_LAP_NUM / CurrentLapNum | currentLap |
| 40 | VALID_TOTAL_LAPS / TotalLaps | totalLaps |
| 41 | VALID_POSITION / Position | position |
| 42 | VALID_CURRENT_LAP / CurrentLap | currentLapTime |
| 43 | VALID_LAST_LAP / LastLap | lastLapTime |
| 44 | VALID_BEST_LAP / BestLap | bestLapTime |
| 45 | VALID_RACE_FLAG / RaceFlag | 旗语 11 bool 共用一位 |

C# 常量类（`public const ulong XXX = 1UL << n;`，命名对照上表，完整可直接粘贴的定义见附录 A）。

### 5.2 使用规则（重要）

1. **唯一权威是掩码**：`validFlags` 对应位为 0 时，字段数值是**未定义的内部填充值**（可能为 0、-1 或其他）——**不要**用 `value == -1` / `value == 0` 之类的数值比较判断有效性；
2. 数组字段一位覆盖整个数组（`VALID_TYRE_PRESSURE` 覆盖 4 个元素）；个别元素异常由健康层置 0 兜底，不代表该位被清除；
3. 旗语 11 bool 共用 bit 45：置位后读 11 个 bool 获知具体旗态（可并发多旗）；
4. 某游戏某字段是否声明支持，查《遥测支持》表（随包 `遥测支持.csv` / 线上文档）；运行时 `GetSupportedFlags()` 为启动时快照，实时能力面以 `data.validFlags` 为准（iRacing 两者可能不同，见 3.5）。

---

## 6. 数据健康层

- **行为**：`GetTelemetryData` 输出前对连续数值做全局边界校验（范围表 = 物理极限而非工作范围），NaN / Inf / 超界值统一**置 0**；
- **与 validFlags 完全解耦**：置 0 是数值层兜底，不代表"游戏不支持"；支持性是协议层声明；
- **不处理**：bool 字段、`validFlags`、`_reserved`；
- **开关**：`SetRangeCheckEnabled(false)` 旁路——开发期看纯真值（含 NaN），用于诊断单位换算错误（忘 ÷100 的胎压 2500、忘 −273.15 的水温 300 这类"离谱值"正是线索）；
- **性能**：~50-150ns/次，60Hz 下可忽略。

---

## 7. GameId 枚举

Steam 游戏 = Steam App ID，非 Steam 游戏用自定义 ID。全部枚举值如下（C# 枚举可直接照抄附录 A）：

| 系列 | 枚举（值） |
|---|---|
| Assetto Corsa | GAME_ASSETTO_CORSA(244210) · GAME_ACC(805550) · GAME_ACRALLY(3917090) · GAME_AC_EVO(3058630) |
| F1 | GAME_F1_2022(1692250) · GAME_F1_2023(2108330) · GAME_F1_2024(2488620) · GAME_F1_2025(3059520) |
| Forza | GAME_FORZA_MOTORSPORT(2440510) · GAME_FORZA_HORIZON_4(1293830) / 5(1551360) / 6(2483190) |
| DiRT | GAME_DIRT_4(421020) · GAME_DIRT_RALLY_2(690790) |
| rFactor | GAME_RF2(365960) · GAME_LMU(2399420) |
| PCARS/AMS2 | GAME_PCARS2(378860) · GAME_PCARS3(958400) · GAME_AMS2(1066890) |
| WRC | GAME_WRC_8(1004750) · GAME_WRC_9(1267540) · GAME_WRC_10(1462810) · GAME_WRC_GENERATIONS(1953520) · GAME_EA_WRC(1849250) |
| 其他竞速 | GAME_IRACING(266410) · GAME_R3E(211500) · GAME_BEAMNG(284160) |
| 模拟驾驶 | GAME_SCS_ETS2(227300) · GAME_SCS_ATS(270880) |
| 非 Steam | GAME_RBR(22) · GAME_LFS(25) |

---

## 8. UDP 高级配置（端口自定义 + fan-out 转发）

### 8.1 默认监听端口（游戏侧配置必读）

UDP 游戏的数据是游戏**主动推送**的——集成第一步是在游戏内开启遥测输出并指向 SDK 的监听地址（通常 `127.0.0.1:下表端口`）。各游戏默认监听端口：

| 游戏 | SDK 默认监听端口 |
|---|---|
| F1 22 / 23 / 24 / 25 | 20777 |
| DiRT 4 / DiRT Rally 2.0 | 20777 |
| WRC Generations | 20777 |
| Forza Motorsport (2023) | 1024 |
| Forza Horizon 4 / 5 | 1024 |
| Forza Horizon 6 | 20440 |
| EA Sports WRC | 26666 |
| LFS / BeamNG（OutGauge 协议） | 30000 |
| Richard Burns Rally | 30000 |

实战提示：

1. **游戏侧遥测必须先开启**——各游戏的遥测开关与目标地址设置互不相同（多在游戏设置的遥测/数据分页内），需逐游戏确认开启并指向 SDK 监听地址；
2. **SimHub 是 20777 的头号占用者**——`TEL_ERR_UDP_PORT_IN_USE` 先查它：关掉 SimHub，或用 `SetUDPSettings` 给 SDK 换端口（注意游戏侧的目标端口也要同步改）；
3. **同机多客户端会抢端口**——第二个进程 bind 同端口即失败（10048）。官方分发方案是 `SetUDPSettings` 的 fan-out 转发：一个进程监听、原样转发给其余进程。

### 8.2 结构体

```c
typedef struct TelemetryForwardTarget {
    char     ip[40];    // IPv4 字符串，null 终止
    uint16_t port;
} TelemetryForwardTarget;

typedef struct TelemetryUDPSettings {
    uint16_t listenPort;                       // 0 = 不覆盖，用适配器默认端口
    uint32_t forwardCount;                     // 0 = 不转发，最大 8
    TelemetryForwardTarget forwardTargets[8];  // 仅前 forwardCount 个生效
} TelemetryUDPSettings;
```

C# 定义**注意是自然对齐（不加 Pack=1，与 NormalizedData 不同）**：`listenPort` 与 `forwardCount` 之间有 2 字节 padding，总大小 344 字节：

```csharp
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct TelemetryForwardTarget
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
    public string ip;
    public ushort port;
}

[StructLayout(LayoutKind.Sequential)]
public struct TelemetryUDPSettings
{
    public ushort listenPort;
    public uint forwardCount;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public TelemetryForwardTarget[] forwardTargets;
}
```

### 8.3 行为说明

- **转发语义**：SDK 收到 UDP 原始字节后**原样转发**（不做解析/修改）到全部目标——SDK 与第三方工具（如 SimHub）可同时消费同一游戏数据；
- 典型用法：游戏只允许填一个遥测目标地址时，用 SDK 做分发器；
- 环回防护：目标为 `127.0.0.1` 且端口等于监听端口时自动跳过（防自环风暴）；
- 转发失败静默（目标不可达不影响本机遥测）。

### 8.4 C# 调用示例

```csharp
var settings = new TelemetryUDPSettings
{
    listenPort = 20777,                    // 0 = 用游戏默认
    forwardCount = 1,
    forwardTargets = new TelemetryForwardTarget[8]   // 必须分配满长数组
};
settings.forwardTargets[0] = new TelemetryForwardTarget { ip = "192.168.1.100", port = 20778 };

TelemetryAPI.SetUDPSettings((int)GameId.F1_2025, ref settings);  // 须在 StartTelemetry 之前
TelemetryAPI.StartTelemetry((int)GameId.F1_2025);

// 清除配置、恢复默认：传 IntPtr.Zero 的重载
TelemetryAPI.SetUDPSettings((int)GameId.F1_2025, IntPtr.Zero);
```

---

## 9. 注意事项

- **线程模型**：API 非线程安全，`GetTelemetryData` 等请在同一线程序列调用（典型：UI 线程/渲染循环）；SDK 内部自建接收线程（UDP 游戏）与共享内存读取，调用方无需关心；
- **调用频率**：适配器缓存最新数据，`GetTelemetryData` 轮询频率不等于数据更新频率；30-60Hz 足够；
- **资源管理**：进程退出前调 `StopTelemetry()`；忘记调用由 OS 兜底回收（套接字/文件映射句柄），但建议显式；
- **位数匹配**：DLL 与客户端必须同为 x64；
- **DLL 版本核对**：升级 SDK 后 `GetSDKVersion()` 显示值应同步变化——没变 = 拷贝遗漏；
- **实测数据参考**：《遥测支持》表（随包 `遥测支持.csv` / 线上文档）是 62 参数 × 31 游戏的实测定稿矩阵（含 WRC8/9/10 数据极有限、iRacing 动态掩码等特殊情况备注）。

---

## 10. 错误处理与状态机（阶段 2 引入，1.0.0 交付终轮更新）

三个 API 协作：`StartTelemetry` 失败 → `GetLastTelemetryError` 拿错误码 → `GetLastTelemetryErrorMessage` 拿中文消息（弹窗给用户）；运行中每帧 `GetTelemetryStatus` 判连接/新鲜度（UI 状态灯）。

### 10.1 错误码表（TelemetryErrorCode）

| 错误码 | 值 | 含义 | 用户该做什么 |
|---|---|---|---|
| `TEL_ERR_NONE` | 0 | 无错误（上次启动成功或尚未调用） | — |
| `TEL_ERR_INVALID_GAME_ID` | 1 | gameId 不在支持列表 | 检查 `GameId` 枚举传参 |
| `TEL_ERR_GAME_NOT_RUNNING` | 2 | 共享内存映射不存在 | 先启动游戏并打开遥测输出，再重新连接 |
| `TEL_ERR_UDP_PORT_IN_USE` | 3 | UDP 监听端口被占用（bind 10048） | 关掉占用端口的程序，或 `SetUDPSettings` 换端口 |
| `TEL_ERR_WINSOCK_FAILURE` | 4 | Winsock 初始化/socket 创建失败 | 检查系统网络组件 |
| `TEL_ERR_INIT_FAILED` | 5 | 其他初始化失败 | 重试；持续失败按消息里的系统错误码反馈 |

错误槽约定：**只记 `StartTelemetry` 失败**，成功即清 `TEL_ERR_NONE`；`GetTelemetryData` 空转（未启动）只返回 false、不写错误——防客户端轮询循环覆盖诊断信息。`TEL_ERR_GAME_NOT_RUNNING` 仅 iRacing 在游戏未运行时触发；其余游戏（含 ACC/AC 等共享内存游戏）无游戏也能启动成功，连接状态走 10.2。

### 10.2 连接状态机（TelemetryConnState）

```
IDLE ──StartTelemetry 成功──→ ┌─ !IsConnected ──→ DISCONNECTED（SDK 检测到传输层失效；
                              │                     iRacing sim 退出时也走此分支）
                              ├─ 从未观察到数据 ──→ WAITING_DATA（UDP 未收包 / SHM 数据从未
                              │                     更新——游戏没在发/写数据，不是启动失败）
                              ├─ age ≤ staleTimeoutMs → CONNECTED（数据新鲜）
                              └─ age > staleTimeoutMs → STALE（超阈值没收到新包/新帧——游戏退出、
                                                  卡死、暂停、回菜单都会停更，如实上报）
```

`UNKNOWN_AGE` 枚举值保留为 **ABI 兼容**：1.0.0 起全部游戏提供 `dataAge`，该状态实际不可达——旧客户端的 switch 分支可安全保留，新客户端无需处理。

**`dataAgeMs` 语义按传输方式分两类**（对客户端用法完全一致：与 `staleTimeoutMs` 比较即可）：

- **UDP 游戏** = 收包年龄——收到新的 UDP 数据即刷新；
- **SHM 游戏** = 数据更新年龄——SDK 检测到共享内存数据被游戏写入即刷新。

**暂停语义差异**：多数游戏（AC 系 / R3E / SCS / AMS2 / iRacing）在游戏暂停、回菜单时数据冻结，超阈值报 STALE——属正常表现；RF2/LMU 在暂停期间引擎通常仍在刷新数据 → 保持 CONNECTED。客户端对 RF2/LMU 不应依赖 STALE 提示暂停状态。

`TelemetryStatus`（20 字节，C# 侧 `Pack=4`）：

| 字段 | 类型 | 语义 |
|---|---|---|
| `sdkState` | int32 | `TEL_SDK_IDLE(0)` / `TEL_SDK_RUNNING(1)` |
| `connState` | int32 | 上表六态 |
| `lastError` | int32 | 错误槽透传（同 `GetLastTelemetryError()`） |
| `dataAgeMs` | uint32 | 数据年龄（UDP = 收包年龄；SHM = 数据更新年龄）；`0xFFFFFFFF` = 未知/尚无数据 |
| `staleTimeoutMs` | uint32 | STALE 判定阈值（默认 5000ms；透传给客户端可展示） |

UI 态建议：`WAITING_DATA` 显示"等待游戏数据"（黄）而非报错；`CONNECTED` 绿 / `STALE` 橙（"超 X ms 无数据"——注意暂停/菜单中 STALE 属正常表现，RF2/LMU 除外见上方暂停语义）/ `UNKNOWN_AGE` 灰白（1.0.0 起实际不可达，兼容保留）/ `DISCONNECTED` 红。

### 10.3 C# P/Invoke（可直接粘贴）

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct TelemetryStatus {          // 20 字节
    public int sdkState;
    public int connState;
    public int lastError;
    public uint dataAgeMs;               // 0xFFFFFFFF = 未知/尚无数据
    public uint staleTimeoutMs;
}

[DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
internal static extern int GetLastTelemetryError();

[DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
internal static extern int GetLastTelemetryErrorMessage(byte[] buf, int bufSize);

[DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
internal static extern bool GetTelemetryStatus(ref TelemetryStatus status);

// 消息解码（探长 → 取内容）：
int need = TelemetryAPI.GetLastTelemetryErrorMessage(null, 0);
var buf = new byte[need];
int written = TelemetryAPI.GetLastTelemetryErrorMessage(buf, need);
string msg = Encoding.UTF8.GetString(buf, 0, written - 1);   // 去尾 null
```

陷阱：消息**不要**用 `string` 参数封送（SDK 写 UTF-8 字节，默认封送按 ANSI 处理会乱码）；旧 DLL（<0.3.0）无这三个导出，`EntryPointNotFoundException` = 陈旧 DLL 信号（重新编译并手动拷贝）。

### 10.4 验收与边界

- SDK 侧经 13 场景错误注入验收（无需游戏；含 STALE 自愈 / 跨游戏切换 / 新鲜度全链路）；
- SHM 数据新鲜度 1.0.0 起全游戏覆盖（此前部分 SHM 游戏不评估新鲜度）；UDP 断连自愈已内建——瞬时网络错误不断流，游戏恢复发包自动回 CONNECTED；
- 已知边界（接受不修）：AMS2 圈数制站立发车过渡窗口可能短暂误报 STALE（起步后自愈，消费端可加迟滞过滤）；明确不做：`GetTelemetryData` 返回值语义变更（ABI 兼容约定）。

### 10.5 恢复语义与重入安全

**断线 / 游戏重启后的恢复矩阵**（客户端做重连 UI 的依据）：

| 场景 | 行为 | 客户端动作 |
|---|---|---|
| UDP 游戏，游戏退出后重开 | **自动恢复**——SDK 持续监听，游戏恢复发包即回 CONNECTED | 无需干预 |
| 共享内存游戏（AC 系 / R3E / AMS2 / SCS 等），退出后重进游戏 | **自动恢复**——重进游戏后数据自然续流 | 无需干预 |
| iRacing，sim 退出 | 红 DISCONNECTED | 提示用户 |
| iRacing，sim 重开 | 一般自动恢复 | 长时间未恢复再干预 |

**万能兜底**：任何传输层、任何状态下，`StopTelemetry()` → `StartTelemetry(gameId)` 必恢复（等价于重建会话，无需重启客户端进程）。建议 UI 对持续 DISCONNECTED / STALE 提供"重连"按钮，绑定这两行调用即可。

**StartTelemetry 重入安全**：运行中直接再调 `StartTelemetry`（同游戏或换游戏）都是安全的——内部先自动停止当前会话再启动新会话；显式 `Stop → Start` 与之等价，只是更显式。

---

## 附录 A：C# P/Invoke 声明全集（可直接粘贴）

10 个导出函数的 DllImport + GameId 枚举 + 46 个 ValidFlags 常量。配套结构体定义分别见：`NormalizedData` → 4.4、`TelemetryStatus` → 10.3、`TelemetryUDPSettings` / `TelemetryForwardTarget` → 8.2。

```csharp
using System;
using System.Runtime.InteropServices;

internal static class TelemetryAPI
{
    const string DLL = "TelemetrySDK.dll";

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool StartTelemetry(int gameId);                    // 1

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool GetTelemetryData(ref NormalizedData data);    // 2

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void StopTelemetry();                               // 3

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetSDKVersion();                                // 4

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ulong GetSupportedFlags();                          // 5

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool SetUDPSettings(int gameId, ref TelemetryUDPSettings settings); // 6

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool SetUDPSettings(int gameId, IntPtr settings);   // 6b nullptr 清除配置

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetRangeCheckEnabled(bool enabled);            // 7

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetLastTelemetryError();                        // 8

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetLastTelemetryErrorMessage(byte[] buf, int bufSize); // 9（探长：传 null, 0）

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool GetTelemetryStatus(ref TelemetryStatus status); // 10
}

public enum GameId
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

    // 非 Steam
    RBR = 22,
    LFS = 25
}

internal static class ValidFlags
{
    public const ulong Throttle       = 1UL << 0;
    public const ulong Brake          = 1UL << 1;
    public const ulong Steer          = 1UL << 2;
    public const ulong Clutch         = 1UL << 3;
    public const ulong Gear           = 1UL << 4;
    public const ulong GearLabel      = 1UL << 5;
    public const ulong Speed          = 1UL << 6;
    public const ulong Rpm            = 1UL << 7;
    public const ulong MaxRpm         = 1UL << 8;
    public const ulong ShiftRpm       = 1UL << 9;
    public const ulong TyrePressure   = 1UL << 10;
    public const ulong TyreCoreTemp   = 1UL << 11;
    public const ulong TyreTempInner  = 1UL << 12;
    public const ulong TyreTempMiddle = 1UL << 13;
    public const ulong TyreTempOuter  = 1UL << 14;
    public const ulong TyreWear       = 1UL << 15;
    public const ulong BrakeTemp      = 1UL << 16;
    public const ulong WheelLocked    = 1UL << 17;
    public const ulong WheelSlipping  = 1UL << 18;
    public const ulong Fuel           = 1UL << 19;
    public const ulong FuelPct        = 1UL << 20;
    public const ulong WaterTemp      = 1UL << 21;
    public const ulong OilTemp        = 1UL << 22;
    public const ulong TurboPressure  = 1UL << 23;
    public const ulong ErsCharge      = 1UL << 24;
    public const ulong ErsDeploy      = 1UL << 25;
    public const ulong ErsRecovery    = 1UL << 26;
    public const ulong EngineRunning  = 1UL << 27;
    public const ulong Ignition       = 1UL << 28;
    public const ulong EnginePower    = 1UL << 29;
    public const ulong PitLimiter     = 1UL << 30;
    public const ulong InPits         = 1UL << 31;
    public const ulong TcActive       = 1UL << 32;
    public const ulong TcLevel        = 1UL << 33;
    public const ulong TcCut          = 1UL << 34;
    public const ulong AbsActive      = 1UL << 35;
    public const ulong AbsLevel       = 1UL << 36;
    public const ulong DrsAvailable   = 1UL << 37;
    public const ulong DrsActive      = 1UL << 38;
    public const ulong CurrentLapNum  = 1UL << 39;
    public const ulong TotalLaps      = 1UL << 40;
    public const ulong Position       = 1UL << 41;
    public const ulong CurrentLap     = 1UL << 42;
    public const ulong LastLap        = 1UL << 43;
    public const ulong BestLap        = 1UL << 44;
    public const ulong RaceFlag       = 1UL << 45;
}
```
