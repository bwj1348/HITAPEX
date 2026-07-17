# HITAPEX 项目完整代码解析文档

> **版本**: 0.1.0 | **框架**: .NET 9.0 + WPF | **语言**: C# 13  
> **项目定位**: 直驱赛车模拟器硬件管理桌面应用（方向盘基座/面盘/踏板的 USB 通信、参数设置、固件更新、游戏遥测）

---

## 阅读指南

本文档按**从底层到上层**的顺序组织，建议按章节顺序阅读：

| 章节 | 内容 | 阅读目标 |
|------|------|----------|
| **第1章** | 项目骨架 | 理解入口点、应用生命周期、主窗口结构 |
| **第2章** | 数据模型层 | 理解所有数据结构定义 |
| **第3章** | 基础设施层 | 理解依赖注入、MVVM 基础、XAML 标记扩展 |
| **第4章** | USB 通信层 | 理解设备发现、串口通信、HID 数据采集 |
| **第5章** | 游戏与遥测层 | 理解遥测 SDK、数据包构建、游戏启动 |
| **第6章** | 网络数据层 | 理解 API 客户端、固件/海报/安装包接口 |
| **第7章** | 业务服务层 | 理解本地化、预设管理、Steam 检测、遥测配置部署 |
| **第8章** | ViewModel 层 | 理解 MVVM 导航、数据绑定 |
| **第9章** | 视图层 | 理解所有 UI 页面和控制逻辑 |

---

## 第1章 项目骨架

项目骨架由 `App.xaml`/`App.xaml.cs`、`AssemblyInfo.cs`、`MainWindow.xaml`/`MainWindow.xaml.cs`、`SplashWindow.xaml`/`SplashWindow.xaml.cs` 和项目文件 `.csproj` 组成。这些文件构成了应用的入口、生命周期管理和主框架。

### 1.1 [HITAPEX.csproj](HITAPEX.csproj) — 项目配置文件

```text
目标框架: net9.0-windows
输出类型: WinExe (Windows GUI)
启用: WPF, Nullable, ImplicitUsings
```

**NuGet 依赖**:
| 包名 | 用途 |
|------|------|
| `FluentWPF` 0.10.2 | Acrylic 毛玻璃 / Win11 风格控件 |
| `SharpVectors.Wpf` 1.8.4.2 | SVG 渲染（所有图标均以 SVG 嵌入） |
| `System.Drawing.Common` 9.0.0 | 系统托盘图标 |
| `System.IO.Ports` 10.0.8 | USB CDC 串口通信 |
| `System.Management` 10.0.8 | WMI 查询 USB 设备 |

### 1.2 [App.xaml](App.xaml) — 应用级资源定义

```text
App.xaml (1-40行)
├── 合并 FluentWPF 资源字典（全局 Acrylic 样式）
├── 自定义 ScrollBar 样式：背景 #0D1117，宽度 6px
├── 自定义 ProgressBar 样式：圆角 3px 的 #0D1B2A 背景
└── Orbitron 字体资源（品牌赛车风格字体）
```

**设计要点**：全局样式定义在 App.xaml 层级，所有页面共享。

### 1.3 [AssemblyInfo.cs](AssemblyInfo.cs) — 程序集信息

```csharp
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,        // 不使用主题特定资源字典
    ResourceDictionaryLocation.SourceAssembly  // 通用资源字典在当前程序集
)]
```

### 1.4 [App.xaml.cs](App.xaml.cs) — 应用入口与全局服务容器

这是整个应用的**核心启动文件**，充当全局服务容器（类似简易 DI 容器）。

**全局静态服务属性（供所有页面访问）**：
```csharp
App.UsbManager        → UsbSerialManager (USB 串口管理)
App.HidService        → HidService (HID 设备数据)
App.ProtocolService   → DeviceProtocolService (协议命令/响应)
App.FirmwareUpdater   → FirmwareUpdateService (固件更新)
App.FirmwareApi       → FirmwareApiService (固件 API)
App.ClientInstallerApi → ClientInstallerApiService (客户端更新 API)
App.PresetService     → PresetService (预设管理)
App.TelemetryService  → TelemetryService (遥测采集)
App.GameDataService   → GameDataService (游戏数据)
```

**启动流程 (`OnStartup`)**：
```
1. 初始化 LocalizationService（必须先于 UI 显示）
2. 在独立 STA 线程显示 SplashWindow（避免主线程初始化阻塞导致动画卡顿）
3. 注册全局异常处理：
   - TaskScheduler.UnobservedTaskException（fire-and-forget 任务异常）
   - DispatcherUnhandledException（UI 线程异常）
4. 调用 InitializeUsbManager() 初始化全部服务
5. 创建 MainWindow
6. 根据 StartMinimizedToTray 设置决定：
   - 最小化到托盘不显示主窗口
   - 关闭 Splash 并显示 MainWindow
```

**服务初始化流程 (`InitializeUsbManager`)**：
```
1. 创建 UsbSerialManager（无参构造，日志仅 Debug 输出）
2. 从 DeviceRegistry 注册目标 VID/PID 对
3. 订阅设备连接/断开/原始数据/错误事件
4. 创建 DeviceProtocolService → FirmwareUpdateService
5. 创建 FirmwareApiService、ClientInstallerApiService、PresetService
6. 创建 TelemetryService、GameDataService
7. 创建 HidService 并订阅踏板/基座/面盘 HID 数据事件
8. 启动 HidService.Start() 和 UsbManager.Start()
```

**安全退出 (`OnExit`)**: 按依赖倒序释放资源（TelemetryService → GameDataService → ClientInstallerApi → HidService → UsbManager）。

### 1.5 [SplashWindow.xaml.cs](SplashWindow.xaml.cs) — 启动闪屏

```text
功能：
├── 品牌名发光呼吸动画（DropShadowEffect 透明度 0.3↔0.8，1800ms 循环）
├── 品牌名文本透明度呼吸（0.75↔1.0）
├── 副标题透明度呼吸（0.35↔0.7）
└── 加载省略号循环（. → .. → ... → . 每 400ms）
```

所有动画使用 `CubicEase` 缓动函数，在 `Loaded` 事件中启动。窗口特征：无边框、透明背景、Topmost、居中、不显示在任务栏。

### 1.6 [MainWindow.xaml](MainWindow.xaml) — 主窗口结构

```
主窗口 (1500×950, 无边框, 透明背景, 不可缩放)
├── 标题栏 (26px 高, #161616 背景)
│   ├── 版本号文本 (Orbitron 字体)
│   ├── 最小化按钮 →
│   └── 关闭按钮 → (悬停变红 #E81123)
├── 左侧导航栏 (158px 宽, 半透明 #80161616)
│   ├── ItemsControl 绑定 NavigationItems
│   │   └── RadioButton 样式 NavButtonStyle
│   │       ├── 左侧红色竖条 (选中时)
│   │       ├── 渐变色背景 (红色渐变 选中时)
│   │       ├── SVG 图标
│   │       └── 文本标签
│   └── 用户信息区域 (右下角)
│       ├── 用户头像 + 绿色在线指示器
│       └── 用户名 "Alex_Racer"
├── 分隔线 (1px #383838)
├── 主内容区 ContentControl → 绑定 MainWindowViewModel.CurrentView
└── 全局模态弹窗 ModalDialog (ZIndex=1000)
```

