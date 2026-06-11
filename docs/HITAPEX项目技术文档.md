# HITAPEX 项目技术文档

> **版本**: 1.0.0  
> **更新日期**: 2026-06-05  
> **项目类型**: WPF 桌面应用程序  
> **目标框架**: .NET 9.0 (Windows)  
> **应用场景**: 竞速模拟器外设配置管理工具

---

## 目录

1. [项目概述](#1-项目概述)
2. [项目架构](#2-项目架构)
   - [2.1 整体架构](#21-整体架构)
   - [2.2 命名空间组织](#22-命名空间组织)
   - [2.3 模块依赖关系](#23-模块依赖关系)
3. [入口点与应用生命周期](#3-入口点与应用生命周期)
4. [ViewModels 层](#4-viewmodels-层)
5. [Models 层](#5-models-层)
   - [5.1 通用模型](#51-通用模型)
   - [5.2 USB 通信模型](#52-usb-通信模型)
   - [5.3 固件模型](#53-固件模型)
6. [Services 层](#6-services-层)
   - [6.1 USB 串口通信服务](#61-usb-串口通信服务)
   - [6.2 HID 服务](#62-hid-服务)
   - [6.3 设备协议服务](#63-设备协议服务)
   - [6.4 固件更新服务](#64-固件更新服务)
   - [6.5 数据服务](#65-数据服务)
   - [6.6 游戏相关服务](#66-游戏相关服务)
   - [6.7 预设管理服务](#67-预设管理服务)
7. [Views 层](#7-views-层)
   - [7.1 主窗口](#71-主窗口)
   - [7.2 主导航视图](#72-主导航视图)
   - [7.3 设备参数控件](#73-设备参数控件)
   - [7.4 弹窗控件](#74-弹窗控件)
8. [Controls 控件](#8-controls-控件)
9. [Helpers 工具类](#9-helpers-工具类)
10. [XAML UI 结构详解](#10-xaml-ui-结构详解)
11. [设计模式与技术框架](#11-设计模式与技术框架)
12. [配置文件与资源](#12-配置文件与资源)
13. [数据流与通信协议](#13-数据流与通信协议)
14. [附录](#14-附录)

---

## 1. 项目概述

HITAPEX 是一款基于 .NET 9 + WPF 的 Windows 桌面应用，专为竞速模拟器外设（方向盘基座、踏板、换挡器、手刹等）提供图形化配置管理工具。主要功能包括：

- **设备参数配置**: 基座力反馈、方向盘按键灯/RPM指示灯、踏板曲线/死区调节
- **预设管理**: 官方/个人预设的创建、编辑、导入导出、应用
- **固件更新**: USB 设备固件版本检测与在线更新
- **HID 实时数据**: 踏板和基座 HID 输入数据的实时采集与可视化
- **游戏启动器**: Steam 游戏库管理与启动
- **系统托盘**: 最小化到托盘、开机自启

---

## 2. 项目架构

### 2.1 整体架构

项目采用经典的 **MVVM (Model-View-ViewModel)** 架构模式，并遵循分层设计：

```
┌─────────────────────────────────────────────────┐
│                   Views (XAML + Code-Behind)     │
│   MainWindow / Home / Device / Game / Settings   │
│   DeviceParameters (Base/Pedal/Wheel Controls)   │
│   Popups (PresetList/EditPreset/Calibration...)  │
├─────────────────────────────────────────────────┤
│               ViewModels (ViewModelBase)         │
│   MainWindowViewModel / NavigationItem           │
│   RelayCommand                                   │
├─────────────────────────────────────────────────┤
│                  Services                        │
│  ┌──────────┐ ┌──────────┐ ┌─────────────────┐  │
│  │ USB/Serial│ │  HID     │ │ DeviceProtocol  │  │
│  │ Comm     │ │ Service  │ │ Service         │  │
│  ├──────────┤ ├──────────┤ ├─────────────────┤  │
│  │ Firmware │ │ Preset   │ │ Data (API/Cache) │  │
│  │ Update   │ │ Service  │ │                 │  │
│  ├──────────┤ ├──────────┤ ├─────────────────┤  │
│  │ Game     │ │ Steam    │ │                 │  │
│  │ Launcher │ │ Install  │ │                 │  │
│  └──────────┘ └──────────┘ └─────────────────┘  │
├─────────────────────────────────────────────────┤
│                    Models                        │
│  DeviceParameters / GameItem / BannerItem       │
│  USB Models (HID Data / Responses / Registry)   │
│  Firmware Models                                │
├─────────────────────────────────────────────────┤
│               Infrastructure                    │
│  Controls (ModalDialog / SkipInkTextBlock)      │
│  Helpers (TrayIcon)                             │
│  Properties (Settings)                          │
└─────────────────────────────────────────────────┘
```

### 2.2 命名空间组织

| 命名空间 | 所在目录 | 职责 |
|---|---|---|
| `HITAPEX` | 根目录 | 应用入口 (`App.xaml.cs`)、主窗口 (`MainWindow.xaml.cs`) |
| `HITAPEX.Controls` | `Controls/` | 自定义 WPF 控件 (`ModalDialog`, `SkipInkTextBlock`) |
| `HITAPEX.Helpers` | `Helpers/` | 工具类 (`TrayIcon` - 系统托盘) |
| `HITAPEX.Models` | `Models/` | 应用模型 (`BannerItem`, `GameItem`, `DeviceParameters`) |
| `HITAPEX.Models.Usb` | `Models/Usb/` | USB 通信模型 (HID数据、设备信息、预设快照) |
| `HITAPEX.Properties` | `Properties/` | 自动生成的应用设置 |
| `HITAPEX.Services` | `Services/` | 顶层服务 (`GameLauncher`, `PresetService`, `SteamInstallService`) |
| `HITAPEX.Services.Data` | `Services/Data/` | 数据服务 (`GameDataService`) |
| `HITAPEX.Services.Data.Api` | `Services/Data/Api/` | API 客户端 (`ApiClient`, `GameApiService`, `BannerApiService`, `FirmwareApiService`) |
| `HITAPEX.Services.Data.Cache` | `Services/Data/Cache/` | 缓存服务 (`CacheService`, `ImageCacheService`, `LocalGameCacheService`) |
| `HITAPEX.Services.Data.Models` | `Services/Data/Models/` | API 响应 DTO (`ApiResponses`) |
| `HITAPEX.Services.Data.Transformation` | `Services/Data/Transformation/` | 数据转换 (`DataTransformer`) |
| `HITAPEX.Services.Usb` | `Services/Usb/` | USB 通信服务 (`UsbSerialManager`, `HidService`, `DeviceProtocolService`, `FirmwareUpdateService`) |
| `HITAPEX.ViewModels` | `ViewModels/` | MVVM ViewModel 层 |
| `HITAPEX.Views` | `Views/` | 顶层视图 UserControl |
| `HITAPEX.Views.DeviceParameters` | `Views/DeviceParameters/` | 设备参数配置控件 |

### 2.3 模块依赖关系

```
App.xaml.cs (入口)
  ├── UsbSerialManager ──────── DeviceSerialChannel, UsbDeviceDiscovery, DeviceLogger
  ├── HidService ────────────── HidChannel, HidNative (P/Invoke)
  ├── DeviceProtocolService ─── 协议帧构建/解析 (通过 UsbSerialManager 通信)
  ├── FirmwareUpdateService ─── 固件下载与刷写
  ├── FirmwareApiService ────── ApiClient → 后端 API
  ├── PresetService ─────────── 本地 JSON 文件读写
  └── MainWindow
        ├── MainWindowViewModel
        │     ├── HomeUserControl ── GameDataService → GameApiService → ApiClient
        │     ├── DeviceUserControl
        │     │     ├── BaseParameterControl
        │     │     ├── PedalParameterControl ── HidService (PedalDataReceived)
        │     │     └── SteeringWheelParameterControl
        │     ├── GameUserControl ── GameDataService, GameLauncher
        │     ├── HelpUserControl
        │     └── SettingsUserControl ── FirmwareUpdateService
        └── TrayIcon (Win32 P/Invoke)
```

---

## 3. 入口点与应用生命周期

### 3.1 `App.xaml`

**文件路径**: [`App.xaml`](App.xaml)

**作用**: WPF 应用程序资源定义文件。

**主要资源**:

| 资源 | 类型 | 说明 |
|---|---|---|
| FluentWPF 样式 | `ResourceDictionary.MergedDictionaries` | 引入 Fluent Design 控件样式 |
| `ScrollBar` 样式 | `Style` | 自定义滚动条: 深色背景 `#0D1117`, 宽6px |
| `ProgressBar` 样式 | `Style` | 自定义进度条: 深色背景 `#0D1B2A`, 圆角3px, 含 `PART_Track`/`PART_Indicator` 模板部件 |
| `OrbitronFont` | `FontFamily` | 自定义字体: `./Assets/Fonts/#Orbitron` (科技感数字字体) |

### 3.2 `App.xaml.cs`

**文件路径**: [`App.xaml.cs`](App.xaml.cs)

**类**: `App : Application`

**静态属性 (全局单例访问点)**:

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsSessionEnding` | `bool` | Windows 会话是否正在结束 |
| `UsbManager` | `UsbSerialManager?` | USB 串口设备管理器全局实例 |
| `HidService` | `HidService?` | HID 设备服务全局实例 |
| `ProtocolService` | `DeviceProtocolService?` | 设备通信协议服务全局实例 |
| `FirmwareUpdater` | `FirmwareUpdateService?` | 固件更新服务全局实例 |
| `FirmwareApi` | `FirmwareApiService?` | 固件 API 服务全局实例 |
| `PresetService` | `PresetService?` | 预设管理服务全局实例 |

**关键方法**:

- **`OnStartup(StartupEventArgs e)`**: 应用启动入口。依次执行:
  1. 调用 `InitializeUsbManager()` 初始化所有硬件服务
  2. 创建 `MainWindow` 实例
  3. 订阅 `SessionEnding` 事件
  4. 根据 `StartMinimizedToTray` 设置决定显示窗口或最小化到托盘

- **`OnExit(ExitEventArgs e)`**: 应用退出清理。依次释放 `HidService` 和 `UsbManager` 资源。

- **`InitializeUsbManager()`**: 核心初始化方法,执行以下步骤:
  1. 创建日志目录 `{AppContext.BaseDirectory}/logs/usb`
  2. 实例化 `UsbSerialManager` 并注册 `DeviceRegistry.GetAllVidPids()` 中的所有目标设备 VID/PID
  3. 为 USB 管理器事件 (`DeviceConnected`, `DeviceDisconnected`, `RawDataReceived`, `DeviceError`, `LogEntryAdded`) 注册 Debug 级日志处理
  4. 实例化 `DeviceProtocolService`, `FirmwareUpdateService`, `FirmwareApiService`, `PresetService`
  5. 实例化 `HidService` 并注册 `PedalDataReceived` 和 `BaseDataReceived` 事件处理器
  6. 调用 `HidService.Start()` 和 `UsbManager.Start()` 启动设备监控

### 3.3 `AssemblyInfo.cs`

**文件路径**: [`AssemblyInfo.cs`](AssemblyInfo.cs)

**作用**: 声明 WPF 主题资源字典位置。

```csharp
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,          // 主题特定资源不在此处
    ResourceDictionaryLocation.SourceAssembly // 通用资源在源程序集中
)]
```

---

## 4. ViewModels 层

### 4.1 `ViewModelBase.cs`

**文件路径**: [`ViewModels/ViewModelBase.cs`](ViewModels/ViewModelBase.cs)

**类**: `ViewModelBase` (abstract, 实现 `INotifyPropertyChanged`)

**职责**: 所有 ViewModel 的抽象基类,提供属性变更通知能力。

**成员**:

| 成员 | 类型 | 说明 |
|---|---|---|
| `PropertyChanged` | `event` | 属性变更通知事件 |
| `OnPropertyChanged([CallerMemberName] string?)` | `void` | 触发 `PropertyChanged` 事件 |
| `SetProperty<T>(ref T, T, [CallerMemberName] string?)` | `bool` | 类型安全的属性设置器: 执行相等性检查 → 赋值 → 触发通知, 返回是否变更 |

```csharp
// SetProperty 典型用法:
private string _name;
public string Name
{
    get => _name;
    set => SetProperty(ref _name, value);
}
```

### 4.2 `NavigationItem.cs`

**文件路径**: [`ViewModels/NavigationItem.cs`](ViewModels/NavigationItem.cs)

**类**: `NavigationItem : ViewModelBase`

**职责**: 侧边栏导航项的数据模型,支持选择状态绑定。

**属性**:

| 属性 | 类型 | 说明 |
|---|---|---|
| `Name` | `string` (get-only) | 导航项唯一标识名 |
| `IconPath` | `string` (get-only) | SVG 图标路径 |
| `Label` | `string` (get-only) | 显示标签文本 |
| `IsSelected` | `bool` | 是否选中, 变更时触发 `PropertyChanged` |

**构造函数**: `NavigationItem(string name, string iconPath, string label)` — 初始化只读属性。

### 4.3 `MainWindowViewModel.cs`

**文件路径**: [`ViewModels/MainWindowViewModel.cs`](ViewModels/MainWindowViewModel.cs)

**类**: `MainWindowViewModel : ViewModelBase`

**职责**: 主窗口的 ViewModel,管理导航状态和当前视图切换。

**属性**:

| 属性 | 类型 | 说明 |
|---|---|---|
| `NavigationItems` | `ObservableCollection<NavigationItem>` | 5个导航项集合 (首页/设备/游戏/帮助/设置) |
| `SelectedNavigationItem` | `NavigationItem` | 当前选中的导航项, setter 中更新 `IsSelected` 并调用 `UpdateCurrentView()` |
| `CurrentView` | `UserControl?` | 当前显示的视图控件, 绑定到 `ContentControl.Content` |
| `Title` | `string` | 窗口标题 (默认 "HITAPEX Racing Simulator") |

**关键方法**:

- **构造函数**: 创建 5 个 `NavigationItem`:
  - `Home` → `/Assets/HomeIcon.svg` → "首页"
  - `Device` → `/Assets/DeviceIcon.svg` → "设备"
  - `Game` → `/Assets/GameIcon.svg` → "游戏"
  - `Help` → `/Assets/HelpIcon.svg` → "帮助"
  - `Settings` → `/Assets/SettingsIcon.svg` → "设置"
  
- **`UpdateCurrentView()`**: 根据 `SelectedNavigationItem.Name` 切换 `CurrentView`:
  - `"Home"` → `new HomeUserControl()`
  - `"Device"` → `new DeviceUserControl()`
  - `"Game"` → `new GameUserControl()`
  - `"Help"` → `new HelpUserControl()`
  - `"Settings"` → `new SettingsUserControl()`

### 4.4 `RelayCommand.cs`

**文件路径**: [`ViewModels/RelayCommand.cs`](ViewModels/RelayCommand.cs)

**类**: `RelayCommand : ICommand`

**职责**: 标准 MVVM `ICommand` 实现,用于将视图操作绑定到 ViewModel 方法。

**构造函数**:
- `RelayCommand(Action<object?> execute)` — 带参数的命令
- `RelayCommand(Action execute)` — 无参数命令 (内部包装为 `_ => execute()`)

**成员**:

| 成员 | 说明 |
|---|---|
| `CanExecuteChanged` | 通过 `CommandManager.RequerySuggested` 自动刷新 |
| `CanExecute(object?)` | 委托给可选的 `_canExecute` 谓词; 未提供则始终返回 `true` |
| `Execute(object?)` | 调用 `_execute` 回调 |

---

## 5. Models 层

### 5.1 通用模型

#### `BannerItem.cs` — 广告横幅模型

**文件路径**: [`Models/BannerItem.cs`](Models/BannerItem.cs)

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `ImageUrl` | `string` | `""` | 横幅图片 URL |
| `LinkUrl` | `string` | `""` | 点击跳转链接 |

#### `GameItem.cs` — 游戏模型

**文件路径**: [`Models/GameItem.cs`](Models/GameItem.cs)

**类**: `GameItem : INotifyPropertyChanged`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Id` | `int` | 游戏 ID |
| `Name` | `string` | 游戏名称 |
| `CoverImageUrl` | `string` | 封面图片 URL |
| `BgImageUrl` | `string?` | 背景图片 URL (可空) |
| `SteamId` | `string` | Steam 应用 ID |
| `IsInstalled` | `bool` | 是否已安装 |
| `LaunchPath` | `string` | 启动路径 |
| `Description` | `string` | 游戏描述 |
| `Version` | `string` | 版本号 |
| `LastPlayed` | `string` | 上次游玩时间 (字符串) |
| `LastLaunchTime` | `DateTime?` | 上次启动时间 |
| `IsPinned` | `bool` | 是否置顶, 变更时触发 `PropertyChanged` |

#### `DeviceParameters.cs` — 设备参数模型

**文件路径**: [`Models/DeviceParameters.cs`](Models/DeviceParameters.cs)

**`BaseParameters` 类** — 基座参数:

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `ForceFeedback` | `double` | 75 | 力反馈强度 |
| `DetailLevel` | `double` | 60 | 细节等级 |
| `DampingLevel` | `double` | 40 | 阻尼等级 |
| `TempWarning` | `double` | 60 | 温度警告阈值 (°C) |
| `TempThrottle` | `double` | 70 | 温度降功率阈值 (°C) |
| `MaxRpm` | `double` | 3000 | 最大转速 |
| `ResponseCurve` | `int` | 0 | 响应曲线类型 |
| `WorkMode` | `int` | 0 | 工作模式 |

**`SteeringWheelParameters` 类** — 方向盘参数:

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `RotationAngle` | `double` | 900 | 旋转角度 (度) |
| `Sensitivity` | `double` | 50 | 灵敏度 |
| `DeadZone` | `double` | 5 | 死区 |
| `Damping` | `double` | 30 | 阻尼 |
| `Vibration` | `double` | 70 | 振动强度 |
| `RoadFeedback` | `double` | 60 | 路面反馈 |
| `ButtonMappings` | `Dictionary<int, string>` | 12个默认映射 | 按键功能映射 |

**`PedalParameters` 类** — 踏板参数:

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `ThrottleSensitivity` | `double` | 80 | 油门灵敏度 |
| `ThrottleDeadZone` | `double` | 2 | 油门死区 |
| `ThrottleCurve` | `int` | 0 | 油门曲线类型 |
| `BrakeSensitivity` | `double` | 90 | 刹车灵敏度 |
| `BrakeDeadZone` | `double` | 1 | 刹车死区 |
| `BrakePressure` | `double` | 70 | 刹车压力 |
| `AbsVibration` | `double` | 50 | ABS 振动强度 |
| `BrakeCurve` | `int` | 0 | 刹车曲线类型 |
| `ClutchSensitivity` | `double` | 70 | 离合器灵敏度 |
| `ClutchBitePoint` | `double` | 50 | 离合器咬合点 |

**`DeviceParametersSet` 类** — 聚合参数集:

| 属性 | 类型 | 说明 |
|---|---|---|
| `Base` | `BaseParameters` | 基座参数 |
| `SteeringWheel` | `SteeringWheelParameters` | 方向盘参数 |
| `Pedal` | `PedalParameters` | 踏板参数 |

所有参数类均实现 `Clone()` (深拷贝) 和 `Apply(T other)` (属性覆盖) 方法。

### 5.2 USB 通信模型

#### 设备识别与注册

**`VidPidPair`** — VID/PID 值对 ([`Models/Usb/VidPidPair.cs`](Models/Usb/VidPidPair.cs)):
```csharp
readonly record struct VidPidPair(int Vid, int Pid)
```
- `ToString()` 返回 `"VID_{XXXX}&PID_{XXXX}"` 格式的标准 USB 设备标识

**`DeviceDescriptor`** — 设备描述符 ([`Models/Usb/DeviceDescriptor.cs`](Models/Usb/DeviceDescriptor.cs)):

| 属性 | 类型 | 说明 |
|---|---|---|
| `ModelName` | `string` | 设备型号名称 |
| `DeviceType` | `DeviceType` | 设备类型枚举 |
| `NormalMode` | `VidPidPair` | 正常工作模式 VID/PID |
| `UpdateMode` | `VidPidPair` | 固件更新模式 (Bootloader) VID/PID |

方法:
- `IsNormalMode(int vid, int pid)` — 判断是否匹配正常工作模式
- `IsUpdateMode(int vid, int pid)` — 判断是否匹配更新模式
- `Matches(int vid, int pid)` — 判断是否匹配任一模式

**`DeviceRegistry`** — 静态设备注册表 ([`Models/Usb/DeviceRegistry.cs`](Models/Usb/DeviceRegistry.cs)):

预注册设备: `"A1踏板"` (Wheel 类型, 正常模式 VID/PID `0xFF3F:0x0002`, 更新模式 `0xFF3F:0xF002`)

| 方法 | 返回值 | 说明 |
|---|---|---|
| `GetAllVidPids()` | `IEnumerable<VidPidPair>` | 获取所有已注册设备的 VID/PID |
| `FindByVidPid(int vid, int pid)` | `DeviceDescriptor?` | 按 VID/PID 查找设备描述符 |
| `IsUpdateMode(int vid, int pid)` | `bool` | 判断设备是否处于更新模式 |
| `GetDeviceType(int vid, int pid)` | `DeviceType` | 获取设备类型 |
| `GetDisplayName(int vid, int pid)` | `string` | 获取设备显示名称 |
| `Register(DeviceDescriptor)` | `void` | 动态注册新设备 |

**`DeviceType`** 枚举: `Unknown(0)`, `Base(1)`, `Pedal(2)`, `Shifter(3)`, `Handbrake(4)`, `Sequential(5)`, `Wheel(6)`

#### 连接状态与日志

**`DeviceConnectionState`** 枚举: `Disconnected`, `Connecting`, `Connected`, `Disconnecting`, `Reconnecting`, `Error`

**`DeviceEventType`** 枚举: `DeviceConnected`, `DeviceDisconnected`, `DeviceConnectFailed`, `DeviceReconnecting`, `DeviceReconnectFailed`, `DeviceRecovered`, `RawDataReceived`, `DataSendFailed`, `SerialError`, `DiscoveryStarted`, `DiscoveryCompleted`, `VidPidMatched`, `VidPidNotMatched`

**`DeviceLogEntry`** — 设备日志条目 ([`Models/Usb/DeviceLogEntry.cs`](Models/Usb/DeviceLogEntry.cs)):
```csharp
class DeviceLogEntry
{
    DateTime Timestamp { get; init; }
    DeviceEventType EventType { get; init; }
    string DeviceKey { get; init; }
    string Message { get; init; }
    string? Detail { get; init; }
    Exception? Exception { get; init; }
}
```

**`UsbDeviceInfo`** — USB 设备运行时信息 ([`Models/Usb/UsbDeviceInfo.cs`](Models/Usb/UsbDeviceInfo.cs)):

| 属性 | 类型 | 说明 |
|---|---|---|
| `DeviceId` | `string` | PNP 设备 ID (init-only) |
| `PortName` | `string` | COM 端口名称 (init-only) |
| `Vid` | `int` | USB Vendor ID (init-only) |
| `Pid` | `int` | USB Product ID (init-only) |
| `Name` | `string` | 设备名称 (init-only) |
| `Description` | `string` | 设备描述 (init-only) |
| `SerialNumber` | `string` | 序列号 (init-only) |
| `State` | `DeviceConnectionState` | 连接状态 (可变) |
| `LastConnectedTime` | `DateTime?` | 最后连接时间 (可变) |
| `ReconnectAttempts` | `int` | 重连尝试次数 (可变) |
| `TotalBytesReceived` | `long` | 总接收字节数 (线程安全, 使用 `Interlocked`) |
| `DeviceKey` | `string` (计算属性) | `"{Vid:X4}:{Pid:X4}_{PortName}"` 格式的唯一键 |

方法:
- `IncrementBytesReceived(long delta)` — 线程安全的字节计数器递增 (使用 `Interlocked.Add`)

#### HID 数据模型

**`HidPedalData`** — 踏板 HID 数据 ([`Models/Usb/HidPedalData.cs`](Models/Usb/HidPedalData.cs)):

| 属性 | 类型 | 说明 |
|---|---|---|
| `ReportId` | `byte` | HID 报告 ID (0x01) |
| `Gas` | `ushort` | 油门原始值 (0-65535) |
| `Brake` | `ushort` | 刹车原始值 (0-65535) |
| `Clutch` | `ushort` | 离合器原始值 (0-65535) |
| `X`, `Y`, `Rz` | `ushort` | 辅助轴 |
| `User` | `ushort[8]` | 用户自定义数据 |
| `GasPercent` | `double` (计算) | 油门百分比 (`Gas / 65535.0 * 100.0`) |
| `BrakePercent` | `double` (计算) | 刹车百分比 |
| `ClutchPercent` | `double` (计算) | 离合器百分比 |

静态方法:
- `Parse(byte[] data)` → `HidPedalData?`: 解析 29 字节 HID 报告, 使用 `BitConverter.ToUInt16` 提取各 2 字节字段

**`HidBaseData`** — 基座 HID 数据 ([`Models/Usb/HidBaseData.cs`](Models/Usb/HidBaseData.cs)):

| 属性 | 类型 | 说明 |
|---|---|---|
| `ReportId` | `byte` | HID 报告 ID (0x11) |
| `Steering` | `ushort` | 方向盘位置 (0-65535, 中心=0x8000) |
| `LeftPaddle` | `ushort` | 左拨片 |
| `RightPaddle` | `ushort` | 右拨片 |
| `Throttle`, `Brake`, `Clutch` | `ushort` | 踏板轴 |
| `Slider` | `ushort` | 滑块 |
| `DirectionKeys1`, `DirectionKeys2` | `byte` | 方向键状态 (0-8, 0=释放) |
| `ButtonBits` | `byte[16]` | 128 位按键掩码 |

静态方法:
- `Parse(byte[] data)` → `HidBaseData?`: 解析 42+ 字节 HID 报告

#### 设备响应模型

**`DeviceInfoResponse`** — 设备信息响应 ([`Models/Usb/DeviceInfoResponse.cs`](Models/Usb/DeviceInfoResponse.cs)):

| 属性 | 类型 | 说明 |
|---|---|---|
| `DeviceType` | `DeviceType` | 设备类型 |
| `UsbSpeed` | `int` | USB 速度 |
| `NormalFirmwareVersion` | `int` | 正常固件版本 (高位=主版本, 低位=次版本) |
| `BootFirmwareVersion` | `int` | Bootloader 版本 |
| `WheelConnectionStatus` | `int` | 方向盘连接状态 (偏移9) |
| `WheelNormalFwVersion` | `int` | 方向盘固件版本 (偏移10-11) |
| `PedalConnectionStatus` | `int` | 踏板连接状态 (偏移14) |
| `PedalNormalFwVersion` | `int` | 踏板固件版本 (偏移15-16) |
| `PedalCount` | `int` | 踏板数量 (偏移30, 0=2踏板, 1=3踏板) |
| `VersionString` | `string` (计算) | `"v{major}.{minor}"` 格式 |
| `IsPedalConnected` | `bool` (计算) | 踏板是否连接 |

**`PedalParametersResponse`** — 踏板参数响应 (协议 0x2110):

包含三个轴的配置: Clutch/Brake/Throttle 各含:
- `Direction` (byte) — 方向
- `Point1Y/X` ~ `Point4Y/X` (byte) — 4点校准曲线坐标
- `DeadZoneFront`/`DeadZoneRear` (byte) — 前后死区

**`PresetNameResponse`** — 预设名称响应 (协议 0x21D0):

| 属性 | 类型 | 说明 |
|---|---|---|
| `DeviceType` | `DeviceType` | 设备类型 |
| `TotalLength` | `int` | 名称总字节长度 |
| `PacketIndex` | `int` | 数据包序号 |
| `NameData` | `byte[]` | 56 字节名称数据片段 |

静态方法:
- `DecodeNameFromPackets(List<PresetNameResponse>)` → `string`: 按序号排序拼接, UTF-8 解码

**方向盘相关响应模型**:

| 模型 | 协议 | 说明 |
|---|---|---|
| `WheelRpmBaseModeResponse` | 0x2103 | RPM 基础灯模式 (基模式/速度/12 LED RGB) |
| `WheelRpmIndicatorResponse` | 0x2104 | RPM 指示灯 (触发模式/12 阈值/12 LED RGB) |
| `WheelRpmModeResponse` | 0x2105 | RPM 灯模式 (亮度/遥测/模式/频闪) |
| `WheelButtonLightResponse` | 0x2107 | 按键灯 (LED模式/索引/颜色/遥测功能) |
| `WheelSleepAndPaddleResponse` | 0x2108 | 休眠与拨片 (休眠时间/效果/离合器模式/咬合点) |

#### 预设快照模型

**`PedalPresetSnapshot`** — 踏板预设快照 ([`Models/Usb/PedalPresetSnapshot.cs`](Models/Usb/PedalPresetSnapshot.cs)):

与 `PedalParametersResponse` 结构一致, 额外包含:
- `*CurveType` (int) — 曲线类型 (Clutch/Brake/Throttle)
- 所有属性使用 `[JsonPropertyName]` 特性支持 JSON 序列化
- `ParametersEqual(PedalPresetSnapshot other)` → `bool`: 逐字段比较 (排除曲线类型, 因设备始终返回自定义曲线)

**`WheelPresetSnapshot`** — 方向盘预设快照 ([`Models/Usb/WheelPresetSnapshot.cs`](Models/Usb/WheelPresetSnapshot.cs)):

全局设置:
| 属性 | 说明 |
|---|---|
| `KeyColorEnabled` | 按键灯颜色启用 |
| `GlobalKeyColor` | 全局按键颜色 (0-7) |
| `ShowKeyNumber` | 显示按键编号 |
| `KeyBrightness` | 按键亮度 (0-100) |
| `RpmBrightness` | RPM 灯亮度 (0-100) |
| `SleepLightDuration` | 休眠灯持续时间 |
| `StandbyLightEffect` | 待机灯效果 |
| `GlobalFlashSpeed` | 全局闪烁速度 |

按键设置 (19个按键, 每个含数组):
- `ButtonColors[int]`, `ButtonTelemetryEnabled[bool]`, `ButtonTelemetryLightEffect[int]`, `ButtonTelemetryFunc[int]`, `ButtonTelemetryTriggerColor[int]`, `ButtonSpeeds[int]`

RPM 设置 (12 LED):
- `RpmColors[int]`, `RpmValues[double]`, `RpmCapValue`, `RpmCurveType`, `RpmDisplayMode`, `RpmLightMode`, `RpmStrobeMode`, `RpmStrobeColor`, `RpmSpeed`, `RpmBaseLightMode`, `RpmBaseLightSpeed`, `RpmTelemetryEnabled`

拨片设置:
- `ClutchMode` (0=合成轴, 1=独立轴, 2=按键)
- `ClutchPointValue` (0-100)

方法:
- `ParametersEqual(WheelPresetSnapshot other)` → `bool`: 逐字段比较, 数组使用 `SequenceEqual`

### 5.3 固件模型

**文件路径**: [`Models/FirmwareVersionInfo.cs`](Models/FirmwareVersionInfo.cs)

**`FirmwareVersionInfo`** — 固件版本信息:

| 属性 | 类型 | JsonPropertyName | 说明 |
|---|---|---|---|
| `Id` | `int` | `id` | 固件记录 ID |
| `DocumentId` | `string` | `documentId` | 文档 ID |
| `Pid` | `string` | `pid` | PID (十六进制字符串) |
| `Vid` | `string` | `vid` | VID (十六进制字符串) |
| `Version` | `string` | `version` | 固件版本号 |
| `DeviceName` | `string` | `deviceName` | 设备名称 |
| `UpdateLog` | `string` | `updateLog` | 更新日志 |
| `UpdateFile` | `FirmwareFileInfo?` | `updateFile` | 固件文件信息 |
| `CreatedAt` | `string` | `createdAt` | 创建时间 |
| `UpdatedAt` | `string` | `updatedAt` | 更新时间 |
| `PublishedAt` | `string` | `publishedAt` | 发布时间 |
| `Locale` | `string?` | `locale` | 语言区域 (可空) |

计算属性:
- `ParsedVid` — 将十六进制 `Vid` 字符串解析为 `int`
- `ParsedPid` — 将十六进制 `Pid` 字符串解析为 `int`

**`FirmwareFileInfo`** — 固件文件信息:

| 属性 | JsonPropertyName | 说明 |
|---|---|---|
| `Id` | `id` | 文件 ID |
| `DocumentId` | `documentId` | 文档 ID |
| `Name` | `name` | 文件名 |
| `Url` | `url` | 下载 URL |
| `Ext` | `ext` | 文件扩展名 |
| `Mime` | `mime` | MIME 类型 |
| `Size` | `size` | 文件大小 (字节) |
| `Hash` | `hash` | 文件哈希/校验和 |

---

## 6. Services 层

### 6.1 USB 串口通信服务

#### `IUsbSerialManager` 接口

**文件路径**: [`Services/Usb/IUsbSerialManager.cs`](Services/Usb/IUsbSerialManager.cs)

定义 USB 串口管理的完整契约:

```csharp
interface IUsbSerialManager : IDisposable
{
    // 事件
    event Action<UsbDeviceInfo>? DeviceConnected;
    event Action<UsbDeviceInfo>? DeviceDisconnected;
    event Action<UsbDeviceInfo, byte[]>? RawDataReceived;
    event Action<DeviceLogEntry>? LogEntryAdded;
    event Action<UsbDeviceInfo, string>? DeviceError;

    // 属性
    IReadOnlyList<UsbDeviceInfo> ConnectedDevices { get; }
    bool IsRunning { get; }

    // 方法
    void RegisterTargetDevice(VidPidPair pair);
    void RegisterTargetDevices(IEnumerable<VidPidPair> pairs);
    void UnregisterTargetDevice(VidPidPair pair);
    IReadOnlyCollection<VidPidPair> GetRegisteredDevices();
    void Start();
    void Stop();
    bool ConnectDevice(UsbDeviceInfo deviceInfo);
    void DisconnectDevice(UsbDeviceInfo deviceInfo);
    void DisconnectAll();
    bool SendToDevice(string deviceKey, byte[] data);
    IReadOnlyList<DeviceLogEntry> GetRecentLogs(int count = 100);
    void SetLoggingEnabled(bool enabled);
}
```

#### `UsbSerialManager` — USB 串口管理器

**文件路径**: [`Services/Usb/UsbSerialManager.cs`](Services/Usb/UsbSerialManager.cs)

**类**: `UsbSerialManager : IUsbSerialManager`

**常量**:

| 常量 | 值 | 说明 |
|---|---|---|
| `DefaultBaudRate` | 115200 | 默认波特率 |
| `MaxReconnectAttempts` | 5 | 最大重连次数 |
| `ReconnectBaseDelayMs` | 1000 | 重连基础延迟 |
| `ReconnectMaxDelayMs` | 30000 | 重连最大延迟 (30秒) |

**核心数据结构**:
- `_channels` (`ConcurrentDictionary<string, DeviceSerialChannel>`) — 设备键 → 串口通道
- `_devices` (`ConcurrentDictionary<string, UsbDeviceInfo>`) — 设备键 → 设备信息
- `_discovery` (`UsbDeviceDiscovery`) — USB 设备发现引擎
- `_logger` (`DeviceLogger`) — 设备日志记录器

**关键方法**:

- **`Start()`**: 线程安全。发现已连接设备 → 逐个连接 → 启动热插拔监控

- **`Stop()`**: 线程安全。停止热插拔监控 → 断开所有设备

- **`OnDeviceArrived(UsbDeviceInfo)`**: 新设备到达处理:
  - 如设备键已存在且已断开 → 重连
  - 否则添加到 `_devices` → 调用 `ConnectDevice`

- **`OnDeviceRemoved(UsbDeviceInfo)`**: 设备移除处理:
  - 释放并移除通道
  - 从 `_devices` 移除
  - 触发 `DeviceDisconnected` 事件

- **`ConnectDevice(UsbDeviceInfo)`** → `bool`:
  1. 创建 `DeviceSerialChannel`
  2. 注册通道事件 (`RawDataReceived`, `ErrorOccurred`, `StateChanged`)
  3. 调用 `channel.Connect()`
  4. 成功: 存储通道, 触发 `DeviceConnected`
  5. 失败: 触发异步重连

- **`TryReconnectAsync(UsbDeviceInfo, DeviceSerialChannel)`**:
  - 最多 `MaxReconnectAttempts` (5) 次尝试
  - 指数退避: 基础 1s, 最大 30s
  - 成功: 存储通道, 触发 `DeviceConnected`
  - 全部失败: 设状态为 `Error`, 释放通道

- **`OnChannelError(DeviceSerialChannel, string)`**:
  - 触发 `DeviceError` 事件
  - 如仍在运行且未处于重连状态 → 创建新通道自动重连

- **`SendToDevice(string deviceKey, byte[] data)`** → `bool`: 按设备键查找通道并发送

#### `DeviceSerialChannel` — 设备串口通道

**文件路径**: [`Services/Usb/DeviceSerialChannel.cs`](Services/Usb/DeviceSerialChannel.cs)

**类**: `DeviceSerialChannel : IDisposable`

**职责**: 管理与单个 USB 串口设备的连接、数据收发和生命周期。

**事件**:
- `RawDataReceived` (`Action<DeviceSerialChannel, byte[]>`) — 原始数据到达
- `ErrorOccurred` (`Action<DeviceSerialChannel, string>`) — 串口错误
- `StateChanged` (`Action<DeviceSerialChannel, DeviceConnectionState, DeviceConnectionState>`) — 状态变更 (旧状态, 新状态)

**关键方法**:

- **`Connect(int baudRate=115200, Parity=None, int dataBits=8, StopBits=One, int readTimeout=500, int writeTimeout=500)`** → `bool`:
  1. 创建 `SerialPort` 实例
  2. 配置波特率/校验位/数据位/停止位/超时
  3. 设置 DTR/RTS 使能
  4. 配置读写缓冲区大小
  5. 打开串口, 清空缓冲区
  6. 启动异步读取循环

- **`Disconnect()`**: 停止读取 → 关闭并释放串口 → 设状态 `Disconnected`

- **`Send(byte[] data)`** → `bool`: 写入原始字节到串口; 串口为 null/已关闭/已释放时返回 false

- **`ReadLoop(CancellationToken token)`** (async):
  1. 循环检查 `BytesToRead`
  2. 通过 `BaseStream.ReadAsync` 读取数据
  3. 日志记录
  4. 触发 `RawDataReceived`
  5. 在取消/异常时退出

- **`OnSerialError(object, SerialErrorReceivedEventArgs)`**: 处理串口错误事件 (Frame, Overrun, RXOver, RXParity, TXFull)

#### `UsbDeviceDiscovery` — USB 设备发现

**文件路径**: [`Services/Usb/UsbDeviceDiscovery.cs`](Services/Usb/UsbDeviceDiscovery.cs)

**类**: `UsbDeviceDiscovery : IDisposable, [SupportedOSPlatform("windows")]`

**职责**: 通过 WMI 查询和事件监控发现 USB 串口设备的插拔。

**两种监控模式**:
1. **WMI 事件监控** (优先): 使用 `ManagementEventWatcher` 监听 `__InstanceCreationEvent` 和 `__InstanceDeletionEvent`
2. **轮询回退**: 定时调用 `DiscoverDevices()` 比较 COM 端口变化

**关键方法**:

- **`DiscoverDevices()`** → `IReadOnlyList<UsbDeviceInfo>`:
  - WMI 查询 `Win32_PnPEntity` (PNPClass='Ports', 名称含 '(COM')
  - 从 PNPDeviceID 解析 VID/PID
  - 按目标设备集过滤
  - 提取 COM 端口名和序列号

- **`StartHotplugMonitoring(int pollIntervalMs=2000)`**: 启动 WMI 监控或回退到轮询

- **`OnDeviceArrived(object, EventArrivedEventArgs)`**: WMI 到达事件处理:
  1. 提取 PNPDeviceID/名称等
  2. 解析 VID/PID → 检查目标集
  3. 提取 COM 端口
  4. 延迟 500ms (等待驱动加载)
  5. 触发 `DeviceArrived` 事件

- **`OnDeviceRemoved(object, EventArrivedEventArgs)`**: WMI 移除事件处理, 触发 `DeviceRemoved`

静态工具方法:
- `TryParseVidPid(string pnpDeviceId, out int vid, out int pid)` → `bool`: 从 PNP 设备 ID 解析 `VID_XXXX`/`PID_XXXX`
- `ExtractComPort(string name)` → `string`: 从 "Device (COM3)" 提取 "COM3"
- `ExtractSerialNumber(string pnpDeviceId)` → `string`: 提取序列号 (截断至32字符)

#### `DeviceLogger` — 设备日志记录器

**文件路径**: [`Services/Usb/DeviceLogger.cs`](Services/Usb/DeviceLogger.cs)

**类**: `DeviceLogger`

**职责**: 设备事件日志的存储与分发。

**特性**:
- 内存队列 (`ConcurrentQueue<DeviceLogEntry>`, 默认最大 1000 条)
- 文件持久化 (按日期命名 `usb_device_yyyyMMdd.log`)
- 线程安全的文件写入 (`lock` 保护)
- 实时事件通知 (`LogEntryAdded`)

| 方法 | 说明 |
|---|---|
| `Log(DeviceEventType, string deviceKey, string message, ...)` | 创建日志条目 → 入队列 → 触发事件 → 写文件 → Debug 输出 |
| `GetRecentEntries(int count=100)` | 获取最近的日志条目 |
| `SetEnabled(bool)` | 启用/禁用日志 |
| `Clear()` | 清空内存日志队列 |

### 6.2 HID 服务

#### `IHidService` 接口

**文件路径**: [`Services/Usb/IHidService.cs`](Services/Usb/IHidService.cs)

```csharp
interface IHidService : IDisposable
{
    IReadOnlyList<UsbDeviceInfo> ConnectedHidDevices { get; }
    event Action<UsbDeviceInfo, HidPedalData>? PedalDataReceived;
    event Action<UsbDeviceInfo, HidBaseData>? BaseDataReceived;
    bool IsRunning { get; }
    void Start();
    void Stop();
}
```

#### `HidService` — HID 设备服务

**文件路径**: [`Services/Usb/HidService.cs`](Services/Usb/HidService.cs)

**类**: `HidService : IHidService`

**内部类**: `HidChannel : IDisposable` — 单个 HID 设备通道

**HidChannel**:

| 属性 | 类型 | 说明 |
|---|---|---|
| `DeviceInfo` | `UsbDeviceInfo` | 关联的设备信息 |
| `DevicePath` | `string` | HID 设备路径 |
| `State` | `DeviceConnectionState` | 连接状态 |

| 方法 | 说明 |
|---|---|
| `Connect()` | 通过 `CreateFile` (GENERIC_READ) 打开 HID 设备 → 获取预解析数据 → 读取能力 (输入报告长度) |
| `Read()` → `byte[]?` | 通过 `ReadFile` 读取 HID 报告; 异常时设状态为 Error |
| `Dispose()` | 设状态 Disconnected → 关闭/释放句柄 |

**HidService 关键方法**:

- **`Start()`**: 启动后台轮询循环 (2秒间隔):
  - 调用 `DiscoverHidDevices()` 发现 HID 设备
  - 对每个设备检查 `DeviceRegistry` 是否匹配
  - 创建 `HidChannel` → 连接 → 启动独立 `ReadLoop`

- **`ReadLoop(HidChannel, DeviceType, CancellationToken)`**:
  - 每 5ms 读取一次
  - 将原始字节传给 `ProcessData`

- **`ProcessData(UsbDeviceInfo, DeviceType, byte[])`**: 数据分发:
  - Report ID `0x01` → `HidPedalData.Parse` → 触发 `PedalDataReceived`
  - Report ID `0x11` → `HidBaseData.Parse` → 触发 `BaseDataReceived`

- **`DiscoverHidDevices()`** → `List<(int vid, int pid, string path)>`: 通过 SetupAPI 枚举所有 HID 设备, 读取 VID/PID

#### `HidNative` — HID 原生 API (P/Invoke)

**文件路径**: [`Services/Usb/HidNative.cs`](Services/Usb/HidNative.cs)

**类**: `HidNative` (static, internal)

**职责**: Windows HID API 的 P/Invoke 声明。

**常量定义**:
- `GENERIC_READ (0x80000000)`, `GENERIC_WRITE (0x40000000)`
- `FILE_SHARE_READ (0x01)`, `FILE_SHARE_WRITE (0x02)`
- `OPEN_EXISTING (3)`, `FILE_FLAG_OVERLAPPED (0x40000000)`
- `DIGCF_PRESENT (0x02)`, `DIGCF_DEVICEINTERFACE (0x10)`

**结构体**:
- `SP_DEVICE_INTERFACE_DATA` — 设备接口数据
- `SP_DEVICE_INTERFACE_DETAIL_DATA` — 设备接口详细信息
- `HIDD_ATTRIBUTES` — HID 设备属性 (VID/PID/版本)
- `HIDP_CAPS` — HID 设备能力 (报告长度等)

**P/Invoke 函数**:

| 函数 | DLL | 说明 |
|---|---|---|
| `SetupDiGetClassDevs` | setupapi.dll | 获取指定类 GUID 的设备信息集 |
| `SetupDiEnumDeviceInterfaces` | setupapi.dll | 枚举设备接口 |
| `SetupDiGetDeviceInterfaceDetail` | setupapi.dll | 获取设备接口详情 |
| `SetupDiDestroyDeviceInfoList` | setupapi.dll | 销毁设备信息集 |
| `HidD_GetAttributes` | hid.dll | 获取 HID 设备属性 |
| `HidD_GetPreparsedData` | hid.dll | 获取预解析数据 |
| `HidD_FreePreparsedData` | hid.dll | 释放预解析数据 |
| `HidP_GetCaps` | hid.dll | 获取 HID 设备能力 |
| `CreateFile` | kernel32.dll | 打开设备句柄 |
| `ReadFile` | kernel32.dll | 读取设备数据 |
| `CloseHandle` | kernel32.dll | 关闭句柄 |

### 6.3 设备协议服务

**文件路径**: [`Services/Usb/DeviceProtocolService.cs`](Services/Usb/DeviceProtocolService.cs)

**类**: `DeviceProtocolService`

**职责**: 实现竞速模拟器外设的 USB 串口通信协议 (帧大小 64 字节)。

**常量**:

| 常量 | 值 | 说明 |
|---|---|---|
| `FrameSize` | 64 | 协议帧大小 |
| `DefaultResponseTimeoutMs` | 3000 | 默认响应超时 |
| `PresetNameMaxBytes` | 512 | 预设名称最大字节数 |
| `PresetNameChunkSize` | 56 | 预设名称每包字节数 |
| `CalibrationStart` | 1 | 校准开始标志 |
| `CalibrationComplete` | 2 | 校准完成标志 |

**静态数据**:
- `ColorIndexToRgb` (`byte[][]`) — 9 个 UI 颜色索引对应的 RGB 三元组数组

**命令构建方法 (静态)**:

| 方法 | 协议 | 说明 |
|---|---|---|
| `BuildGetDeviceInfoCommand(DeviceType)` | 0x8101 | 构建获取设备信息命令 [0x81, 0x01, 0x81, deviceType, 0x00...] |
| `BuildSetPedalParametersCommand(...)` | 0x2110 | 构建设置踏板参数命令 (方向/4点曲线/死区) |
| `BuildGetPedalParametersCommand()` | 0x8110 | 构建获取踏板参数命令 |
| `BuildPedalCalibrationCommand(byte,byte,byte)` | 0x21E1 | 构建踏板校准命令 |
| `BuildSetBaseParametersCommand(...)` | 0x2101 | 构建设置基座参数命令 (15个参数) |
| `BuildUpdateStartCommand(int)` | — | 构建固件更新开始帧 |
| `BuildFirmwareDataCommand(int,int,byte[])` | — | 构建固件数据包 |
| `BuildUpdateCompleteCommand(int)` | — | 构建固件更新完成帧 |
| `BuildSetWheelRpmBaseModeCommand(...)` | 0x2103 | 设置 RPM 基础模式 |
| `BuildGetWheelRpmBaseModeCommand()` | 0x8103 | 获取 RPM 基础模式 |
| `BuildSetWheelRpmIndicatorCommand(...)` | 0x2104 | 设置 RPM 指示灯 |
| `BuildGetWheelRpmIndicatorCommand()` | 0x8104 | 获取 RPM 指示灯 |
| `BuildSetWheelRpmModeCommand(...)` | 0x2105 | 设置 RPM 模式 |
| `BuildGetWheelRpmModeCommand()` | 0x8105 | 获取 RPM 模式 |
| `BuildSetWheelButtonLightCommand(...)` | 0x2107 | 设置按键灯 |
| `BuildGetWheelButtonLightCommand(byte)` | 0x8107 | 获取指定按键灯 |
| `BuildSetWheelSleepAndPaddleCommand(...)` | 0x2108 | 设置休眠和拨片 |
| `BuildGetWheelSleepAndPaddleCommand()` | 0x8108 | 获取休眠和拨片 |
| `BuildGetPresetNameCommand(DeviceType)` | 0x81D0 | 获取预设名称 |
| `BuildSetPresetNameCommand(DeviceType,byte[],int,int)` | 0x21D0 | 设置预设名称 (分包) |

**响应解析方法 (静态)**:

| 方法 | 说明 |
|---|---|
| `ParseDeviceInfoResponse(byte[])` → `DeviceInfoResponse?` | 解析设备信息响应 |
| `ParsePedalParametersResponse(byte[])` → `PedalParametersResponse?` | 解析踏板参数响应 |
| `ParseUpdateStartResponse(byte[], int)` → `int` | 解析更新开始响应 |
| `ParseFirmwareDataResponse(byte[], int)` → `int` | 解析固件数据响应 |
| `ParseUpdateCompleteResponse(byte[], int)` → `int` | 解析更新完成响应 |
| `ParseWheelRpmBaseModeResponse(byte[])` → `WheelRpmBaseModeResponse?` | 解析 RPM 基础模式响应 |
| `ParseWheelRpmIndicatorResponse(byte[])` → `WheelRpmIndicatorResponse?` | 解析 RPM 指示灯响应 |
| `ParseWheelRpmModeResponse(byte[])` → `WheelRpmModeResponse?` | 解析 RPM 模式响应 |
| `ParseWheelButtonLightResponse(byte[])` → `WheelButtonLightResponse?` | 解析按键灯响应 |
| `ParseWheelSleepAndPaddleResponse(byte[])` → `WheelSleepAndPaddleResponse?` | 解析休眠拨片响应 |
| `ParsePresetNameResponse(byte[])` → `PresetNameResponse?` | 解析预设名称响应 |

**实例方法**:

- **`SendCommandAsync(string deviceKey, byte[] command, int timeoutMs=3000)`** → `Task<byte[]?>`:
  1. 清除设备之前的待处理命令
  2. 通过 `_manager.SendToDevice` 发送命令
  3. 创建 `TaskCompletionSource<byte[]?>` 等待响应
  4. `Task.WhenAny` 等待响应或超时
  5. 返回响应字节数组或 null

- **`OnRawDataReceived(UsbDeviceInfo, byte[])`**: 接收数据处理:
  1. 优先检查是否是多包预设名称收集
  2. 否则匹配待处理命令, 完成 `TaskCompletionSource`

- **`GetPresetNameAsync(string deviceKey, DeviceType deviceType, int timeoutMs=3000)`** → `Task<string?>`:
  1. 发送获取预设名称命令
  2. 收集所有分包 (按 `TotalLength / ChunkSize` 计算数量)
  3. 排序 → 拼接 → UTF-8 解码
  4. 返回名称字符串或 null

- **`SetPresetName(string deviceKey, DeviceType deviceType, string name)`** → `bool`:
  1. 编码为 UTF-8
  2. 验证最大 512 字节
  3. 分割为 56 字节块
  4. 逐包发送
  5. 返回是否全部成功

- **`RgbToColorIndex(byte r, byte g, byte b)`** → `int`: 查找最接近的 UI 颜色索引 (0-8)

### 6.4 固件更新服务

**文件路径**: [`Services/Usb/FirmwareUpdateService.cs`](Services/Usb/FirmwareUpdateService.cs)

**类**: `FirmwareUpdateService`

**职责**: 协调固件下载、设备更新流程和进度报告。

**关键方法** (推断):

| 方法 | 说明 |
|---|---|
| `GetDeviceInfoAsync(string deviceKey, DeviceType)` | 发送获取设备信息命令并解析响应 |
| `StartFirmwareUpdateAsync(string deviceKey, DeviceType, byte[] firmwareData, IProgress<int>)` | 执行完整的固件更新流程: 发送开始命令 → 分包传输固件数据 → 发送完成命令 |
| `BuildFirmwareUpdateStartCommand(DeviceType)` | 构建更新开始命令 (基于设备类型) |

### 6.5 数据服务

#### `ApiClient` — HTTP API 客户端

**文件路径**: [`Services/Data/Api/ApiClient.cs`](Services/Data/Api/ApiClient.cs)

**类**: `ApiClient : IDisposable`

**职责**: 封装 HTTP 请求, 提供带重试机制的 GET 请求。

**`ApiResult<T>` 类**:

| 属性 | 说明 |
|---|---|
| `IsSuccess` | 是否成功 |
| `Data` | 反序列化的数据 (成功时) |
| `ErrorMessage` | 错误消息 (失败时) |
| `IsClientError` | 是否为 4xx 客户端错误 |
| `Success(T data)` (static) | 创建成功结果 |
| `Failure(string error, bool isClientError)` (static) | 创建失败结果 |

**构造参数**:
- `baseUrl` — API 基础 URL
- `apiToken` — Bearer 认证令牌
- `maxRetries` (默认 3) — 最大重试次数
- `retryDelayMs` (默认 500) — 重试基础延迟

**`GetAsync<T>(string endpoint, CancellationToken ct)`** → `Task<ApiResult<T>>`:
1. 发送 HTTP GET 请求
2. 4xx 客户端错误 → 立即返回失败 (不重试)
3. `TaskCanceledException` → 返回取消失败
4. `HttpRequestException` → 指数退避重试 (最多 `_maxRetries` 次)
5. 全部失败 → 返回带最后异常消息的失败结果
6. 成功 → 反序列化 JSON → 返回 `Success`

#### `GameApiService` — 游戏 API 服务

**文件路径**: [`Services/Data/Api/GameApiService.cs`](Services/Data/Api/GameApiService.cs)

**类**: `GameApiService`

**职责**: 管理游戏数据的三级缓存策略 (内存 → 本地文件 → 远程 API)。

**`GameDataState` 枚举**: `Loading`, `Loaded`, `Error`

**事件**: `StateChanged` (`Action<GameDataState>?`) — 数据状态变更通知

**缓存策略** (`GetGamesAsync`):
1. **不强制刷新时**: 返回内存缓存 (如有)
2. **无内存缓存时**: 返回本地磁盘缓存 + 触发后台刷新
3. **回退**: 从 API 获取并合并

**`FetchAndMergeAsync(bool skipIfUnchanged, CancellationToken ct)`**:
1. 通知 `Loading` 状态
2. 从 API 获取数据 → 转换
3. 错误时回退到内存缓存 → 本地缓存
4. 检测数据是否变更 (`HasChanges`)
5. 合并 API 数据与本地状态 (`MergeWithLocal` — 保留 `IsPinned`, `LaunchPath`, `LastLaunchTime`, `IsInstalled`)
6. 保存到本地缓存
7. 缓存图片 (`ImageCacheService.CacheAllAsync`)
8. 更新内存缓存
9. 通知 `Loaded` 状态

#### `BannerApiService` — 横幅 API 服务

**文件路径**: [`Services/Data/Api/BannerApiService.cs`](Services/Data/Api/BannerApiService.cs)

**类**: `BannerApiService`

**`GetBannersAsync(CancellationToken ct)`** → `Task<List<BannerItem>>`:
- 从 API 获取横幅数据
- 映射前 3 条为 `BannerItem` (拼接 `_mediaBaseUrl` 到图片 URL)
- 失败时返回空列表

#### `FirmwareApiService` — 固件 API 服务

**文件路径**: [`Services/Data/Api/FirmwareApiService.cs`](Services/Data/Api/FirmwareApiService.cs)

**类**: `FirmwareApiService`

**硬编码配置**:
- `BaseUrl`: `http://192.168.1.214:1337/api`
- `MediaBaseUrl`: `http://192.168.1.214:1337`

**关键方法**:

- **`GetFirmwareVersionsAsync(CancellationToken ct)`** → `Task<List<FirmwareVersionInfo>>`: 获取所有固件版本列表

- **`DownloadFirmwareAsync(string fileUrl, IProgress<int>? progress, CancellationToken ct)`** → `Task<byte[]?>`:
  - 创建临时 `HttpClient` (5 分钟超时)
  - 流式下载, 报告进度
  - 返回完整字节数组或 null

- **`FindFirmwareForDevice(List<FirmwareVersionInfo>, int vid, int pid)`** → `FirmwareVersionInfo?`: 按 VID/PID 匹配 (十六进制字符串比较, 不区分大小写)

#### `CacheService` — 通用内存缓存

**文件路径**: [`Services/Data/Cache/CacheService.cs`](Services/Data/Cache/CacheService.cs)

**类**: `CacheService`

**职责**: 基于 `ConcurrentDictionary` 的 TTL 过期缓存, 带定期清理。

**参数**:
- `defaultTtl` (默认 5 分钟)
- `cleanupIntervalMs` (默认 60000ms / 1 分钟)

| 方法 | 说明 |
|---|---|
| `Set<T>(string key, T value, TimeSpan? ttl)` | 添加/更新缓存项 |
| `TryGet<T>(string key, out T? value)` | 获取缓存值 (过期自动移除) |
| `Contains(string key)` | 检查键是否存在 |
| `Remove(string key)` | 移除缓存项 |
| `Clear()` | 清空全部缓存 |

#### `ImageCacheService` — 图片缓存服务

**文件路径**: [`Services/Data/Cache/ImageCacheService.cs`](Services/Data/Cache/ImageCacheService.cs)

**类**: `ImageCacheService` (static)

**缓存路径**: `%LOCALAPPDATA%/HITAPEX/images/`

| 方法 | 说明 |
|---|---|
| `CacheAllAsync(List<GameItem>)` | 并行下载所有游戏的封面和背景图 |
| `CacheImageAsync(int gameId, string type, string url, Action<string> setPath)` | 检查本地缓存 → 下载 → 写文件 → 更新 GameItem 路径 |
| `LocalPath(int gameId, string type)` | 返回本地文件路径 `{gameId}_{type}.jpg` |

#### `LocalGameCacheService` — 本地游戏缓存

**文件路径**: [`Services/Data/Cache/LocalGameCacheService.cs`](Services/Data/Cache/LocalGameCacheService.cs)

**类**: `LocalGameCacheService` (static)

**缓存路径**: `%LOCALAPPDATA%/HITAPEX/game_cache.json`

| 方法 | 说明 |
|---|---|
| `Save(List<GameItem>)` | JSON 序列化游戏列表到文件 |
| `Load()` → `List<GameItem>?` | 从文件反序列化; 文件不存在或解析失败返回 null |

#### `DataTransformer` — 数据转换器

**文件路径**: [`Services/Data/Transformation/DataTransformer.cs`](Services/Data/Transformation/DataTransformer.cs)

**类**: `DataTransformer`

| 方法 | 说明 |
|---|---|
| `TransformGame(GameApiDto)` → `GameItem` | DTO → 领域模型映射, 解析相对 URL |
| `TransformGames(IEnumerable<GameApiDto>)` → `List<GameItem>` | 批量转换 |
| `BuildFullUrl(string?)` → `string?` | 相对路径 → 绝对 URL (拼接 `_baseUrl`) |

#### `ApiResponses` — API 响应 DTO

**文件路径**: [`Services/Data/Models/ApiResponses.cs`](Services/Data/Models/ApiResponses.cs)

**类**:

| 类 | 属性 (均带 `[JsonPropertyName]`) |
|---|---|
| `ApiResponse<T>` | `Data` (T) |
| `GameApiDto` | `Id`, `Name`, `Description`, `CoverImage`, `BgImage`, `SteamId` |
| `BannerApiDto` | `Id`, `Url`, `Image` |
| `MediaAssetDto` | `Id`, `Name`, `Url` |

### 6.6 游戏相关服务

#### `GameDataService` — 游戏数据服务

**文件路径**: [`Services/Data/GameDataService.cs`](Services/Data/GameDataService.cs)

**类**: `GameDataService : IDisposable`

**职责**: 游戏数据的统一访问入口, 协调 API 获取、Steam 状态检测和数据缓存。

**关键方法**:

- **`GetGamesAsync(bool forceRefresh, CancellationToken ct)`** → `Task<List<GameItem>>`: 委托给 `_gameApi`

- **`EnrichWithInstallStatus(IList<GameItem>)`**:
  - 收集所有唯一 Steam ID
  - 调用 `_steamInstall.CheckInstalled` 查询安装状态
  - 更新 `IsInstalled`, `LaunchPath`, `LastLaunchTime`

- **`GetBannersAsync(CancellationToken ct)`** → `Task<List<BannerItem>>`: 委托给 `_bannerApi`

#### `GameLauncher` — 游戏启动器

**文件路径**: [`Services/GameLauncher.cs`](Services/GameLauncher.cs)

**类**: `GameLauncher` (static)

**`Launch(GameItem game)`** → `bool`:
- 检查 `game.IsInstalled` 和 `SteamId`
- 通过 `Process.Start("steam://run/{SteamId}")` 启动 (UseShellExecute = true)
- 成功时更新 `LastLaunchTime`
- 异常或前置条件不满足时返回 false

#### `SteamInstallService` — Steam 安装检测

**文件路径**: [`Services/SteamInstallService.cs`](Services/SteamInstallService.cs)

**类**: `SteamInstallService`

**`SteamInstallInfo` 类**:
- `IsInstalled` (bool)
- `InstallDir` (string?)
- `LibraryPath` (string?)
- `LastPlayed` (DateTime?)

**关键方法**:

- **`CheckInstalled(IEnumerable<string> steamIds)`** → `Dictionary<string, SteamInstallInfo>`:
  - 获取所有 Steam 库路径
  - 搜索 `appmanifest_{steamId}.acf` 文件
  - 解析安装目录和最后游玩时间

- **`GetSteamLibraryPaths()`** → `List<string>`:
  1. 从注册表读取 Steam 安装路径 (`HKCU\Software\Valve\Steam\SteamPath`)
  2. 解析 `libraryfolders.vdf` 获取额外库路径

- **`GetSteamPath()`** (static) → `string?`: 从注册表读取 Steam 路径

- **`ParseLibraryFoldersVdf(string)`** (static) → `List<string>`: 逐行解析 VDF, 提取 `"path"` 条目

- **`FindGameManifest(string steamId, List<string> libraries)`** → `SteamInstallInfo`: 在库路径中搜索 ACF 文件

- **`ParseManifestInstallDir(string)`** (static) → `string?`: 从 ACF 解析 `"installdir"`

- **`ParseManifestTimestamp(string)`** (static) → `DateTime?`: 从 ACF 解析 `"LastPlayed"` (Unix 时间戳)

### 6.7 预设管理服务

**文件路径**: [`Services/PresetService.cs`](Services/PresetService.cs)

**类**: `PresetService`

**职责**: 官方/个人预设的 JSON 文件读写管理。

**文件路径**:
- 官方预设: `{AppContext.BaseDirectory}/Assets/Presets/official_presets.json`
- 个人预设: `%LOCALAPPDATA%/HITAPEX/Presets/personal.json`

**关键方法**:

| 方法 | 返回值 | 说明 |
|---|---|---|
| `LoadOfficialPresets()` | `List<PresetItem>` | 加载官方预设, 标记 `IsPersonal = false` |
| `LoadOfficialPresets(DeviceType?)` | `List<PresetItem>` | 按设备类型过滤官方预设 |
| `LoadPersonalPresets()` | `List<PresetItem>` | 加载个人预设, 标记 `IsPersonal = true` |
| `LoadPersonalPresets(DeviceType?)` | `List<PresetItem>` | 按设备类型过滤个人预设 |
| `SavePersonalPresets(List<PresetItem>, DeviceType)` | `void` | 合并指定类型预设与其他类型 → 写入磁盘 |
| `SavePersonalPresets(List<PresetItem>)` | `void` | 覆写整个个人预设文件 |
| `ExportPreset(PresetItem, string)` | `void` | 导出单个预设到指定文件 |
| `ImportPreset(string)` | `PresetItem?` | 从文件反序列化预设; 失败时返回 null |

---

## 7. Views 层

### 7.1 主窗口

#### `MainWindow.xaml`

**文件路径**: [`MainWindow.xaml`](MainWindow.xaml)

**窗口属性**:
- 标题: "HITAPEX Racing Simulator"
- 尺寸: 1500×950
- 无边框 (`WindowStyle="None"`, `AllowsTransparency="True"`)
- 居中显示 (`WindowStartupLocation="CenterScreen"`)
- 禁止调整大小 (`ResizeMode="NoResize"`)
- DataContext: 设计时 `MainWindowViewModel` 实例

**UI 结构**:

```
Window (1500×950)
├── 背景层
│   ├── 纯色 #0B0B0B
│   ├── 渐变叠加 #0B0B0B → #353535
│   └── SVG 装饰图案 (Group126548063.svg)
├── 标题栏 (Row 0, Height 26)
│   ├── 背景 #161616
│   ├── "HITAPEX V 1.0.0" (Orbitron 字体)
│   ├── 最小化按钮 (横线图标)
│   └── 关闭按钮 (X 图标)
├── 内容区 (Row 1, 两列)
│   ├── 左侧导航栏 (158px)
│   │   ├── 半透明背景 #80161616
│   │   ├── ItemsControl → NavigationItems (RadioButton 列表)
│   │   └── 用户面板: 头像/绿色状态指示/用户名 "Alex_Racer"
│   ├── 分隔线 (1px, #383838)
│   └── 主内容区: ContentControl {Binding CurrentView}
└── 模态层: ModalDialog (Panel.ZIndex=1000)
```

**XAML 资源样式**:

| 样式 | 目标 | 特性 |
|---|---|---|
| `BooleanToVisibilityConverter` | 通用 | 标准 Bool→Visibility 转换 |
| `NavButtonStyle` | RadioButton | 44px高, 选中时红色左边条(#C60E0E)+渐变背景, SVG 图标+文字标签 |
| `TitleBarButtonStyle` | Button | 46×26px, 前景 #8892A0, 悬停背景 #1A2535 |
| `CloseButtonStyle` | Button | 继承 TitleBarButtonStyle, 悬停时红色 (#E81123) |

#### `MainWindow.xaml.cs`

**文件路径**: [`MainWindow.xaml.cs`](MainWindow.xaml.cs)

**类**: `MainWindow : Window`

**字段**:

| 字段 | 类型 | 说明 |
|---|---|---|
| `_viewModel` | `MainWindowViewModel` | MVVM ViewModel |
| `_presetListPopups` | `Dictionary<DeviceType, PresetListPopup>` | 各设备类型的预设列表弹窗 |
| `_trayIcon` | `TrayIcon?` | 系统托盘图标 |
| `_isCheckingUnsavedNavigation` | `bool` | 防重入导航检查标志 |

**属性**:
- `GlobalDialogControl` → `ModalDialog`: 获取全局模态对话框实例

**预设弹窗管理**:
- `GetPresetListPopup(DeviceType)` → `PresetListPopup?`: 获取已创建的弹窗
- `ShowPresetListPopup(DeviceType)`: 创建 (如需要) 并显示预设列表弹窗

**窗口操作**:
- `TitleBar_MouseLeftButtonDown`: 双击切换最大化/还原, 单击拖动
- `MinimizeButton_Click`: 最小化窗口
- `CloseButton_Click`: 关闭或最小化到托盘 (取决于设置)

**系统托盘**:
- `InitializeTrayIcon()`: 创建托盘图标, 关联双击恢复/退出事件, 注册 `Closing` 事件
- `MinimizeToTray()`: 隐藏窗口, 显示托盘图标
- `RestoreFromTray()`: 显示窗口, 激活
- `ExitApplication()`: 释放托盘图标, 退出应用

**导航守卫** (`NavigationItem_Checked`):
1. 检查 `_isCheckingUnsavedNavigation` 防重入
2. 如果当前视图是 `DeviceUserControl`:
   - 检查 `PedalControl.HasUnsavedChanges`
   - 检查 `SteeringWheelControl.HasUnsavedChanges`
   - 检查 `BaseControl.HasUnsavedChanges`
3. 如有未保存更改 → `ShowUnsavedDialog`:
   - "保存": 保存后切换
   - "不保存": 直接切换
   - "取消": 不切换
4. 无未保存更改 → 直接切换

### 7.2 主导航视图

#### `HomeUserControl` — 首页仪表盘

**文件路径**: [`Views/HomeUserControl.xaml`](Views/HomeUserControl.xaml) / [`.xaml.cs`](Views/HomeUserControl.xaml.cs)

**UI 布局**:
```
UserControl
├── Row 0: 横幅轮播 (Canvas + 3张幻灯片 + 指示点)
├── Row 1: 设备预览区
│   ├── 基座卡片: 力矩表 (圆弧+三角), 温度条 (15段)
│   ├── 方向盘卡片: 34段可视化 + 旋转动画
│   ├── 踏板卡片: 3个垂直进度条 (离合/刹车/油门)
│   └── 换挡器卡片: "设备未连接" 占位
└── Row 2: 快速启动游戏列表 (水平滚动)
```

**关键技术实现**:
- **横幅轮播**: 5秒自动播放, 淡入淡出过渡动画, 鼠标悬停暂停
- **力矩表**: 三角学计算圆弧路径 (中心 71.5,71.5, 半径 60, 270度扫描, 起始角135度)
- **温度条**: 15段颜色渐变 (绿→黄→红), 模拟振荡 0-120度
- **方向盘**: 34个 `Rectangle` 段排列在 `Canvas` 上, `RotateTransform` 动画 (-900° ~ +900°)
- **踏板条**: 高度动画, 随机游走模拟
- **游戏列表**: FLIP 动画, 自定义滚动条, 卡片悬停偏移

#### `DeviceUserControl` — 设备管理页

**文件路径**: [`Views/DeviceUserControl.xaml`](Views/DeviceUserControl.xaml) / [`.xaml.cs`](Views/DeviceUserControl.xaml.cs)

**UI 布局**:
```
UserControl
├── 左侧标签栏 (StackPanel)
│   ├── 基座 (Base) RadioButton
│   ├── 方向盘 (SteeringWheel) RadioButton
│   └── 踏板 (Pedal) RadioButton
├── 装饰分隔符 (Path 图形)
└── 右侧内容区 (ContentControl → 子控件交换)
```

**导航逻辑**:
- 键盘快捷键: `1/2/3` 或 `NumPad1-3` 切换标签, `Up/Down` 导航
- 交叉淡入淡出动画 (`FadeOutAnimation` → `FadeInAnimation`)
- 切换前检查未保存更改 (`ShowUnsavedDialog`)

#### `GameUserControl` — 游戏库

**文件路径**: [`Views/GameUserControl.xaml`](Views/GameUserControl.xaml) / [`.xaml.cs`](Views/GameUserControl.xaml.cs)

**UI 布局**:
```
UserControl
├── Row 0: 游戏详情 (标题/描述/启动按钮/启动模式)
├── Row 1: TabControl
│   ├── Tab "设备配置": 4个预设卡片 (基座/方向盘/踏板/换挡器)
│   └── Tab "遥测支持": 26个遥测参数状态表
└── Row 2: 游戏卡片列表
    ├── 过滤器 (全部/已安装/未安装)
    └── 水平滚动 ItemsControl → GameItem 卡片
```

**关键功能**:
- 游戏库加载 (三级缓存策略)
- Steam/自定义路径启动
- 游戏置顶 (FLIP 动画)
- 自定义水平滚动条
- 卡片悬停偏移 (TranslateTransform 8.12px)
- 遥测数据模拟动画

#### `HelpUserControl` — 帮助中心

**文件路径**: [`Views/HelpUserControl.xaml`](Views/HelpUserControl.xaml) / [`.xaml.cs`](Views/HelpUserControl.xaml.cs)

纯静态内容页面, 包含 5 个 FAQ 条目 (带箭头指示器)。

#### `SettingsUserControl` — 设置页

**文件路径**: [`Views/SettingsUserControl.xaml`](Views/SettingsUserControl.xaml) / [`.xaml.cs`](Views/SettingsUserControl.xaml.cs)

**UI 布局**:
```
UserControl
├── 系统设置 Tab
│   ├── 软件设置: 开机自启/启动最小化/关闭最小化 CheckBox
│   ├── 语言: ComboBox (zh-CN/en-US/ja-JP)
│   ├── 主题: ComboBox (暗夜红/亮色/系统)
│   ├── 版本更新: 检查更新按钮 + 进度动画
│   ├── 关于我们: 社交媒体按钮 + 二维码
│   └── 版权声明
└── 固件更新 Tab
    ├── 检查更新按钮
    ├── 设备固件列表 (DeviceType/型号/序列号/版本/状态/更新)
    └── 更新进度 UI
```

**关键功能**:
- Windows 注册表读写 (开机自启)
- 版本检查模拟 (1.5s 延迟 → 进度动画)
- 固件更新完整流程: 查询设备 → 获取固件列表 → 下载 → 刷写
- 语言/主题切换 → 重启提示对话框

### 7.3 设备参数控件

#### `BaseParameterControl` — 基座参数控制

**文件路径**: [`Views/DeviceParameters/BaseParameterControl.xaml`](Views/DeviceParameters/BaseParameterControl.xaml) / [`.xaml.cs`](Views/DeviceParameters/BaseParameterControl.xaml.cs)

**UI**: 设备信息 + 预设管理 + 空参数区 (占位/待实现)

**关键方法**:
- `RefreshDeviceInfoAsync()` — 查找连接的基座设备, 获取设备信息和固件版本
- `FetchPresetNameAsync()` — 从设备读取当前预设名称
- `SendPresetName()` — 向设备发送预设名称
- `UpdateConnectionStatusDisplay()` — 更新设备名称/连接状态/固件版本, 更新 7 个连接图路径颜色
- `CheckFirmwareVersionAsync()` — 比较设备固件 vs API 固件, 显示"新版本可用"
- `ShowUnsavedDialog()` — 显示保存/不保存/取消对话框 (个人预设) 或 另存为/不保存/取消 (非个人预设)
- `UndoButton_Click` / `SaveButton_Click` / `SaveAsButton_Click` / `ExportButton_Click` — 预设操作
- `OnPresetApplied()` — 处理预设应用事件

#### `PedalParameterControl` — 踏板参数控制

**文件路径**: [`Views/DeviceParameters/PedalParameterControl.xaml`](Views/DeviceParameters/PedalParameterControl.xaml) (1690 行) / [`.xaml.cs`](Views/DeviceParameters/PedalParameterControl.xaml.cs) (2319 行)

**UI 布局**:
```
UserControl
├── 设备信息区 (同 BaseParameterControl)
├── 预设管理区
├── 三段曲线编辑区 (离合/刹车/油门)
│   ├── 轴图标 + 标题
│   ├── 反向切换开关
│   ├── 5 种曲线类型选择 (线性/凸/凹/S曲线/自定义)
│   │   └── Canvas 坐标系 (345×266)
│   │       ├── 虚线网格
│   │       ├── X/Y 轴
│   │       ├── 渐变填充区域
│   │       ├── 贝塞尔曲线 (Fritsch-Carlson 单调三次插值)
│   │       └── 4 个可拖动控制点
│   ├── 死区双拇指滑块
│   ├── 当前位置显示 (%)
│   └── 双进度条 (处理后值 + 原始值)
└── 校准按钮
```

**核心技术 - 曲线算法**:
- **Fritsch-Carlson 单调三次 Hermite 插值** (`ComputeMonotonicSlopes`):
  - 输入: 控制点数组
  - 输出: 保单调的三次样条曲线
  - 算法: 计算初始割线斜率 → 检测单调性 → 必要时限制斜率 → 生成贝塞尔段
- **`CreateSmoothCurveGeometry()`**: 将曲线点转换为 `PathGeometry` (贝塞尔曲线)
- **`CreateSmoothFillGeometry()`**: 生成曲线下方的填充区域几何
- **`ApplyCurveTransform()`**: 将原始踏板位置 (0-100%) 映射到处理后输出
- **`RebuildCurveCaches()`**: 构建线程安全的 `Point[]`/`double[]` 副本供后台线程使用

**HID 实时数据管道**:
1. 订阅 `App.HidService.PedalDataReceived`
2. 后台线程通过缓存曲线处理原始值
3. UI 更新在 Render 优先级通过 Dispatcher 调度
4. 变化检测 (阈值 >0.05) + 联锁队列防溢出

**USB 通信**:
- `SendPedalParameters()` — 构建并发送离合/刹车/油门配置
- `FetchPedalParametersAsync()` — 请求设备参数 → 应用到 UI
- `RefreshDeviceInfoAsync()` — 检测直连踏板或通过基座连接

**预设 CRUD**:
- 捕获当前状态 → `PedalPresetSnapshot`
- 从快照恢复到 UI
- 保存到个人预设 (带重试逻辑)
- `SaveAsInternal()` → 打开 `EditPresetPopup` 命名 → 保存

**校准集成**:
- `CalibrationButton_Click()` → 打开 `CalibrationDialog`
- 订阅 `StartCalibrationRequested` / `CompleteRequested` / `CloseRequested` 事件
- `SendPedalCalibration()` — 发送校准开始/完成命令

#### `SteeringWheelParameterControl` — 方向盘参数控制

**文件路径**: [`Views/DeviceParameters/SteeringWheelParameterControl.xaml`](Views/DeviceParameters/SteeringWheelParameterControl.xaml) (1854 行) / [`.xaml.cs`](Views/DeviceParameters/SteeringWheelParameterControl.xaml.cs) (2168 行)

**UI 布局**:
```
UserControl
├── 背景层 (NeoX.png + 渐变透明度蒙版)
├── 设备信息区
├── 预设管理区
├── Tab 栏: "按键 & 转速灯" / "拨片"
├── 主内容区
│   ├── 按键内容面板:
│   │   ├── Canvas (方向盘图片上的 19 个圆形按键 B1-B19)
│   │   ├── RPM 设置触发区
│   │   └── 按键响应名称显示
│   └── 拨片内容面板:
│       ├── 离合器模式选择 (合成轴/独立轴/按键)
│       ├── 离合器咬合点滑块
│       └── 左右拨片校准面板
└── 右侧全局设置面板:
    ├── 全局按键颜色 (8 个颜色块)
    ├── 按键颜色启用开关
    ├── 显示按键编号开关
    ├── 按键亮度滑块
    ├── RPM 亮度滑块
    ├── 休眠灯时间 ComboBox
    ├── 待机效果 ComboBox
    ├── 全局闪烁速度滑块
    └── 遥测播放/暂停按钮
```

**按键状态** (19 按钮, 每按钮 6 个属性):
- `_buttonColors[int]` — 颜色索引
- `_buttonTelemetryEnabled[bool]` — 遥测启用
- `_buttonTelemetryLightEffect[int]` — 遥测灯效果
- `_buttonTelemetryFunc[int]` — 遥测功能
- `_buttonTelemetryTriggerColor[int]` — 遥测触发颜色
- `_buttonSpeeds[int]` — 速度

**RPM 状态** (12 LED):
- `_rpmColors[int]` — 颜色索引
- `_rpmValues[double]` — 触发值
- `_rpmCapValue`, `_rpmCurveType`, `_rpmDisplayMode`, `_rpmLightMode` 等

**设备通信** (5 组协议并行获取/发送):
- `FetchWheelParametersAsync()` — 并行获取 RPM基础模式/RPM指示灯/RPM模式/按键灯/休眠拨片
- `FetchButtonLightAsync()` — 读取 LED 模式 (统一/独立), 逐个获取 19 个按键灯配置
- `SendWheelParameters(WheelSendMask)` — 按掩码发送参数子集, 协议值映射
- `SendWheelButtonLight()` — 统一模式单命令; 独立模式逐按键发送 (含 `_singleButtonIndex` 优化)

**UI 交互**:
- `KeyButton_Checked()` → 打开 `ButtonSettingsPopup`
- `RpmSettingsTrigger_Click()` → 打开 `RpmSettingsPopup`
- `BrightnessSlider_DragCompleted` → 延迟发送 (仅松手后)
- `ClutchPointThumb_Mouse*` → 拖拽离合器咬合点

**预设快照**: `CaptureCurrentParameters()` / `ApplyPresetSnapshot(WheelPresetSnapshot)`

### 7.4 弹窗控件

#### `PresetListPopup` — 预设列表弹窗

**文件路径**: [`Views/DeviceParameters/PresetListPopup.xaml`](Views/DeviceParameters/PresetListPopup.xaml) / [`.xaml.cs`](Views/DeviceParameters/PresetListPopup.xaml.cs) (1354 行)

**UI 布局**:
```
UserControl (半透明遮罩 + 右侧 540px 面板)
├── 标题 "预设列表"
├── 游戏分类 ComboBox (可筛选/搜索)
├── 官方/个人 Tab 按钮
├── 预设列表 ItemsControl
│   ├── 官方预设项: 旗帜图标 + 名称
│   └── 个人预设项: 旗帜图标 + 名称 + 游戏标签 + 操作按钮
└── 导入/应用按钮
```

**关键功能**:
- 动态 ComboBox 筛选 (编辑模式覆盖 DropDown, 键盘导航 Up/Down/Enter/Escape)
- 预设项完全代码构建 (非 DataTemplate)
- 共享详情 Popup (悬停显示名称/游戏标签, 500ms 延迟, 可取消)
- 自动尺寸多边形背景 (倒角, 代码动态测量)
- 游戏标签超过显示区域修剪
- 幻灯片动画 (从右侧滑入 300ms, 滑出 260ms)
- Toast 通知 (成功提示, 自动消失)

**内部类 `PresetItem`**:
- `Name`, `Description`, `Category`, `ItemCount`
- `Games` (List<string>)
- `Parameters` (PedalPresetSnapshot?)
- `WheelParameters` (WheelPresetSnapshot?)
- `IsPersonal` (bool), `DeviceType`

#### `EditPresetPopup` — 编辑预设弹窗

**文件路径**: [`Views/DeviceParameters/EditPresetPopup.xaml`](Views/DeviceParameters/EditPresetPopup.xaml) / [`.xaml.cs`](Views/DeviceParameters/EditPresetPopup.xaml.cs) (620 行)

**UI 布局**:
```
UserControl (遮罩 + 居中 872×639 面板)
├── 标题 "编辑"
├── 预设名称输入 (TextBox + 水印 + 重复名称警告 + 字符计数 0/20)
└── 双列游戏选择区
    ├── 左侧: 全部游戏 (A-Z 字母索引 + 分组列表 + 全选复选框)
    └── 右侧: 已选游戏 (红色标记 + 名称 + X 移除按钮)
```

**关键功能**:
- A-Z 字母索引 (点击滚到对应分组, 滚动联动高亮)
- 名称验证: 20 字符限制 (支持 IME), 重复检测
- 缩放 + 淡入动画 (0.94→1, 260ms)
- `BeginEdit(PresetItem, IEnumerable<string>)` / `BeginSaveAs(IEnumerable<string>)` 双入口

#### `CalibrationDialog` — 校准对话框

**文件路径**: [`Views/DeviceParameters/CalibrationDialog.xaml`](Views/DeviceParameters/CalibrationDialog.xaml) / [`.xaml.cs`](Views/DeviceParameters/CalibrationDialog.xaml.cs) (170 行)

**UI 布局**:
```
UserControl (遮罩 + 居中 655×386 面板)
├── 标题 "校准"
├── 说明文字 + "开始校准" 按钮
├── 分隔线
├── 3 个进度条 (离合/刹车/油门)
│   └── 双列进度 (绿色/红色 + 锯齿图案覆盖)
└── "完成" 按钮 (初始禁用)
```

**方法**:
- `UpdateClutchProgress(double)` / `UpdateBrakeProgress(double)` / `UpdateThrottleProgress(double)` — 更新进度条 (星单位 GridLength 列 + 百分比文本)
- `ResetState()` — 重置所有状态
- `SetStartButtonDisabled()` — 启用完成按钮

**事件**: `StartCalibrationRequested`, `CompleteRequested`, `CloseRequested`

#### `ButtonSettingsPopup` — 按键灯设置弹窗

**文件路径**: [`Views/DeviceParameters/ButtonSettingsPopup.xaml`](Views/DeviceParameters/ButtonSettingsPopup.xaml) / [`.xaml.cs`](Views/DeviceParameters/ButtonSettingsPopup.xaml.cs) (264 行)

**UI 布局**:
```
UserControl (遮罩 + 居中 455×639 面板)
├── 标题 "按键灯设置"
├── 选中按键名称 (红色)
├── 按键灯颜色: 9 个颜色 RadioButton
├── 遥测功能: 开关 + 功能 ComboBox (7个遥测功能)
├── 遥测灯效果: ComboBox (常亮/闪烁)
├── 遥测触发颜色: 9 个颜色 RadioButton (条件性灰色)
├── 速度: 分段滑块 (条件性灰色)
└── 确认/取消按钮
```

**条件性 UI 掩码**: 遥测关闭时, 触发颜色和速度滑块变灰 (OpacityMask + IsHitTestVisible)。

**`UpdateSpeedSliderFill()`**: 离散步骤值映射到梯度停止点 ([0, 0.2063, 0.4091, 0.6084, 0.8112, 1.0])。

#### `RpmSettingsPopup` — RPM 灯设置弹窗

**文件路径**: [`Views/DeviceParameters/RpmSettingsPopup.xaml`](Views/DeviceParameters/RpmSettingsPopup.xaml) (1724 行) / [`.xaml.cs`](Views/DeviceParameters/RpmSettingsPopup.xaml.cs) (581 行)

**UI 布局**:
```
UserControl (遮罩 + 居中 1047×704 宽面板)
├── 标题 "转速灯设置"
├── 左侧主列:
│   ├── 12 色块条 (ColorBlock1-12, 左右斜切+中间矩形)
│   ├── 12 个垂直滑块 (配色渐变背景)
│   ├── 虚线帽线 (可拖动三角指示器)
│   ├── 12 个百分比值显示
│   └── 曲线类型选择 (线性/凸/凹/自定义) + 显示模式 (百分比/RPM)
└── 右侧设置列 (可滚动):
    ├── RPM 灯颜色: 9 个颜色 RadioButton
    ├── 遥测模式: 开关
    ├── 灯模式: ComboBox (顺序/扩散/汇聚)
    ├── 频闪灯: ComboBox (同RPM颜色/自定义/关闭)
    ├── 频闪颜色: 8 个颜色 RadioButton (条件性遮罩)
    ├── 速度滑块 (条件性遮罩)
    ├── 基础灯: ComboBox (常亮/呼吸/颜色循环)
    └── 基础灯速度滑块 (条件性遮罩)
```

**关键功能**:
- **双向颜色同步**: 左侧色块选中 → 右侧灯颜色同步; 右侧颜色选择 → 左侧色块更新 + 滑块渐变重新生成
- **帽线交互**: 拖动虚线设置最大 RPM 阈值, 所有滑块钳制到帽值
- **条件性 UI 掩码**: 频闪模式 (0=自动, 1=自定义, 2=关闭) 控制右侧面板遮罩; 基础灯模式 (常亮时强制速度为 0)
- **`CreateGradient(Color)`**: 基于已知颜色值生成 `LinearGradientBrush` (含硬编码深色变体)
- **`LoadSettings(...)`**: 13 参数加载; getter 方法返回所有设置

---

## 8. Controls 控件

### `ModalDialog`

**文件路径**: [`Controls/ModalDialog.xaml`](Controls/ModalDialog.xaml) / [`.xaml.cs`](Controls/ModalDialog.xaml.cs)

**类**: `ModalDialog : UserControl`

**职责**: 全局可复用的模态对话框, 支持动态标题、图标、内容和按钮配置。

**依赖属性**:

| 属性 | 类型 | 说明 |
|---|---|---|
| `Title` | `string` | 对话框标题 |
| `DialogContent` | `object` | 自定义内容 |
| `ShowIcon` | `bool` | 是否显示图标 |

**方法**:

- **`AddButton(string text, RoutedEventHandler handler, bool isPrimary)`**:
  - 代码动态创建 `Button`
  - 主按钮: 多边形模板 (六边形 Path) + 渐变红色填充
  - 附加到水平 `ButtonPanel`

- **`ClearButtons()`**: 移除 `ButtonPanel` 所有子元素

- **`Show()` / `Hide()`**:
  - `Show()`: 设置 `Visibility.Visible`
  - `Hide()`: 重置 Title/DialogContent/ShowIcon/清空按钮后隐藏

### `SkipInkTextBlock`

**文件路径**: [`Controls/SkipInkTextBlock.cs`](Controls/SkipInkTextBlock.cs)

**类**: `SkipInkTextBlock : FrameworkElement`

**职责**: 自定义 WPF 元素, 渲染带 "跳过墨水" 下划线的文本 (下划线在文字笔画处断开, 类似现代浏览器 CSS `text-decoration-skip-ink`)。

**依赖属性** (9 个):

| 属性 | 类型 |
|---|---|
| `Text` | `string` |
| `Foreground` | `Brush` |
| `UnderlineBrush` | `Brush` |
| `Background` | `Brush` |
| `FontSize` | `double` |
| `FontWeight` | `FontWeight` |
| `FontFamily` | `FontFamily` |
| `FontStyle` | `FontStyle` |
| `TextTrimming` | `TextTrimming` |

**渲染流程** (`OnRender`):
1. 绘制背景
2. 构建 `FormattedText` 文字几何
3. 创建加宽的 "保护" 几何 (文字笔画的扩展)
4. 通过 `Geometry.Combine(Exclude)` 从下划线条中减去保护几何 → 实现笔画处断线
5. 绘制跳过墨水的下划线 → 再绘制文字 (上层)

---

## 9. Helpers 工具类

### `TrayIcon`

**文件路径**: [`Helpers/TrayIcon.cs`](Helpers/TrayIcon.cs)

**类**: `TrayIcon : IDisposable`

**职责**: Windows 系统托盘通知图标的 Win32 包装器。

**事件**:
- `DoubleClick` (`Action?`) — 双击托盘图标
- `ExitRequested` (`Action?`) — 右键菜单 "退出"

**方法**:

| 方法 | 说明 |
|---|---|
| `SetIcon(Icon)` | 修改图标 |
| `SetTooltip(string)` | 修改提示文本 |
| `ShowBalloonTip(string title, string text)` | 显示气泡通知 |
| `ShowNativeContextMenu()` | 显示 Win32 右键弹出菜单 (显示主窗口/退出) |

**Win32 P/Invoke**:
- `Shell_NotifyIcon` (shell32.dll) — 添加/删除/修改托盘图标
- `CreatePopupMenu` / `AppendMenu` / `TrackPopupMenu` / `DestroyMenu` / `SetForegroundWindow` / `GetCursorPos` (user32.dll/kernel32.dll)

**生命周期**:
- 构造函数: 提取默认图标 → 添加 `WndProc` 钩子 → 调用 `AddIcon()`
- `Dispose()`: 删除图标 → 移除钩子 → 释放图标资源

---

## 10. XAML UI 结构详解

### 10.1 全局样式体系

**位置**: [`App.xaml`](App.xaml)

```
Application.Resources
├── ResourceDictionary.MergedDictionaries
│   └── FluentWPF/Styles/Controls.xaml (Fluent Design 基础)
├── Style x:Key="ScrollBarStyle" (TargetType=ScrollBar)
│   └── 深色 #0D1117, 宽 6px
├── Style x:Key="ProgressBarStyle" (TargetType=ProgressBar)
│   └── 高 6px, #0D1B2A 背景, Radius 3 圆角
│   └── ControlTemplate: PART_Track + PART_Indicator
└── FontFamily x:Key="OrbitronFont"
    └── ./Assets/Fonts/#Orbitron
```

### 10.2 按钮样式体系

| 样式名 | 用途 | 视觉特征 |
|---|---|---|
| `NavButtonStyle` | 侧边栏导航 | 平行四边形红色左边条, 渐变背景, SVG 图标 |
| `TitleBarButtonStyle` | 标题栏按钮 | 46×26px, #8892A0 前景 |
| `CloseButtonStyle` | 关闭按钮 | 继承 TitleBar, 悬停红色 #E81123 |
| `ActionButtonStyle` | 设备页操作按钮 | 渐变路径, 红色主题 |
| `PrimaryButtonStyle` | 主要操作按钮 | 红色按钮 + 悬停/按下状态 |
| `UpdateButtonStyle` | 固件更新按钮 | 进度填充, ProgressClipTransform |
| `LinkButtonStyle` | 链接文字按钮 | 下划线文字 |
| `SmallButtonStyle` | 方向盘小圆按钮 | 圆形 + 径向辉光 + 红色外环 |
| `NavIconButtonStyle` | 设备页标签 | 平行四边形路径, 选中红色高亮 |
| `SettingsTabButtonStyle` | 设置页标签 | 独特路径 + 渐变填充 |
| `TabButtonStyle` | 预设列表标签 | 纯文字 + 下划线指示器 |
| `TextButtonStyle` | 文字按钮 | 简洁文字样式 |

### 10.3 滑块样式体系

| 样式名 | 用途 | 特征 |
|---|---|---|
| `BrightnessSliderStyle` | 亮度调节 | 渐变轨道 |
| `SpeedSliderStyle` | 速度分段滑块 | 锯齿状分段轨道 + 三角拇指 |
| `VerticalRpmSliderStyle` | RPM 垂直滑块 | 圆形拇指 + 渐变背景 |
| `PopupSpeedSliderStyle` | 弹窗速度滑块 | 6 步分段, TrackFillBrush 梯度停止点 |

### 10.4 ComboBox 样式体系

| 样式名 | 用途 | 特征 |
|---|---|---|
| `SteeringComboBoxStyle` | 方向盘设置 | 自定义下拉, 6px 斜接角 |
| `TechPresetComboBoxStyle` | 预设选择 | 预设卡片内组合框 |
| `PresetComboBoxStyle` | 预设列表筛选 | 带搜索/筛选 TextBox |
| `PopupComboBoxStyle` | 弹窗设置 | 自定义下拉, 斜接角 |

### 10.5 CheckBox/RadioButton 样式体系

| 样式名 | 用途 |
|---|---|
| `DualStateSvgToggleStyle` | SVG 切换开关 (动画拇指) |
| `FilterRadioButtonStyle` | 游戏筛选标签 (平行四边形路径) |
| `ColorBlockRadioStyle` | 颜色块选择 (平行四边形色块) |
| `CurveTypeRadioStyle` | 曲线类型选择 |
| `PaddleModeRadioStyle` | 拨片模式选择 |
| `SelectCheckBoxStyle` | 预设编辑多选 (平行四边形复选框) |

### 10.6 数据绑定关系

| 绑定路径 | 目标 | 控件 |
|---|---|---|
| `{Binding CurrentView}` | `ContentControl.Content` | 主内容区视图切换 |
| `{Binding NavigationItems}` | `ItemsControl.ItemsSource` | 侧边栏导航列表 |
| `{Binding CoverImageUrl}` | `Image.Source` | 游戏卡片封面 |
| `{Binding Name}` | `TextBlock.Text` | 游戏名称 |
| `{Binding IsPinned}` | `CheckBox.IsChecked` | 置顶状态 |
| `{Binding IsInstalled}` | Visibility | 安装状态指示 |
| `{Binding DeviceType}` | `TextBlock.Text` | 固件列表设备类型 |
| `{Binding Model}` | `TextBlock.Text` | 固件列表型号 |
| `{Binding SerialNumber}` | `TextBlock.Text` | 固件列表序列号 |
| `{Binding CurrentVersion}` | `TextBlock.Text` | 固件列表版本 |
| `{Binding Status}` | `TextBlock.Text` | 固件列表状态 |
| `{Binding ButtonBackground}` | `Button.Background` | 更新按钮背景 |

---

## 11. 设计模式与技术框架

### 11.1 设计模式

| 模式 | 应用位置 | 说明 |
|---|---|---|
| **MVVM** | 全局 | Model (`Models/`) - View (`Views/`) - ViewModel (`ViewModels/`), 通过 `ViewModelBase` + `RelayCommand` 实现 |
| **单例 (Singleton)** | `App.xaml.cs` | 全局服务对象 (`UsbManager`, `HidService`, `ProtocolService` 等静态属性) |
| **工厂 (Factory)** | `MainWindowViewModel.UpdateCurrentView()` | 根据导航项名称创建对应的 UserControl |
| **策略 (Strategy)** | `UsbDeviceDiscovery` | WMI 监控 (优先) / 轮询回退 (fallback) |
| **观察者 (Observer)** | 全局事件系统 | `DeviceConnected/Disconnected`, `RawDataReceived`, `PedalDataReceived`, `StateChanged` 等 |
| **命令 (Command)** | `RelayCommand`, 匿名 `ICommand` | View 按钮操作绑定到 ViewModel/Code-Behind 方法 |
| **外观 (Facade)** | `GameDataService` | 统一封装 `GameApiService` + `BannerApiService` + `SteamInstallService` |
| **代理 (Proxy)** | `ApiClient` | HTTP 请求代理, 封装重试/错误处理 |
| **装饰器 (Decorator)** | `CacheService` | 为数据访问添加 TTL 缓存层 |
| **建造者 (Builder)** | `DeviceProtocolService.Build*` | 协议帧构建方法族 |
| **快照 (Snapshot/Memento)** | `PedalPresetSnapshot`, `WheelPresetSnapshot` | 设备参数状态快照, 用于预设保存/恢复/撤销 |
| **模板方法 (Template Method)** | `ViewModelBase.SetProperty<T>()` | 属性变更的标准流程 (检查→赋值→通知) |

### 11.2 技术框架

| 框架/库 | 版本 | 应用场景 |
|---|---|---|
| **.NET 9 WPF** | 9.0.0 | 整个 UI 框架, XAML + 数据绑定 + 依赖属性 |
| **FluentWPF** | 0.10.2 | Fluent Design System 风格控件 (控件模板、丙烯酸效果) |
| **SharpVectors.Wpf** | 1.8.4.2 | SVG 图标渲染 (`SvgViewbox` 控件, SVG 转 WPF Drawing) |
| **System.IO.Ports** | 10.0.8 | USB 串口通信 (`SerialPort` 类) |
| **System.Management** | 10.0.8 | WMI 查询设备发现 (`ManagementEventWatcher`, `Win32_PnPEntity`) |
| **System.Drawing.Common** | 9.0.0 | GDI+ 图形处理 (图标提取) |

### 11.3 Windows 原生 API 使用

| API | 用途 | 文件 |
|---|---|---|
| `SetupDi*` (setupapi.dll) | HID 设备枚举 | `HidNative.cs` |
| `HidD_*` / `HidP_*` (hid.dll) | HID 设备属性/能力读取 | `HidNative.cs` |
| `CreateFile` / `ReadFile` / `CloseHandle` (kernel32.dll) | HID 设备读写句柄 | `HidNative.cs` |
| `Shell_NotifyIcon` (shell32.dll) | 系统托盘图标 | `TrayIcon.cs` |
| `CreatePopupMenu` / `AppendMenu` / `TrackPopupMenu` 等 (user32.dll) | 右键上下文菜单 | `TrayIcon.cs` |
| WMI (`ManagementEventWatcher`) | USB 设备热插拔监控 | `UsbDeviceDiscovery.cs` |

---

## 12. 配置文件与资源

### 12.1 应用设置 (`Properties/Settings.settings`)

| 设置项 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Language` | `string` | `"zh-CN"` | UI 语言 |
| `Theme` | `string` | `"Dark"` | UI 主题 (暗夜红/亮色/系统) |
| `AutoStart` | `bool` | `False` | 开机自启 (写 Windows 注册表 Run 键) |
| `StartMinimizedToTray` | `bool` | `False` | 启动时最小化到系统托盘 |
| `CloseMinimizedToTray` | `bool` | `False` | 关闭按钮最小化到托盘 |
| `LastUpdateCheck` | `DateTime` | (空) | 上次检查更新时间 |

### 12.2 预设文件

| 文件 | 路径 | 说明 |
|---|---|---|
| `official_presets.json` | `Assets/Presets/` | 20 个官方内置预设 (包含踏板和方向盘预设) |
| `personal.json` | `%LOCALAPPDATA%/HITAPEX/Presets/` | 用户个人预设 (运行时读写) |
| `*.json` (多个) | `Assets/Presets/` | 用户个人预设导出文件 |

**预设 JSON 结构**:
```json
{
  "Name": "预设名称",
  "Description": "描述",
  "Category": "游戏分类",
  "ItemCount": 0,
  "Games": ["游戏1", "游戏2"],
  "DeviceType": 2,
  "Parameters": {
    "ClutchCurveType": 1,
    "ClutchDirection": 0,
    "ClutchPoint1Y": 0, "ClutchPoint1X": 0,
    "...": "..."
  },
  "WheelParameters": {
    "KeyColorEnabled": true,
    "GlobalKeyColor": 0,
    "ButtonColors": [0,1,2,...],
    "RpmColors": [0,1,2,...],
    "...": "..."
  },
  "IsPersonal": true
}
```

### 12.3 资源文件

| 类别 | 文件 | 说明 |
|---|---|---|
| 应用图标 | `Assets/AppIcon.ico` | 应用程序图标 |
| 导航图标 | `Assets/{Home/Device/Game/Help/Settings}Icon.svg` | 侧边栏导航 SVG |
| 设备图标 | `Assets/{base/pedal/steeringwheel/disconnect/edit/startuppath}.svg` | 设备页功能图标 |
| 装饰 SVG | `Assets/Group*.svg`, `Maskgroup.svg`, `Intersect.svg`, `Vector1231.svg`, `Rectangle*.svg` | UI 背景装饰/边框/图案 |
| 产品图片 | `Assets/{NeoX/OriginalImage/DevicePreview}.png`, `Rectangle_24845.png` | 产品展示图片 |
| 用户头像 | `Assets/UserAvatar.png` | 默认用户头像 |
| 横幅图片 | `Assets/{images1-images6}.png`, `unnamed3.png` | 首页轮播横幅 |
| 字体 | `Assets/Fonts/Orbitron-VariableFont_wght.ttf` | 科技感数字字体 |
| 固件二进制 | `docs/pedal_Update_v0.4.bin` | 踏板固件文件 |

---

## 13. 数据流与通信协议

### 13.1 应用启动数据流

```
App.OnStartup()
  → InitializeUsbManager()
    → new UsbSerialManager(logDirectory)
    → DeviceRegistry.GetAllVidPids() → RegisterTargetDevices()
    → 注册 USB 事件处理器 (Debug 日志)
    → new DeviceProtocolService(UsbManager)
    → new FirmwareUpdateService(UsbManager, ProtocolService)
    → new FirmwareApiService()
    → new PresetService()
    → new HidService()
    → HidService.Start() + UsbManager.Start() (开始设备发现和监控)
  → new MainWindow()
    → new MainWindowViewModel()
    → InitializeTrayIcon()
  → 根据设置决定 Show() 或 MinimizeToTray()
```

### 13.2 USB 设备发现与连接流程

```
UsbSerialManager.Start()
  → UsbDeviceDiscovery.DiscoverDevices()
    → WMI 查询 Win32_PnPEntity (PNPClass='Ports')
    → 解析 VID/PID → 目标设备过滤
    → 提取 COM 端口和序列号
  → 对每个发现的设备: ConnectDevice()
    → new DeviceSerialChannel()
    → channel.Connect(115200, N, 8, One)
      → new SerialPort()
      → 配置波特率/校验/数据位/停止位/超时
      → port.Open()
      → StartReading() (异步读取循环)
  → StartHotplugMonitoring()
    → WMI ManagementEventWatcher (__InstanceCreationEvent/__InstanceDeletionEvent)
    → 或回退到轮询模式
```

### 13.3 HID 数据采集流程

```
HidService.Start()
  → DevicePollLoop (每 2s)
    → DiscoverHidDevices()
      → SetupDiGetClassDevs (HID GUID)
      → 枚举设备接口
      → HidD_GetAttributes (获取 VID/PID)
    → DeviceRegistry 匹配
    → new HidChannel()
    → channel.Connect()
      → CreateFile (GENERIC_READ)
      → HidD_GetPreparsedData
      → HidP_GetCaps (获取报告长度)
  → ReadLoop (每 5ms)
    → channel.Read() (ReadFile)
    → ProcessData()
      → Report 0x01 → HidPedalData.Parse → PedalDataReceived
      → Report 0x11 → HidBaseData.Parse → BaseDataReceived
```

### 13.4 踏板实时数据处理流程

```
HidService.PedalDataReceived 事件
  → PedalParameterControl (订阅)
    → 后台线程: 曲线变换计算
      → ApplyCurveTransform(rawValue, curveCache)
        → 通过 Fritsch-Carlson 样条曲线映射
    → UI 线程: Dispatcher.BeginInvoke(Render)
      → 变化检测 (>0.05 阈值)
      → 更新进度条 UI
```

### 13.5 协议帧格式

**通用帧**: 64 字节固定长度。

**命令帧** (Host → Device):
| 偏移 | 大小 | 说明 |
|---|---|---|
| 0 | 2 | 帧头 (例: `0x81 0x01` = Get, `0x21 0x10` = Set) |
| 2 | 1 | 帧类型 (`0x81` = Get, `0x21` = Set) |
| 3 | 1 | 设备类型 |
| 4+ | — | 命令特定数据 |
| 63 | — | 填充 0x00 |

**响应帧** (Device → Host):
| 偏移 | 大小 | 说明 |
|---|---|---|
| 0 | 2 | 帧头 (`0xC1 0x01` = Get Response) |
| 2 | 1 | 帧类型 (`0xC1` = Response) |
| 3 | 1 | 设备类型 |
| 4+ | — | 响应特定数据 |

### 13.6 固件更新协议流程

```
1. BuildUpdateStartCommand(deviceCommand)
   → 发送 [0x80, 0x01, devCmdLow, devCmdHigh, ...]
   → 等待响应 [0xC0, 0x01, devCmdLow, devCmdHigh, status]

2. 循环发送固件数据块:
   BuildFirmwareDataCommand(deviceCommand, dataIndex, chunk)
   → 发送 [0x80, 0x00, devCmdLow, devCmdHigh, index(4B LE), len(2B LE), data...]
   → 等待响应 [0xC0, 0x00, devCmdLow, devCmdHigh, receivedCount(4B LE)]

3. BuildUpdateCompleteCommand(deviceCommand)
   → 发送 [0x80, 0x03, devCmdLow, devCmdHigh, ...]
   → 等待响应 [0xC0, 0x03, devCmdLow, devCmdHigh, status]
```

---

## 14. 附录

### 14.1 项目文件统计

| 类别 | 数量 |
|---|---|
| `.cs` 源文件 | 56 |
| `.xaml` 源文件 | 16 |
| View/Control (XAML+CS 对) | 14 |
| `.json` 配置文件 | 6 |
| 文档 (.md) | 8 |
| NuGet 包 | 5 |
| 程序集 | 1 |

### 14.2 类数量统计

| 命名空间 | 类/结构/枚举/接口 |
|---|---|
| `HITAPEX.Models` | 4 类 |
| `HITAPEX.Models.Usb` | 14 类, 1 记录结构, 3 枚举 |
| `HITAPEX.Models` (Firmware) | 5 类 |
| `HITAPEX.Services.Usb` | 4 类, 1 接口, 4 结构 |
| `HITAPEX.Services.Data.Api` | 5 类, 1 枚举, 1 异常类 |
| `HITAPEX.Services.Data.Cache` | 4 类 |
| `HITAPEX.Services.Data` | 1 类 |
| `HITAPEX.Services.Data.Models` | 4 类 |
| `HITAPEX.Services.Data.Transformation` | 1 类 |
| `HITAPEX.Services` | 3 类 |
| `HITAPEX.ViewModels` | 4 类 |
| `HITAPEX.Views` | 13 UserControl |
| `HITAPEX.Controls` | 2 类 |
| `HITAPEX.Helpers` | 1 类 |
| `HITAPEX` (App/MainWindow) | 2 类 |

### 14.3 代码规范约定

- **命名空间**: 与文件夹结构对应 (`HITAPEX.Services.Usb` ↔ `Services/Usb/`)
- **类**: PascalCase, 后缀按职责 (`*Service`, `*Control`, `*ViewModel`, `*Popup`, `*Dialog`)
- **私有字段**: 下划线前缀 (`_fieldName`)
- **异步方法**: `Async` 后缀
- **事件**: `EventHandler` 风格 (`Action<T>` 委托)
- **JSON 序列化**: `System.Text.Json` + `[JsonPropertyName]` 特性
- **线程安全**: `ConcurrentDictionary`, `Interlocked`, `lock` 关键字
- **资源释放**: `IDisposable` 模式, `using` 语句

### 14.4 已知问题与待办事项

- `BaseParameterControl` 参数调节区 (Row 3) 为空 Grid — 待实现基座参数 UI
- 固件 API 地址硬编码 (`http://192.168.1.214:1337/api`) — 待抽取为配置
- `SettingsUserControl` 版本检查为模拟实现 — 待对接真实 API
- `Services/Device/Models/` 目录存在但为空 — 预留扩展点
- 部分 View 中 Code-Behind 代码量较大 (如 `PedalParameterControl` 2319 行) — 后续可考虑提取到 ViewModel

### 14.5 关键文件索引

| 文件 | 行数 (约) | 核心职责 |
|---|---|---|
| [`App.xaml.cs`](App.xaml.cs) | ~80 | 应用入口, 全局服务初始化 |
| [`MainWindow.xaml.cs`](MainWindow.xaml.cs) | ~193 | 主窗口, 导航, 系统托盘 |
| [`DeviceProtocolService.cs`](Services/Usb/DeviceProtocolService.cs) | ~500 | 协议帧构建/解析, 响应匹配 |
| [`UsbSerialManager.cs`](Services/Usb/UsbSerialManager.cs) | ~300 | USB 设备连接管理, 重连 |
| [`HidService.cs`](Services/Usb/HidService.cs) | ~250 | HID 设备发现和读取 |
| [`PedalParameterControl.xaml.cs`](Views/DeviceParameters/PedalParameterControl.xaml.cs) | 2319 | 踏板 3 轴曲线编辑, HID 实时处理 |
| [`SteeringWheelParameterControl.xaml.cs`](Views/DeviceParameters/SteeringWheelParameterControl.xaml.cs) | 2168 | 方向盘 19 按键灯/12 LED RPM 配置 |
| [`PresetListPopup.xaml.cs`](Views/DeviceParameters/PresetListPopup.xaml.cs) | 1354 | 预设列表展示, 动态 UI 构建 |
| [`RpmSettingsPopup.xaml`](Views/DeviceParameters/RpmSettingsPopup.xaml) | 1724 | RPM 灯详细设置 UI |
| [`SettingsUserControl.xaml.cs`](Views/SettingsUserControl.xaml.cs) | ~600 | 应用设置, 固件更新流程 |
| [`HomeUserControl.xaml.cs`](Views/HomeUserControl.xaml.cs) | ~800 | 仪表盘, 圆弧/温度/方向盘可视化 |

---

> **文档生成信息**  
> 本文档基于 HITAPEX 项目 `main` 分支 (commit `792d962`) 生成。  
> 项目地址: `d:\work\HITAPEX`  
> 最后更新: 2026-06-05
