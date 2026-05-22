# HID 数据采集与 UI 实时更新逻辑

## 概述

本文档说明踏板设备 HID 数据的完整处理链路：从设备硬件上报 → HidService 采集解析 → PedalParameterControl 缓存与曲线变换 → UI 控件实时更新。

核心设计目标：**后台线程完成全部计算，UI 线程只负责控件更新，曲线变换不触碰 WPF 线程亲和对象**。

---

## 整体数据流

```
设备硬件
  │  USB HID 中断传输（~5ms 间隔）
  ▼
HidService (后台线程)
  │  ReadFile → byte[] → HidPedalData.Parse
  │  PedalDataReceived?.Invoke(device, data)
  ├──────────────────────────────────────────┐
  ▼                                          ▼
App.xaml.cs lambda                    PedalParameterControl.OnPedalDataReceived (后台线程)
  Debug.WriteLine 日志输出               │  Vid/Pid 过滤
                                          │  更新原始值缓存
                                          │  曲线变换（用 Point[] 缓存，无线程亲和）
                                          │  防抖 + DispatcherPriority.Render 入队
                                          ▼
                                      Dispatcher 回调 (UI 线程)
                                          │  读取最新缓存值
                                          │  跳过冗余更新（差值 ≤ 0.05%）
                                          │  UpdatePedalPositionDisplay → 控件
```

---

## 第一层：HID 数据采集（HidService）

**文件：** `Services/Usb/HidService.cs`

### 启动流程

`App.OnStartup()` → `InitializeUsbManager()` → `HidService.Start()`

`Start()` 内部开启后台任务 `DevicePollLoop`，每 2 秒扫描一次 Windows HID 设备：

```
SetupDiGetClassDevs(HID GUID)
  → SetupDiEnumDeviceInterfaces (枚举)
    → SetupDiGetDeviceInterfaceDetail (获取设备路径)
      → HidD_GetAttributes (获取 VID/PID)
        → DeviceRegistry 匹配目标设备
          → 创建 HidChannel → 开启 ReadLoop 后台线程
```

### 数据读取循环

每个已连接设备有独立的 `ReadLoop` 后台线程：

```
while (设备在线)
    channel.Read()                           // 阻塞读 HID 报告
    → ProcessData(deviceInfo, type, data)    // 根据 reportId 和 DeviceType 路由
      → HidPedalData.Parse(data)            // 解析 29 字节报文
      → PedalDataReceived?.Invoke(...)       // 触发事件（后台线程）
    Task.Delay(5ms)                          // 避免空转
```

### HID 报文格式（29 字节）

| 偏移 | 长度 | 字段 | 说明 |
|------|------|------|------|
| 0 | 1 | ReportId | 0x01 = 踏板数据 |
| 1-2 | 2 | X | ushort, 原始 ADC |
| 3-4 | 2 | Y | ushort |
| 5-6 | 2 | Gas | ushort → GasPercent = Gas/65535×100 |
| 7-8 | 2 | Brake | ushort → BrakePercent = Brake/65535×100 |
| 9-10 | 2 | Clutch | ushort → ClutchPercent = Clutch/65535×100 |
| 11-12 | 2 | Rz | ushort |
| 13-28 | 16 | User[8] | ushort×8, 用户自定义 |

### 事件订阅者

`PedalDataReceived` 是多播事件（`event Action<UsbDeviceInfo, HidPedalData>`）：

1. **App.xaml.cs lambda**：仅 Debug 日志 `[HID] 踏板数据 [...]`
2. **PedalParameterControl.OnPedalDataReceived**：实际业务处理

---

## 第二层：数据订阅与字段缓存（PedalParameterControl）

**文件：** `Views/DeviceParameters/PedalParameterControl.xaml.cs`

### 订阅生命周期

```
控件 Loaded → SubscribeHidData()   → PedalDataReceived += OnPedalDataReceived
控件 Unloaded → UnsubscribeHidData() → PedalDataReceived -= OnPedalDataReceived
```

订阅独立于串口连接状态——控件显示期间始终订阅。设备过滤在 `OnPedalDataReceived` 第一行通过 `_connectedPedalDevice` 的 Vid/Pid 匹配完成。

### 关键字段

```csharp
// ── 数据缓存（后台线程写，UI 线程读）──
private double _latestRawClutch;        // 最新原始离合位置 0-100%
private double _latestRawBrake;         // 最新原始刹车位置
private double _latestRawGas;           // 最新原始油门位置
private double _latestProcessedClutch;  // 最新处理后离合位置（经曲线映射）
private double _latestProcessedBrake;
private double _latestProcessedGas;

// ── 防抖标记 ──
private int _pendingUiUpdate;           // 0=空闲, 1=已入队待执行

// ── 去重比较 ──
private double _displayedRawClutch = -1;      // 上次显示值，初始 -1 保证首次必刷新
private double _displayedProcessedClutch = -1;

// ── 曲线缓存（值类型数组，无线程亲和性）──
private Point[] _clutchCurvePointsCache;      // 曲线控制点（6 个 Point）
private double[] _clutchCurveSlopesCache;     // 预计算 Fritsch-Carlson 单调三次样条斜率
```