**关键样式**:
- `NavButtonStyle`: 左侧 5px 红色指示条 + 红色渐变背景的导航按钮
- `TitleBarButtonStyle`: 标题栏按钮（悬停蓝色 #1A2535）
- `CloseButtonStyle`: 继承 TitleBarButtonStyle（悬停红色 #E81123）

### 1.7 [MainWindow.xaml.cs](MainWindow.xaml.cs) — 主窗口逻辑

**核心职责**：
1. **MVVM 导航**: 创建 `MainWindowViewModel`，通过 `RadioButton.Checked` 事件切换视图
2. **系统托盘**: 创建 `TrayIcon`，支持双击恢复、右键菜单（显示窗口 / 退出）、最小化到托盘、点击关闭最小化到托盘
3. **未保存检测**: 导航切换时检查 `DeviceUserControl` 子控件（踏板/面盘/基座）的 `HasUnsavedChanges`，弹出未保存确认对话框
4. **固件更新模式检测**:
   - 启动时检查已连接的更新模式设备，弹出强制更新对话框
   - 运行时监听 `UsbManager.DeviceConnected`，检测更新模式设备
   - 忽略固件更新流程中主动切换的更新模式设备（`FirmwareUpdater.IsUpdating`）
5. **预设列表弹窗管理**: 维护 `Dictionary<DeviceType, PresetListPopup>` 缓存

---

## 第2章 数据模型层 (Models)

### 2.1 USB 设备模型 (`Models/Usb/`)

这些模型定义了 USB 设备的数据结构，是整个系统的核心数据基础。

#### 2.1.1 `VidPidPair` — VID/PID 对 (record struct)
```csharp
VidPidPair(int Vid, int Pid)  // 值类型，用于设备匹配
ToString() → "VID_XXXX&PID_XXXX"
```

#### 2.1.2 `DeviceDescriptor` — 设备描述符
```csharp
ModelName: string, DeviceType: DeviceType
NormalMode: VidPidPair  // 正常通信模式 VID/PID
UpdateMode: VidPidPair  // 固件更新模式 VID/PID
Matches(vid, pid)       // 判断是否匹配（任一模式）
IsNormalMode(vid, pid)  // 判断是否为正常模式
IsUpdateMode(vid, pid)  // 判断是否为更新模式
```

#### 2.1.3 `DeviceRegistry` — 设备注册表（硬编码）
```csharp
当前注册设备:
├── A1踏板: 正常模式 VID=FF3F PID=0002, 更新模式 VID=FF3F PID=F002
└── A1面盘: 正常模式 VID=FF86 PID=FF0C, 更新模式 VID=FF86 PID=FF0D

方法:
├── GetAllVidPids()     // 获取所有需要监听的 VID/PID
├── FindByVidPid(v,p)   // 查找匹配设备
├── IsUpdateMode(v,p)   // 判断是否为更新模式
├── GetDeviceType(v,p)  // 获取设备类型
├── GetDisplayName(v,p) // 获取显示名称（更新模式会标注）
└── Register(desc)      // 运行时动态注册（通过 API 发现新型号）
```

#### 2.1.4 `DeviceType` 枚举
```csharp
Unknown=0, Base=1, Pedal=2, Shifter=3, Handbrake=4, Sequential=5, Wheel=6
```

#### 2.1.5 `DeviceConnectionState` 枚举
```csharp
Disconnected, Connecting, Connected, Disconnecting, Reconnecting, Error
```

#### 2.1.6 `DeviceEventType` 枚举（19 种事件类型）
```csharp
DeviceConnected, DeviceDisconnected, DeviceConnectFailed
DeviceReconnecting, DeviceReconnectFailed, DeviceRecovered
RawDataReceived, DataSendFailed, SerialError
DiscoveryStarted, DiscoveryCompleted
VidPidMatched, VidPidNotMatched
```

#### 2.1.7 `UsbDeviceInfo` — 运行时的 USB 设备信息
```csharp
DeviceId, PortName, Vid, Pid, Name, Description, SerialNumber
State: DeviceConnectionState, LastConnectedTime, ReconnectAttempts
TotalBytesReceived (Interlocked 原子操作)
DeviceKey → "{Vid:X4}:{Pid:X4}_{PortName}"
```

#### 2.1.8 `DeviceInfoResponse` — 设备信息查询响应
```csharp
DeviceType, UsbSpeed, NormalFirmwareVersion, BootFirmwareVersion
// 基座特有: 连接的踏板/面盘信息
WheelConnectionStatus, WheelNormalFwVersion, WheelBootFwVersion
PedalConnectionStatus, PedalNormalFwVersion, PedalBootFwVersion
PedalCount (0=两踏板, 1=三踏板含离合)
HasThreePedals → PedalCount == 1
```

#### 2.1.9 HID 数据模型

**`HidBaseData`** — 基座 HID 上报数据（42 字节，ReportId=0x11）
```
offset 0:  ReportId (0x11)
offset 1-2:  Steering (转向，归中=0x8000)
offset 3-4:  Y
offset 5-6:  LeftPaddle
offset 7-8:  Throttle
offset 9-10: Brake
offset 11-12: Clutch
offset 13-14: RightPaddle
offset 15-16: Slider
offset 17:  DirectionKeys1 (0-8, 0=释放)
offset 18-33: ButtonBits[16] (128 键位掩码)
offset 34:  DirectionKeys2
```

**`HidPedalData`** — 踏板 HID 上报数据（29 字节，ReportId=0x01）
```
offset 0:  ReportId (0x01)
offset 5-6:  Gas → GasPercent = Gas/65535*100
offset 7-8:  Brake → BrakePercent
offset 9-10: Clutch → ClutchPercent
```

**`HidWheelData`** — 面盘 HID 上报数据（22 字节，ReportId=0x01）
```
offset 0:  ReportId (0x01)
offset 5-6:  RightBottomPaddle
offset 7-8:  LeftBottomPaddle
offset 13:  Dpad (8 方向，0=释放)
offset 14-21: ButtonBits[8] (64 按键位图)
IsButtonPressed(buttonIndex) → 判断 1-based 按键是否按下
```

#### 2.1.10 设备参数响应模型

**`PedalParametersResponse`** — 踏板属性参数（5类 × 11字段 = 55 字节）:
```text
各轴公共字段（离合/刹车/油门）:
├── Direction (方向)
├── Point1Y/1X ~ Point4Y/4X (4 个曲线控制点)
└── DeadZoneFront, DeadZoneRear (前/后死区)
```

**`WheelButtonLightGlobalResponse`** — 按键灯全局设置:
```text
LedMode (0=单独颜色, 1=统一颜色), Brightness, ColorR/G/B
```

**`WheelButtonLightResponse`** — 按键灯单独效果:
```text
LedIndex (0-25), ColorR/G/B, TelemetryFunc (0=关闭..7=打滑)
FlashSpeed, TelemetryColorR/G/B
```

**`WheelRpmBaseModeResponse`** — 转速灯基础模式:
```text
BaseMode (0=恒亮,1=呼吸,2=彩色循环), BaseSpeed (0-5)
12 个 LED 颜色 (每个 3 字节 RGB)
```

**`WheelRpmIndicatorResponse`** — 转速灯指示属性:
```text
TriggerMode (0=百分比,1=RPM), 12 个触发值, 12 个 LED 颜色
```

