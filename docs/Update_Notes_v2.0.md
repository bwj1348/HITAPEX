# TelemetrySDK v2.0 客户端迁移指南

> **从 v1.x 升级到 v2.0.0** · 这是**破坏性变更**，C# 端必须迁移代码，否则 P/Invoke 内存错位、读到垃圾数据。
> 完整接口说明见同目录 `TelemetrySDK_API_Document.md`，本文档只讲**变化**与**怎么改**。

---

## 1. 破坏性变更概览

| # | 变更 | 影响 | 迁移动作 |
|---|------|------|---------|
| ① | **`NormalizedData` 结构体重排**：`raceFlag` 之后新增第三批 17 个字段，`validFlags` 偏移由 **136B** 移至 **288B**，`_reserved` 由 376B 缩至 224B（总大小仍 512B 不变） | C# 结构体定义错位 → P/Invoke 读到垃圾数据 | **替换整个 `NormalizedData` C# 结构体**（见下文 §2） |
| ② | **`ValidFlags` 扩展**：新增 bit 28-44 共 17 个常量 | 老代码不冲突，但新字段读法需要新常量 | **替换整个 `ValidFlags` C# 常量类**（见下文 §3） |
| ③ | **`tyreWear[4]` 值域变更**：从 `0-1` 浮点改为 `0-100` 递增百分比（0=全新，100=完全磨损） | 老代码若做过 `*100` 显示，现在会变成 `10000%` | **全局搜索 `*100`、`tyreWear`，去掉客户端的 `*100`** |
| ④ | **`-1` 哨兵消失**：SDK 内部新增 `SanitizeNormalizedData` 层，不支持/超界/NaN/Inf 的字段**一律置 0**，不再返回 `-1.0` / `-1` | 老代码若用 `value == -1` 判无效，现在永远不成立 | **改用 `validFlags` 掩码判字段有效性**（见下文 §4） |

> 非破坏性：5 个 C 接口签名（`StartTelemetry` / `GetTelemetryData` / `StopTelemetry` / `GetSDKVersion` / `GetSupportedFlags`）**完全不变**，P/Invoke 声明不用改。

---

## 2. 完整新 `NormalizedData` C# 定义（直接复制替换）

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NormalizedData
{
    // ---- 基础参数 ----
    public float speed;
    public float rpm;
    public float maxRpm;
    public int gear;
    public float throttle;
    public float brake;
    public float steer;

    // ---- 状态标志 ----
    [MarshalAs(UnmanagedType.U1)] public bool isPitLimiterActive;
    [MarshalAs(UnmanagedType.U1)] public bool isTcActive;
    [MarshalAs(UnmanagedType.U1)] public bool isAbsActive;
    [MarshalAs(UnmanagedType.U1)] public bool isDrsAvailable;
    [MarshalAs(UnmanagedType.U1)] public bool isDrsActive;

    // ---- 轮胎滑移数据 (0=FL, 1=FR, 2=RL, 3=RR) ----
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] slipRatio;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] slipAngle;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] combinedSlip;

    // ---- ERS/混合动力系统 ----
    public float ersCharge;
    public int ersDeployMode;
    [MarshalAs(UnmanagedType.U1)] public bool isErsActive;
    public int ersRecoveryLevel;

    // ---- 发动机状态系统 ----
    [MarshalAs(UnmanagedType.U1)] public bool isEngineRunning;
    [MarshalAs(UnmanagedType.U1)] public bool isIgnitionOn;
    public int enginePowerMode;

    // ---- TC/ABS 档位系统 ----
    public int tcLevel;
    public int absLevel;
    public int tcCutLevel;

    // ---- 燃油系统 ----
    public float fuelRemaining;
    public float fuelRemainingPct;

    // ---- 赛事旗语系统 ----
    public int raceFlag;

    // ============ v2.0 新增：第三批参数（bit 28-44） ============

    // ---- 离合系统 ----
    public float clutch;                // 离合踏板行程 0.0-1.0

    // ---- 圈速计时 ----
    public int currentLap;              // 当前圈数（1起计）
    public int totalLaps;               // 赛事设定的总圈数
    public float currentLapTime;        // 当前圈已用时间（秒）
    public float lastLapTime;           // 上一圈用时（秒）
    public float bestLapTime;           // 个人最佳圈时（秒）

    // ---- 胎面温度（0=FL, 1=FR, 2=RL, 3=RR，摄氏度）----
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreTempInner;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreTempMiddle;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreTempOuter;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreCoreTemp;

    // ---- 轮胎压力（kPa）----
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyrePressure;

    // ---- 轮胎磨损（0-100 递增百分比，0=全新, 100=完全磨损）----
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] tyreWear;

    // ---- 刹车温度（摄氏度）----
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] brakeTemp;

    // ---- 赛事排名 ----
    public int position;                // 当前车手排名（1起计）

    // ---- 发动机温度 ----
    public float waterTemp;             // 冷却水温度（摄氏度）
    public float oilTemp;               // 机油温度（摄氏度）

    // ---- 涡轮增压 ----
    public float turboPressure;         // 涡轮增压压力（bar）

    // ============ 字段有效性掩码（v2.0 移至 288B 偏移处）============
    public ulong validFlags;

    // ---- 预留空间：结构体固定 512 字节，当前已用 288 字节，剩余 224 字节 ----
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 224)]
    public byte[] _reserved;
}
```

> **关键检查点**：`_reserved` 的 `SizeConst` 必须是 **224**（v1.x 是 376）。如果你看到 376，说明还在用旧结构体。

---

## 3. 完整新 `ValidFlags` C# 常量类（直接复制替换）

```csharp
public static class ValidFlags
{
    // ---- 第一批：基础驾驶（bit 0-14）----
    public const ulong Speed          = 1UL << 0;
    public const ulong Rpm            = 1UL << 1;
    public const ulong MaxRpm         = 1UL << 2;
    public const ulong Gear           = 1UL << 3;
    public const ulong Throttle       = 1UL << 4;
    public const ulong Brake          = 1UL << 5;
    public const ulong Steer          = 1UL << 6;
    public const ulong PitLimiter     = 1UL << 7;
    public const ulong TcActive       = 1UL << 8;
    public const ulong AbsActive      = 1UL << 9;
    public const ulong DrsAvailable   = 1UL << 10;
    public const ulong DrsActive      = 1UL << 11;
    public const ulong SlipRatio      = 1UL << 12;
    public const ulong SlipAngle      = 1UL << 13;
    public const ulong CombinedSlip   = 1UL << 14;

