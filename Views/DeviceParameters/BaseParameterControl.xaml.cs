using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HITAPEX.Models.Usb;
using HITAPEX.Services;

namespace HITAPEX.Views.DeviceParameters;

/// <summary>
/// 基座设备参数控制视图。
/// 负责管理基座设备的参数显示、USB 连接状态监控、固件版本检查以及预设方案的加载、匹配与保存。
/// 通过订阅 USB 设备事件实现设备热插拔时的界面实时响应，并支持中英文语言切换。
/// </summary>
public partial class BaseParameterControl : UserControl
{
    // ═══════════════════════════════════════════
    // 设备通信状态字段
    // ═══════════════════════════════════════════

    /// <summary>当前通过 USB 串口连接的基座设备信息，未连接时为 null</summary>
    private UsbDeviceInfo? _connectedBaseDevice;
    /// <summary>设备类型显示名称（可通过语言服务本地化）</summary>
    private string _deviceTypeName = LocalizationService.Instance["Status.DeviceTypeBase"];
    /// <summary>设备型号名称（从 DeviceRegistry 解析）</summary>
    private string _deviceModel = "";
    /// <summary>连接状态文本（已连接/未连接）</summary>
    private string _connectionStatusText = LocalizationService.Instance["DeviceParam.NotConnected"];
    /// <summary>连接状态指示灯颜色（绿色已连接，红色未连接）</summary>
    private string _connectionStatusColor = "#C60E0E";
    /// <summary>设备固件版本号字符串</summary>
    private string _firmwareVersion = LocalizationService.Instance["DeviceParam.UnknownVersion"];
    /// <summary>API 服务器上可用的最新固件版本号，用于判断是否需要提示升级</summary>
    private string? _latestApiFirmwareVersion;

    // ═══════════════════════════════════════════
    // 预设管理字段
    // ═══════════════════════════════════════════

    /// <summary>当前参数是否已被用户修改，用于标记未保存状态</summary>
    private bool _isPresetModified;
    /// <summary>当前应用的预设是否属于个人预设（false 则为官方预设）</summary>
    private bool _isAppliedPresetPersonal;
    /// <summary>当前预设名称，默认为"Default"</summary>
    private string _currentPresetName = LocalizationService.Instance["DeviceParam.Default"];
    /// <summary>从设备上报读取到的预设名称，用于与本地预设进行名称匹配</summary>
    private string _devicePresetName = string.Empty;
    /// <summary>标记控件是否已完成首次初始化，避免重复注册事件</summary>
    private bool _isInitialized;

    /// <summary>获取当前是否存在未保存的参数修改</summary>
    public bool HasUnsavedChanges => _isPresetModified;

    // ═══════════════════════════════════════════
    // 构造函数与初始化
    // ═══════════════════════════════════════════

    /// <summary>
    /// 初始化基座参数控件，注册 Loaded 事件以便在界面加载完成后执行设备检测与预设刷新。
    /// </summary>
    public BaseParameterControl()
    {
        InitializeComponent();
        Loaded += BaseParameterControl_Loaded;
    }

    /// <summary>
    /// 控件首次加载时执行初始化：订阅 USB 设备事件、注册语言切换回调，
    /// 并刷新设备信息与预设显示。
    /// </summary>
    private async void BaseParameterControl_Loaded(object sender, RoutedEventArgs e)
    {
        // 仅首次加载时执行一次性初始化，避免重复注册事件
        if (!_isInitialized)
        {
            _isInitialized = true;
            SubscribeUsbSerialEvents();
            LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        }

        // 每次页面切换回来时刷新设备信息和预设显示
        await RefreshDeviceInfoAsync();
        UpdatePresetDisplay();
    }

    // ═══════════════════════════════════════════
    // 设备通信
    // ═══════════════════════════════════════════