**`WheelRpmModeResponse`** — 转速灯模式:
```text
Brightness, TelemetryOff (遥控模式), LightMode (序列/扩散/汇聚)
StrobeMode (0=跟色,1=自定义,2=关闭), StrobeSpeed (0-5)
StrobeColorR/G/B, StrobeTriggerValue
```

**`WheelSleepAndPaddleResponse`** — 睡眠与拨片:
```text
SleepTime, SleepEffect, SleepEffectSpeed
ClutchPaddleMode (0=独立轴,1=合成轴,2=按键), ClutchBitePoint
```

**`PresetNameResponse`** — 预设名称响应（多包拼合）:
```text
DeviceType, TotalLength, PacketIndex, NameData[]
DecodeNameFromPackets(packets) → UTF-8 解码拼接
```

#### 2.1.11 预设快照模型

**`PedalPresetSnapshot`** — 踏板参数快照（74 字段，JSON 持久化）:
```text
各轴（离合/刹车/油门）:
├── CurveType, Direction
├── Point1Y/1X ~ Point4Y/4X
└── DeadZoneFront/Rear
ParametersEqual(other) → 逐字段比较 + 差异日志输出
```

**`WheelPresetSnapshot`** — 面盘参数快照（30 字段，JSON 持久化）:
```text
全局: KeyColorEnabled, GlobalKeyColor, ShowKeyNumber, KeyBrightness
      RpmBrightness, SleepLightDuration, StandbyLightEffect, GlobalFlashSpeed
按键: 14 个按钮 × 6 属性 (Color/TelemetryEnabled/LightEffect/Func/TriggerColor/Speed)
转速灯: 12 灯 × 2 属性 (Color/Value) + Cap/Speed/CurveType/DisplayMode
        LightMode/StrobeMode/StrobeColor/BaseLightMode/BaseLightSpeed/TelemetryEnabled
拨片: ClutchMode, ClutchPointValue
ParametersEqual(other) → 逐字段+序列比较（跳过 RpmCurveType/ShowKeyNumber）
```

### 2.2 业务数据模型 (`Models/`)

#### 2.2.1 `DeviceParameters.cs` — 设备参数（UI 层使用）

```csharp
BaseParameters: ForceFeedback, DetailLevel, DampingLevel, TempWarning,
                TempThrottle, MaxRpm, ResponseCurve, WorkMode
                Clone() / Apply(other)

SteeringWheelParameters: RotationAngle, Sensitivity, DeadZone, Damping,
                         Vibration, RoadFeedback, ButtonMappings (1-12 默认映射)
                         Clone() / Apply(other)

PedalParameters: ThrottleSensitivity/DeadZone/Curve, BrakeSensitivity/
                 DeadZone/Pressure/AbsVibration/Curve, ClutchSensitivity/BitePoint
                 Clone() / Apply(other)

DeviceParametersSet: 聚合 Base + SteeringWheel + Pedal
                     Clone() / Apply(other) / ResetToDefaults()
```

#### 2.2.2 `GameItem.cs` — 游戏条目（实现 INotifyPropertyChanged）
```csharp
Id, Name, CoverImageUrl, BgImageUrl, SteamId, IsInstalled
NeedsTelemetryConfig, LaunchPath, LaunchMode (Steam/CustomPath)
Description, DescriptionEn, Version, LastPlayed, LastLaunchTime
IsPinned (支持属性变更通知)
```

#### 2.2.3 `GameListConfig.cs` — 硬编码游戏列表（31 款游戏）
```text
Assetto Corsa 系列 (4): AC/ACC/AC Rally/AC EVO — 启动后自动遥测
F1 系列 (4): 22/23/24/25 — 需开 UDP 遥测
Forza 系列 (4): FM2023/FH4/FH5/FH6
DiRT 系列 (2): DiRT4/DR2.0 — 需改配置
rFactor/LMU (2): rF2/LMU — 需加插件
PCars/AMS2 (3): PC2/PC3/AMS2
WRC 系列 (5): WRC8/9/10/Generations/EA WRC — 需 DLL 注入
其他 (3): iRacing/R3E/BeamNG
模拟驾驶 (2): ETS2/ATS — 需加 SDK
非Steam (2): RBR/LFS
```

#### 2.2.4 `UserGameData.cs` — 用户游戏数据（持久化到磁盘）
```csharp
IsPinned, LaunchPath, LastLaunchTime, LaunchMode
```

#### 2.2.5 API 相关模型
- **`FirmwareVersionInfo`**: 固件版本信息（支持 JSON 反序列化，VID/PID 十六进制解析）
- **`ClientInstallerInfo`**: 客户端安装包信息（支持版本号解析为 System.Version）
- **`BannerItem`**: 宣传海报（ImageUrl + LinkUrl）

---

## 第3章 基础设施层

### 3.1 MVVM 基础类型

#### 3.1.1 `ViewModelBase` [ViewModels/ViewModelBase.cs](ViewModels/ViewModelBase.cs)
```csharp
abstract class ViewModelBase : INotifyPropertyChanged
├── OnPropertyChanged([CallerMemberName])  → 触发属性变更通知
└── SetProperty<T>(ref field, value)       → 值相等返回 false，不等则更新+通知
```

#### 3.1.2 `RelayCommand` [ViewModels/RelayCommand.cs](ViewModels/RelayCommand.cs)
```csharp
class RelayCommand : ICommand
├── 构造函数: Action<object?> + 可选的 CanExecute 判断
├── 无参重载: Action + 可选的 Func<bool>
└── CanExecuteChanged → CommandManager.RequerySuggested
```

#### 3.1.3 `NavigationItem` [ViewModels/NavigationItem.cs](ViewModels/NavigationItem.cs)
```csharp
class NavigationItem : ViewModelBase
├── Name (导航名称 "Home"/"Device"/...)
├── IconPath (SVG 图标路径)
├── LocKey (本地化键 "Nav.Home"/...)
├── Label (绑定到 LocalizationService[LocKey]，语言切换时自动刷新)
├── IsSelected (是否选中，支持双向绑定)
└── RefreshLabel() → 语言切换时刷新 Label
```

### 3.2 XAML 标记扩展 (`Helpers/`)

#### 3.2.1 `LocExtension` — 本地化字符串绑定
```xml
用法: Text="{lex:Loc Window.Title}"
原理: 创建到 LocalizationService.Instance[key] 的单向绑定
```

#### 3.2.2 `FontExtension` — 动态字体绑定
```xml
用法: FontFamily="{lex:Font}"
原理: 绑定到 LocalizationService.CurrentFontFamily（中文=Microsoft YaHei, 英文=Segoe UI）
```

#### 3.2.3 `LocFontSizeExtension` — 本地化字号
```xml
用法: FontSize="{lex:LocFontSize Home.ForceFeedbackFontSize}"
原理: JSON 中配置字号，语言切换时动态更新，含 StringToDoubleConverter
```

#### 3.2.4 `LocThicknessExtension` — 本地化边距
```xml
用法: Margin="{lex:LocThickness WheelParam.ClutchModeSpacing}"
原理: JSON 中配置 "left,top,right,bottom" 格式
```

#### 3.2.5 `NavSpacingExtension` — 导航栏图标间距
```xml
用法: Margin="{lex:NavSpacing}"
原理: 绑定到 LocalizationService.NavSpacing（从 JSON Nav.IconTextSpacing 读取）
```

### 3.3 系统托盘 (`Helpers/TrayIcon.cs`)

