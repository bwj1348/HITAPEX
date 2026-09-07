# HITAPEX 项目结构分析与代码解析文档

> **文档版本**：v1.0 ｜ **适用代码基线**：main 分支 HEAD（`77ac544`）
> **项目类型**：Windows 桌面应用（WPF / .NET 9.0）
> **产品定位**：乘游（HITAPEX）直驱方向盘生态的 PC 控制台软件 —— 面向模拟赛车（Sim Racing）玩家的硬件控制中心，负责 USB 设备（基座 / 面盘 / 踏板）的参数配置、固件升级、游戏遥测数据采集与下发、云预设 / 用户系统，以及游戏启动与安装检测。

---

## 目录

1. [项目整体架构概述](#1-项目整体架构概述)
2. [技术栈与依赖关系](#2-技术栈与依赖关系)
3. [目录结构详解](#3-目录结构详解)
4. [应用启动流程与生命周期](#4-应用启动流程与生命周期)
5. [关键模块代码分析](#5-关键模块代码分析)
   - 5.1 USB/HID 硬件通信层
   - 5.2 设备协议编解码（DeviceProtocolService）
   - 5.3 遥测数据管道（SDK → 打包 → 下发）
   - 5.4 数据服务与 API 层
   - 5.5 预设系统
   - 5.6 UI 框架、导航与 MVVM
   - 5.7 视图层与设备参数界面
   - 5.8 本地化与辅助基础设施
6. [核心算法与数据流详解](#6-核心算法与数据流详解)
7. [代码设计模式与最佳实践总结](#7-代码设计模式与最佳实践总结)
8. [已知问题、技术债务与改进建议](#8-已知问题技术债务与改进建议)
9. [附录](#9-附录)

---

## 1. 项目整体架构概述

### 1.1 一句话定位

HITAPEX 是"乘游直驱方向盘"硬件产品的 PC 配套软件：**一端连接 USB 串口 / HID 硬件设备，一端连接模拟赛车游戏，中间完成参数管理、固件升级、云同步与遥测数据转发**。

### 1.2 分层架构

整个应用采用经典的 **WPF MVVM 分层**，但"服务层"采用 **App 静态服务定位器（Service Locator）** 模式，而非依赖注入容器：

```
┌─────────────────────────────────────────────────────────────────────┐
│  UI 层 (Views / Controls / Helpers)                                  │
│  MainWindow（导航宿主 + 托盘 + 全局弹窗）                             │
│  Home / Device / Game / Help / Settings 五大页面 UserControl           │
│  DeviceParameters 子页面群（基座/面盘/踏板参数、预设、校准、灯光）      │
└───────────────▲─────────────────────────────────────────────────────┘
                │ 数据绑定（INotifyPropertyChanged / 事件）
┌───────────────┴─────────────────────────────────────────────────────┐
│  VM 层 (ViewModels)                                                  │
│  MainWindowViewModel（导航） / NavigationItem / ViewModelBase /      │
│  RelayCommand                                                        │
└───────────────▲─────────────────────────────────────────────────────┘
                │ 静态访问 App.XXXService
┌───────────────┴─────────────────────────────────────────────────────┐
│  服务层 (Services)                                                   │
│  ├─ USB 域：UsbSerialManager → DeviceSerialChannel → SerialPort     │
│  │          DeviceProtocolService（协议编解码）                      │
│  │          HidService/HidNative（HID 通道）                         │
│  │          FirmwareUpdateService（固件升级）                        │
│  ├─ 遥测域：TelemetryService（60Hz 采集循环）                        │
│  │          TelemetryAPI（P/Invoke TelemetrySDK.dll）                │
│  │          TelemetryPacketBuilder（打包 0x6101~0x6105）             │
│  ├─ 数据域：ApiClient + 6 个 ApiService / GameDataService /          │
│  │          PresetService / LocalGameCacheService                    │
│  ├─ 集成域：GameLauncher / SteamInstallService /                     │
│  │          TelemetryConfigService（游戏配置文件部署）               │
│  └─ 基础域：LocalizationService / PasswordHasher                     │
└───────────────▲─────────────────────────────────────────────────────┘
                │ P/Invoke / System.IO.Ports / WMI / 注册表 / HTTP
┌───────────────┴─────────────────────────────────────────────────────┐
│  外部世界                                                             │
│  硬件：A1基座 / A1面盘 / A1踏板（USB 串口 + HID）                    │
│  游戏：31 款模拟赛车（Steam/共享内存/UDP/注入DLL）                    │
│  云端：Strapi API（用户/海报/固件/预设）+ 用户系统 API               │
└──────────────────────────────────────────────────────────────────────┘
```

### 1.3 三大核心数据流

| 数据流 | 方向 | 路径 | 频率 |
|---|---|---|---|
| **遥测下行** | 游戏 → 方向盘 | 游戏 → TelemetrySDK.dll → `TelemetryService`（60Hz 循环）→ `TelemetryPacketBuilder`（5 包×64B）→ `UsbSerialManager` → 串口 → 设备 | ~60Hz |
| **参数控制** | 软件 → 设备 | UI 控件 → `DeviceProtocolService.SendCommandAsync`（64B 命令帧，请求-应答配对）→ 串口 → 设备 | 用户操作触发 |
| **状态上报** | 设备 → 软件 | 串口/HID 原始数据 → `UsbSerialManager.RawDataReceived` → 协议解析（响应帧 0xC1/0xC0...）→ UI 刷新 / HID 数据（转向角、踏板行程）→ 参数页面实时曲线 | 事件/轮询 |

### 1.4 代码规模统计

- 源文件约 **110 个**（.cs / .xaml / .json），总代码量约 **4.3 万行**
- 最大代码文件：`SteeringWheelParameterControl.xaml.cs`（2803 行）、`SettingsUserControl.xaml.cs`（2490 行）、`PedalParameterControl.xaml.cs`（2556 行）、`PresetListPopup.xaml.cs`（2052 行）
- 核心服务：`DeviceProtocolService.cs`（989 行）、`MainWindow.xaml.cs`（857 行）、`TelemetryConfigService.cs`（708 行）

---

## 2. 技术栈与依赖关系

### 2.1 运行时与框架

| 技术 | 用途 | 说明 |
|---|---|---|
| .NET 9.0-windows / WPF | 应用框架 | `net9.0-windows`，`UseWPF=true`，WinExe 输出 |
| C# 12+ | 语言 | 大量使用 `record`、模式匹配、`Span`、`async/await`、集合表达式 |
| FluentWPF 0.10.2 | UI 控件库 | 现代 Fluent 风格基础控件（App.xaml 中合并其 Controls.xaml 资源字典） |
| SharpVectors.Wpf 1.8.4.2 | SVG 渲染 | `SvgViewbox` 加载导航图标、背景装饰等 SVG 资源 |
| System.IO.Ports 10.0.8 | 串口通信 | `SerialPort` 驱动 USB 虚拟串口（115200-8-N-1） |
| System.Management 10.0.8 | WMI | USB 设备热插拔监控（`ManagementEventWatcher`） |
| System.Drawing.Common 9.0.0 | 图像处理 | 头像/图片处理 |
| TelemetrySDK.dll | 遥测 SDK | 非托管 C++ DLL，通过 P/Invoke 调用（随输出复制） |

### 2.2 工程配置要点（HITAPEX.csproj）

```xml
<OutputType>WinExe</OutputType>
<TargetFramework>net9.0-windows</TargetFramework>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<UseWPF>true</UseWPF>
<ApplicationManifest>app.manifest</ApplicationManifest>
```

资源打包策略（三种方式并用）：
- `Resource Include="Assets\**\*"`：图标、字体、SVG、图片编译进程序集（BAML 资源）；
- `Content CopyToOutputDirectory`：本地化 JSON（`Resources\Locales`）、遥测配置（`Assets\TelemetryConfigs`）、预设（`Assets\Presets`）、`TelemetrySDK.dll`、`AppIcon.ico` 复制到输出目录 —— 因为这类文件**需要运行时按路径读写/动态加载**；
- 版本号：`AssemblyVersion 0.1.0`，运行时由 `Assembly.GetExecutingAssembly().GetName().Version` 读取并显示在标题栏。

### 2.3 解决方案结构

单工程单文件解决方案（`HITAPEX.sln` → `HITAPEX.csproj`），无测试工程、无类库拆分。所有代码按**功能目录**（Views / Services / Models / Helpers / Controls / ViewModels）组织在单一程序集内。

### 2.4 构建与分发

- 发布：`dotnet publish -c Release -r win-x64 --self-contained true`
- 安装包：Inno Setup 脚本 `installer/setup.iss`（LZMA2 压缩、中英双语、`PrivilegesRequired=admin`）
- 安装器特殊处理：完成页自绘"运行 HITAPEX"勾选框，用 `ShellExec('runas', ...)` 提权启动以规避 CreateProcess 错误 740

---

## 3. 目录结构详解

```
HITAPEX/
├── App.xaml / App.xaml.cs          # 应用入口：本地化初始化、Splash 线程、全局服务装配、异常钩子
├── MainWindow.xaml(.cs)            # 主窗口：无边框导航宿主、托盘、DPI 自适应、更新模式弹窗、未保存导航保护
├── SplashWindow.xaml(.cs)          # 启动闪屏（独立 STA 线程，避免初始化卡顿）
├── HITAPEX.csproj / HITAPEX.sln    # 工程与解决方案
├── app.manifest                    # 权限清单（requireAdministrator，DLL 搜索路径等）
├── TelemetrySDK.dll                # 非托管遥测 SDK（P/Invoke 目标）
│
├── ViewModels/                     # MVVM 视图模型层
│   ├── ViewModelBase.cs            # INotifyPropertyChanged 基类 + SetProperty 辅助
│   ├── RelayCommand.cs             # ICommand 中继命令（CommandManager.RequerySuggested）
│   ├── MainWindowViewModel.cs      # 导航项集合、视图缓存、语言联动
│   └── NavigationItem.cs           # 单个导航项（名称/图标/本地化键/选中态）
│
├── Views/                          # 页面级视图（UserControl）
│   ├── HomeUserControl.xaml(.cs)   # 首页：海报轮播、功能入口、用户区
│   ├── GameUserControl.xaml(.cs)   # 游戏页：31 款游戏卡片、启动/配置、UDP 端口设置
│   ├── DeviceUserControl.xaml(.cs) # 设备页：三个设备参数子页面的宿主与切换
│   ├── SettingsUserControl.xaml(.cs)# 设置页：固件更新、语言、账户、关于（体量最大之一）
│   ├── HelpUserControl.xaml(.cs)   # 帮助页
│   ├── LoginPopup.xaml(.cs)        # 登录/注册弹窗（用户系统）
│   └── DeviceParameters/           # 设备参数子页面群（核心业务 UI）
│       ├── BaseParameterControl    # 基座参数（力反馈、转向角、预设）
│       ├── SteeringWheelParameterControl  # 面盘参数（按键灯、转速灯、拨片）
│       ├── PedalParameterControl   # 踏板参数（曲线校准、死区）
│       ├── PresetListPopup         # 预设列表选择弹窗
│       ├── EditPresetPopup         # 预设编辑/另存为弹窗
│       ├── ButtonSettingsPopup     # 按键灯配置弹窗
│       ├── RpmSettingsPopup        # 转速灯配置弹窗
│       └── CalibrationDialog       # 踏板校准对话框
│
├── Controls/                       # 通用控件
│   ├── ModalDialog.xaml(.cs)       # 全局模态弹窗（MainWindow 挂载，ZIndex=1000）
│   ├── PortStepperControl.xaml(.cs)# 端口号步进器（UDP 端口配置用）
│   └── SkipInkTextBlock.cs         # 跳过 Ink 渲染的 TextBlock 变体（性能优化）
│
├── Models/                         # 数据模型
│   ├── GameItem.cs / GameListConfig.cs  # 游戏条目 + 31 款游戏硬编码配置
│   ├── DeviceParameters.cs         # 基座/踏板/面盘参数快照模型（含 Validate()）
│   ├── FirmwareVersionInfo.cs / ClientInstallerInfo.cs / BannerItem.cs / UserGameData.cs
│   └── Usb/                        # USB 域模型（21 个文件）
│       ├── DeviceRegistry.cs       # 设备注册表（VID/PID → 型号/类型/模式）
│       ├── DeviceDescriptor.cs / VidPidPair.cs / DeviceType.cs
│       ├── UsbDeviceInfo.cs / DeviceConnectionState.cs / DeviceEventType.cs
│       ├── HidBaseData.cs / HidWheelData.cs / HidPedalData.cs  # HID 上报数据
│       ├── WheelPresetSnapshot.cs / PedalPresetSnapshot.cs / BasePresetSnapshot.cs
│       └── *Response.cs 系列        # 各协议命令的响应模型（8 个）
│
├── Services/                       # 服务层（核心业务逻辑）
│   ├── Usb/                        # USB 硬件域
│   │   ├── UsbDeviceDiscovery.cs   # WMI 热插拔监控 + 串口枚举
│   │   ├── UsbSerialManager.cs     # 设备管理器（连接/重连/事件总线）
│   │   ├── DeviceSerialChannel.cs  # 单设备串口通道（异步读循环）
│   │   ├── DeviceProtocolService.cs# 协议编解码核心（989 行）
│   │   ├── HidNative.cs / HidService.cs / IHidService.cs  # HID P/Invoke 与轮询服务
│   │   ├── FirmwareUpdateService.cs# 固件升级流程
│   │   ├── IUsbSerialManager.cs / DeviceLogger.cs
│   ├── TelemetryAPI.cs             # TelemetrySDK.dll P/Invoke + NormalizedData 512B 结构体
│   ├── TelemetryService.cs         # 60Hz 采集循环、进程检测、自适应 maxRpm
│   ├── TelemetryPacketBuilder.cs   # 归一化数据 → 5 个 64B 协议包
│   ├── TelemetryConfigService.cs   # 游戏遥测配置生成/部署（UDP/插件/共享内存）
│   ├── GameLauncher.cs / SteamInstallService.cs  # 游戏启动与 Steam 安装检测
│   ├── PresetService.cs            # 预设本地管理 + 云端增量同步
│   ├── LocalizationService.cs      # 单例本地化（JSON 资源、运行时切换）
│   ├── PasswordHasher.cs           # 密码哈希
│   └── Data/                       # 数据访问域
│       ├── Api/ApiClient.cs        # HTTP 客户端封装（重试/鉴权/错误解析）
│       ├── Api/BannerApiService.cs / UserApiService.cs / FirmwareApiService.cs
│       ├── Api/DevicePresetApiService.cs / ClientInstallerApiService.cs
│       ├── Cache/LocalGameCacheService.cs  # 用户游戏数据磁盘缓存
│       ├── GameDataService.cs      # 游戏数据聚合服务
│       └── Models/ApiResponses.cs
│
├── Helpers/                        # XAML 基础设施
│   ├── LocExtension.cs             # {lex:Loc} 本地化标记扩展
│   ├── LocFontSizeExtension.cs / LocThicknessExtension.cs / NavSpacingExtension.cs
│   ├── FontExtension.cs            # {lex:Font} 字体标记扩展
│   ├── MarqueeBehavior.cs          # 跑马灯附加行为（游戏名称滚动）
│   ├── TrayIcon.cs                 # 系统托盘封装（Shell_NotifyIcon）
│   └── UiPreloader.cs              # 离屏 UI 预热工具
│
├── Resources/Locales/              # 本地化资源（zh-CN.json / en-US.json，462 行×2）
├── Assets/                         # 图片/SVG/字体/遥测配置/预设
│   ├── Fonts/Orbitron-VariableFont_wght.ttf  # 数字显示字体
│   ├── Presets/                    # 官方预设（official_presets.json 1584 行 + 测试用杂项）
│   ├── TelemetryConfigs/           # 各游戏遥测配置文件（xml/json/dll）
│   └── *.svg / *.png / *.jpg       # UI 素材
├── docs/                           # 项目文档（协议/API/手册，详见附录）
├── installer/                      # Inno Setup 安装脚本与许可证
├── Properties/Settings.settings    # 应用设置（语言、托盘行为等）
├── 123.txt                         # 开发调试笔记（游戏卡片动画问题复盘）
└── .gitignore / .gitattributes
```

### 3.1 目录职责速查表

| 目录 | 职责 | 典型依赖 |
|---|---|---|
| `ViewModels` | 绑定层状态与导航逻辑 | Services（Localization） |
| `Views` | 页面 XAML + code-behind（业务事件处理） | Services / Models / Controls |
| `Controls` | 可复用 UI 组件 | — |
| `Models` | 纯数据模型（含协议响应 DTO） | — |
| `Services` | 全部业务逻辑（硬件/遥测/网络/文件） | Models / 系统 API |
| `Helpers` | XAML 扩展、附加行为、Win32 封装 | — |
| `Assets` | 资源与外部配置文件 | 构建时复制/编译 |

> **架构观察**：本项目并非严格 MVVM —— ViewModel 层很薄（仅导航），**绝大多数业务逻辑与事件处理直接写在视图 code-behind 中**（如 `SteeringWheelParameterControl.xaml.cs` 2803 行）。属于"MVVM 外壳 + Code-behind 业务"的混合风格，后面 §7 会详细评估。

---

## 4. 应用启动流程与生命周期

### 4.1 启动时序（App.OnStartup，App.xaml.cs:29-88）

启动流程经过精心设计，核心目标是**"零卡顿首屏"**：

```
OnStartup
 ├─ 1. 初始化本地化（读取 Settings.Language，默认 zh-CN）───── 必须在任何 UI 之前
 ├─ 2. 创建独立 STA 线程显示 SplashWindow
 │     （ManualResetEventSlim 等待 Loaded，保证闪屏先出现）
 ├─ 3. 注册全局异常钩子
 │     ├─ TaskScheduler.UnobservedTaskException → SetObserved()  防 fire-and-forget 静默丢失
 │     └─ DispatcherUnhandledException → Handled = true          防 UI 线程崩溃
 ├─ 4. InitializeUsbManager()
 │     ├─ new UsbSerialManager() + RegisterTargetDevices(DeviceRegistry.GetAllVidPids())
 │     ├─ new DeviceProtocolService(UsbManager)    ← 协议服务
 │     ├─ new FirmwareUpdateService(...)           ← 固件升级
 │     ├─ new FirmwareApiService / ClientInstallerApiService / PresetService / UserApiService
 │     ├─ fire-and-forget: UserApi.TryRestoreSessionAsync() → RefreshCurrentUserAsync()
 │     ├─ fire-and-forget: PresetService.EnsureOfficialPresetsRefreshedAsync()
 │     ├─ new TelemetryService() / GameDataService()
 │     ├─ new HidService()（HID 通道，与串口并行）
 │     └─ HidService.Start() + UsbManager.Start()
 ├─ 5. new MainWindow()（构造中创建 VM → 预选 Home → InitializeComponent）
 ├─ 6. mainWindow.PreloadAndWarmUp()  ←★ 启动预热（见 4.2）
 └─ 7. 关闭 Splash → Show() → Activate()
       （若 Settings.StartMinimizedToTray → 直接最小化到托盘）
```

**关键设计点**：

1. **Splash 独立 STA 线程**（App.xaml.cs:38-51）：主线程初始化 USB/网络/UI 期间，闪屏动画不卡顿；关闭时通过 `Dispatcher.BeginInvokeShutdown` 释放线程（App.xaml.cs:93-108）。
2. **全局服务以 App 静态属性暴露**（App.xaml.cs:16-25）：`App.UsbManager` / `App.ProtocolService` / `App.PresetService` 等共 11 个静态属性，任何视图/服务可直接访问 —— 这是全项目的**服务定位器（Service Locator）**。
3. **登录态与官方预设刷新均 fire-and-forget**（App.xaml.cs:155-165）：不阻塞启动；配合全局未观测异常钩子兜底。
4. **会话结束标志**（App.xaml.cs:69）：`SessionEnding` 时置 `IsSessionEnding=true`，让关闭/注销时主窗口不拦截到托盘（MainWindow.xaml.cs:663）。

### 4.2 启动预热（PreloadAndWarmUp，MainWindow.xaml.cs:109-138）

针对"首次切换页面卡顿"的专项优化，在 Splash 展示期间完成三类重型 UI 的预创建与离屏构图：

| 预热对象 | 方式 |
|---|---|
| 5 个导航视图（Home/Device/Game/Help/Settings） | `ViewModel.PreloadView(name)` 创建并入缓存，随后的导航直接复用同一实例 |
| Device 页内 3 个设备参数子页面 | 对 `DeviceUserControl` 的 Base/SteeringWheel/Pedal 三个控件逐个预热 |
| 4 种设备类型的预设列表弹窗 | 预创建 `PresetListPopup` 并按 `DeviceType` 缓存（与 `ShowPresetListPopup` 共享同一缓存字典） |

预热工具 `UiPreloader.WarmUp` 把控件放入离屏 `Canvas` 强制完成 Measure/Arrange（完整布局），从而在用户真正导航时**零等待**。`PreloadAndWarmUp` 还统计耗时输出到 Debug。

### 4.3 退出流程（App.OnExit，App.xaml.cs:110-123）

按依赖逆序释放全部资源：`TelemetryService.Dispose()` → `GameDataService.Dispose()` → `ClientInstallerApi.Dispose()` → `HidService.Dispose()` → `UsbManager.Dispose()`（内部停止监控、断开所有串口通道）。托盘图标由 MainWindow.OnClosed 释放（`Shell_NotifyIcon` 的 Win32 句柄）。

### 4.4 主窗口关键职责（MainWindow）

| 职责 | 实现位置 | 说明 |
|---|---|---|
| 页面导航宿主 | MainWindow.xaml:298 | `ContentControl x:Name="MainContentHost" Content="{Binding CurrentView}"` |
| 导航栏 | MainWindow.xaml:213-222 | `ItemsControl` + `RadioButton`（GroupName="NavItems"），`Checked` 事件触发切换 |
| 无边框拖拽 | MainWindow.xaml.cs:715-719 | `WindowStyle=None` + `DragMove()` |
| 系统托盘 | MainWindow.xaml.cs:652-704 | `TrayIcon` 封装 Shell_NotifyIcon；双击恢复、右键退出；`CloseMinimizedToTray` 设置拦截 Closing |
| 高分屏自适应 | MainWindow.xaml.cs:267-311 | `WM_DPICHANGED/WM_DISPLAYCHANGE` 钩子 + `LayoutTransform` 等比缩放（设计基准 1500×950，留 20px 边距） |
| 未保存修改导航保护 | MainWindow.xaml.cs:758-830 | 导航前检查设备参数页 `HasUnsavedChanges`，弹全局对话框（防重入标志 `_isCheckingUnsavedNavigation`） |
| 更新模式设备检测 | MainWindow.xaml.cs:434-558 | 启动扫描 + 热插拔回调，发现更新模式 VID/PID 设备即弹"强制更新"对话框 |
| 全局模态弹窗 | MainWindow.xaml:303 | `ModalDialog x:Name="GlobalDialog"`（ZIndex=1000），子页面通过 `((MainWindow)Application.Current.MainWindow).GlobalDialogControl` 访问 |
| 登录状态区 | MainWindow.xaml:225-291 | 左下角用户卡片：头像/角色/用户名，登录事件与语言切换时刷新 |

**页面切换动效**（MainWindow.xaml.cs:177-188）：监听 `CurrentView` 属性变化，用 0.3s `CubicEase EaseOut` 淡入动画（`FadeInAnimation` Storyboard），与设备子页切换视觉一致。

**预设列表弹窗缓存**（MainWindow.xaml.cs:38, 87-99）：按设备类型缓存 `PresetListPopup` 实例，复用滚动位置与选中 tab；弹窗作为窗口 Grid 子元素挂载，靠 `Panel.ZIndex` 浮层显示。

**更新模式弹窗内容全部代码构建**（MainWindow.xaml.cs:485-558）：`FrameworkElementFactory` 构建 ControlTemplate，动态生成红色渐变斜切角按钮 —— 与 XAML 预定义样式等价，是"少写 XAML"的务实做法。

---

## 5. 关键模块代码分析

### 5.1 USB/HID 硬件通信层

#### 5.1.1 分层结构

```
┌────────────────────────────────────────────────────────────────┐
│  业务层（UI 控件 / 固件升级 / 遥测广播）                        │
│  订阅 RawDataReceived / SendToDevice / SendCommandAsync        │
├────────────────────────────────────────────────────────────────┤
│  UsbSerialManager（IUsbSerialManager 实现）                    │
│  · 设备集合 ConcurrentDictionary<string, UsbDeviceInfo>        │
│  · 通道集合 ConcurrentDictionary<string, DeviceSerialChannel>  │
│  · 事件总线：DeviceConnected/Disconnected/RawDataReceived/Error│
│  · 指数退避重连（1s→2s→4s→8s→16s，最多 5 次）                   │
├────────────────────────────────────────────────────────────────┤
│  UsbDeviceDiscovery                                            │
│  · 枚举已连接串口（WMI Win32_PnPEntity 过滤 VID/PID）          │
│  · 热插拔监控（WMI 事件，失败降级轮询）                        │
├────────────────────────────────────────────────────────────────┤
│  DeviceSerialChannel（每设备一个）                             │
│  · SerialPort 115200-8-N-1，DTR/RTS 开启                       │
│  · 后台 Task 读循环（~1ms 轮询 BytesToRead → BaseStream.ReadAsync）│
├────────────────────────────────────────────────────────────────┤
│  HidService / HidNative（与串口并行的 HID 通道）               │
│  · HidD_GetHidGuid / SetupDiGetClassDevs / ReadFile 轮询       │
│  · 三类 HID 上报：Base（转向角）/ Wheel（按键位图）/ Pedal（行程）│
└────────────────────────────────────────────────────────────────┘
```

#### 5.1.2 设备发现与注册表（DeviceRegistry / UsbDeviceDiscovery）

`DeviceRegistry`（Models/Usb/DeviceRegistry.cs）是**设备型号的单一事实来源**：每个 `DeviceDescriptor` 声明型号名、设备类型（Base/Wheel/Pedal）、正常模式与更新模式的 VID/PID 对。当前注册 3 款硬件：

| 型号 | 类型 | 正常模式 VID:PID | 更新模式 VID:PID |
|---|---|---|---|
| A1基座 | Base | 1A86:FE0C | 1A86:FE0D |
| A1面盘 | Wheel | FF86:FF0C | FF86:FF0D |
| A1踏板 | Pedal | FF3F:0002 | FF3F:F002 |

```csharp
// DeviceRegistry.cs:58-67 —— VID/PID 解析
public static DeviceDescriptor? FindByVidPid(int vid, int pid)
    => _devices.Find(d => d.Matches(vid, pid));
public static bool IsUpdateMode(int vid, int pid)
    => _devices.Exists(d => d.IsUpdateMode(vid, pid));
```

**更新模式（UpdateMode）是本项目一个关键概念**：设备固件异常或进入 Bootloader 时切换为另一组 PID。软件据此区分"可正常通信"与"必须强制升级"两种状态 —— `MainWindow` 启动时扫描 + 热插拔检测更新模式设备并弹强制更新框；`TelemetryService.DispatchPackets` 只向正常模式设备广播遥测。

`UsbDeviceDiscovery` 通过 WMI 枚举与监控（System.Management），监控失败时降级为轮询。`UsbSerialManager.RediscoverDevices()` 提供手动刷新兜底（WMI 事件偶发丢失场景）。

#### 5.1.3 串口通道与线程模型（DeviceSerialChannel）

每设备一个 `DeviceSerialChannel`：`Connect()` 打开 115200-8-N-1 串口（DTR/RTS 使能、64KB 读缓冲），随后 `StartReading()` 启动后台 `Task` 读循环：

```csharp
// DeviceSerialChannel.cs:192-224 —— 异步读循环（节选）
while (!token.IsCancellationRequested)
{
    var bytesToRead = _serialPort.BytesToRead;
    if (bytesToRead == 0) { await Task.Delay(1, token); continue; }
    var bytesRead = await _serialPort.BaseStream.ReadAsync(
        readBuffer, 0, Math.Min(readBuffer.Length, bytesToRead), token);
    if (bytesRead > 0)
    {
        _deviceInfo.IncrementBytesReceived(bytesRead);
        var rawData = new byte[bytesRead];
        Array.Copy(readBuffer, 0, rawData, 0, bytesRead);
        RawDataReceived?.Invoke(this, rawData);   // 未解析原始数据上抛
    }
}
```

要点：原始字节**不做任何粘包/分包处理**直接上抛（`UsbSerialManager.RawDataReceived` → `DeviceProtocolService.OnRawDataReceived`），帧边界识别完全交给协议层 —— 因为协议帧固定 64 字节且设备响应是完整帧到达串口后一次性可读，简单场景下的务实选择。

#### 5.1.4 连接生命周期与自动重连（UsbSerialManager）

- `Start()`：先全量扫描已连接设备（`DiscoverDevices`），再启动 WMI 热插拔监控；
- 设备插入（`OnDeviceArrived`）：新设备建通道连接；已知设备未连接则重连；
- 设备拔出（`OnDeviceRemoved`）：移除通道、反订阅、Dispose、广播 `DeviceDisconnected`；
- 首次连接失败或运行中串口错误：`TryReconnectAsync` 以指数退避（`1s×2^(n-1)`，封顶 30s）重试最多 5 次，成功后广播 `DeviceConnected`，耗尽则置 `Error` 状态；
- `OnChannelError` 用新通道替换旧通道再重连，避免复用脏串口对象。

#### 5.1.5 HID 通道（HidService / HidNative）

与串口并行的第二类硬件通道。`HidNative` 封装 P/Invoke：`HidD_GetHidGuid`、`SetupDiGetClassDevs/SetupDiEnumDeviceInterfaces` 枚举 HID 设备、`CreateFile` 打开、`ReadFile` 读取输入报告。`HidService` 按 VID/PID 匹配目标设备，独立轮询线程读取三类上报：

- `BaseDataReceived`：基座转向角度（`HidBaseData.Steering`）；
- `WheelDataReceived`：面盘按键位图（`HidWheelData.ButtonBits`）；
- `PedalDataReceived`：踏板行程百分比（`HidPedalData.Clutch/Brake/GasPercent`）。

这些数据由 `BaseParameterControl` / `SteeringWheelParameterControl` / `PedalParameterControl` 订阅，驱动参数页的实时状态显示（如踏板实时位置）。

#### 5.1.6 固件升级（FirmwareUpdateService）

基于协议 0x80xx 命令族实现完整升级流程（配合 `docs/固件版本接口.md` 的 API 版本比对）：

**状态机**：`FirmwareUpdatePhase` 共 11 个阶段 —— `Idle → CheckingMode → SwitchingToUpdateMode → WaitingForUpdateModeDevice → StartingUpdate → TransferringData → CompletingUpdate → WaitingForNormalModeDevice → Success/Failed/Cancelled`，进度通过 `FirmwareUpdateProgress`（含 ProgressPercent 计算）事件上抛 UI。

**升级流程**（UpdateFirmwareAsync，FirmwareUpdateService.cs:212-435）：

1. **模式检测**：`DeviceRegistry.IsUpdateMode(VID,PID)` —— 更新模式有独立 PID；
2. **切换模式**：非更新模式时发送切换命令帧（`[0xF3F3/0xF4F4, 0x2026]`），随后注册 `_deviceWaiters["update_mode"]` 等待者，`Task.WhenAny(waiter, 10s)` 等待设备**以更新模式重新枚举连接**（连接事件 `OnDeviceConnected` 唤醒）—— 这是固件升级与设备发现的联动点；
3. **更新开始**：`BuildUpdateStartCommand`（`[0x80,0x01,devCmdLE]`）→ 3s 超时无应答则重发一次；`ParseUpdateStartResponse` 校验 `0xC0 0x01`，status=3 报"擦除 FLASH 失败"；
4. **分包传输**：每包最多 54 字节（`MaxFirmwareChunkSize=54`），帧布局 `[0x80,0x00,devCmd,index(4B LE),len(2B LE),data...]`；每包 `SendCommandAsync` 1500ms 超时，**超时仍继续下一包**（协议要求），但连续超时 ≥10 次中止（`MaxConsecutiveTimeouts=10`）；`ParseFirmwareDataResponse` 回读设备"已收到字节数"仅作日志；
5. **完成**：`BuildUpdateCompleteCommand`（`[0x80,0x03,devCmd]`）→ 5s 超时重发一次；status：0=成功，1=长度不对，2=校验不对，3=擦除失败；成功后等待设备重启回正常模式（再次联动等待者）；
6. **取消**：`CancelUpdate()` 取消 `_currentUpdateCts`；`IsUpdating` 全局单飞。

**设备命令码**（L114-121）：面盘 0x7913、踏板 0x7A14、切换命令 0xF3F3/0xF4F4；蓝牙 0x7711、主控双核 0x5634/0x7812 常量已声明但**未接入流程**（`GetDeviceCommandForVid` 对非踏板一律返回 0x7913）—— 基座主控/蓝牙升级属未完成功能。

#### 5.1.7 自定义控件（Controls/）

| 控件 | 实现要点 |
|---|---|
| `ModalDialog` | 全屏半透明遮罩 + 六边形 Path 边框；`AddButton` 用 `FrameworkElementFactory` 动态构建六边形按钮模板（首个左对齐、其余右对齐）；`Hide()` 一次性重置所有状态防污染复用 —— 全局模态框的关键实践 |
| `PortStepperControl` | string 型依赖属性（BindsTwoWayByDefault）+ 步进按钮，clamp 到 [0,65535]；无输入合法性校验 |
| `SkipInkTextBlock` | 继承 FrameworkElement 的自绘控件：`FormattedText.BuildGeometry` + `Geometry.Combine(underline, widenedText, Exclude)` 把文字笔画从下划线挖掉，实现"跳过墨水"效果；四重几何缓存 + PixelsPerDip 保证高 DPI 清晰 |

### 5.2 设备协议编解码核心（DeviceProtocolService）

这是**全项目最重要的协议层**，989 行，职责：构建/解析全部命令帧与响应帧 + 请求-应答配对机制。

#### 5.2.1 帧格式（协议文档 docs/乘游直驱方向盘与PC软件usb通信协议 v0.1.md）

- **固定 64 字节帧**，多字节小端序；
- **命令帧**：`[0x81=获取 | 0x21=设置, 命令号低字节, 命令号高字节, 参数...]`；
- **响应帧**：`[0xC1=获取应答 | 0xC0=设置/其他应答, 命令号低, 命令号高, 数据...]`；
- 命令号示例：`0x8101` 设备信息、`0x2101` 基座参数、`0x2110` 踏板参数、`0x2103~0x2108` 面盘灯效、`0x21D0` 预设名称、`0x6101~0x6105` 遥测数据（下行）。

```csharp
// DeviceProtocolService.cs:107-115 —— 构建设备信息查询帧
public static byte[] BuildGetDeviceInfoCommand(DeviceType deviceType)
{
    var frame = new byte[FrameSize];        // FrameSize = 64
    frame[0] = 0x81;          // Get command
    frame[1] = 0x01;          // 0x8101 LE low byte
    frame[2] = 0x81;          // 0x8101 LE high byte
    frame[3] = (byte)deviceType;
    return frame;
}
```

#### 5.2.2 请求-应答配对机制（核心算法）

`SendCommandAsync` 用 **TaskCompletionSource + ConcurrentDictionary** 实现"发送后挂起等待设备应答"：

```csharp
// DeviceProtocolService.cs:275-312 —— 命令-响应配对（节选）
public async Task<byte[]?> SendCommandAsync(string deviceKey, byte[] command, int timeoutMs = 3000)
{
    if (_pendingCommands.TryRemove(deviceKey, out var oldTcs)) oldTcs.TrySetCanceled();
    var tcs = new TaskCompletionSource<byte[]?>();
    _pendingCommands[deviceKey] = tcs;              // 以设备 Key 登记等待者
    try
    {
        var ok = _manager.SendToDevice(deviceKey, command);
        if (!ok) return null;
        var timeoutTask = Task.Delay(timeoutMs);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);  // 竞争：应答 vs 超时
        if (completedTask == timeoutTask) { /* 超时 → 返回 null */ }
        return await tcs.Task;
    }
    finally { _pendingCommands.TryRemove(deviceKey, out _); }
}
```

应答侧由 `OnRawDataReceived`（DeviceProtocolService.cs:53-101）完成配对：收到数据后查 `_pendingCommands`，有等待者即 `TrySetResult(data)`。

**设计特征与局限**：
- 以"设备 Key"为键而非"命令号"，同一时刻每设备只允许一个挂起命令 —— 天然串行化，规避协议无序列号导致的应答混淆；代价是并发请求被隐式排队（业务层 UI 操作本来就是串行交互，实际影响小）；
- 超时兜底 3s，避免设备无应答时永久挂起；
- 这是典型的**"future/promise 模式 + 超时竞争"**，无需后台轮询应答。

#### 5.2.3 多包预设名称传输（0x21D0，特殊协议）

预设名称最长 512 字节（UTF-8），单帧只装 56 字节数据，需分片：

- 发送侧（`SetPresetName`，962-988 行）：按 `PresetNameChunkSize=56` 切块，每包携带 `totalLength(2B) + packetIndex(1B)`；
- 接收侧（`OnRawDataReceived` 优先分支，58-92 行）：`_presetNameCollections` 收集分片 → 在 **lock 内完成完整性判定**（避免 TOCTOU 竞态）→ 凑齐 `ceil(totalLen/56)` 包后 `DecodeNameFromPackets` 拼接 UTF-8 字符串 → `TrySetResult` + 取消超时 CTS；
- 状态类 `PresetNameCollectionState` 用 `record` 封装（Packets/Tcs/Cts 三元组）。

#### 5.2.4 协议命令族全景（Build*/Parse* 成对出现）

| 命令 | 用途 | 关键方法对 |
|---|---|---|
| 0x8101 | 设备信息（含基座子设备连接状态/版本） | `BuildGetDeviceInfoCommand` / `ParseDeviceInfoResponse` |
| 0x2101 | 基座参数（转向角 90-2700°、力回馈、惯量、阻尼等 16 项） | `BuildSetBaseParametersCommand` / `ParseBaseParametersResponse` |
| 0x2102 | 面盘按键（拨片模式、25 键键值、旋钮） | `BuildSetWheelButtonParametersCommand` |
| 0x2103 | 转速灯基础模式（恒亮/呼吸/彩循环 + 12 灯 RGB） | `BuildSetWheelRpmBaseModeCommand` / `ParseWheelRpmBaseModeResponse` |
| 0x2104 | 转速灯指示（触发模式百分比/RPM + 12 灯触发值/颜色） | `BuildSetWheelRpmIndicatorCommand` / `ParseWheelRpmIndicatorResponse` |
| 0x2105 | 转速灯模式（亮度/遥测开关/频闪） | `BuildSetWheelRpmModeCommand` / `ParseWheelRpmModeResponse` |
| 0x2106 | 按键灯全局（模式/亮度/颜色） | `BuildSetWheelButtonLightGlobalCommand` |
| 0x2107 | 按键灯单键（12 LED 独立效果 + 遥测联动） | `BuildSetWheelButtonLightCommand` / `ParseWheelButtonLightResponse` |
| 0x2108 | 面盘睡眠与拨片（休眠时间/效果、拨片离合模式/结合点） | `BuildSetWheelSleepAndPaddleCommand` |
| 0x2110 | 踏板参数（三踏板方向 + 4 点曲线 + 前后死区） | `BuildSetPedalParametersCommand` / `ParsePedalParametersResponse` |
| 0x2111 | 踏板校准 | `CalibrationStart/Complete` 常量 + 校准帧 |
| 0x21D0 | 预设名称读写（多包） | `GetPresetNameAsync` / `SetPresetName` |
| 0x80xx | 固件更新（开始/数据/完成） | `BuildUpdateStartCommand` / `ParseUpdateStartResponse` 等 |

所有 `Build*` 返回 64 字节帧、所有 `Parse*` 先校验帧头 `[0xC1/0xC0, cmdLo, cmdHi]` 再逐偏移解析 —— **模板方法式的对称设计**，新增命令的扩展成本很低。

#### 5.2.5 基座参数帧示例（0x2101，355-383 行）

```csharp
frame[0] = 0x21; frame[1] = 0x01; frame[2] = 0x21;   // Set 0x2101
frame[3] = (byte)(maxSteeringAngle & 0xFF);           // 最大转向角 u16 LE
frame[4] = (byte)((maxSteeringAngle >> 8) & 0xFF);
frame[5] = limitRigidity;   // 限位刚力 0-2
frame[6] = maxSpeed;        // 最大转速 0-100
frame[7] = smoothLevel;     // 力反馈平滑 0-10
frame[8] = forceStrength;   // 力反馈强度 0-100
...  // 机械/游戏惯量、阻尼、摩擦、弹性共 10 项
frame[18] = handsOffProtect;  // 离手保护
frame[19] = forceReverse;     // 力回馈反向
```

### 5.3 遥测数据管道（TelemetryAPI → TelemetryService → TelemetryPacketBuilder）

#### 5.3.1 P/Invoke 边界（TelemetryAPI.cs）

`TelemetrySDK.dll`（378,880 字节，随发布复制）为 **C++17 原生 x64 二进制**（PE 头 Machine=0x8664 / PE32+，内部含 20 个 C++ 游戏适配器把各游戏协议归一化为统一结构），**只能经 P/Invoke 调用**，无法反编译为 C#。csproj 以 `Content + PreserveNewest` 复制到输出目录（与 exe 同目录，按名称解析）。暴露 5 个 C 接口，C# 侧以 `DllImport(CallingConvention.Cdecl)` 封装（bool 显式 `UnmanagedType.U1`）：

```csharp
[DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.U1)]
public static extern bool StartTelemetry(int gameId);      // 初始化并启动采集
[DllImport("TelemetrySDK.dll", CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.U1)]
public static extern bool GetTelemetryData(ref NormalizedData outData);  // 拉取一帧
public static extern void StopTelemetry();
public static extern int GetSDKVersion();
public static extern ulong GetSupportedFlags();
```

**NormalizedData 结构体**：`[StructLayout(LayoutKind.Sequential, Pack = 1)]` 固定 **512 字节**，字段按 C++ 端 v2.0 布局严格排列（`validFlags` 在偏移 288B，`_reserved` 224B）。内联数组用 `[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]`。`docs/Update_Notes_v2.0.md` 记录了 v1.x → v2.0 的破坏性迁移（结构体重排、`tyreWear` 0-100 值域、`-1` 哨兵取消、改用 validFlags 掩码），并强调 `Marshal.SizeOf<NormalizedData>() == 512` 自检。

**ValidFlags 位掩码**（194-246 行）：bit 0-44 共 45 个字段有效性标志 —— **"先查掩码再读值"是遥测消费的唯一正确姿势**（SDK 对不支持/超界/NaN 字段一律置 0，不再返回 -1 哨兵）。

#### 5.3.2 60Hz 采集主循环（TelemetryService.cs:232-286）

```csharp
private void LoopProc(object? state)
{
    var token = (CancellationToken)state!;
    var data = TelemetryAPI.CreateNormalizedData();
    var frameCount = 0;
    while (!token.IsCancellationRequested)
    {
        var tickStart = Stopwatch.GetTimestamp();
        frameCount++;
        if (TelemetryAPI.GetTelemetryData(ref data))
        {
            ApplyAdaptiveMaxRpm(ref data);   // LFS/RBR/BeamNG 自适应 maxRpm
            ProcessFrame(data);              // 打包 + 下发
        }
        if (frameCount % 300 == 0 && !IsTargetProcessAlive())  // 每 ~5s 查进程
        { Task.Run(() => Stop()); break; }
        var elapsed = Stopwatch.GetElapsedTime(tickStart);
        var sleepMs = (int)(LoopInterval - elapsed).TotalMilliseconds;  // 目标 16ms
        if (sleepMs > 0) token.WaitHandle.WaitOne(Math.Max(1, sleepMs));
    }
}
```

要点：
- **固定步长循环**：目标 ~60Hz，用 Stopwatch 实测耗时补偿睡眠，超时 >10ms 打警告；
- **进程存活检测**：`GameProcessNames` 字典（38-81 行）把 31 个 GameId 映射到进程名列表（含反作弊子进程名如 `EAAntiCheat.GameService`），每 300 帧查一次，游戏退出即自动停遥测；
- **自适应 maxRpm 算法**（352-378 行）：对不提供 maxRpm 的三款游戏（LFS/RBR/BeamNG，GameId 22/25/284160），默认 6000 RPM，追踪转速峰值，连续 5 秒转速为 0（换车场景）则重置默认值。

#### 5.3.3 打包（TelemetryPacketBuilder.cs）

`NormalizedData` → 5 个 64B 帧，统一包头 `[0x61, typeLo, typeHi, timestamp u32 LE]`（WriteHeader，34-43 行）：

| 包 | 类型 | 内容 |
|---|---|---|
| 0x6101 | 车辆信息 1 | 车速/转速/maxRpm/档位/油门/刹车/离合/转向、状态标志（限速/TC/ABS/DRS）、TC/ABS 档位、旗语、ERS、发动机、油量 |
| 0x6102 | 车辆信息 2 | 四轮刹车温度、胎面内侧/中间温度 |
| 0x6103 | 车辆信息 3 | 胎面外侧温度、胎核温度、胎压 |
| 0x6104 | 车辆信息 4 | 胎磨损、水温、油温、涡轮压力 |
| 0x6105 | 比赛信息 | 总圈/当前圈、排名、当前圈时/上一圈/最佳圈时 |

每个字段写入前先 `HasFlag(data, ValidFlags.XXX)` 判有效，数值 `Math.Clamp` 到协议范围，轮胎数组统一 `[FL,FR,RL,RR]`。档位映射（`GearFromNormalized`，374-384 行）：`-1→0xFF(倒挡)`、`0→0x00(空挡)`、`1-100→原值`、`>100→截断`。

#### 5.3.4 下发（DispatchPackets，TelemetryService.cs:388-433）

向所有**正常模式**已连接设备（基座/面盘/踏板各自独立直连电脑）广播 5 包；单设备某包发送失败即跳过剩余包；`OnPacketsDispatched` 事件携带时间戳供 UI 显示"遥测已下发"状态。

#### 5.3.5 遥测配置部署（TelemetryConfigService + Assets/TelemetryConfigs）

`Assets/TelemetryConfigs/` 按游戏目录存放接入素材，`TelemetryConfigService.ApplyConfig(game)`（708 行，按 GameId switch 分发 14 个游戏）负责把配置文件部署/改写进游戏目录。五种部署策略：

| 策略 | 游戏 | 实现（TelemetryConfigService.cs） |
|---|---|---|
| **复制文件** | LFS（cfg.txt）、DiRT 4 / DR2.0（hardware_settings_config.xml） | `CopyConfigFile` 从安装目录素材复制到游戏目录/文档目录（L105-115, 688-707） |
| **复制 DLL 插件** | rF2（Bin64\Plugins）、LMU（Plugins）、ETS2/ATS（bin\win_x64\plugins） | 区分 Steam 安装目录与自定义路径两种根目录解析（`ResolveGameRoot` L75-96） |
| **改写 XML** | F1 22-25 | `XDocument` 加载 `Documents\My Games\F1 {year}\hardwaresettings\hardware_settings_config.xml`，把 `<motion>/<udp enabled>` 置为 true（L303-355） |
| **改写 key-value 配置** | WRC Generations | `UserSettings.cfg` 逐行匹配键并更新/追加 4 项遥测参数（地址 127.0.1.1:20777、60Hz）（L365-417） |
| **DLL 替换补丁** | WRC 8/9/10 | 备份 `PhysXCooking64_s.dll` → 覆盖为 `WrcInjectionPayload.dll`（可还原，L426-473） |
| **JSON 部署+注入** | EA WRC | `Documents\My Games\WRC\telemetry\` 复制 config.json/custom1.json/wrc_cwyx.json；`InjectEaWrcPackets` 用**文本级括号深度扫描**在 `udp.packets` 数组内注入 5 条 wrc_cwyx 条目（127.0.0.1:26666，session_update 启用），保持原文件格式不被重序列化破坏（L482-679） |

其中 EA WRC 的 `InjectEaWrcPackets` 是值得注意的实现：为避免 JSON 重序列化破坏游戏不认的格式细节，采用**逐行文本操作 + 括号深度计数**定位数组边界并手工拼接注入条目 —— 鲁棒性依赖游戏配置格式稳定，属于"协议化文本手术"。

**游戏启动链路**（GameLauncher.cs）：
- Steam 模式：`steam://run/{SteamId}`（正则 `^\d+$` 防命令注入）；
- 自定义路径模式：直接 `Process.Start` 可执行文件；
- 启动成功后**延迟 5 秒** `TelemetryService.Start(gameId)`（给游戏加载时间），启动前校验 GameId 在 `TelemetryAPI.GameId` 枚举内。

各游戏手册见 `docs/manual/`（AC/ACC/F1/Forza/R3E/RBR/LFS/BeamNG 等），`docs/遥测支持.csv` 给出 31 款游戏 × 45 字段的完整支持矩阵。按数据源接入方式，31 款游戏分四类：

| 类别 | 游戏 | 数据源 | 本软件侧动作 |
|---|---|---|---|
| 原生共享内存/UDP（自动） | AC 系列 4、iRacing、R3E、AMS2/PC2/PC3、Forza 系列 4 | 共享内存 / UDP 1024 | 无需配置（AC 需游戏内开 UDP；AMS2 系需游戏内切 Project Car 2 共享内存） |
| UDP + 配置部署 | F1 22-25、DiRT 4 / DR2.0、WRCG、EA WRC | UDP 20777 / 26666 | 改写/覆写硬件设置 XML、UserSettings.cfg、telemetry 目录 + packets 注入 |
| 插件 DLL 注入 | rF2、LMU、ETS2、ATS | 共享内存插件 | 复制 DLL 到 Plugins（LMU 额外改 CustomPluginVariables.JSON 启用插件） |
| 替换补丁 / OutGauge | WRC 8/9/10（DLL 替换）、LFS / RBR / BeamNG（UDP 30000） | 注入 DLL / OutGauge | 备份+替换 PhysXCooking64_s.dll；覆写 cfg.txt |

数据丰富度梯度：AC/ACC/F1/AMS2/R3E/rF2-LMU 最全（45 位掩码大部支持），WRC 8/9/10 最简（仅速度+转速+档位）。SDK 适配层还负责单位换算（速度 m/s→km/h、R3E 转速 rad/s→RPM、AC 燃油 kg→L×0.75）与字段推断（LFS/BeamNG/R3E 的 `isEngineRunning` 由 RPM>0 推断；ETS2 的 `isPitLimiterActive` 实为驻车制动）。

### 5.4 数据服务与 API 层（Services/Data）

#### 5.4.1 分层结构

```
传输层   ApiClient ── 唯一持有 HttpClient：BaseAddress + Bearer Token + 15s 超时
                    + 指数退避重试（500ms→1s→2s，最多 3 次，4xx 不重试）
                    + ApiResult<T> 判别联合返回（不抛异常约定）
                    + 401 全局回调（UnauthorizedHandler）
                    + 两套响应契约：Strapi {data} / 用户系统 {success,data}
服务层   UserApiService / BannerApiService / ClientInstallerApiService
        / DevicePresetApiService / FirmwareApiService / GameDataService
持久化   LocalGameCacheService（用户游戏数据）/ PresetService（预设文件）
```

#### 5.4.2 ApiClient 关键设计（ApiClient.cs）

```csharp
// ApiClient.cs:96-156 —— GET 请求主循环（节选）
for (int attempt = 0; attempt <= _maxRetries; attempt++)
{
    try
    {
        var response = await _httpClient.GetAsync(endpoint, ct);
        if (response.IsSuccessStatusCode)
            return ApiResult<T>.Success(JsonSerializer.Deserialize<T>(responseBody, JsonOptions)!);
        // 非 2xx：解析统一错误体 { error: { status, code, message, details } }
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            UnauthorizedHandler?.Invoke();          // 401 → 全局清登录态
        if ((int)response.StatusCode is >= 400 and < 500)
            return ApiResult<T>.Failure(lastErrorMessage, isClientError: true, ...); // 4xx 不重试
    }
    catch (HttpRequestException ex) { lastException = ex; }   // 网络异常 → 进入退避重试
    if (attempt < _maxRetries)
        await Task.Delay((int)(_retryDelayBase.TotalMilliseconds * Math.Pow(2, attempt)), ct);
}
```

要点：
- **重试策略**：4xx 客户端错误不重试（含 401/429），5xx 与网络异常指数退避重试；
- **鉴权**：构造时注入 Strapi API Token（Bearer）；用户登录后 `SetUserToken(jwt)` 整体替换请求头；每请求可用 `authToken` 覆盖（step-up JWT 场景，如找回密码流程）；
- **错误契约**：`ApiResult<T>` 判别联合（IsSuccess/Data/ErrorMessage/IsClientError/ErrorCode/RetryAfterSeconds），注释明确"调用方不使用 try/catch"；429 限流从 `error.details.retry_after` 或 `Retry-After` 头读取倒计时；
- **multipart 支持**：`UploadFileAsync`（头像上传 → Strapi `/api/upload`）、`PutMultipartAsync`（update-me 一次性上传头像+改字段）。

#### 5.4.3 各 ApiService 端点速查

| 服务 | 端点 | 用途 |
|---|---|---|
| UserApiService | POST `/api/auth/local/register-otp`、`/verify-otp`、`/local`（密码登录）、`/refresh-token`；GET `/api/users/me`；PUT `/api/auth/update-me`；POST `/api/auth/change-password`、`/forgot-password`、`/verify-stepup`、`/reset-password`；GET/PUT/DELETE `/api/user-presets/{documentId}` | 注册（OTP）/登录（密码+OTP）/会话恢复/资料/头像/改密/云预设 |
| BannerApiService | GET `/api/banners?populate=*` | 首页海报（取前 3 条，拼媒体 URL） |
| ClientInstallerApiService | GET `/api/client-installers?locale=...&sort=publishedAt:desc`；流式下载安装包 | 客户端更新 |
| DevicePresetApiService | GET `/api/device-presets?populate=*` | 官方预设（映射为 DevicePresetEntry，含 publishedAt） |
| FirmwareApiService | GET `/api/firmware-versions?locale=...`；流式下载固件 | 固件版本列表与文件下载 |

**会话管理链路**（UserApiService）：
- JWT 用 **DPAPI 加密**（`ProtectedData.Protect`，CurrentUser 范围 + 固定 entropy）存入 `Settings.UserAccessToken`，兼容旧明文 token 自动升级；
- 启动时 fire-and-forget `TryRestoreSessionAsync`（refresh-token 续期 → 换发新 JWT → `RefreshCurrentUserAsync` 补齐头像）；
- 任意请求 401 → `UnauthorizedHandler` → 清 token + 通知 `LoginStateChanged`（UI 刷新为游客态）；
- 改/重置密码后服务端 `token_version` 作废旧 JWT，客户端保存响应中的新 JWT（`ChangePasswordAsync`）。

#### 5.4.4 游戏数据聚合（GameDataService）

```csharp
// GameDataService.cs:50-74 —— 获取游戏列表（节选）
var games = GameListConfig.GetGames();        // 硬编码 31 款（唯一元数据来源）
_userData = LocalGameCacheService.Load();     // %LocalAppData%\HITAPEX\user_game_data.json
ApplyUserData(games, _userData);              // 合并：置顶/启动路径/最后启动时间
_cachedGames = games;                         // 内存缓存
```

- 数据流：`GameListConfig（硬编码）→ LocalGameCacheService（用户操作数据）→ SteamInstallService（安装状态）`。注意：**注释宣称的"API 元数据"环节实际未实现**，游戏列表完全是本地静态数据；
- `EnrichWithInstallStatus` 通过 `SteamInstallService` 解析本机 Steam（注册表 SteamPath + `libraryfolders.vdf` 多库 + `appmanifest_*.acf` 的 installdir/LastPlayed）判定 31 款游戏的安装状态与最后游玩时间；
- 状态事件 `StateChanged`（Loading/Loaded/Error）驱动 UI。

#### 5.4.5 密码处理（PasswordHasher.cs:13-19）

```csharp
byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
return Convert.ToHexString(bytes).ToLowerInvariant();
```

客户端**单次 SHA-256、无盐、无迭代**（目的仅为"避免明文密码出网"，服务端再哈希落库）。安全性局限：无盐哈希无法防御彩虹表，且"哈希即密码等价物"——若服务端按原样比对，哈希泄露等于密码泄露。仅适合作为传输预哈希，需服务端二次加盐。

### 5.5 预设系统（PresetService）

预设是"参数方案"：一组设备参数快照（基座 16 项 / 踏板 33 项 / 面盘 27 项）+ 名称 + 关联游戏列表，可整体应用/保存/导入/导出。

**存储布局**：

| 数据 | 路径 | 说明 |
|---|---|---|
| 个人预设 | `%LocalAppData%\HITAPEX\Presets\personal.json` | `List<PresetItem>`，按设备类型过滤/合并写 |
| 官方预设缓存 | `%LocalAppData%\HITAPEX\Presets\official_cache.json` | `{ presets: [{ publishedAt, preset }] }` |
| 随包官方预设 | `Assets\Presets\official_presets.json`（1584 行） | 旧格式 `List<PresetItem>`，首次运行无缓存时回退加载 |
| 用户云预设 | 服务器 `/api/user-presets` | 独立链路，与本地 personal.json 无自动同步 |

**官方预设增量同步算法**（EnsureOfficialPresetsRefreshedAsync，PresetService.cs:54-140）：

```
云端条目（含各自 publishedAt）
   │ 以名称（OrdinalIgnoreCase）为唯一键与本地缓存比对
   ├─ 本地已有 && 云端 publishedAt 更新  → 覆盖（updated++）
   ├─ 本地没有                         → 新增（added++）
   └─ 云端缺失（本地有云端无）          → 删除（removed++）
仅当有变更才写盘；_hasCheckedApi 双检 + _checkLock 保证全程只执行一次
```

**导入校验四步**（ImportPreset，349-440 行）：DeviceType 枚举有效 → 参数快照存在 → **字段完整性校验**（缺失字段与未知字段均拒绝，防止手改 JSON 出错）→ `snapshot.Validate()` 值域校验。

**并发控制**：文件写入经 `SemaphoreSlim _fileLock`（1,1）串行化；读取未加锁（读-写竞态风险，见 §8）。

### 5.6 UI 框架、导航与 MVVM

（由「应用外壳」子模块分析整合，见 §4.4 与 §7；此处仅补充基础设施结论）

- **MVVM 基座**：`ViewModelBase`（SetProperty + CallerMemberName，EqualityComparer 防重复通知）、`RelayCommand`（双构造重载，CanExecuteChanged 挂 `CommandManager.RequerySuggested`，缺手动 RaiseCanExecuteChanged）；
- **标记扩展家族**（Helpers/）：`{lex:Loc}` 绑定本地化索引器、`{lex:Font}` 绑定语言字体、`LocFontSizeExtension`/`LocThicknessExtension`/`NavSpacingExtension` 绑定 JSON 内嵌配置值 —— 统一模式：ProvideValue 返回 OneWay Binding + FallbackValue + 设计时降级；
- **MarqueeBehavior**：附加属性实现游戏名跑马灯（悬停时"原文+3空格+原文"无缝循环，FormattedText 精确测量步长，TranslateTransform 动画）；
- **TrayIcon**：纯 Win32 实现（HwndSource 钩子 + Shell_NotifyIcon + 原生弹出菜单 + 气泡通知 + 图标三级回退）；
- **UiPreloader**：离屏 Measure/Arrange/UpdateLayout 强制完成模板实例化与首次绑定求值。

### 5.7 视图层与设备参数界面

#### 5.7.1 页面组织与宿主

五大导航页面均为 `UserControl`，宿主是 **ContentControl（非 Frame）**（MainWindow.xaml:298），由 `MainWindowViewModel.PreloadView` 按名称 switch 创建并缓存（见 §4.2）。页面切换 0.3s 淡入；`ModalDialog` 与 `LoginPopup` 均为 **UserControl 覆盖层**（ZIndex 1000/1001），**全项目没有独立 Window 弹窗**，与无边框主窗口风格统一。

| 页面 | 职责 | code-behind 规模 |
|---|---|---|
| HomeUserControl | 首页：海报轮播、设备预览卡片、游戏快速启动 | 1337 行 |
| GameUserControl | 游戏库：31 款游戏卡片、启动/遥测配置、UDP 设置 | 1681 行 |
| DeviceUserControl | 设备页容器：三个参数子页的宿主与切换 | 490 行 |
| SettingsUserControl | 设置：系统/固件更新/账户三 Tab | 2490 行 |
| HelpUserControl | 帮助页（纯 XAML 无逻辑） | 13 行 |
| LoginPopup | 登录/注册/找回密码（三面板） | 930 行 |

**关键架构事实**：除主导航与游戏卡片模板外，**页面内几乎零 MVVM** —— 子控件不设 DataContext、无 XAML Binding，全部 code-behind 用 `x:Name` 命名控件直写 + 静态服务事件通信（`App.UsbManager.DeviceConnected`、`App.HidService.XxxDataReceived`、`LocalizationService.PropertyChanged`、`App.UserApi.LoginStateChanged`）与 MainWindow 公开方法（`ShowPresetListPopup`、`GetCurrentSettingsView`）。

#### 5.7.2 首页（HomeUserControl）

- **海报轮播**：3 张图按 Canvas.Left 平移，DispatcherTimer 5s 自动播放 + 滚轮/指示器手动切换，Banner 从 `BannerApiService` 拉取；
- **设备预览卡片**：4 张异形 Path 卡片（基座/面盘/踏板/排挡）—— **重要发现：卡片上的仪表（力反馈弧、温度色块、方向盘 34 段刻度、踏板柱条）全部是 code-behind 模拟数据**（100ms/50ms DispatcherTimer 随机波动），尚未接线真实遥测；控件虽暴露 `SetTemperature/SetSteeringAngle/SetPedalValues` 公开方法但无人调用；
- **游戏快速启动**：横向卡片列表 + 置顶 FLIP 动画（坐标记录→补间） + 滚轮转横向 + 自定义 SkewTransform 滚动条 + `MarqueeBehavior` 游戏名跑马灯；
- **卡片"撕裂"悬浮效果**：底层红色斜切 Path + 顶层封面 Grid.Clip 遮罩，悬浮时双向位移形成裸眼 3D；`123.txt` 记录的"帕金森动画"（鼠标判定逃逸抽搐）在首页已用**透明 HitBox 幽灵命中层**修复（HomeUserControl.xaml:569-572），但 **Game 页同款模板未修复**（见 §8.3 U8）。

#### 5.7.3 游戏页（GameUserControl）

- 底部横向游戏列表 + 右侧详情面板（标题双层字+跑马灯、描述、启动按钮、Steam/自定义路径单选）；筛选"全部/已安装/未安装"，刷新按钮重扫 Steam 安装状态；
- **启动链路**：`LaunchGameButton_Click` 先 `ApplyPresetsIfAutoApplyEnabled`（把四个预设下拉框的预设**立即下发到设备**——`BuildSetXxxCommand` → `App.UsbManager.SendToDevice`），再 `GameLauncher.Launch`，失败弹 GlobalDialog；
- **遥测 Tab 三件套**：设备配置（手动触发 `TelemetryConfigService.ApplyConfig`，成功/失败 Toast 由代码动态构建 Grid+Path 挂窗口根 Panel）、遥测支持（静态支持矩阵硬编码）、遥测设置（UDP 转发目标编辑器：IP + `PortStepperControl` 端口行，ObservableCollection 直绑 ItemsSource）；
- **遥测状态**：`StartTelemetrySimulation` 仅订阅 `OnPacketsBuilt/OnPacketsDispatched` 计数刷新（Dispatcher.BeginInvoke 封送）；
- **样式复制问题**：游戏卡片 DataTemplate 与首页几乎整段重复（各 ~500 行，仅差事件处理细节）。

#### 5.7.4 设备页容器（DeviceUserControl，490 行）

三个设备参数子页面的**智能宿主**，核心逻辑是"设备连接状态驱动 UI"：

- 导航：左侧 RadioButton 三按钮（基座/面盘/踏板）+ 键盘快捷键（1/2/3、上下箭头循环）；`NavigateToTab` 供首页 Group 图标跨页跳转；
- **状态联动**：某类型未连接 → 按钮禁用+图标 40% 透明；全部未连接 → 隐藏参数页显示"设备未连接"占位（含手动刷新按钮）；多设备连接 → 默认展示最上方已连接页；
- **自动跳转**：当前展示页断开时自动切到其他已连接页（`_autoSwitching` 标志跳过未保存确认）；
- **未保存保护**：子页间切换同样检查 `HasUnsavedChanges` 并弹确认框（与主窗口顶级导航的双层保护）；
- **检测口径**：只认串口已连接设备（注释明确不合并 HID 通道 —— HID 轮询最长 2 秒的摘除延迟会造成中间态，DeviceUserControl.xaml.cs:224-226）；
- **手动刷新**：`RediscoverDevices()` + 800ms 后二次刷新（捕获驱动枚举延迟）。

```csharp
// DeviceUserControl.xaml.cs:218-254 —— 状态刷新核心（节选）
var devices = App.UsbManager?.ConnectedDevices ?? ReadOnlyCollection<UsbDeviceInfo>.Empty;
_baseConnected = HasConnectedDevice(devices, DeviceType.Base);
...
int firstConnected = GetFirstConnectedIndex();
if (firstConnected < 0) { ShowNoDeviceState(); return; }   // 全部断开 → 占位
NoDevicePanel.Visibility = Visibility.Collapsed;
ContentHost.Visibility = Visibility.Visible;
if (_currentControl == null || !IsConnectedAt(_currentIndex))
    AutoSelectFirstConnected();                            // 断开跳转
```

#### 5.7.5 参数子页面模式（Base/SteeringWheel/Pedal 三控件）

三个参数控件（801/2803/2556 行）共享同一套页面模式，**全部不设 DataContext、不做 XAML Binding，纯 code-behind 命名字段直写**：

1. **连接识别**（RefreshDeviceInfoAsync）：过滤"正常模式 + 本类型"设备 → 经 `FirmwareUpdater.GetDeviceInfoAsync`（0x8101）读固件版本 → 显示绿色/红色连接状态（7 个指示图标描边色统一切换）；
2. **预设同步**：`FetchPresetNameAsync`（0x21D0 读设备端预设名）→ `TryMatchLocalPreset`（个人预设优先于官方预设的名称匹配，TODO 注明待补参数等价比较）；
3. **固件版本检查**：`CheckFirmwareVersionAsync` fire-and-forget（API 不可达可能阻塞 15s，不能挡住 USB 参数流程）→ 有新版显示"新版本可用"边框，点击跳转设置页固件更新 tab；
4. **参数修改追踪**：`OnParameterModified` 置 `_isPresetModified` → `UpdatePresetDisplay` 动态管理预设名截断（Measure 计算 MaxWidth）、已更改标记、撤回/保存按钮可用态、三种预设图标（Onboard/个人/官方）切换；
5. **未保存确认**：`ShowUnsavedDialog(onSaved, onCancelled)` 复用 `MainWindow.GlobalDialog`，个人预设额外提供"保存"按钮；
6. **热插拔响应**：订阅 `DeviceConnected/Disconnected`，经 `Dispatcher.InvokeAsync` 封送刷新（设备事件来自 WMI 线程）。

**面盘控件（2803 行，最大）** 的读写实现值得注意：
- **读取必须顺序 await 6 条命令**（注释明确：底层 SendCommandAsync 按设备键单例 TCS，并发会互相取消）—— 这是 §5.2.2 协议配对机制的 UI 侧影响；
- `WheelSendMask` 位掩码按 0x2103~0x2108 分发 6 个写入函数；**写入全部 fire-and-forget，无写后回读确认**；单按键增量优化、亮度滑块在 `Thumb.DragCompleted` 才下发；
- 面盘图形叠加层：Canvas 手工摆放 12 颗 LED、19 个按键；HID 数据（`OnWheelHidDataReceived`）经防抖 + Dispatcher.BeginInvoke + **预缓存模板部件**（GlowCircle/OuterRing，避免高频轮询遍历视觉树）驱动按键发光；
- 弹窗集成：点按键开 ButtonSettingsPopup（懒创建单例）、点转速灯热区开 RpmSettingsPopup（`SaveRpmSettings` 带"全零数据回滚"防御）。

**踏板控件（2556 行）** 的曲线编辑器是纯 Canvas 手绘 + 鼠标拖拽（无标准 Slider）：三张踏板卡片（离合黄/刹车红/油门绿），6 个曲线点（Y/X）与死区双滑块全部自绘；`PointFromProtocol`/`GetCurvePointsAsProtocolBytes` 完成协议字节 ↔ 画布坐标双向换算；实时位置由 `HidService.PedalDataReceived` **事件驱动**（非轮询）——`Interlocked.Exchange` 门 + Render 优先级 + 0.05 阈值防抖，后台线程做曲线变换后 UI 线程渲染；校准按钮 → `SendPedalCalibration`（0x21E1）→ `CalibrationDialog` 双列进度。

#### 5.7.6 设置页（SettingsUserControl，2490 行）

三 Tab（RadioButton 切换 Visibility + 淡入）：

| Tab | 内容 |
|---|---|
| 系统设置 | 开机自启（直接写注册表 `HKCU\...\Run`）、最小化到托盘（配合 MainWindow Closing 拦截）、语言切换（`LocalizationService.SetLanguage` 即时全 UI 刷新）、软件更新（`ClientInstallerApi` 两阶段：查版本 → 下载带 Path.Clip 进度 → 自动启动安装器）、关于（五社媒按钮 + 客服二维码） |
| 固件更新 | 拉云端固件列表 → 枚举已连接设备读当前版本 → 设备列表行（含 RelayCommand）→ 单设备更新（**代码动态构建**的进度弹窗：下载 0-20% + 刷写 20-100%，完成后后台轮询 15s 等设备重连）→ `StartBatchUpdateAsync` 批量更新 → 更新模式设备强制弹窗联动 |
| 账户设置 | 登录/未登录双态、改用户名（2-20 字符校验）、改密码（8 位+两次一致）、头像上传（multipart）、退出登录；邮箱修改整段注释掉 |

#### 5.7.7 登录弹窗（LoginPopup）

MainWindow 根 Grid 上的 UserControl 覆盖层（ZIndex=1001），`Show()/Hide()` 切 Visibility。三个可切换面板：

- **登录**：邮箱（"真实文本+GotFocus 清空"占位手法）+ PasswordBox（眼睛按钮用"叠加透明 TextBox"实现明文切换）+ 用户协议勾选（未勾选**抖动动画**提醒）；成功走 `LoginSuccessful`：`RefreshCurrentUserAsync` 补齐头像 → 刷新设置页登录态 → 关闭；
- **注册**：邮箱/用户名/密码/确认密码/验证码五字段 + OTP 验证码（60s 倒计时）+ 错误码映射（AUTH_EMAIL_TAKEN / AUTH_USERNAME_TAKEN / 429）；
- **找回密码**：两步式 —— `VerifyStepupAsync` 签发 **step-up JWT**（5 分钟有效，用后即弃）→ `ResetPasswordAsync` 设新密码；
- token 持久化全部委托 `UserApiService`（DPAPI 加密，见 §5.4.3），LoginPopup 自身不碰。

#### 5.7.8 弹窗群（DeviceParameters/）

| 弹窗 | 用途 | 实现要点 |
|---|---|---|
| PresetListPopup（2052 行） | 预设选择/管理 | 右侧滑入面板（AnimateIn/Out）+ 官方/个人/云三 Tab + 游戏类别"输入即搜索"ComboBox；列表项 **code-behind 命令式创建**（无 DataTemplate）；删除走 GlobalDialog 确认（云=API 删除、个人=本地移除）；`SelectAndScrollToPreset` 供游戏页跨页定位；MainWindow 按 DeviceType 缓存实例复用（保持滚动/tab 状态） |
| ButtonSettingsPopup | 单键 LED 配置 | 颜色色板/遥测开关/功能与灯效/速度滑块；`IsGlobalColorMode` 依赖属性联动禁用；出入场 = 遮罩淡入 + 面板 94%→100% 缩放 + BitmapCache 提升动画性能 |
| RpmSettingsPopup | 12 颗转速灯配置 | 左侧 12 色块+滑块一一映射；**爆闪 cap 虚线可拖拽**（Y↔百分比换算并反向截断全部滑块）；9 色索引表与协议 `ColorIndexToRgb` 对应 |
| EditPresetPopup | 预设编辑/另存为 | 编辑/另存为双入口（BeginEdit/BeginSaveAs）；A-Z 字母索引游戏勾选区（命令式构建）；校验空名/重名；自身不碰 PresetService（持久化由父弹窗完成） |
| CalibrationDialog | 踏板校准 | 绿色/红色双列 Star 比例进度，`UpdateXxxProgress` 实时刷新 |

#### 5.7.9 样式与主题

- 全局深色主题（#0B0B0B 底 + 红色 #C60E0E 渐变），无边框窗口（WindowStyle=None + AllowsTransparency）；FluentWPF 控件库（GameUserControl 用 `{fw:AcrylicBrush}` 模拟亚克力卡片；PresetListPopup 注释说明"Popup 不支持真实亚克力"用纯色替代）；
- `OrbitronFont` 数字显示字体 + `{lex:Font}` 语言字体联动（中文回退微软雅黑）+ `lex:LocFontSize` 按语言切换字号；
- 图标全 SVG（SharpVectors SvgViewbox），交互图标大量内联 `Path.Data` 手绘；斜切角（45° bevel）为贯穿全 UI 的视觉语言（按钮、卡片、弹窗边框、导航选中条）；
- 页面切换统一 0.3s CubicEase 淡入动效（顶级与子页一致）；
- **样式重复问题**：App 级只收敛了 ScrollBar/ProgressBar/CheckBox 与字体，ComboBox 斜角 Popup 模板、ScrollBar 模板、卡片模板在各文件重复 5+ 份，每页 Resources 各自重复定义 AccentGradient。

#### 5.7.10 UI 模式总结

- **弹窗三层模式**：① 全局确认框统一复用 `Controls\ModalDialog`（ClearButtons/AddButton/Show/Hide 编程式复用：未保存确认、启动失败、固件确认/进度、批量结果、更新模式强制弹窗）；② 复杂功能弹窗是"UserControl 覆盖层"（LoginPopup、PresetListPopup、EditPresetPopup、ButtonSettingsPopup、RpmSettingsPopup、CalibrationDialog），动态 Add + Panel.ZIndex + 进出场动画；③ 仅 ComboBox 下拉用 WPF Popup；
- **事件聚合**：主导航 MVVM；页面内几乎全 code-behind 直连；跨层通信靠静态单例事件 + MainWindow 公开方法。

### 5.8 本地化与辅助基础设施

`LocalizationService` 单例（Lazy 初始化）：扁平字典 JSON（zh-CN / en-US 各 462 行），`this[key]` 索引器缺失返回 key 本身（key 即兜底文案）；`SetLanguage` 重载字典 + 持久化 Settings + `OnPropertyChanged(null)` 全量刷新绑定；JSON 内还携带 `App.FontFamily`、`Nav.IconTextSpacing` 等**配置值**，语言切换同时驱动字体与布局间距变化。

---

## 6. 核心算法与数据流详解

### 6.1 算法索引

| # | 算法/机制 | 位置 | 核心思路 |
|---|---|---|---|
| 1 | 命令-应答配对 | DeviceProtocolService.cs:275-312 | `ConcurrentDictionary<deviceKey, TCS>` + `Task.WhenAny(应答, 超时)` 竞争；单设备单挂起天然串行化 |
| 2 | 多包分片重组 | DeviceProtocolService.cs:58-92 | 56B/包分片，首包 TotalLength 推算期望包数，**锁内判完整**防 TOCTOU，拼装后 UTF-8 解码 |
| 3 | 指数退避重连 | UsbSerialManager.cs:277-317 | `1s × 2^(n-1)`（封顶 30s）最多 5 次；成功广播恢复，耗尽置 Error |
| 4 | 固件升级状态机 | FirmwareUpdateService.cs | 11 阶段；模式切换用"等待者注册 + 连接事件唤醒"联动设备发现（两次：切 Boot 模式、回正常模式） |
| 5 | 遥测 60Hz 固定步长循环 | TelemetryService.cs:232-286 | Stopwatch 实测耗时补偿睡眠；每 300 帧进程存活检测；超时 10ms 告警 |
| 6 | 自适应 maxRpm | TelemetryService.cs:352-378 | 无 maxRpm 的游戏追踪峰值；转速连续 5s 为 0（换车）重置默认 6000 |
| 7 | 官方预设增量合并 | PresetService.cs:54-140 | 名称唯一键 + publishedAt 字典序比较：覆盖/新增/删除三路合并，双检锁只跑一次 |
| 8 | 预设导入四步校验 | PresetService.cs:349-440 | 枚举有效 → 快照存在 → 字段完整性（缺失+未知均拒）→ 值域 Validate |
| 9 | 高 DPI 等比缩放 | MainWindow.xaml.cs:267-311 | 显示器工作区（DIP）÷ 设计尺寸取 min，LayoutTransform 缩放 + 手动居中 |
| 10 | 跑马灯无缝循环 | Helpers/MarqueeBehavior.cs | 文本拼接"原文+3空格+原文"，FormattedText 测量步长，TranslateTransform 恒速动画 |
| 11 | 参数下发闭环 | BaseParameterControl 等 | 修改 → 快照 Validate → 下发 → 读回 → `ParametersEqual` 核对差异 |
| 12 | 请求重试策略 | ApiClient.cs:96-156 | 4xx 不重试；5xx/网络异常指数退避 500ms→1s→2s；401 全局回调 |

### 6.2 遥测数据流全链路（时序）

```
[游戏进程]                     [TelemetrySDK.dll]              [TelemetryService]              [USB 设备]
     │  游戏遥测 API/共享内存        │                                │                              │
     │ ───────────────────────────► │                                │                              │
     │                              │ 归一化（SanitizeNormalizedData） │                              │
     │                              │ ── GetTelemetryData(ref data) ─► │ 60Hz 循环                    │
     │                              │        validFlags 位掩码        │  ├─ ApplyAdaptiveMaxRpm      │
     │                              │                                │  ├─ BuildAllPackets → 5×64B  │
     │                              │                                │  └─ DispatchPackets ─────────►│ 基座/面盘/踏板
     │                              │                                │       （仅正常模式设备）      │  LED/振动/显示
     │ ◄── 每300帧检测进程存活 ──────┼────────────────────────────────│                              │
```

### 6.3 参数读写数据流（以"保存基座参数"为例）

```
Slider 拖动 → code-behind 值变更 → _isPresetModified=true（未保存标记）
  → 点击保存 → 从 UI 收集 16 项参数 → BuildSetBaseParametersCommand(0x2101)
  → SendCommandAsync(deviceKey, frame) ──► 串口 ──► 设备写入
  ◄── 设备应答 0xC1 0x01 0x21 ── TCS.TrySetResult
  → 读回参数校验（Get → Parse → ParametersEqual 核对）
  → SendPresetName（0x21D0 多包）→ 设备端记住预设名
  → 本地 personal.json 落盘（SemaphoreSlim 串行化）
```

### 6.4 设备发现与连接数据流

```
启动 / WMI 热插拔事件 / 手动刷新
  → UsbDeviceDiscovery.DiscoverDevices()（WMI Win32_PnPEntity 过滤 VID/PID）
  → UsbDeviceInfo（PortName/VID/PID/序列号）
  → UsbSerialManager.OnDeviceArrived
  → DeviceSerialChannel.Connect(115200-8-N-1) → StartReading()
  → DeviceConnected 事件 → UI 刷新连接状态（绿灯）→ 协议查询设备信息/固件版本/预设名
```

---

## 7. 代码设计模式与最佳实践总结

### 7.1 使用的设计模式

| 模式 | 应用位置 | 评价 |
|---|---|---|
| **Service Locator（服务定位器）** | `App` 静态属性暴露 11 个服务，全局 `App.XXX` 访问 | 零 DI 配置、上手快；耦合度高、可测试性弱；对固定单窗口应用务实 |
| **Observer（观察者）** | 全项目事件总线：`UsbManager.DeviceConnected/RawDataReceived`、`LocalizationService.PropertyChanged`、`TelemetryService.OnPacketsBuilt` 等 | 硬件事件→UI 解耦的标准做法；注意事件订阅/反订阅配对 |
| **Future/Promise（任务承诺）** | `SendCommandAsync` 的 TCS + 超时竞争 | 异步硬件 IO 的优雅封装，避免轮询 |
| **Template Method（模板方法）** | `Build*/Parse*` 成对静态函数，帧头校验→逐偏移解析的对称结构 | 新协议命令扩展成本极低 |
| **State Machine（状态机）** | 固件升级 11 阶段；`DeviceConnectionState` 连接状态机 | 复杂时序流程的正确抽象 |
| **Markup Extension（标记扩展）** | `{lex:Loc}`/`{lex:Font}`/`NavSpacing` 等 5 个扩展 | WPF 本地化与动态资源的正确姿势 |
| **Attached Behavior（附加行为）** | `MarqueeBehavior` 跑马灯 | 无 MVVM 侵入的 UI 行为复用 |
| **Discriminated Union（判别联合）** | `ApiResult<T>` 成功/失败一体化返回 | 避免异常驱动的控制流，错误契约统一 |
| **Double-Checked Locking（双重检查锁）** | `PresetService._hasCheckedApi` + `_checkLock` | 后台任务只执行一次的并发技巧 |
| **Object Pool / ArrayPool** | `HidChannel.Read` 租借 `ArrayPool<byte>` | 高频读取减少 GC 分配 |
| **Cache-Aside（旁路缓存）** | `MainWindowViewModel._viewCache`、`GameDataService._cachedGames`、`_presetListPopups` | 视图/数据实例复用，保状态、省开销 |

### 7.2 最佳实践亮点

1. **启动体验工程化**：独立 STA 线程 Splash + 离屏 UI 预热 + 先关 Splash 再 Show 主窗（避免闪烁）+ fire-and-forget 网络任务不阻塞启动 —— 三个层面的卡顿优化形成完整闭环；
2. **协议层可对照文档逐字节核验**：所有 Build/Parse 与 `docs/乘游直驱方向盘与PC软件usb通信协议 v0.1.md` 一一对应，静态纯函数可单测；
3. **"下发前校验、下发后核对"闭环**：预设快照 `Validate()` + `ParametersEqual()`，且明确区分纯 UI 概念字段（不参与下发比对），工程意识扎实；
4. **本地化即配置**：语言 JSON 不只存文案，还携带字体、字号、间距等布局参数，`OnPropertyChanged(null)` 全量刷新 —— 一套机制同时解决文案与布局的国际化；
5. **更新模式（Bootloader PID）统一治理**：设备注册表同时登记正常/更新两组 VID/PID，发现、强制弹窗、遥测广播过滤、固件升级模式切换全部基于同一事实来源；
6. **防御性编程细节**：`TaskScheduler.UnobservedTaskException` 兜底、CTS 延迟 1s Dispose（防引用已释放 Token）、锁内完整性判定防 TOCTOU、`Interlocked` 字节统计、模态框 `Hide()` 全量重置防状态污染。

### 7.3 有待改进的实践（详见 §8）

- 服务访问方式（App 静态属性）与事件订阅缺乏统一生命周期管理；
- 无日志框架（全 Debug.WriteLine，Release 零可观测性）；
- 密钥/内网地址硬编码进源码；
- 异步方法名不副实（GetGamesAsync 无 await）、下载实现每次 new HttpClient。

---

## 8. 已知问题、技术债务与改进建议

### 8.1 硬件通信层（优先级最高）

| # | 问题 | 位置 | 影响 | 建议 |
|---|---|---|---|---|
| H1 | **串口无 64 字节帧对齐缓冲**：ReadLoop 按 BytesToRead 任意切块，协议层把每个 byte[] 当一条响应 | DeviceSerialChannel.cs:203-224 / DeviceProtocolService.cs:53-101 | 定长帧被拆分/合并时解析错位或丢帧 | 通道层增加 64B 环形对齐重组，不足一帧则缓存 |
| H2 | **应答无命令码校验**：任意到达数据即完成挂起 TCS | DeviceProtocolService.cs:94-100 | 设备主动上报（协议 §2 明确）会"偷走"应答 | 应答匹配时校验帧头命令码与期望值 |
| H3 | **同设备并发命令互相覆盖**：`_pendingCommands[deviceKey]=tcs` 索引赋值 | DeviceProtocolService.cs:284 | 并发请求先者只能等超时 | 改为每设备命令队列或命令码+序列号匹配 |
| H4 | **旧通道未 Dispose（句柄泄漏）** | UsbSerialManager.cs:339-342 | 异常重连路径 SerialPort 句柄悬空到 GC | 摘除通道后显式 Disconnect + Dispose |
| H5 | **基座主控/蓝牙固件升级未接入**：非踏板一律用面盘命令码 0x7913 | FirmwareUpdateService.cs:163-167 | 基座升级会发错命令码 | 按 VID 补全命令码映射（0xF0F0/0x5634/0x7812/0x7711） |
| H6 | 串口读循环 `Task.Delay(1)` 轮询自旋 | DeviceSerialChannel.cs:204-207 | 多设备时 CPU 空转 | 放大间隔或事件驱动（DataReceived） |
| H7 | `DeviceLogger` 仅 Debug.WriteLine，与 `docs/USB串口通信模块说明.md` 宣称的文件日志/内存队列不符 | DeviceLogger.cs | Release 零可观测性 | 接入 Serilog/NLog 或实现文档所述能力 |
| H8 | HID 通道键 VID:PID 唯一，同型号双设备互相覆盖 | HidService.cs:109 | 双设备场景失效 | 键中加入序列号 |
| H9 | `UsbDeviceInfo.State` 无 volatile/锁，多线程读写 | UsbSerialManager.cs:206 等 | 理论脏读 | 状态字段 volatile 或锁保护 |
| H10 | `FirmwareUpdateService._updateLock` 声明未使用，`IsUpdating` 检查非原子 | FirmwareUpdateService.cs:95,218 | 并发升级互相覆盖 CTS | 用 lock 或 Interlocked 保护 |

### 8.2 数据/API 层

| # | 问题 | 位置 | 建议 |
|---|---|---|---|
| D1 | **Strapi API Token 与内网 IP 硬编码 4 处**（ClientInstaller/DevicePreset/Firmware/GameData），明文入库 | 各 ApiService.cs:17-27 | 统一配置化，Token 移出仓库 |
| D2 | `GetGamesAsync` 无 await 却声明 async；`StateChanged` 同步触发 | GameDataService.cs:50-74 | 真异步化或改同步方法 |
| D3 | 下载实现每次 `new HttpClient`；FirmwareApiService.cs:85 分配未使用的 totalBytes 缓冲 | ClientInstallerApiService.cs:61 / FirmwareApiService.cs:70,85 | 复用单例 HttpClient；删除无用缓冲 |
| D4 | 退避 `Task.Delay(ct)` 在 try 外，取消异常直接抛给调用方，破坏"不抛异常"约定；`TaskCanceledException` 无法区分超时与取消 | ApiClient.cs:131-134,148-150 | 统一返回 ApiResult.Failure |
| D5 | `LocalGameCacheService.Save` 无锁全量覆写；`PresetService` 缓存读取未加锁 | LocalGameCacheService.cs:29-33 / PresetService.cs | 加锁或原子写（写临时文件+替换） |
| D6 | Debug.WriteLine 打印完整请求/响应体（含邮箱、密码哈希） | ApiClient.cs:285,301-303 | 脱敏后记录 |
| D7 | `GetWrappedAsync` 解包 `result.Data!.Data`，200 但 body 非法时 NRE | ApiClient.cs:248,272 | null 判空 |
| D8 | 客户端密码为无盐 SHA-256 | PasswordHasher.cs:13-19 | 改为服务端下发 salt 的加盐哈希，或至少客户端加盐 |

### 8.3 UI/框架层

| # | 问题 | 位置 | 建议 |
|---|---|---|---|
| U1 | app.manifest 声明 `requireAdministrator`，每次启动触发 UAC | app.manifest:19 | 评估降级 `asInvoker`（仅固件刷写提权） |
| U2 | `UserAccessToken` 虽 DPAPI 加密，但 `Settings.settings` 中 ApiBaseUrl 为明文内网 HTTP 地址 | Settings.settings:27 | 部署配置化 |
| U3 | 视图 code-behind 体量过大（最大 2803 行），业务逻辑与 UI 耦合 | SteeringWheelParameterControl.xaml.cs 等 | 逐步抽取参数 VM 与命令 |
| U4 | 全局异常仅 Debug 输出，生产不可观测 | App.xaml.cs:54-64 | 落盘日志 + 崩溃报告 |
| U5 | `_viewCache`/`_presetListPopups` 实例直到进程结束才释放；主窗口事件订阅未反订阅 | MainWindow.xaml.cs | 关闭时统一清理（实际随进程退出，影响小） |
| U6 | 根目录 `123.txt` 为调试笔记（游戏卡片动画 Bug 复盘），`Assets/Presets/` 下有大量测试预设（123.json、asdaf.json 等） | 根目录 / Assets/Presets | 清理或移入 docs |
| U7 | `LocThicknessExtension` FallbackValue 与 Parse 失败默认值不一致 | LocThicknessExtension.cs:69,131 | 统一默认值 |
| U8 | **Game 页卡片悬浮动画的"帕金森"问题未修复**：首页已用透明 HitBox 幽灵命中层修复（123.txt 记录的 bug），Game 页同款模板仍无隔离 | GameUserControl.xaml:1376-1409 | 与首页模板统一 |
| U9 | **本地化字符串被当代码写**：`SettingsUserControl.xaml.cs:2027, 2197` 出现 `currentVersion = "LocalizationService.Instance[\"Firmware.UpdateMode\"]"`（字符串字面量而非调用） | SettingsUserControl.xaml.cs | 改为实际调用 |
| U10 | **巨型 code-behind + 代码重复**：PresetItem/RelayCommand 等模型类定义在 View 文件内（PresetListPopup.xaml.cs:1955-2052）；三参数控件信息条/预设条结构、双份互译表、两套游戏卡片模板大量重复 | 多个 View 文件 | 抽取共享控件与模型 |
| U11 | **事件订阅永不取消**：`UnsubscribeHidData` 定义后从未调用；`LocalizationService.PropertyChanged` 无对应反订阅；`async void` 事件处理器异常逃逸 | SteeringWheelParameterControl.xaml.cs 等 | 视图销毁时反订阅；async void 加 try/catch |
| U12 | **首页 5 个 DispatcherTimer（50-100ms）常驻运行**：模拟仪表动画在页面不可见时也不停；`SizeChanged` 中 `Geometry.Parse` 字符串拼接重绘 Path | HomeUserControl.xaml.cs / SettingsUserControl.xaml.cs:2407-2426 | 页面隐藏时停止定时器；缓存几何 |
| U13 | 小 bug 群：`OnPresetApplied` 的 onSaved/onCancelled 都执行 ApplyPreset（疑似）；`SendWheelParameters` if/else 两分支相同；`SetDisconnected` 静默丢弃未保存修改；`KeyResponseName` 无按键时回退显示"未知版本"文案语义错误 | GameUserControl.xaml.cs:1356-1358 等 | 逐一修正 |

### 8.4 文档与实现漂移

| 文档 | 漂移点 |
|---|---|
| `docs/USB串口通信模块说明.md` | 宣称文件日志/内存队列/LogEntryAdded，代码仅 Debug.WriteLine |
| `docs/完整API文档.md` | 存在 `/api/games` 游戏端点，代码未调用（游戏列表纯本地硬编码） |
| `docs/用户系统api文档(1).md` | BaseUrl 可配置要求仅 UserApiService 遵守 |
| `docs/Update_Notes_v2.0.md` | 迁移指南与代码一致（v2.0 已迁移完成） |
| `docs/TelemetrySDK_API_Document.md` / `docs/manual/归一化参数.md` | `GetSDKVersion` 文档仍写"返回 1"（实际 v2.0）；归一化手册仍描述 `-1` 哨兵语义（v2.0 已改为置 0 + validFlags） |

### 8.5 遥测/游戏集成层

| # | 问题 | 位置 | 建议 |
|---|---|---|---|
| T1 | **TelemetrySDK.dll 缺失无优雅降级**：首次 P/Invoke 抛 `DllNotFoundException`，启动无 try/catch | TelemetryService.cs:156 | 启动前检测 DLL 存在并提示 |
| T2 | **配置覆写破坏用户设置**：LFS cfg.txt、DiRT hardware_settings_config.xml 为整文件覆写（含分辨率/刷新率等无关项） | TelemetryConfigService.cs:105-115, 688-707 | 改为最小化改写（如 F1 的 XDocument 方式） |
| T3 | **EA WRC 文本级 JSON 注入脆弱**：靠缩进/括号深度启发式定位 packets 数组 | TelemetryConfigService.cs:538-679 | 改用结构化 JSON 操作或更鲁棒的定位 |
| T4 | **WRCG 地址疑似笔误**：`WRC.Telemetry.TelemetryAdress = "127.0.1.1"` 与注入条目的 `127.0.0.1` 不一致 | TelemetryConfigService.cs:379 vs 630 | 统一地址 |
| T5 | **游戏 ID 四处重复维护**：GameListConfig / GameId 枚举 / GameProcessNames 表 / ApplyConfig 分发表 | 多处 | 单一数据源生成 |
| T6 | 事件全部后台线程触发，服务层不封送（当前靠 UI 侧 Dispatcher.BeginInvoke 约定维持） | TelemetryService.cs:88-101 | 文档化线程契约或服务内封送 |
| T7 | 无卸载/还原流程（除 WRC 备份还原外，SCS 插件/LMU JSON/DiRT/F1 配置均无"移除配置"） | TelemetryConfigService.cs | 增加还原操作 |
| T8 | `_trackedMaxRpm` 等状态无锁（依赖先 Join 旧线程的时序）；`Task.Run(() => Stop())` 与并发 Stop 边缘竞争 | TelemetryService.cs:171-175, 259 | 状态字段 volatile 或锁保护 |

---

## 9. 附录

### 9.1 文档资产清单（docs/）

| 文档 | 内容 |
|---|---|
| `乘游直驱方向盘与PC软件usb通信协议 v0.1.md`（1104 行） | 硬件协议全量定义：帧格式、设备信息、参数命令、固件升级、遥测、HID 报告 |
| `TelemetrySDK_API_Document.md`（1010 行） | TelemetrySDK.dll C 接口、NormalizedData 结构、字段支持矩阵 |
| `Update_Notes_v2.0.md` | SDK v1→v2 破坏性迁移指南 |
| `用户系统api文档(1).md` / `user-api.md` / `完整API文档.md` | 用户系统/云预设 API 契约 |
| `固件版本接口.md` / `海报接口.md` / `游戏接口.md` | 各业务端点说明 |
| `USB串口通信模块说明.md` / `HID数据采集与UI更新逻辑说明.md` | 模块设计说明（部分与实现漂移，见 §8.4） |
| `manual/`（19 篇） | 各游戏接入手册（含 ETS2/ATS 共享内存插件、归一化参数说明） |
| `遥测支持.csv` | 31 游戏 × 45 遥测字段支持矩阵 |
| `WRC系列/` | WRC 8/9/10 注入补丁与安装脚本（InstallWrc10.bat / InstallWrc23.bat + checksum） |
| `遥测与游戏集成层分析报告.md` | 本次分析子报告（遥测层专题，与本文档 §5.3/§8.5 互补） |

### 9.2 支持游戏全景（31 款，来自 GameListConfig）

AC 系列 4（AC/ACC/AC Rally/AC EVO）、F1 系列 4（F1 22-25）、Forza 系列 4（FM/FH4/FH5/FH6）、DiRT 2（D4/DR2.0）、rFactor/LMU 2、PCARS/AMS2 3、WRC 5（WRC8/9/10/WRCG/EA WRC）、其他 3（iRacing/R3E/BeamNG）、卡车 2（ETS2/ATS）、非 Steam 2（RBR/LFS）—— 与 TelemetryAPI.GameId 枚举一一对应；遥测能力差异见 `docs/遥测支持.csv`。

### 9.3 关键行号索引

| 关注点 | 位置 |
|---|---|
| 启动时序 | App.xaml.cs:29-88 |
| 服务装配 | App.xaml.cs:125-195 |
| 启动预热 | MainWindow.xaml.cs:109-138 |
| 高 DPI 缩放 | MainWindow.xaml.cs:267-311 |
| 未保存导航保护 | MainWindow.xaml.cs:758-830 |
| 更新模式弹窗 | MainWindow.xaml.cs:434-558 |
| 命令-应答配对 | DeviceProtocolService.cs:275-312 |
| 多包预设名收集 | DeviceProtocolService.cs:58-92, 916-956 |
| 指数退避重连 | UsbSerialManager.cs:277-317 |
| 固件升级状态机 | FirmwareUpdateService.cs:212-435 |
| 遥测主循环 | TelemetryService.cs:232-286 |
| 自适应 maxRpm | TelemetryService.cs:352-378 |
| 遥测打包 | TelemetryPacketBuilder.cs:56-366 |
| API 重试策略 | ApiClient.cs:96-156 |
| 官方预设增量同步 | PresetService.cs:54-140 |
| 本地化服务 | LocalizationService.cs:98-162 |