    // ---- 第二批：辅助系统（bit 15-27）----
    public const ulong ErsCharge      = 1UL << 15;
    public const ulong ErsDeploy      = 1UL << 16;
    public const ulong ErsActive      = 1UL << 17;
    public const ulong ErsRecovery    = 1UL << 18;
    public const ulong EngineRunning  = 1UL << 19;
    public const ulong Ignition       = 1UL << 20;
    public const ulong EnginePower    = 1UL << 21;
    public const ulong TcLevel        = 1UL << 22;
    public const ulong AbsLevel       = 1UL << 23;
    public const ulong TcCut          = 1UL << 24;
    public const ulong Fuel           = 1UL << 25;
    public const ulong FuelPct        = 1UL << 26;
    public const ulong RaceFlag       = 1UL << 27;

    // ---- 第三批：竞赛深度（bit 28-44，v2.0 新增）----
    public const ulong Clutch           = 1UL << 28;
    public const ulong CurrentLapNum    = 1UL << 29;
    public const ulong TotalLaps        = 1UL << 30;
    public const ulong CurrentLapTime   = 1UL << 31;
    public const ulong LastLap          = 1UL << 32;
    public const ulong BestLap          = 1UL << 33;
    public const ulong TyreTempInner    = 1UL << 34;
    public const ulong TyreTempMiddle   = 1UL << 35;
    public const ulong TyreTempOuter    = 1UL << 36;
    public const ulong TyreCoreTemp     = 1UL << 37;
    public const ulong TyrePressure     = 1UL << 38;
    public const ulong TyreWear         = 1UL << 39;
    public const ulong BrakeTemp        = 1UL << 40;
    public const ulong Position         = 1UL << 41;
    public const ulong WaterTemp        = 1UL << 42;
    public const ulong OilTemp          = 1UL << 43;
    public const ulong TurboPressure    = 1UL << 44;
}
```

---

## 4. 必查迁移点清单

升级后请逐项排查客户端代码：

### ① 替换结构体与常量（必做）
- [ ] 用 §2 的 `NormalizedData` 替换旧定义
- [ ] 用 §3 的 `ValidFlags` 替换旧常量类
- [ ] **验证**：`Marshal.SizeOf<NormalizedData>()` 应返回 **512**；`_reserved` 的 `SizeConst` 应为 **224**

### ② `tyreWear` 值域迁移（必做）
- [ ] 全局搜索 `tyreWear`，找到所有使用点
- [ ] **去掉客户端的 `* 100`**：v2.0 已经是 0-100 百分比，再做 `*100` 会变成 0-10000
- [ ] 显示逻辑改为直接 `$"轮胎磨损: {data.tyreWear[0]:F1}%"`

### ③ 判空逻辑迁移（必做）
- [ ] 全局搜索 `== -1`、`== -1.0`、`== -1.0f`，确认是否用于判断遥测字段无效
- [ ] **改用 `validFlags` 掩码**：
  ```csharp
  // ❌ 旧写法（v2.0 失效，永远不成立）
  if (data.fuelRemaining != -1.0f) { ... }

  // ✅ 新写法
  bool fuelValid = (data.validFlags & ValidFlags.Fuel) != 0;
  if (fuelValid) { ... }
  ```
- [ ] 不支持的字段现在数值为 **0**（不再是 -1），别再用 `== 0` 判无效——0 可能是合法值（如空挡 gear=0、未踩踏板 throttle=0）

### ④ 字段顺序假设排查（建议）
- [ ] 若客户端曾用 `unsafe` 指针偏移读取字段，**必须重新核对偏移**（`validFlags` 由 136B → 288B）
- [ ] 若客户端曾用反射/序列化按字段顺序遍历，确认顺序与 v2.0 一致
- [ ] 推荐改用**命名字段访问**（`data.speed`、`data.tyrePressure[i]`），不依赖物理偏移

### ⑤ 数组初始化排查（建议）
- [ ] C# 端 4 元素数组（`slipRatio` / `slipAngle` / `combinedSlip` / `tyreTemp*` / `tyrePressure` / `tyreWear` / `brakeTemp`）在结构体实例化时默认为 `null`，**读取前确认已被 SDK 填充或手动初始化**：
  ```csharp
  // 安全写法：防止 valid=false 时求值 data.tyrePressure[i] 触发 NRE
  float pressure = (data.validFlags & ValidFlags.TyrePressure) != 0 && data.tyrePressure != null
      ? data.tyrePressure[0] : 0f;
  ```

---

## 5. 新字段读法示例（可选）

v2.0 新增 17 个字段，读取方式统一为「**先用 `validFlags` 判支持，再读值**」。以下为典型示例：

```csharp
// 轮胎四轮温度（FL/FR/RL/RR）
if ((data.validFlags & ValidFlags.TyreCoreTemp) != 0 && data.tyreCoreTemp != null)
{
    Console.WriteLine($"FL 胎温: {data.tyreCoreTemp[0]:F1} °C");
    Console.WriteLine($"FR 胎温: {data.tyreCoreTemp[1]:F1} °C");
    Console.WriteLine($"RL 胎温: {data.tyreCoreTemp[2]:F1} °C");
    Console.WriteLine($"RR 胎温: {data.tyreCoreTemp[3]:F1} °C");
}