```csharp
class TrayIcon : IDisposable
├── Win32 Shell_NotifyIcon / CreatePopupMenu / TrackPopupMenu
├── 图标: 优先 EXE 提取 → Assets/AppIcon.ico → 系统默认
├── 功能: 添加/删除图标、气泡提示、右键菜单（显示窗口/退出）
├── 消息: WM_TRAYICON → 单击/双击恢复窗口, 右键弹出菜单
└── WM_COMMAND → CMD_SHOW / CMD_EXIT
```

### 3.4 自定义控件 (`Controls/`)

#### 3.4.1 `ModalDialog` — 全局模态弹窗
```csharp
依赖属性: Title, DialogContent, ShowIcon, ShowCloseButton
├── 自定义 ScrollBar 样式（红色 #C60E0E 滑块）
├── 斜切角边框（Path 绘制多边形）
├── AddButton(text, handler, isPrimary) → 动态添加按钮
│   ├── isPrimary=true → 红色渐变填充
│   └── isPrimary=false → 白色 0.2 透明度填充
├── ClearButtons() → 清除所有按钮
├── Show() / Hide() → 显示/隐藏并重置状态
└── CloseButton_Click → 点击 X 关闭
```

#### 3.4.2 `SkipInkTextBlock` — 自定义文本控件
```csharp
class SkipInkTextBlock : FrameworkElement
├── 完全绕过 WPF TextBlock 的 Ink 叠加层
├── 依赖属性: Text, Foreground, UnderlineBrush, Background,
│            FontSize, FontWeight, FontFamily, FontStyle, TextTrimming
├── 内部缓存: FormattedText, TextGeometry, UnderlineGeometry
├── OnRender → DrawGeometry（文本 + 下划线几何体）
└── 下划线: 使用 Geometry.Combine(下划线矩形, 文字扩展路径, Exclude) 生成
```

---

## 第4章 USB 通信层 (`Services/Usb/`)

这是整个应用的**硬件通信核心**，负责发现 USB 设备、管理串口连接、HID 数据采集和固件更新。

### 4.1 [IUsbSerialManager.cs](Services/Usb/IUsbSerialManager.cs) — 接口定义
```csharp
interface IUsbSerialManager
├── ConnectedDevices: IReadOnlyList<UsbDeviceInfo>
├── 事件: DeviceConnected/Disconnected/RawDataReceived/DeviceError
├── RegisterTargetDevices(vidPids) / IsRunning
└── SendToDevice(deviceKey, data) → bool
```

### 4.2 [UsbDeviceDiscovery.cs](Services/Usb/UsbDeviceDiscovery.cs) — 设备发现服务

使用 WMI (`ManagementEventWatcher`) 和 `Win32_PnPEntity` 发现 USB 设备。

**核心流程**:
```
1. 初始枚举: WMI 查询 "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Ports'"
2. 解析设备路径，提取 VID/PID（正则 "VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})"）
3. 建立 PortName 映射（通过 ParentId 关联 COM 端口与 USB 设备）
4. 持续监听: WMI 事件 __InstanceCreationEvent / __InstanceDeletionEvent
5. 注册 Windows 通知: RegisterDeviceNotification (DBT_DEVTYP_DEVICEINTERFACE)
6. 通过 WndProc 钩子接收 WM_DEVICECHANGE 消息
```

### 4.3 [UsbSerialManager.cs](Services/Usb/UsbSerialManager.cs) — USB 串口管理器

**职责**: 管理串口设备的连接/断开生命周期，提供收发接口。

```
核心流程:
├── Start()
│   ├── 初始发现 (UsbDeviceDiscovery.InitialDiscovery)
│   ├── 对匹配的 VID/PID 设备建立 DeviceSerialChannel
│   └── 订阅 DeviceDiscovery.DeviceArrived/Removed 事件
├── DeviceArrived → GetOrCreateDevice → 添加 SerialChannel → 发送设备信息查询
├── DeviceRemoved → 触发断开事件 → 清理 Channel
├── SendToDevice(deviceKey, data) → 查找 Channel → Write(data)
├── 重连机制: 断开后指数退避重连（1s→2s→4s→8s→16s），最多 5 次
└── 异常自动重连: 串口错误时自动触发重连流程
```

**DeviceSerialChannel** [DeviceSerialChannel.cs](Services/Usb/DeviceSerialChannel.cs):
```csharp
内部类 / 独立类
├── SerialPort 配置: 115200-8-N-1（根据固件协议）
├── DataReceived 事件 → 协议分包（帧头 0x61，64 字节固定帧长）
├── 帧同步: 滑动窗口查找 0x61 → 验证帧长 → CheckSum 验证
├── Write(data) → 串口同步写入
└── 断线检测: SerialPort.ErrorReceived / PinChanged
```

### 4.4 [HidNative.cs](Services/Usb/HidNative.cs) — HID API P/Invoke 封装

```csharp
Windows HID API:
├── HidD_GetHidGuid()          → 获取 HID 设备 GUID
├── SetupDiGetClassDevs()      → 枚举 HID 设备
├── SetupDiEnumDeviceInterfaces() → 获取设备接口
├── SetupDiGetDeviceInterfaceDetail() → 获取设备路径
├── CreateFile() + ReadFile()  → 打开/读取 HID 设备（异步重叠 I/O）
├── HidD_GetAttributes()       → 获取 VID/PID/Version
├── HidD_GetPreparsedData()    → 获取预解析数据
├── HidP_GetCaps()             → 获取设备能力（报告长度）
└── HidD_GetSerialNumberString() → 获取序列号
```

### 4.5 [HidService.cs](Services/Usb/HidService.cs) — HID 数据采集服务

**职责**: 高频率（最大频率）读取基座、面盘、踏板的 HID 输入报告。

```
初始化:
├── 通过 HID API 枚举所有 HID 设备
├── 匹配 DeviceRegistry 中注册的 VID/PID
├── 根据 DeviceType 分类为 baseDevices / wheelDevices / pedalDevices
└── 为每个设备创建 HID 设备句柄

Start() → 启动后台读取线程:
├── 每个设备独立线程（IsBackground）
├── 循环 ReadFile（异步重叠 I/O）
├── 解析: HidBaseData.Parse() / HidWheelData.Parse() / HidPedalData.Parse()
├── 触发事件:
│   ├── BaseDataReceived(device, HidBaseData)
│   ├── WheelDataReceived(device, HidWheelData)
│   └── PedalDataReceived(device, HidPedalData)
└── 延迟: 1ms（~1000Hz 最大读取频率）

Stop() → 取消所有读取令牌 → 关闭设备句柄
```

### 4.6 [DeviceProtocolService.cs](Services/Usb/DeviceProtocolService.cs) — 协议命令服务

**职责**: 打包/解析 USB 通信协议命令和响应（帧格式：1B 帧头 + 2B 命令 + 4B 时间戳 + 57B 数据/参数）。

