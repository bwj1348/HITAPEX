# HITAPEX 项目代码审查报告

> 审查日期：2025-06-16 | 审查范围：全部手写源代码（~18,588 行 C#，15 个 XAML 文件）

---

## 🔴 严重问题 (CRITICAL)

### 1. 硬编码 API Token（安全）
- **文件：** [Services/Data/GameDataService.cs](../Services/Data/GameDataService.cs#L15-L17)、[Services/Data/Api/FirmwareApiService.cs](../Services/Data/Api/FirmwareApiService.cs#L13-L15)
- **问题：** 256 字符的 Bearer Token 以 `const string` 硬编码在两个文件中。任何人通过反编译（ILSpy/dnSpy）即可提取。Token 通过 HTTP 明文传输。
- **修复：** 移至加密配置文件、Windows 凭据管理器或环境变量；切勿将密钥作为 `const string`。

### 2. 无依赖注入 / 全局可变状态（架构）
- **文件：** [App.xaml.cs](../App.xaml.cs#L14-L20)
- **问题：** 所有服务以 `public static` 属性暴露在 `App` 类上（服务定位器反模式）。所有视图直接访问 `App.UsbManager`、`App.ProtocolService` 等。无 IoC 容器，所有依赖通过 `new()` 创建。
- **影响：** 零可测试性，无法进行单元测试。

### 3. 所有视图缺少 ViewModel（架构）
- **文件：** 所有 `Views/*.xaml.cs`
- **问题：** 除 `MainWindowViewModel`（仅 84 行的导航辅助类）外，所有视图将业务逻辑直接放在 code-behind 中。`SettingsUserControl.xaml.cs` 高达 1365 行，`HomeUserControl.xaml.cs` 1107 行，code-behind 中包含服务调用、状态管理、动画逻辑和 USB 通信。
- **影响：** 职责分离完全缺失，无法进行视图级别的单元测试。

### 4. 无固件签名验证（安全）
- **文件：** [FirmwareApiService.cs](../Services/Data/Api/FirmwareApiService.cs#L48-L96)、[FirmwareUpdateService.cs](../Services/Data/Api/../Usb/FirmwareUpdateService.cs#L156-L379)
- **问题：** 固件从 HTTP 明文下载后**未进行任何加密签名验证**即刷入硬件设备。`FirmwareFileInfo` 模型有 `Hash` 属性但**从未被验证**。
- **影响：** MITM 攻击可注入恶意固件导致硬件变砖或受损。

### 5. HTTP 明文通信（安全）
- **文件：** [GameDataService.cs](../Services/Data/GameDataService.cs#L15)、[FirmwareApiService.cs](../Services/Data/Api/FirmwareApiService.cs#L13)
- **问题：** 所有 API 通信（游戏数据、海报、固件）通过 `http://192.168.1.214:1337/api` 明文传输。API Token 和所有数据均无加密。
- **修复：** 切换至 HTTPS 并配置正确的证书验证。

---

## 🟠 高危问题 (HIGH)

### 6. 固件更新 ProgressChanged 事件泄漏
- **文件：** [SettingsUserControl.xaml.cs](../Views/SettingsUserControl.xaml.cs#L445-L451)
- **问题：** 如果 `UpdateFirmwareAsync` 在 `await` 前抛异常，`ProgressChanged -= OnUpdateProgress` 不会执行，handler 持续泄漏到静态的 `App.FirmwareUpdater` 上，捕获已释放的 `_updateCts`。

### 7. DeviceProtocolService 竞态条件
- **文件：** [DeviceProtocolService.cs](../Services/Usb/DeviceProtocolService.cs#L37-L73)
- **问题：** 在 `lock(collection.Packets)` 释放锁后，又读取 `collection.Packets[0].TotalLength`。期间另一个线程可能通过 `_presetNameCollections.TryRemove` 清除了该条目（TOCTOU 竞态）。

### 8. PedalParameterControl 跨线程访问曲线缓存数组
- **文件：** [PedalParameterControl.xaml.cs](../Views/DeviceParameters/PedalParameterControl.xaml.cs#L101-L106)
- **问题：** `Point[]` 曲线缓存数组在后台 HID 线程读取、UI 线程写入，无任何同步机制。`Point[]` 操作非原子性。

### 9. 大量 Fire-and-Forget 任务丢失异常
- **文件：** 全项目 10+ 处 `_ = Task.Run(...)` 模式
- **问题：** 后台任务未注册 `TaskScheduler.UnobservedTaskException` handler，异常静默丢失，导致读取循环无声崩溃、重连任务中止。

### 10. HID ReadLoop 以 200Hz 忙等待烧 CPU
- **文件：** [HidService.cs](../Services/Usb/HidService.cs#L165-L188)
- **问题：** `Task.Delay(5, token).Wait(token)` 阻塞线程池线程，3+ 设备连接时 3 个线程在 200Hz 下忙等待。每 5ms 分配一次新的 `byte[]` 缓冲区。
- **修复：** 使用 overlapped I/O 或 `CancellationToken.WaitHandle.WaitOne(5)`。

### 11. SkipInkTextBlock 每帧创建 FormattedText 和几何对象
- **文件：** [SkipInkTextBlock.cs](../Controls/SkipInkTextBlock.cs#L108-L129)
- **问题：** `OnRender` 每次合成传递都创建新的 `FormattedText`、`Typeface` 并调用 `VisualTreeHelper.GetDpi`，造成极高的渲染开销。
- **修复：** 缓存 `FormattedText` 和几何对象，仅在文本/格式改变时重建。

### 12. HID/Serial 数据解析频繁分配数组
- **文件：** [HidPedalData.cs](../Models/Usb/HidPedalData.cs#L38-L48)、[HidBaseData.cs](../Models/Usb/HidBaseData.cs#L28-L51)、[HidWheelData.cs](../Models/Usb/HidWheelData.cs#L35-L55)
- **问题：** 每次 HID 读取（200Hz）分配 `new ushort[8]`、`new byte[16]` 等。3 设备 × 200Hz = 600+ 分配/秒。
- **修复：** 使用 `stackalloc`、`ArrayPool<byte>` 或固定大小结构体。

### 13. ConnectedDevices / ConnectedHidDevices 每次访问创建新列表
- **文件：** [UsbSerialManager.cs](../Services/Usb/UsbSerialManager.cs#L27-L28)、[HidService.cs](../Services/Usb/HidService.cs#L24-L28)
- **问题：** 每次访问 `.ToList().AsReadOnly()` 分配新列表和包装器。这些属性被反复调用。
- **修复：** 缓存结果，在连接/断开时失效。

### 14. UpdateButtonText 破坏按钮模板结构
- **文件：** [SettingsUserControl.xaml.cs](../Views/SettingsUserControl.xaml.cs#L876-L882)
- **问题：** 找到模板内 `TextBlock` 后却设置 `CheckUpdateButton.Content = text`，将整个按钮 Content 替换为字符串，破坏自定义模板视觉结构。

### 15. 大量代码重复
- **文件：** `HomeUserControl.xaml` 和 `GameUserControl.xaml`
- **问题：** 游戏卡片模板（~450 行）、滚动条逻辑、悬停动画、对话框、按钮模板等均有大幅重复。`RelayCommand` 在两个文件中重复定义。

### 16. DeviceProtocolService 超大类（962 行）
- **文件：** [DeviceProtocolService.cs](../Services/Usb/DeviceProtocolService.cs)
- **问题：** 单一类包含所有协议解析/序列化逻辑（设备信息、踏板参数、基座参数、方向盘参数、固件更新、预设名称等 10+ 种协议）。应拆分为按协议族的独立类。

### 17. 无 Unit Test / 零可测试性
- **问题：** 解决方案中没有测试项目。无 DI、无接口、全局静态状态使添加测试需要大量重构。

---

## 🟡 中危问题 (MEDIUM)

### 线程安全
- `FirmwareUpdateService._currentUpdateCts` 无同步访问（`IsUpdating` 与 `= null` 竞态）
- `SerialPort.ErrorReceived` handler 在非线程安全的上下文中创建新 `DeviceSerialChannel`
- `UsbDeviceDiscovery.Thread.Sleep(500)` 阻塞 WMI 事件线程 500ms

### 异常处理
- 大量空 `catch { }` 静默吞下异常（`GameLauncher.cs`、`SteamInstallService.cs`、`ImageCacheService.cs` 等 10+ 处）
- `catch (Exception)` 捕获所有异常包括 `OutOfMemoryException`（`HomeUserControl.xaml.cs:159`）
- `SettingsUserControl.LoadSettings` 空 catch 导致可能的不一致 UI 状态
- 服务层缺少 `ConfigureAwait(false)`，存在死锁风险

### 资源泄漏
- 弹窗动画 `BitmapCache` 未释放（4 个弹窗，每次显示/隐藏循环泄漏）
- `CancellationTokenSource` 释放竞态导致 `ObjectDisposedException`
- `HomeUserControl.xaml.cs` 的 `MemoryStream` 传给 `BitmapImage.StreamSource` 后未释放
- `HomeUserControl` 启动时同时启动 6 个 `DispatcherTimer`

### 架构问题
- `PresetService.SavePersonalPresets` 读取-修改-写入模式缺少文件锁
- `GameDataService`、`PresetService` 等多处缺少接口抽象
- 模型类（`FirmwareVersionInfo`、`DeviceParameters` 等）缺少 `INotifyPropertyChanged`

---

## 🟢 低危问题 (LOW)

- 多处使用 `Debug.WriteLine` 在 Release 构建中**不会被剥离**（需要 `[Conditional("DEBUG")]` 或 `#if DEBUG`）
- USB 原始数据记录到明文日志文件，无加密、无轮转策略、无大小限制
- API 异常消息直接显示给用户（`ApiClient.cs:86-88`、`SettingsUserControl.xaml.cs:506`）
- `HidPedalData.Percent` 计算属性每次访问重算浮点除法（应预计算）
- `DeviceLogger.TakeLast` 在 `ConcurrentQueue` 上使用 LINQ，低效
- `DecodePixelWidth` 缺失导致全分辨率解码图片（浪费内存）
- 自定义曲线类型 5（手动拖拽）与类型 1（线性）返回相同曲线
- `PresetItem` 模型类定义在 `PresetListPopup.xaml.cs` 视图文件中
- 重复从 `FindResource` 获取 `Storyboard`
- 所有 XAML 使用硬编码像素尺寸，无自适应布局

---

## 📋 优先修复建议

1. **移除硬编码 API Token** → 环境变量或加密配置
2. **切换至 HTTPS** + 固件签名验证
3. **修复 HID ReadLoop 忙等待** → overlapped I/O
4. **修复 ProgressChanged 事件泄漏** → try/finally
5. **修复 DeviceProtocolService 竞态** → 锁范围内完成所有读取
6. **引入 DI 容器**（`Microsoft.Extensions.DependencyInjection`）
7. **为每个 View 创建 ViewModel**，逐步迁移业务逻辑
8. **拆分 DeviceProtocolService** 和超大 code-behind 文件
9. **提取共享 XAML 资源**（游戏卡片模板、按钮样式）
10. **添加 `TaskScheduler.UnobservedTaskException` handler**