### 曲线缓存构建

曲线只在用户手动操作时变化（切换曲线类型、拖拽控制点），此时在 UI 线程调用 `RebuildCurveCaches()`：

```
PointCollection (6 个控制点)
  → source.CopyTo(Point[], 0)          // 值类型拷贝，脱离 WPF 线程亲和
    → ComputeMonotonicSlopes(Point[])  // 预计算所有节点的切线斜率
      → _xxxCurvePointsCache / _xxxCurveSlopesCache
```

缓存重建调用点（共 6 处）：
- `UpdateClutchCurve()` / `UpdateBrakeCurve()` / `UpdateThrottleCurve()` — 切换曲线类型
- 三个拖拽回调 lambda — 拖拽控制点

---

## 第三层：每帧 HID 数据处理（OnPedalDataReceived）

**位置：** `PedalParameterControl.xaml.cs` → `OnPedalDataReceived`

每个 HID 报文到达时在**后台线程**依次执行：

### 步骤 1：设备过滤

```csharp
if (_connectedPedalDevice == null
    || device.Vid != _connectedPedalDevice.Vid
    || device.Pid != _connectedPedalDevice.Pid)
    return;
```

确保 HID 数据来自当前串口连接的踏板，防止多设备串扰。`_connectedPedalDevice` 由 `RefreshDeviceInfoAsync()` 从 `App.UsbManager.ConnectedDevices` 中匹配设置。

### 步骤 2：更新原始值缓存

```csharp
_latestRawClutch = data.ClutchPercent;   // 如 50.3
_latestRawBrake  = data.BrakePercent;    // 如 99.8
_latestRawGas    = data.GasPercent;      // 如 0.0
```

不经过任何节流——每个 HID 包到达都立即更新，确保缓存始终是设备最新状态。

### 步骤 3：曲线变换（在后台线程完成）

```csharp
_latestProcessedClutch = ApplyCurveTransform(
    _clutchCurvePointsCache,       // Point[] 值类型数组（线程安全）
    _clutchCurveSlopesCache,       // 预计算斜率
    data.ClutchPercent);
```

**为什么可以在这里用 `Point[]`？** `System.Windows.Point` 是值类型 struct，`Point[]` 数组没有 WPF 线程亲和性。而 `PointCollection` 继承自 `Freezable → DependencyObject → DispatcherObject`，跨线程访问会抛出 `InvalidOperationException`。

**曲线变换算法（Fritsch-Carlson 单调三次 Hermite 插值）：**

```
输入: positionPercent (0-100%)

1. 映射到画布坐标: canvasX = positionPercent / 100 × 345

2. 查找 canvasX 所在的控制点区间 [x_i, x_{i+1}]

3. 边界处理:
   - canvasX ≤ 首个控制点 X → 直接用首点 Y
   - canvasX ≥ 末点 X → 直接用末点 Y

4. 区间内 Hermite 插值:
   t = (canvasX - x_i) / (x_{i+1} - x_i)
   m0 = slope[i] × dx       // 起点切线
   m1 = slope[i+1] × dx     // 终点切线

   y = (2t³-3t²+1) × y_i          // 起点位置权重
     + (t³-2t²+t)  × m0           // 起点切线权重
     + (-2t³+3t²)  × y_{i+1}      // 终点位置权重
     + (t³-t²)     × m1           // 终点切线权重

5. 映射回百分比: result = (266 - y) / 266 × 100
```

**斜率预计算（Fritsch-Carlson 算法）：** 在缓存构建时调用 `ComputeMonotonicSlopes(Point[])`，确保插值曲线保持单调性（输入的递增关系在输出中不变），避免非物理的振荡。

### 步骤 4：投递 UI 更新（防抖）

```csharp
// Render 优先级 + Interlocked 防抖：同一渲染帧内只入队一次
if (Interlocked.Exchange(ref _pendingUiUpdate, 1) == 0)
{
    Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
    {
        // ... UI 更新逻辑
    });
}
```

**关键设计决策：**