**关键命令**:
```
Set 命令:
├── 0x2101 Set → 踏板参数 (离合/刹车/油门 × 11 字段)
├── 0x2102 Set → 面盘按键灯全局 (LED 模式 + 亮度 + 统一颜色)
├── 0x2103 Set → 面盘转速灯基础模式
├── 0x2104 Set → 面盘 RPM 指示器 (12 LED 触发值 + 颜色)
├── 0x2105 Set → 面盘转速灯模式 (亮度/遥控/模式/爆闪)
├── 0x2106 Set → 面盘按键灯单独效果 (按键索引 + 颜色 + 遥测功能)
├── 0x2109 Set → 基座参数 (力反馈/细节/阻尼/温控/RPM/响应曲线/工作模式)
├── 0x2110 Get → 踏板属性查询
├── 0x2200 → 面盘睡眠 + 拨片设置
├── 0x2400 → 转向角度校准 + 死区
└── 0x2A00 → 固件跳转更新模式

Get 响应解析:
└── 对应每个 Set 命令的响应 + 设备信息查询
```

### 4.7 [FirmwareUpdateService.cs](Services/Usb/FirmwareUpdateService.cs) — 固件更新服务

**职责**: 管理固件更新流程。

```
流程:
├── UpdateFirmwareAsync(device, firmwareData, progress)
│   ├── 1. 发送 0x2A00 命令 → 设备进入更新模式
│   ├── 2. 等待设备切换 VID/PID（更新模式）
│   ├── 3. 重新连接更新模式设备
│   ├── 4. 分块发送固件数据（每块 256 字节 + 校验）
│   ├── 5. 等待设备确认每块数据
│   ├── 6. 完成 → 发送启动命令 → 设备切换回正常模式
│   └── 7. 等待设备重新连接（正常模式）
└── IsUpdating 属性 → 防止外部事件干扰更新流程
```

### 4.8 [DeviceLogger.cs](Services/Usb/DeviceLogger.cs) — USB 通信日志

```csharp
class DeviceLogger
├── 构造函数: 无参
├── Log(eventType, deviceKey, message, detail, ex)
│   └── 仅在 DEBUG 模式下通过 Debug.WriteLine 输出格式化日志
│       格式: [时间戳] [事件类型] [设备标识] 消息 | 详情 | Exception
└── SetEnabled(bool) → 开关日志输出
```

**设计要点**: 日志仅作调试用途，不写入磁盘、不维护内存队列、不触发外部事件。Release 模式下 `Debug.WriteLine` 被编译器自动移除，零开销。

---

## 第5章 游戏与遥测层

### 5.1 [TelemetryAPI.cs](Services/TelemetryAPI.cs) — TelemetrySDK.dll 封装

**职责**: P/Invoke 封装 TelemetrySDK.dll（C++ 原生 DLL）的 5 个导出函数。

**导出函数**:
```csharp
StartTelemetry(gameId) → bool    // 初始化并启动遥测采集
GetTelemetryData(ref data) → bool // 获取归一化数据帧
StopTelemetry() → void           // 停止并释放资源
GetSDKVersion() → int            // 获取 SDK 版本号
GetSupportedFlags() → ulong      // 获取当前游戏支持的字段掩码
```

**`NormalizedData` 结构体**（512 字节，Pack=1）:
```text
基础驾驶: speed, rpm, maxRpm, gear, throttle, brake, steer
状态标志: isPitLimiterActive, isTcActive, isAbsActive, isDrsAvailable, isDrsActive
轮胎滑移: slipRatio[4], slipAngle[4], combinedSlip[4]  (FL/FR/RL/RR)
ERS/混动: ersCharge, ersDeployMode, isErsActive, ersRecoveryLevel
发动机:   isEngineRunning, isIgnitionOn, enginePowerMode
辅助:     tcLevel, absLevel, tcCutLevel
燃油:     fuelRemaining, fuelRemainingPct
旗帜:     raceFlag
离合:     clutch
圈速:     currentLap, totalLaps, currentLapTime, lastLapTime, bestLapTime
胎温:     tyreTempInner/Middle/Outer[4], tyreCoreTemp[4]
胎压:     tyrePressure[4]
磨损:     tyreWear[4]
刹车温:   brakeTemp[4]
排名:     position
温度:     waterTemp, oilTemp
涡轮:     turboPressure
有效性:   validFlags (45 位掩码)
预留:     _reserved[224]
```

**`ValidFlags` 常量类**（45 个位掩码）: 按批次分组（基础驾驶 bit 0-14, 辅助系统 bit 15-27, 竞赛深度 bit 28-44）。

**`GameId` 枚举**: 31 款游戏，Steam 游戏使用 App ID，非 Steam 使用自定义 ID (RBR=22, LFS=25)。

### 5.2 [TelemetryPacketBuilder.cs](Services/TelemetryPacketBuilder.cs) — 遥测数据包构建

**职责**: 将 NormalizedData 转换为 USB 协议的 5 个 64 字节遥测数据包。

```
帧格式: [0x61][PacketType uint16 LE][Timestamp uint32 LE][Data 57B...]

包1 (0x6101) — 车辆信息 #1 (基础驾驶参数):
├── speed(float), maxRpm(uint16), rpm(uint16), gear(byte)
├── throttle(float), brake(float), clutch(float), steer(float)
├── 状态标志: 维修区限速/TC/ABS/DRS可用/DRS激活
├── 辅助: TC档位/ABS档位/TC Cut档位
├── 旗语, ERS 电量/部署/工作/回收
├── 发动机: 启动/点火/动力档位, 剩余油量/百分比

包2 (0x6102) — 车辆信息 #2 (刹车温度+胎面温度):
├── 四轮刹车温度 (float×4)
├── 四轮胎面内侧温度 (float×4)
└── 四轮胎面中间温度 (float×4)

包3 (0x6103) — 车辆信息 #3 (胎面外侧+胎核+胎压):
├── 四轮胎面外侧温度 (float×4)
├── 四轮胎核心温度 (float×4)
└── 四轮胎压力 (float×4)

包4 (0x6104) — 车辆信息 #4 (磨损+水温+油温+涡轮):
├── 四轮胎磨损 (float×4, 0-100%)
├── 冷却水温 (float), 机油温度 (float)
└── 涡轮增压压力 (float, bar)

包5 (0x6105) — 比赛/车速信息:
├── 总圈数(uint16), 当前圈数(uint16)
├── 排名(byte), 当前圈时(float), 上一圈时(float), 最佳圈时(float)
```

**档位转换**: `GearFromNormalized()` — -1→0xFF(倒挡), 0→0x00(N), 1-100→直接映射。

### 5.3 [TelemetryService.cs](Services/TelemetryService.cs) — 遥测数据采集服务

**职责**: 管理遥测 SDK 生命周期，60Hz 循环采集并广播到所有已连接的 USB 设备。

```
Start(gameId):
├── 如果已在运行同一游戏 → 直接返回
├── 停止现有采集 → StartTelemetry(gameId) → 记录 _telemetryStartTick
├── 初始化自适应 RPM 追踪 (LFS/RBR/BeamNG 需要)
└── 启动后台循环线程 TelemetryLoop

后台循环 (LoopProc) — 60Hz:
├── GetTelemetryData(ref data) → 有数据才处理
│   ├── ApplyAdaptiveMaxRpm(ref data) → 自适应最大转速追踪
│   └── ProcessFrame(data)
│       ├── BuildAllPackets(data, timestampMs) → 5 个 64 字节包
│       └── DispatchPackets(packets) → 广播到所有正常模式设备
├── 每 ~5 秒检查目标游戏进程是否存活
│   └── 进程退出 → 自动停止遥测
└── 睡眠 LoopingInterval - elapsed (保持 ~16ms 帧间隔)
```