    /// <summary>
    /// 刷新设备连接信息。
    /// 从 USB 管理器获取当前连接的所有设备，筛选出处于正常模式（非更新模式）的基座设备，
    /// 并读取其型号、固件版本、设备端预设名称等信息。
    /// </summary>
    public async Task RefreshDeviceInfoAsync()
    {
        try
        {
            // 获取当前所有 USB 连接的设备列表
            var connectedDevices = App.UsbManager?.ConnectedDevices
                ?? System.Collections.ObjectModel.ReadOnlyCollection<UsbDeviceInfo>.Empty;

            // 筛选处于正常模式（非更新模式）的基座设备：
            // 1. 通过 VID/PID 在 DeviceRegistry 中查找设备描述符
            // 2. 设备类型必须为 Base（基座）
            // 3. 设备必须处于正常模式（IsNormalMode 返回 true），排除更新模式设备
            _connectedBaseDevice = connectedDevices.FirstOrDefault(d =>
            {
                var descriptor = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                return descriptor != null && descriptor.DeviceType == DeviceType.Base
                       && descriptor.IsNormalMode(d.Vid, d.Pid);
            });

            if (_connectedBaseDevice != null)
            {
                // 设备已连接：解析型号名称，更新连接状态文本和颜色
                var descriptor = DeviceRegistry.FindByVidPid(_connectedBaseDevice.Vid, _connectedBaseDevice.Pid);
                _deviceModel = descriptor?.ModelName ?? "";
                _connectionStatusText = LocalizationService.Instance["DeviceParam.ConnectedDirect"];
                _connectionStatusColor = "#179548"; // 绿色表示已连接

                // 通过协议服务获取设备固件版本号
                if (App.ProtocolService != null && App.FirmwareUpdater != null)
                {
                    var deviceInfo = await App.FirmwareUpdater.GetDeviceInfoAsync(
                        _connectedBaseDevice, DeviceType.Base);
                    _firmwareVersion = deviceInfo?.VersionString ?? LocalizationService.Instance["DeviceParam.Unknown"];
                }
            }
            else
            {
                // 未找到已连接的基座设备，恢复未连接状态
                SetDisconnected();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BaseControl] 刷新设备信息异常: {ex.Message}");
            SetDisconnected();
        }

        // 更新连接状态指示灯与文本显示
        UpdateConnectionStatusDisplay();

        // 固件版本检查采用 fire-and-forget 模式：
        // API 服务器不可达时可能阻塞 15 秒以上，不应延迟后续 USB 参数获取命令
        _ = CheckFirmwareVersionAsync();

        // 从设备获取预设名称
        await FetchPresetNameAsync();

        // 尝试将设备上报的预设名称匹配到本地已保存的预设
        TryMatchLocalPreset();
    }