| 问题 | 之前 | 现在 |
|------|------|------|
| 投递频率控制 | 30ms 时间节流 + `_processingHidData` 锁 | `_pendingUiUpdate` 防抖标记 |
| 丢失最后一帧 | 是（锁住期间到达的归零数据被丢弃） | 否（缓存始终更新，回调读到最新值） |
| 控制线程安全 | `Interlocked.Exchange` 双重锁 | 单标记 + 值类型字段读写 |
| 回调优先级 | `BeginInvoke` 默认优先级 | `DispatcherPriority.Render`（渲染帧前执行） |
| 曲线计算线程 | UI 线程（访问 PointCollection） | 后台线程（用 Point[] 缓存） |

`DispatcherPriority.Render` 的效果：回调在 WPF 布局和渲染之前执行，与屏幕刷新率自然同步（60Hz = ~16ms/帧）。如果设备上报频率高于帧率，同一帧内到达的多个 HID 包都更新 `_latest*` 缓存，但只有一个回调在帧前执行，读到的是最新的值。

### 步骤 5：UI 回调 —— 跳过冗余更新

```csharp
_pendingUiUpdate = 0;

// 读此刻最新值（非投递时快照）
var rawClutch = _latestRawClutch;
var pClutch = _latestProcessedClutch;
// ...

// 与上次已显示值比较，差值 ≤ 0.05% 则跳过
if (HasDisplayChanged(rawClutch, rawBrake, rawGas, pClutch, pBrake, pGas))
{
    _displayedRawClutch = rawClutch;        // 记录本次显示值
    _displayedProcessedClutch = pClutch;
    // ...
    UpdatePedalPositionDisplay(rawClutch, pClutch, rawBrake, pBrake, rawGas, pGas);
}
```

**为什么需要去重？** `UpdatePedalPositionDisplay` 设置 12 个 `ColumnDefinition.Width` 属性（每个触发一次 WPF 布局无效化）。如果设备踏板静止不动，每次回调值相同，跳过设置避免无意义的重排。

---

## 第四层：UI 控件更新（UpdatePedalPositionDisplay）

**位置：** `PedalParameterControl.xaml.cs` → `UpdatePedalPositionDisplay`

### 每个踏板轴的显示元素

| XAML 元素 | 显示内容 | 更新方式 |
|-----------|----------|----------|
| `ClutchProgressGreen` | 处理后进度绿色段 | `ColumnDefinition.Width = new GridLength(percent, Star)` |
| `ClutchProgressRed` | 处理后进度红色段 | `ColumnDefinition.Width = new GridLength(100-percent, Star)` |
| `ClutchProgressGreen2` | 原始值进度绿色段 | 同上 |
| `ClutchProgressRed2` | 原始值进度红色段 | 同上 |
| `ClutchCurrentPosition` | 处理后百分比文本 | `TextBlock.Text = "50%"` |

共 12 个 ColumnDefinition（3 轴 × 2 组 × 2 列）+ 3 个 TextBlock。

### 进度条原理

两个 `ColumnDefinition` 在同一 Grid 中，使用 `GridUnitType.Star` 比例分配宽度：

```
绿色 Width = processedClutch *    (如 50*)
红色 Width = (100-processedClutch)*  (如 50*)
→ 各占 50%，显示为半绿半红
```

---

## 线程模型总结

```
┌─────────────────────────────────────────────────────────┐
│  后台线程 (HidService.ReadLoop)                         │
│  ├─ channel.Read()           // 阻塞读 HID              │
│  ├─ HidPedalData.Parse()     // 解析报文                │
│  ├─ PedalDataReceived?.Invoke()                         │
│  │   ├─ App lambda           // Debug 日志              │
│  │   └─ OnPedalDataReceived  // ★ 曲线变换在此执行     │
│  │       ├─ 更新 _latestRaw*                            │
│  │       ├─ ApplyCurveTransform(Point[])  ← 值类型缓存  │
│  │       ├─ 更新 _latestProcessed*                      │
│  │       └─ Dispatcher.BeginInvoke(Render) ← 防抖入队   │
│  └─ Task.Delay(5ms)                                     │
├─────────────────────────────────────────────────────────┤
│  UI 线程 (WPF Dispatcher, Render 优先级)                 │
│  └─ BeginInvoke 回调                                    │
│      ├─ 读取 _latestRaw* / _latestProcessed*            │
│      ├─ HasDisplayChanged() 去重                        │
│      └─ UpdatePedalPositionDisplay()                    │
│          ├─ ColumnDefinition.Width = GridLength (×12)    │
│          └─ TextBlock.Text = "N%" (×3)                  │
└─────────────────────────────────────────────────────────┘
```

**关键原则：**
- 后台线程：只读写值类型字段（`double`、`Point[]`、`double[]`），不触碰任何 WPF 对象
- UI 线程：只读取后台线程写入的字段快照，再操作 WPF 控件
- 无锁设计：`double` 在 x64 上原子读写，`_pendingUiUpdate` 用 `Interlocked`，不阻塞后台线程