**自适应最大转速追踪**: LFS/RBR/BeamNG 不提供 maxRpm（始终为 0），服务追踪 rpm 峰值作为 maxRpm；当 rpm 连续 5 秒为 0 时重置为默认值 6000。

**下发策略**: 只向正常模式设备广播（跳过更新模式设备）。

### 5.4 [GameLauncher.cs](Services/GameLauncher.cs) — 游戏启动器

```
Launch(game, mode):
├── CustomPath 模式:
│   ├── 验证 LaunchPath 存在
│   └── Process.Start(LaunchPath) (UseShellExecute=true)
├── Steam 模式:
│   ├── 验证 SteamId 是纯数字
│   └── Process.Start("steam://run/{SteamId}")
└── 启动成功后:
    ├── game.LastLaunchTime = DateTime.Now
    └── Task.Run(async → delay 5000ms → TelemetryService.Start(steamAppId))
```

**设计要点**: 延迟 5 秒启动遥测，给游戏加载时间。

### 5.5 [TelemetryConfigService.cs](Services/TelemetryConfigService.cs) — 遥测配置部署

**职责**: 根据游戏 ID 执行对应的遥测配置文件/插件部署操作。

**各游戏配置策略**:

| 游戏 | 策略 | 操作 |
|------|------|------|
| **LFS** | 文件复制 | 复制 `cfg.txt` 到游戏根目录 |
| **rFactor 2** | DLL 部署 | 复制 `rFactor2SharedMemoryMapPlugin64.dll` 到 `Bin64\Plugins\` |
| **LMU** | DLL + JSON | 同上 + 更新 `CustomPluginVariables.JSON` |
| **ETS2/ATS** | DLL 部署 | 复制 `cwxyAETS2Telemetry.dll` 到 `bin\win_x64\plugins\` |
| **F1 22/23/24/25** | XML 修改 | 在 `hardware_settings_config.xml` 中启用 UDP `<udp enabled="true">` |
| **WRC Generations** | CFG 修改 | 在 `UserSettings.cfg` 中设置 UDP 遥测参数 |
| **WRC 8/9/10** | DLL 注入 | 备份 `PhysXCooking64_s.dll` → 替换为注入 DLL |
| **EA WRC** | JSON + 目录 | 部署 `config.json` + `udp/` 目录 + 注入 `wrc_cwyx` 数据包条目 |
| **DiRT 4/DR2.0** | 文件复制 | 复制 `hardware_settings_config.xml` 到 My Games 目录 |
| **Forza 系列** | 无需配置 | NeedsTelemetryConfig = false（游戏内置 UDP 遥测） |
| **AC 系列** | 无需配置 | NeedsTelemetryConfig = false（共享内存自动采集） |

### 5.6 [SteamInstallService.cs](Services/SteamInstallService.cs) — Steam 安装检测

```
CheckInstalled(steamIds):
├── GetSteamLibraryPaths()
│   ├── 注册表: HKCU\Software\Valve\Steam → SteamPath
│   └── 解析 libraryfolders.vdf → 获取所有库文件夹
├── 对每个 steamId:
│   ├── 在各库文件夹中查找 steamapps\appmanifest_{id}.acf
│   ├── 解析 "installdir" → 拼接安装路径
│   └── 解析 "LastPlayed" → Unix 时间戳转 DateTime
└── 返回 Dictionary<string, SteamInstallInfo>
```

---

## 第6章 网络数据层

### 6.1 [ApiClient.cs](Services/Data/Api/ApiClient.cs) — HTTP API 客户端

```csharp
class ApiClient : IDisposable
├── 构造函数: baseUrl + Bearer Token 认证
├── GetAsync<T>(endpoint)
│   ├── 自动重试 (最多 3 次, 指数退避: 500ms → 1000ms → 2000ms)
│   ├── 4xx → 立即返回失败 (不重试)
│   └── 5xx / 网络错误 → 重试
└── 返回 ApiResult<T> (IsSuccess/Data/ErrorMessage/IsClientError)
```

### 6.2 API 服务

**`BannerApiService`** — 海报接口:
```csharp
GetBannersAsync() → GET /api/banners?populate=*
→ 取前 3 条 → 拼接完整图片 URL (MediaBaseUrl + Image.Url)
```

**`FirmwareApiService`** — 固件版本接口:
```csharp
GetFirmwareVersionsAsync() → GET /api/firmware-versions?populate=*&locale={zh-Hans|en}
DownloadFirmwareAsync(fileUrl, progress) → HTTP GET 流式下载 + 进度回调
FindFirmwareForDevice(firmwares, vid, pid) → 按 VID/PID 匹配
```

**`ClientInstallerApiService`** — 客户端安装包接口:
```csharp
GetLatestInstallerAsync() → GET /api/client-installers?populate=*&sort=publishedAt:desc&pagination[pageSize]=1
DownloadInstallerAsync(fileUrl, progress) → HTTP GET 流式下载 + 进度回调 + 保存到临时目录
```

### 6.3 [LocalGameCacheService.cs](Services/Data/Cache/LocalGameCacheService.cs) — 本地缓存

```csharp
保存位置: %LocalAppData%\HITAPEX\user_game_data.json
格式: Dictionary<int, UserGameData> (GameId → 用户数据)
Save(data) → JsonSerializer.Serialize → File.WriteAllText
Load()     → File.ReadAllText → JsonSerializer.Deserialize
```

### 6.4 [GameDataService.cs](Services/Data/GameDataService.cs) — 游戏数据聚合

```csharp
class GameDataService : IDisposable
├── GetGamesAsync(forceRefresh)
│   ├── 从 GameListConfig.GetGames() 获取硬编码游戏元数据
│   ├── 从 LocalGameCacheService.Load() 获取用户数据
│   ├── ApplyUserData(games, userData): 合并 IsPinned/LaunchPath/LaunchMode/LastLaunchTime
│   └── 缓存 _cachedGames
├── EnrichWithInstallStatus(games) → 通过 SteamInstallService 检测安装
├── SaveUserData(game) → 持久化单个游戏的用户数据
└── StateChanged 事件 (Loading/Loaded/Error) → GameUserControl 订阅
```

---

## 第7章 业务服务层

### 7.1 [LocalizationService.cs](Services/LocalizationService.cs) — 本地化服务（单例）

```
核心功能:
├── Initialize(culture) → 加载 JSON 文件 (Resources/Locales/{culture}.json)
├── SetLanguage(culture)
│   ├── 加载新语言 JSON → 更新 CurrentFontFamily/CurrentLanguage/NavSpacing
│   ├── 持久化到 Properties.Settings.Default.Language
│   └── OnPropertyChanged(null) → 通知所有绑定刷新（包括索引器）
├── 索引器 this[key] → 取值，key 不存在时返回 key 本身（fallback）
├── Format(key, args) → string.Format 包装
└── CurrentFontFamily → 中文=Microsoft YaHei, 英文=Segoe UI
```

**JSON 结构**: 约 358 个键值对，覆盖所有 UI 文本、遥测标签、固件提示、设置选项。

### 7.2 [PresetService.cs](Services/PresetService.cs) — 预设管理服务

```
文件位置:
├── 官方预设: {baseDir}/Assets/Presets/official_presets.json (安装目录，20 个预设)
└── 个人预设: %LocalAppData%/HITAPEX/Presets/personal.json (用户目录)