    /// <summary>
    /// 对比设备上报的预设名称与本地预设列表，若名称匹配则视为本地预设。
    /// 匹配优先级：个人预设 > 官方预设。
    /// TODO: 添加参数等价比较（需要 BasePresetSnapshot 模型）
    /// </summary>
    private void TryMatchLocalPreset()
    {
        // 如果设备未上报预设名称或预设服务不可用，跳过匹配
        if (string.IsNullOrEmpty(_devicePresetName) || App.PresetService == null)
            return;

        try
        {
            // 加载本地个人预设和官方预设
            var officialPresets = App.PresetService.LoadOfficialPresets(DeviceType.Base);
            var personalPresets = App.PresetService.LoadPersonalPresets(DeviceType.Base);

            // 先查个人预设，再查官方预设（个人预设优先级更高）
            PresetItem? matched = personalPresets.FirstOrDefault(p => p.Name == _devicePresetName);
            bool isPersonal = true;
            if (matched == null)
            {
                matched = officialPresets.FirstOrDefault(p => p.Name == _devicePresetName);
                isPersonal = false;
            }

            if (matched != null)
            {
                // TODO: 添加 ParametersEqual 调用 when BasePresetSnapshot is implemented
                // 名称匹配成功，将设备预设映射为本地预设
                _currentPresetName = matched.Name;
                _isAppliedPresetPersonal = isPersonal;
                _devicePresetName = string.Empty; // 清空设备预设名，表示已匹配到本地
                Debug.WriteLine($"[BaseControl] 设备预设匹配到本地{(isPersonal ? "个人" : "官方")}预设: {matched.Name}");
                UpdatePresetDisplay();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BaseControl] 匹配本地预设异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 通过串口协议从设备获取当前预设名称。
    /// 获取到名称后存储到 _devicePresetName，后续由 TryMatchLocalPreset 进行本地匹配。
    /// </summary>
    private async Task FetchPresetNameAsync()
    {
        if (_connectedBaseDevice == null || App.ProtocolService == null)
            return;

        try
        {
            var name = await App.ProtocolService.GetPresetNameAsync(_connectedBaseDevice.DeviceKey, DeviceType.Base);
            if (name != null)
            {
                _devicePresetName = name;
                // 如果当前处于默认预设且未修改，刷新预设显示以反映设备端预设名
                if (_currentPresetName == LocalizationService.Instance["DeviceParam.Default"] && !_isPresetModified)
                    UpdatePresetDisplay();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BaseControl] 获取预设名称异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 将预设名称通过串口协议下发到设备。
    /// 用于在应用预设后将名称写入设备，使设备端能够显示当前使用的预设信息。
    /// </summary>
    private void SendPresetName(string name)
    {
        if (_connectedBaseDevice == null || App.ProtocolService == null)
            return;

        try
        {
            App.ProtocolService.SetPresetName(_connectedBaseDevice.DeviceKey, DeviceType.Base, name);
            Debug.WriteLine($"[BaseControl] 预设名称已下发: {name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BaseControl] 下发预设名称异常: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════
    // 连接状态与UI显示更新
    // ═══════════════════════════════════════════

    /// <summary>
    /// 设置断开连接状态，重置所有设备相关字段为默认值。
    /// 包括清空设备信息、重置预设状态、恢复未连接的颜色和文本。
    /// </summary>
    private void SetDisconnected()
    {
        _connectedBaseDevice = null;
        _deviceModel = "";
        _connectionStatusText = LocalizationService.Instance["DeviceParam.NotConnected"];
        _connectionStatusColor = "#C60E0E"; // 红色表示未连接
        _firmwareVersion = LocalizationService.Instance["DeviceParam.UnknownVersion"];

        // 重置预设状态：恢复为默认预设，清除修改标记
        _currentPresetName = LocalizationService.Instance["DeviceParam.Default"];
        _devicePresetName = string.Empty;
        _isPresetModified = false;
        _isAppliedPresetPersonal = false;
    }

    /// <summary>
    /// 刷新连接状态 UI 显示：更新设备型号文本、连接状态文本、固件版本文本，
    /// 并根据连接状态设置 7 个连接指示图标（ConnStatusIcon1-7）的描边颜色。
    /// </summary>
    private void UpdateConnectionStatusDisplay()
    {
        // 更新设备型号显示（例如 "基座 HITAPEX-Base"）
        if (DeviceModelName != null)
            DeviceModelName.Text = BuildDeviceDisplayName();

        // 更新连接状态文本
        if (ConnectionStatusText != null)
            ConnectionStatusText.Text = _connectionStatusText;

        // 更新固件版本显示
        if (FirmwareVersionText != null)
            FirmwareVersionText.Text = _firmwareVersion;

        // 将颜色字符串解析为 Brush，统一设置 7 个连接状态图标的描边颜色
        var color = (Color)ColorConverter.ConvertFromString(_connectionStatusColor);
        var brush = new SolidColorBrush(color);
        var iconPaths = new[] { ConnStatusIcon1, ConnStatusIcon2, ConnStatusIcon3,
                                ConnStatusIcon4, ConnStatusIcon5, ConnStatusIcon6, ConnStatusIcon7 };
        foreach (var path in iconPaths)
        {
            if (path != null)
                path.Stroke = brush;
        }
    }

    // ═══════════════════════════════════════════
    // 固件版本检查
    // ═══════════════════════════════════════════

    /// <summary>
    /// 异步检查设备固件版本是否为最新。
    /// 通过 FirmwareApi 从服务器获取最新固件列表，
    /// 根据当前设备的 VID/PID 匹配合适的固件版本，并与设备当前版本进行对比。
    /// 如果有更新版本可用，则显示新版本提示边框（NewVersionAvailableBorder）。
    /// </summary>
    private async Task CheckFirmwareVersionAsync()
    {
        try
        {
            // 固件版本未知或为空时，不检查更新，隐藏提示边框
            if (App.FirmwareApi == null || string.IsNullOrEmpty(_firmwareVersion) || _firmwareVersion == LocalizationService.Instance["DeviceParam.UnknownVersion"] || _firmwareVersion == LocalizationService.Instance["DeviceParam.Unknown"])
            {
                if (NewVersionAvailableBorder != null)
                    NewVersionAvailableBorder.Visibility = Visibility.Collapsed;
                return;
            }

            // 设备未连接时，无法获取 VID/PID，隐藏提示边框
            if (_connectedBaseDevice == null)
            {
                if (NewVersionAvailableBorder != null)
                    NewVersionAvailableBorder.Visibility = Visibility.Collapsed;
                return;
            }

            // 获取当前设备的 VID/PID 用于固件匹配
            var vid = _connectedBaseDevice.Vid;
            var pid = _connectedBaseDevice.Pid;

            // 从 API 获取所有可用固件版本列表
            var firmwareList = await App.FirmwareApi.GetFirmwareVersionsAsync();

            // 根据 VID/PID 匹配当前设备对应的最新固件
            var matched = App.FirmwareApi.FindFirmwareForDevice(firmwareList, vid, pid);

            // 使用版本比较工具判断 API 上的版本是否比设备当前版本更新
            if (matched != null && Services.Usb.FirmwareUpdateService.IsNewerVersion(_firmwareVersion, matched.Version))
            {
                _latestApiFirmwareVersion = matched.Version;
                // 显示"新版本可用"提示边框
                if (NewVersionAvailableBorder != null)
                    NewVersionAvailableBorder.Visibility = Visibility.Visible;
            }
            else
            {
                _latestApiFirmwareVersion = null;
                if (NewVersionAvailableBorder != null)
                    NewVersionAvailableBorder.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BaseControl] 固件版本检查异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 点击"新版本可用"提示，跳转到设置页面的固件更新标签页。
    /// </summary>
    private void NewVersionAvailable_Click(object sender, MouseButtonEventArgs e)
    {
        // 获取主窗口并切换到设置页面
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var vm = mainWindow.DataContext as ViewModels.MainWindowViewModel;
            if (vm != null)
            {
                // 在导航列表中查找设置项
                var settingsItem = vm.NavigationItems.FirstOrDefault(n => n.Name == "Settings");
                if (settingsItem != null)
                {
                    vm.SelectedNavigationItem = settingsItem;
                    // 延迟到界面加载完成后再切换固件更新标签页，确保 SettingsView 已初始化
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
                    {
                        var settingsView = vm.CurrentView as SettingsUserControl;
                        settingsView?.SwitchToFirmwareUpdateTab();
                    });
                }
            }
        }
        e.Handled = true;
    }

    // ═══════════════════════════════════════════
    // USB 设备热插拔事件处理
    // ═══════════════════════════════════════════

    /// <summary>
    /// 订阅 USB 串口设备的连接与断开事件。
    /// 当用户在应用运行时插入或拔出设备时，UI 能够实时响应，刷新设备信息和预设状态。
    /// </summary>
    private void SubscribeUsbSerialEvents()
    {
        if (App.UsbManager == null) return;
        App.UsbManager.DeviceConnected += OnUsbDeviceConnected;
        App.UsbManager.DeviceDisconnected += OnUsbDeviceDisconnected;
    }

    /// <summary>
    /// USB 设备连接事件处理。
    /// 仅处理基座类型且处于正常模式的设备，忽略更新模式设备（由 MainWindow 统一处理）。
    /// </summary>
    private async void OnUsbDeviceConnected(UsbDeviceInfo device)
    {
        // 检查是否为基座设备
        var descriptor = DeviceRegistry.FindByVidPid(device.Vid, device.Pid);
        if (descriptor == null || descriptor.DeviceType != DeviceType.Base)
            return;
        // 更新模式由 MainWindow 统一处理，参数页面忽略
        if (descriptor.IsUpdateMode(device.Vid, device.Pid))
            return;

        Debug.WriteLine($"[BaseControl] 基座串口设备已连接: {device.DeviceKey}");
        // 在 UI 线程刷新设备信息
        await Application.Current.Dispatcher.InvokeAsync(async () => await RefreshDeviceInfoAsync());
    }

    /// <summary>
    /// USB 设备断开事件处理。
    /// 仅当断开的设备是当前连接的基座时，才重置界面状态。
    /// </summary>
    private void OnUsbDeviceDisconnected(UsbDeviceInfo device)
    {
        // 无已连接设备时无需处理
        if (_connectedBaseDevice == null) return;

        // 仅处理基座类型设备的断开事件
        var descriptor = DeviceRegistry.FindByVidPid(device.Vid, device.Pid);
        if (descriptor == null || descriptor.DeviceType != DeviceType.Base)
            return;

        Debug.WriteLine($"[BaseControl] 基座串口设备已断开: {device.DeviceKey}");
        // 在 UI 线程更新断开状态
        Application.Current.Dispatcher.Invoke(() =>
        {
            SetDisconnected();
            UpdateConnectionStatusDisplay();
            UpdatePresetDisplay();
            if (NewVersionAvailableBorder != null)
                NewVersionAvailableBorder.Visibility = Visibility.Collapsed;
        });
    }

    // ═══════════════════════════════════════════
    // 预设保存、放弃与弹窗
    // ═══════════════════════════════════════════

    /// <summary>
    /// 放弃当前所有未保存的参数修改，恢复到已应用预设的原始状态。
    /// </summary>
    public void DiscardChanges()
    {
        if (!_isPresetModified) return;

        _isPresetModified = false;
        UpdatePresetDisplay();
    }

    /// <summary>
    /// 弹出未保存确认弹窗。
    /// 如果存在未保存的修改，显示对话框让用户选择保存、不保存或取消。
    /// 如果是个人预设，额外提供"保存"按钮。
    /// </summary>
    /// <param name="onSaved">用户选择保存或不保存后的回调（操作完成）</param>
    /// <param name="onCancelled">用户选择取消后的回调（操作取消）</param>
    public void ShowUnsavedDialog(Action? onSaved, Action? onCancelled = null)
    {
        // 无修改时直接执行回调，不弹窗
        if (!_isPresetModified)
        {
            onSaved?.Invoke();
            return;
        }

        // 获取 MainWindow 实例以访问全局弹窗
        var mainWindow = Window.GetWindow(this) as HITAPEX.MainWindow
                          ?? Application.Current.MainWindow as HITAPEX.MainWindow;
        if (mainWindow == null)
        {
            // 无法获取主窗口时，放弃修改并直接回调
            _isPresetModified = false;
            onSaved?.Invoke();
            return;
        }

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = LocalizationService.Instance["Dialog.UnsavedTitle"];
        dialog.ClearButtons();

        // 设置弹窗内容文本
        dialog.DialogContent = new TextBlock
        {
            Text = LocalizationService.Instance["Dialog.UnsavedMessage"],
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 个人预设额外提供"保存"按钮
        if (_isAppliedPresetPersonal)
        {
            dialog.AddButton(LocalizationService.Instance["Common.Save"], (_, _) =>
            {
                dialog.Hide();
                SaveCurrentPreset();
                onSaved?.Invoke();
            });
        }

        // "不保存"按钮：放弃修改并继续
        dialog.AddButton(LocalizationService.Instance["Dialog.DontSave"], (_, _) =>
        {
            dialog.Hide();
            DiscardChanges();
            onSaved?.Invoke();
        });

        // "取消"按钮：保持当前修改状态，不执行操作
        dialog.AddButton(LocalizationService.Instance["Common.Cancel"], (_, _) =>
        {
            dialog.Hide();
            onCancelled?.Invoke();
        });

        dialog.Show();
    }

    /// <summary>
    /// 保存当前预设参数到本地存储。
    /// </summary>
    /// <returns>保存成功返回 true，失败返回 false</returns>
    private bool SaveCurrentPreset()
    {
        try
        {
            // TODO: 实现基座预设保存逻辑（需要 BasePresetSnapshot 模型）
            _isPresetModified = false;
            UpdatePresetDisplay();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BaseControl] 保存预设失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 任意参数修改后的统一入口。
    /// 将预设标记为已修改状态，并刷新预设名称和图标显示。
    /// </summary>
    private void OnParameterModified()
    {
        // 控件未加载时无需响应参数变化
        if (!IsLoaded) return;
        _isPresetModified = true;
        UpdatePresetDisplay();
    }

    /// <summary>
    /// 更新预设名称、已更改提示标记、撤回按钮和保存按钮的状态。
    /// 同时根据当前预设类型（设备内置/个人/官方）切换对应的预设类型图标。
    /// </summary>
    private void UpdatePresetDisplay()
    {
        // 判断当前是否为设备内置预设（设备已连接且预设为默认）
        var isDeviceConnected = _connectedBaseDevice != null;
        var isOnboard = _currentPresetName == LocalizationService.Instance["DeviceParam.Default"] && isDeviceConnected;

        // 更新预设名称文本：
        // 1. 设备内置且有设备端预设名 → "设备预设名_Onboard"
        // 2. 设备内置无设备端预设名 → "Onboard"
        // 3. 本地预设 → 直接显示预设名称
        if (PresetNameText != null)
        {
            if (isOnboard && !string.IsNullOrEmpty(_devicePresetName))
                PresetNameText.Text = $"{_devicePresetName}_{LocalizationService.Instance["DeviceParam.Onboard"]}";
            else if (isOnboard)
                PresetNameText.Text = LocalizationService.Instance["DeviceParam.Onboard"];
            else
                PresetNameText.Text = _currentPresetName;
            // 有未保存修改时缩小名称区域宽度，为"已更改"文字腾出空间
            PresetNameText.MaxWidth = _isPresetModified ? 195 : 270;
        }

        // 控制"已更改"提示文字的显示/隐藏
        if (ModifiedIndicator != null)
            ModifiedIndicator.Visibility = _isPresetModified ? Visibility.Visible : Visibility.Collapsed;

        // 撤回按钮状态控制：已修改时可用（默认颜色 + 手型光标），未修改时半透明禁用
        if (UndoButtonPath != null)
        {
            if (_isPresetModified)
            {
                // 清除之前设置的半透明 Fill，恢复默认样式
                UndoButtonPath.ClearValue(System.Windows.Shapes.Path.FillProperty);
                UndoButtonPath.Cursor = System.Windows.Input.Cursors.Hand;
            }
            else
            {
                UndoButtonPath.Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xEE, 0xEE, 0xEE));
                UndoButtonPath.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        // 保存按钮状态控制：已修改时可用，未修改时半透明禁用
        if (SaveButtonPath != null)
        {
            if (_isPresetModified)
            {
                SaveButtonPath.ClearValue(System.Windows.Shapes.Path.FillProperty);
                SaveButtonPath.Cursor = System.Windows.Input.Cursors.Hand;
            }
            else
            {
                SaveButtonPath.Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xEE, 0xEE, 0xEE));
                SaveButtonPath.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        // 根据预设类型切换图标：
        // - 设备内置（Onboard）→ 显示 Onboard 图标，隐藏官方和个人图标
        // - 个人预设 → 显示个人图标，隐藏官方和 Onboard 图标
        // - 官方预设 → 显示官方图标，隐藏个人和 Onboard 图标
        if (isOnboard && isDeviceConnected)
        {
            if (OfficialPresetIcon != null) OfficialPresetIcon.Visibility = Visibility.Collapsed;
            if (PersonalPresetIcon != null) PersonalPresetIcon.Visibility = Visibility.Collapsed;
            if (OnboardPresetIcon != null) OnboardPresetIcon.Visibility = Visibility.Visible;
        }
        else if (_isAppliedPresetPersonal)
        {
            if (OfficialPresetIcon != null) OfficialPresetIcon.Visibility = Visibility.Collapsed;
            if (PersonalPresetIcon != null) PersonalPresetIcon.Visibility = Visibility.Visible;
            if (OnboardPresetIcon != null) OnboardPresetIcon.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (OfficialPresetIcon != null) OfficialPresetIcon.Visibility = Visibility.Visible;
            if (PersonalPresetIcon != null) PersonalPresetIcon.Visibility = Visibility.Collapsed;
            if (OnboardPresetIcon != null) OnboardPresetIcon.Visibility = Visibility.Collapsed;
        }
    }

    // ═══════════════════════════════════════════
    // 按钮事件处理
    // ═══════════════════════════════════════════

    /// <summary>
    /// 撤回按钮点击：放弃当前所有未保存的修改。
    /// </summary>
    private void UndoButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isPresetModified) return;
        DiscardChanges();
    }

    /// <summary>
    /// 保存按钮点击。
    /// 如果是个人预设，直接覆盖保存；如果是官方预设或默认预设，触发另存为流程。
    /// </summary>
    private void SaveButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isPresetModified) return;

        // 非个人预设且非默认预设 → 另存为新预设
        if (!_isAppliedPresetPersonal && _currentPresetName != LocalizationService.Instance["DeviceParam.Default"])
        {
            SaveAsButton_Click(sender, e);
            return;
        }

        // 个人预设或默认预设 → 直接保存
        SaveCurrentPreset();
    }

    /// <summary>
    /// 另存为按钮点击：将当前参数另存为一个新的预设。
    /// </summary>
    private void SaveAsButton_Click(object sender, MouseButtonEventArgs e)
    {
        // TODO: 实现基座预设另存为（需要 EditPresetPopup 适配基座参数）
        Debug.WriteLine("[BaseControl] 另存为功能待实现");
    }

    /// <summary>
    /// 导出按钮点击：导出当前个人预设。
    /// 仅当已应用的预设是个人预设时才允许导出。
    /// </summary>
    private void ExportButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isAppliedPresetPersonal) return;

        // TODO: 实现基座预设导出（需要 BasePresetSnapshot 模型）
        Debug.WriteLine("[BaseControl] 导出功能待实现");
    }

    /// <summary>
    /// 预设列表按钮点击：打开预设选择弹窗。
    /// 用户可以从弹窗中选择官方或个人的基座预设进行应用。
    /// </summary>
    private void PresetListButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var popup = mainWindow.ShowPresetListPopup(Models.Usb.DeviceType.Base);
            // 先取消之前的订阅，再重新订阅，避免重复触发
            popup.PresetApplied -= OnPresetApplied;
            popup.PresetApplied += OnPresetApplied;
        }
    }

    /// <summary>
    /// 预设应用事件处理：当用户在弹窗中选择并应用一个预设后，
    /// 将预设名称和类型记录到当前状态，并刷新界面显示。
    /// </summary>
    private void OnPresetApplied(object? sender, PresetItem preset)
    {
        // TODO: 应用基座预设参数（需要 BasePresetSnapshot 模型）
        _currentPresetName = preset.Name;
        _isAppliedPresetPersonal = preset.IsPersonal;
        _isPresetModified = false;
        UpdatePresetDisplay();
        Debug.WriteLine($"[BaseControl] 预设已应用: {preset.Name}");
    }

    /// <summary>
    /// 操作按钮区域尺寸变化时动态重新绘制按钮背景形状，
    /// 确保圆角矩形路径与按钮容器宽度保持一致。
    /// </summary>
    private void ActionButton_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid) return;
        var w = grid.ActualWidth;
        if (w <= 0) return;
        // 查找 Canvas 中的 Path 元素并动态更新其几何路径
        if (grid.Children.OfType<Canvas>().FirstOrDefault()?.Children.OfType<Path>().FirstOrDefault() is { } path)
        {
            path.Width = w;
            // 动态生成圆角矩形路径：左上圆角 5px，右上圆角 5px，底部直角
            path.Data = Geometry.Parse($"M{w},5 H11 L5,11 V42 H5.32 H{w - 6} L{w},36 V5 Z");
        }
    }

    // ═══════════════════════════════════════════
    // 语言切换与界面自适应
    // ═══════════════════════════════════════════

    /// <summary>
    /// 构建设备显示名称，格式为"设备类型 型号"。
    /// 设备类型名称通过语言服务获取，支持中英文切换时自动刷新。
    /// </summary>
    private string BuildDeviceDisplayName()
    {
        _deviceTypeName = LocalizationService.Instance["Status.DeviceTypeBase"];
        return string.IsNullOrEmpty(_deviceModel) ? _deviceTypeName : $"{_deviceTypeName} {_deviceModel}";
    }

    /// <summary>
    /// 语言切换回调：当本地化服务通知语言变更时，
    /// 重新构建设备显示名称以反映当前语言环境。
    /// </summary>
    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        // PropertyChangedEventArgs 的 PropertyName 为 null 表示所有属性已变更（语言切换）
        if (e.PropertyName == null && DeviceModelName != null)
            DeviceModelName.Text = BuildDeviceDisplayName();
    }
}