// 圈速
if ((data.validFlags & ValidFlags.CurrentLapTime) != 0)
    Console.WriteLine($"当前圈: {FormatLapTime(data.currentLapTime)}");
if ((data.validFlags & ValidFlags.LastLap) != 0)
    Console.WriteLine($"上一圈: {FormatLapTime(data.lastLapTime)}");
if ((data.validFlags & ValidFlags.BestLap) != 0)
    Console.WriteLine($"最佳圈: {FormatLapTime(data.bestLapTime)}");

// 排名与温度
if ((data.validFlags & ValidFlags.Position) != 0)
    Console.WriteLine($"排名: P{data.position}");
if ((data.validFlags & ValidFlags.WaterTemp) != 0)
    Console.WriteLine($"水温: {data.waterTemp:F0} °C");
if ((data.validFlags & ValidFlags.TurboPressure) != 0)
    Console.WriteLine($"涡轮: {data.turboPressure:F2} bar");

static string FormatLapTime(float seconds) =>
    TimeSpan.FromSeconds(seconds).ToString(@"m\:ss\.fff");
```

> **运行时支持的字段因游戏而异**：用 `GetSupportedFlags()` 一次性查询当前游戏支持哪些字段，或在每帧数据里读 `data.validFlags`。各游戏的完整支持矩阵见主文档 §「第三批参数支持矩阵」。

---

## 6. 特殊情况提醒

- **F1 系列 / RBR**：协议层只有胎面整体单一标量温度，SDK 把它**降级复制**到 `tyreTempInner/Middle/Outer` 三层（三点同值，非真实三层分布）。客户端做胎压/倾角调校分析时**请勿**将三层值当作真实分布。
- **AC Rally**：拉力赛制下圈时/排名/涡轮等字段协议层不投递，第三批仅支持 5/17（clutch/胎核温/胎压/刹车温/水温）。
- **WRC 8/9/10**：协议数据极有限，仅速度+转速+档位，不要期待任何第三批字段。
- **iRacing**：无 `tyreCoreTemp` / `tyreWear` / `brakeTemp` / `position`（协议层不投递）。

---

## 7. 迁移完成自检

完成上述改动后，用以下方式快速验证：

```csharp
// 1. 启动任一已安装的游戏（示例用 ACC）
TelemetryAPI.StartTelemetry(805550);

// 2. 结构体大小应为 512
Debug.Assert(Marshal.SizeOf<NormalizedData>() == 512);

// 3. 查询支持的第三批字段（ACC 示例）
ulong flags = TelemetryAPI.GetSupportedFlags();
Console.WriteLine($"支持水温: {(flags & ValidFlags.WaterTemp) != 0}");      // True
Console.WriteLine($"支持排名: {(flags & ValidFlags.Position) != 0}");        // True
Console.WriteLine($"支持总圈数: {(flags & ValidFlags.TotalLaps) != 0}");     // False（ACC 不支持）

// 4. 读取一帧数据
var data = new NormalizedData();
if (TelemetryAPI.GetTelemetryData(ref data))
{
    Console.WriteLine($"速度: {data.speed:F1} km/h");
    Console.WriteLine($"validFlags: 0x{data.validFlags:X}");
}

TelemetryAPI.StopTelemetry();
```

若速度数值正常、`validFlags` 非零，说明迁移成功；若速度为 0 或异常大数，说明结构体对齐有问题，回头检查 §2 的字段顺序与 `Pack = 1`。