操作:
├── LoadOfficialPresets(deviceType?) → 加载官方预设（可按设备类型过滤）
├── LoadPersonalPresets(deviceType?) → 加载个人预设
├── SavePersonalPresets(presets, deviceType) → 合并其他类型预设后写入（线程安全 @SemaphoreSlim）
├── ExportPreset(preset, filePath) → 导出单个预设 JSON
└── ImportPreset(filePath) → 从文件导入预设
```

---

## 第8章 ViewModel 层

### 8.1 [MainWindowViewModel.cs](ViewModels/MainWindowViewModel.cs) — 主窗口 VM

```
属性:
├── NavigationItems: ObservableCollection<NavigationItem>
│   └── 5 个导航项: Home / Device / Game / Help / Settings
├── SelectedNavigationItem → 设置时更新 CurrentView
├── CurrentView → 当前显示的 UserControl
└── Title → 窗口标题

视图缓存:
├── Dictionary<string, UserControl> _viewCache
├── 首次导航创建 → 缓存 → 后续复用（保持设备事件订阅有效）
└── 语言切换: 监听 LocalizationService.PropertyChanged → RefreshLabel()
```

---

## 第9章 视图层

### 9.1 [HomeUserControl](Views/HomeUserControl.xaml.cs) — 首页仪表盘

```
三大区域:
├── 轮播图 (3 张 Banner，5 秒自动轮播)
│   ├── 从 API 加载 Banner 图片
│   ├── 滑动动画（TranslateTransform 过渡）
│   └── 指示器圆点
├── 设备预览 (4 张卡片)
│   ├── 基座: 力反馈弧形仪表盘（270° arc，动态线段）+ 温度块（15 块渐变）
│   ├── 面盘: 34 段方向指示 + 角度文字
│   ├── 踏板: 离合/刹车/油门 3 条垂直填充条（渐变色）
│   └── 换挡器: 未连接占位
└── 快速启动游戏列表
    ├── Horizontal ItemsControl + 自定义滚动条
    ├── FLIP 动画: 悬停时右侧卡片右移 8.12px
    ├── 置顶功能
    └── 启动按钮
```

**模拟数据**: 初始启动时使用随机生成的模拟数据，真实设备连接后由 HID 事件驱动更新。

### 9.2 [DeviceUserControl](Views/DeviceUserControl.xaml.cs) — 设备页面

```
三子页切换:
├── 基座参数 → BaseParameterControl
├── 面盘参数 → SteeringWheelParameterControl
└── 踏板参数 → PedalParameterControl

导航控制:
├── RadioButton 样式的3个竖排导航按钮
├── 键盘快捷键: 1/2/3 或 Up/Down
├── 未保存检测: 切换时检查 HasUnsavedChanges → 弹窗确认
└── 切换动画: FadeOut(0.15s) → 替换内容 → FadeIn(0.3s)
```

### 9.3 [BaseParameterControl](Views/DeviceParameters/BaseParameterControl.xaml.cs) — 基座参数

```
UI 控件:
├── 力反馈强度 (滑块 0-100)
├── 细节水平 (滑块 0-100)
├── 阻尼水平 (滑块 0-100)
├── 温度警告 (°C, 滑块)
├── 温度节流 (°C, 滑块)
├── 最大转速 (RPM, 滑块 0-8000)
├── 响应曲线 (下拉: 0=线性, 1=自定义)
├── 工作模式 (下拉)
├── 连接状态文本
└── 固件版本显示

数据流:
├── USB 连接 → DeviceProtocolService.Get 查询参数 → 更新滑块
├── 滑块变化 → 标记 HasUnsavedChanges → 确认后 → DeviceProtocolService.Set 下发
├── HID 数据: 订阅 HidService.BaseDataReceived → 更新转向显示
└── 预设操作: 加载/保存/另存/导出/导入/撤销
```

### 9.4 [SteeringWheelParameterControl](Views/DeviceParameters/SteeringWheelParameterControl.xaml.cs) — 面盘参数

这是最复杂的参数控件（2571 行）。

```
两大区域:
├── 左侧: 方向盘预览图 (19 个圆形按键，实时按压效果)
│   ├── 按键按下: 订阅 HidService.WheelDataReceived → IsButtonPressed → 红色发光
│   └── 按键点击: 弹出 ButtonSettingsPopup
├── 右侧: 参数面板
│   ├── 按键灯设置
│   │   ├── 全局颜色模式开关 (KeyColorEnabled)
│   │   ├── 全局按键颜色选择 (8 色)
│   │   ├── 按键亮度 / RPM 亮度 滑块
│   │   ├── 睡眠灯光时间
│   │   └── 待机灯效 + 闪烁速度
│   ├── 转速灯设置 → 弹出 RpmSettingsPopup
│   │   ├── 12 LED 灯泡 (颜色 × 触发值)
│   │   ├── 爆闪灯 (颜色/模式/速度/触发值)
│   │   ├── 基础灯光模式 (恒亮/呼吸/彩色循环)
│   │   ├── 曲线类型, 显示模式(百分比/RPM)
│   │   └── 遥测模式开关
│   └── 拨片设置
│       ├── 离合拨片模式: 合成轴/独立轴/按键
│       ├── 离合咬合点拖拽
│       └── 换挡拨片校准
```

**数据发送**: 使用 `WheelSendMask` 标志位追踪哪些参数已修改，只发送修改的字段。

### 9.5 [PedalParameterControl](Views/DeviceParameters/PedalParameterControl.xaml.cs) — 踏板参数

第二复杂的控件（2485 行）。

```
三轴设置:
├── 离合轴: 曲线类型 + 死区 + 实时 HID 数据
├── 刹车轴: 曲线类型 + 死区 + ABS 振动 + 实时 HID 数据
└── 油门轴: 曲线类型 + 死区 + 实时 HID 数据

曲线类型 (每个轴 5 种):
└── 自定义模式下: 4 个可拖拽控制点 + Fritsch-Carlson 单调三次样条插值

实时数据:
├── 订阅 HidService.PedalDataReceived → 离合/刹车/油门百分比
├── 曲线转换: 将原始值通过曲线映射为输出值
└── 标定对话框: CalibrationDialog

参数下发:
├── 修改 → 标记 HasUnsavedChanges
├── 保存 → DeviceProtocolService.SendPedalParameters → 较验回读
└── 较验失败 → 自动重试（最多 3 次，每次延迟递增）
```

### 9.6 [GameUserControl](Views/GameUserControl.xaml.cs) — 游戏库页面

```
左侧: 游戏详情
├── 游戏背景图片 (RadialGradient 不透明度遮罩)
├── 游戏标题 (DropShadowEffect)
├── 游戏描述 (120px 高度)
├── 启动按钮 (Install/Launch/Play)
├── 启动方式: Steam / 自定义路径 → 选择文件
└── 遥测配置按钮 (需要配置的游戏显示)

右侧: TabControl
├── "设备参数" Tab → 4 张预设卡片 (基座/面盘/踏板/换挡器)
│   └── 自动应用预设开关 + 预设下拉选择 + 详情展开
└── "遥测数据" Tab → 26 个数据项 (RPM/速度/档位/胎温/排名...)
    └── 实时显示支持/不支持状态

底部: 游戏列表
├── 过滤: 全部/已安装/未安装
├── 排序: 已安装优先 → 已置顶优先 → 最后启动 → 名称
├── FLIP 动画: 同首页
└── 刷新按钮 (旋转加载动画)
```

**遥测模拟**: 启动游戏后，订阅 `TelemetryService` 事件，实时更新 26 个数据项的 Supported 状态。

### 9.7 [SettingsUserControl](Views/SettingsUserControl.xaml.cs) — 设置页面

```
两个标签:
├── 系统设置
│   ├── 软件设置: 开机自启(注册表 Run 键), 启动最小化, 关闭最小化
│   ├── 语言设置: 中文/English 下拉
│   ├── 主题设置: Dark Red
│   ├── 版本更新: 检查更新 → 下载 → 安装 三段流程
│   ├── 关于: 公司信息 + 社交媒体链接(抖音/小红书/微博/微信/Bilibili)
│   └── 版权信息
└── 固件更新
    ├── 检查更新按钮 + 最后检查时间
    ├── 设备列表: 类型图标/型号/序列号/当前版本/状态/更新按钮
    ├── 单个更新: 构建进度对话框 UI (百分比文字 + 渐变进度条 + 网格轨道)
    └── 批量更新: 依次更新多个设备 + 结果汇总弹窗
```

**设置持久化**:
```
Properties.Settings.Default:
├── Language → 切换时调用 Default.Save()
├── Theme
├── AutoStart (注册表 HKCU\...\Run)
├── StartMinimizedToTray
└── CloseMinimizedToTray
```

### 9.8 弹窗控件 (DeviceParameters/)

**`PresetListPopup`** (1391 行) — 预设列表弹窗:
```
├── 从右侧滑入的覆层弹窗
├── 双标签: 官方预设 / 个人预设
├── 分类过滤: 按游戏或按类别，搜索框
├── 操作: 双击应用 / 编辑 / 删除 / 导出 / 导入
├── 详情浮窗: 悬停 500ms 后显示预设参数预览
└── Toast 提示: 操作成功/失败 (渐入渐出动画)
```

**`ButtonSettingsPopup`** — 按键灯设置弹窗:
```
├── 缩放动画 (ScaleTransform 0→1)
├── 颜色选择 (8 色: 红/橙/黄/绿/青/蓝/紫/白, 无)
├── 遥测功能开关 + 功能类型 (ABS/TC/DRS/抱死/维修区/打滑)
├── 灯光效果 (常亮/闪烁) + 闪烁速度 (0-5)
└── 触发颜色 (与常亮颜色一致 / 自定义颜色)
```

**`RpmSettingsPopup`** — 转速灯设置弹窗:
```
├── 12 LED 颜色块 (点击选颜色)
├── 每灯触发值滑块 (0-100% 或 0-65535 RPM)
├── 可拖拽爆闪线 (标注 "RPM CAP")
├── 爆闪设置: 模式/颜色/速度
├── 基础灯光: 模式(恒亮/呼吸/彩色循环)/速度
├── 曲线类型 (序列/扩散/汇聚)
├── 显示模式 (百分比/RPM)
└── 遥测模式开关
```

**`EditPresetPopup`** — 预设编辑弹窗:
```
├── 游戏选择: 字母索引导航 (A-Z, 右侧浮动字母条)
├── 已选游戏显示 (可删除)
├── 预设名称输入 (20 字限制, 重复检测)
├── 应用范围: 基座 / 面盘 / 踏板 多选
└── 确认 / 取消
```

**`CalibrationDialog`** — 踏板标定对话框:
```
├── 三轴进度条 (离合/刹车/油门, HID 实时数据)
├── 开始标定 / 完成
└── 缩放弹入/弹出动画
```

### 9.9 [HelpUserControl](Views/HelpUserControl.xaml.cs) — 帮助页面

简单的占位页面（仅 `InitializeComponent()`），XAML 包含帮助文档内容。

---

## 附录A: 数据流总览

```
┌─────────────────────────────────────────────────────────┐
│                    硬件层 (USB / HID)                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐               │
│  │ 基座(CDC)│  │ 面盘(HID) │  │ 踏板(HID) │               │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘               │
└───────┼──────────────┼──────────────┼────────────────────┘
        │              │              │
   ┌────▼────┐    ┌───▼────┐    ┌───▼────┐
   │UsbSerial│    │  Hid   │    │  Hid   │
   │ Manager │    │Service │    │Service │
   └────┬────┘    └───┬────┘    └───┬────┘
        │              │              │
   ┌────▼────┐    ┌───▼──────────▼───┐
   │ Device  │    │  HID 数据事件     │
   │Protocol │    │ (踏板/基座/面盘)  │
   │ Service │    └────────┬─────────┘
   └────┬────┘             │
        │          ┌───────▼────────┐
   ┌────▼────┐    │  UI 参数控件    │
   │Firmware │    │ Base/Steering/ │
   │ Update  │    │ Pedal Control  │
   │ Service │    └────────────────┘
   └─────────┘

┌─────────────────────────────────────────────────────────┐
│                    遥测层                                   │
│  TelemetrySDK.dll → TelemetryAPI → TelemetryService       │
│       │                                                  │
│       ├→ TelemetryPacketBuilder (5×64B数据包)             │
│       │      └→ UsbSerialManager.SendToDevice()           │
│       │                                                  │
│       └→ GameUserControl (26项遥测数据显示)                │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                    网络层 (HTTP)                           │
│  Strapi API (192.168.1.214:1337)                         │
│       ├→ FirmwareApiService (固件版本 + 下载)             │
│       ├→ ClientInstallerApiService (软件更新 + 下载)      │
│       └→ BannerApiService (海报图片)                     │
└─────────────────────────────────────────────────────────┘
```

## 附录B: 关键协议帧格式

```
USB 通信帧 (64 字节固定长度):
┌──────┬──────────┬──────────┬─────────────────────────┬──────┐
│ 0x61 │ Command  │ Timestamp│ Data / Parameters       │ CRC? │
│ 1B   │ 2B(LE)   │ 4B(LE)   │ 57B                     │      │
└──────┴──────────┴──────────┴─────────────────────────┴──────┘

遥测数据帧 (64 字节固定长度):
┌──────┬──────────┬──────────┬─────────────────────────────┐
│ 0x61 │ PktType  │ Timestamp│ 57B 遥测数据 (按协议定义)    │
│ 1B   │ 2B(LE)   │ 4B(LE)   │                             │
└──────┴──────────┴──────────┴─────────────────────────────┘
包类型: 0x6101~0x6105 (车辆信息1-4 + 比赛信息)
```

## 附录C: 文件统计

| 层级 | 文件数 | 代码规模 |
|------|--------|----------|
| 项目骨架 | 8 | ~500 行 |
| 数据模型 (Models) | 21 | ~1150 行 |
| 基础设施 (Helpers/Controls/ViewModels) | 11 | ~2000 行 |
| USB 通信 (Services/Usb) | 10 | ~5000 行 |
| 遥测系统 (Services/Telemetry*) | 4 | ~1300 行 |
| 网络数据 (Services/Data) | 7 | ~1400 行 |
| 业务服务 (Services/Other) | 4 | ~800 行 |
| ViewModel | 1 | ~100 行 |
| 视图 (Views) | 18 | ~10000+ 行 |
| 配置清单 (JSON/XML/Presets) | 10+ | — |
| **总计** | **~84+** | **~25000+ 行** |

---

> 📅 文档生成时间: 2025-07-15  
> 📝 基于 HITAPEX v0.1.0 完整源码分析
