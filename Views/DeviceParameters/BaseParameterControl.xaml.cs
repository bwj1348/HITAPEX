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

public partial class BaseParameterControl : UserControl
{
    // 设备通信状态
    private UsbDeviceInfo? _connectedBaseDevice;
    private string _deviceTypeName = LocalizationService.Instance["Status.DeviceTypeBase"];
    private string _deviceModel = "";
    private string _connectionStatusText = LocalizationService.Instance["DeviceParam.NotConnected"];
    private string _connectionStatusColor = "#C60E0E";
    private string _firmwareVersion = LocalizationService.Instance["DeviceParam.UnknownVersion"];
    private string? _latestApiFirmwareVersion;

    // 预设管理
    private bool _isPresetModified;
    private bool _isAppliedPresetPersonal;
    private string _currentPresetName = LocalizationService.Instance["DeviceParam.Default"];
    private string _devicePresetName = string.Empty;
    private bool _isInitialized;

    public bool HasUnsavedChanges => _isPresetModified;

    public BaseParameterControl()
    {
        InitializeComponent();
        Loaded += BaseParameterControl_Loaded;
    }

    private async void BaseParameterControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            SubscribeUsbSerialEvents();
            LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
        }

        await RefreshDeviceInfoAsync();
        UpdatePresetDisplay();
    }

    public async Task RefreshDeviceInfoAsync()
    {
        try
        {
            var connectedDevices = App.UsbManager?.ConnectedDevices
                ?? System.Collections.ObjectModel.ReadOnlyCollection<UsbDeviceInfo>.Empty;

            _connectedBaseDevice = connectedDevices.FirstOrDefault(d =>
            {
                var descriptor = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                return descriptor != null && descriptor.DeviceType == DeviceType.Base
                       && descriptor.IsNormalMode(d.Vid, d.Pid);
            });

            if (_connectedBaseDevice != null)
            {
                var descriptor = DeviceRegistry.FindByVidPid(_connectedBaseDevice.Vid, _connectedBaseDevice.Pid);
                _deviceModel = descriptor?.ModelName ?? "";
                _connectionStatusText = LocalizationService.Instance["DeviceParam.ConnectedDirect"];
                _connectionStatusColor = "#179548";

                if (App.ProtocolService != null && App.FirmwareUpdater != null)
                {
                    var deviceInfo = await App.FirmwareUpdater.GetDeviceInfoAsync(
                        _connectedBaseDevice, DeviceType.Base);
                    _firmwareVersion = deviceInfo?.VersionString ?? LocalizationService.Instance["DeviceParam.Unknown"];
                }
            }
            else
            {
                SetDisconnected();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BaseControl] 刷新设备信息异常: {ex.Message}");
            SetDisconnected();
        }

        UpdateConnectionStatusDisplay();
        // 固件版本检查改为 fire-and-forget：API 服务器不可达时会阻塞 15s+，
        // 不应延迟后续 USB 参数获取命令
        _ = CheckFirmwareVersionAsync();

        // 获取设备预设名称
        await FetchPresetNameAsync();

        // 尝试将设备预设匹配到本地预设
        TryMatchLocalPreset();
    }

    /// <summary>
    /// 对比设备上报的预设名称与本地预设，若匹配则视为本地预设。
    /// TODO: 添加参数等价比较（需要 BasePresetSnapshot 模型）
    /// </summary>
    private void TryMatchLocalPreset()
    {
        if (string.IsNullOrEmpty(_devicePresetName) || App.PresetService == null)
            return;

        try
        {
            var officialPresets = App.PresetService.LoadOfficialPresets(DeviceType.Base);
            var personalPresets = App.PresetService.LoadPersonalPresets(DeviceType.Base);

            // 先查个人预设，再查官方预设
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
                _currentPresetName = matched.Name;
                _isAppliedPresetPersonal = isPersonal;
                _devicePresetName = string.Empty;
                Debug.WriteLine($"[BaseControl] 设备预设匹配到本地{(isPersonal ? "个人" : "官方")}预设: {matched.Name}");
                UpdatePresetDisplay();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BaseControl] 匹配本地预设异常: {ex.Message}");
        }
    }

    /// <summary>从设备获取预设名称</summary>
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
                if (_currentPresetName == LocalizationService.Instance["DeviceParam.Default"] && !_isPresetModified)
                    UpdatePresetDisplay();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BaseControl] 获取预设名称异常: {ex.Message}");
        }
    }

    /// <summary>下发预设名称到设备</summary>
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

    private void SetDisconnected()
    {
        _connectedBaseDevice = null;
        _deviceModel = "";
        _connectionStatusText = LocalizationService.Instance["DeviceParam.NotConnected"];
        _connectionStatusColor = "#C60E0E";
        _firmwareVersion = LocalizationService.Instance["DeviceParam.UnknownVersion"];

        // 重置预设状态
        _currentPresetName = LocalizationService.Instance["DeviceParam.Default"];
        _devicePresetName = string.Empty;
        _isPresetModified = false;
        _isAppliedPresetPersonal = false;
    }

    private void UpdateConnectionStatusDisplay()
    {
        if (DeviceModelName != null)
            DeviceModelName.Text = BuildDeviceDisplayName();

        if (ConnectionStatusText != null)
            ConnectionStatusText.Text = _connectionStatusText;

        if (FirmwareVersionText != null)
            FirmwareVersionText.Text = _firmwareVersion;

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

    private async Task CheckFirmwareVersionAsync()
    {
        try
        {
            if (App.FirmwareApi == null || string.IsNullOrEmpty(_firmwareVersion) || _firmwareVersion == LocalizationService.Instance["DeviceParam.UnknownVersion"] || _firmwareVersion == LocalizationService.Instance["DeviceParam.Unknown"])
            {
                if (NewVersionAvailableBorder != null)
                    NewVersionAvailableBorder.Visibility = Visibility.Collapsed;
                return;
            }

            // 确定用于 API 匹配的 VID/PID
            if (_connectedBaseDevice == null)
            {
                if (NewVersionAvailableBorder != null)
                    NewVersionAvailableBorder.Visibility = Visibility.Collapsed;
                return;
            }

            var vid = _connectedBaseDevice.Vid;
            var pid = _connectedBaseDevice.Pid;

            var firmwareList = await App.FirmwareApi.GetFirmwareVersionsAsync();
            var matched = App.FirmwareApi.FindFirmwareForDevice(firmwareList, vid, pid);

            if (matched != null && Services.Usb.FirmwareUpdateService.IsNewerVersion(_firmwareVersion, matched.Version))
            {
                _latestApiFirmwareVersion = matched.Version;
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

    private void NewVersionAvailable_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var vm = mainWindow.DataContext as ViewModels.MainWindowViewModel;
            if (vm != null)
            {
                var settingsItem = vm.NavigationItems.FirstOrDefault(n => n.Name == "Settings");
                if (settingsItem != null)
                {
                    vm.SelectedNavigationItem = settingsItem;
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

    /// <summary>订阅 USB 串口设备连接/断开事件，设备随时插拔时 UI 实时响应</summary>
    private void SubscribeUsbSerialEvents()
    {
        if (App.UsbManager == null) return;
        App.UsbManager.DeviceConnected += OnUsbDeviceConnected;
        App.UsbManager.DeviceDisconnected += OnUsbDeviceDisconnected;
    }

    private async void OnUsbDeviceConnected(UsbDeviceInfo device)
    {
        var descriptor = DeviceRegistry.FindByVidPid(device.Vid, device.Pid);
        if (descriptor == null || descriptor.DeviceType != DeviceType.Base)
            return;
        // 更新模式由 MainWindow 统一处理，参数页面忽略
        if (descriptor.IsUpdateMode(device.Vid, device.Pid))
            return;

        Debug.WriteLine($"[BaseControl] 基座串口设备已连接: {device.DeviceKey}");
        await Application.Current.Dispatcher.InvokeAsync(async () => await RefreshDeviceInfoAsync());
    }

    private void OnUsbDeviceDisconnected(UsbDeviceInfo device)
    {
        if (_connectedBaseDevice == null) return;

        var descriptor = DeviceRegistry.FindByVidPid(device.Vid, device.Pid);
        if (descriptor == null || descriptor.DeviceType != DeviceType.Base)
            return;

        Debug.WriteLine($"[BaseControl] 基座串口设备已断开: {device.DeviceKey}");
        Application.Current.Dispatcher.Invoke(() =>
        {
            SetDisconnected();
            UpdateConnectionStatusDisplay();
            UpdatePresetDisplay();
            if (NewVersionAvailableBorder != null)
                NewVersionAvailableBorder.Visibility = Visibility.Collapsed;
        });
    }

    /// <summary>放弃当前修改，恢复到已应用预设的状态</summary>
    public void DiscardChanges()
    {
        if (!_isPresetModified) return;

        _isPresetModified = false;
        UpdatePresetDisplay();
    }

    /// <summary>弹出未保存确认弹窗</summary>
    public void ShowUnsavedDialog(Action? onSaved, Action? onCancelled = null)
    {
        if (!_isPresetModified)
        {
            onSaved?.Invoke();
            return;
        }

        var mainWindow = Window.GetWindow(this) as HITAPEX.MainWindow
                          ?? Application.Current.MainWindow as HITAPEX.MainWindow;
        if (mainWindow == null)
        {
            _isPresetModified = false;
            onSaved?.Invoke();
            return;
        }

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = LocalizationService.Instance["Dialog.UnsavedTitle"];
        dialog.ClearButtons();

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

        if (_isAppliedPresetPersonal)
        {
            dialog.AddButton(LocalizationService.Instance["Common.Save"], (_, _) =>
            {
                dialog.Hide();
                SaveCurrentPreset();
                onSaved?.Invoke();
            });
        }

        dialog.AddButton(LocalizationService.Instance["Dialog.DontSave"], (_, _) =>
        {
            dialog.Hide();
            DiscardChanges();
            onSaved?.Invoke();
        });

        dialog.AddButton(LocalizationService.Instance["Common.Cancel"], (_, _) =>
        {
            dialog.Hide();
            onCancelled?.Invoke();
        });

        dialog.Show();
    }

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

    /// <summary>任意参数修改后的统一入口</summary>
    private void OnParameterModified()
    {
        if (!IsLoaded) return;
        _isPresetModified = true;
        UpdatePresetDisplay();
    }

    /// <summary>更新预设名称、已更改提示、撤回按钮状态</summary>
    private void UpdatePresetDisplay()
    {
        var isDeviceConnected = _connectedBaseDevice != null;
        var isOnboard = _currentPresetName == LocalizationService.Instance["DeviceParam.Default"] && isDeviceConnected;

        if (PresetNameText != null)
        {
            if (isOnboard && !string.IsNullOrEmpty(_devicePresetName))
                PresetNameText.Text = $"{_devicePresetName}_{LocalizationService.Instance["DeviceParam.Onboard"]}";
            else if (isOnboard)
                PresetNameText.Text = LocalizationService.Instance["DeviceParam.Onboard"];
            else
                PresetNameText.Text = _currentPresetName;
            PresetNameText.MaxWidth = _isPresetModified ? 195 : 270;
        }

        if (ModifiedIndicator != null)
            ModifiedIndicator.Visibility = _isPresetModified ? Visibility.Visible : Visibility.Collapsed;

        if (UndoButtonPath != null)
        {
            if (_isPresetModified)
            {
                UndoButtonPath.ClearValue(System.Windows.Shapes.Path.FillProperty);
                UndoButtonPath.Cursor = System.Windows.Input.Cursors.Hand;
            }
            else
            {
                UndoButtonPath.Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xEE, 0xEE, 0xEE));
                UndoButtonPath.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }

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

        // 更新预设类型图标
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

    // ────────── 按钮事件处理 ──────────

    private void UndoButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isPresetModified) return;
        DiscardChanges();
    }

    private void SaveButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isPresetModified) return;

        if (!_isAppliedPresetPersonal && _currentPresetName != LocalizationService.Instance["DeviceParam.Default"])
        {
            SaveAsButton_Click(sender, e);
            return;
        }

        SaveCurrentPreset();
    }

    private void SaveAsButton_Click(object sender, MouseButtonEventArgs e)
    {
        // TODO: 实现基座预设另存为（需要 EditPresetPopup 适配基座参数）
        Debug.WriteLine("[BaseControl] 另存为功能待实现");
    }

    private void ExportButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isAppliedPresetPersonal) return;

        // TODO: 实现基座预设导出（需要 BasePresetSnapshot 模型）
        Debug.WriteLine("[BaseControl] 导出功能待实现");
    }

    private void PresetListButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var popup = mainWindow.ShowPresetListPopup(Models.Usb.DeviceType.Base);
            popup.PresetApplied -= OnPresetApplied;
            popup.PresetApplied += OnPresetApplied;
        }
    }

    private void OnPresetApplied(object? sender, PresetItem preset)
    {
        // TODO: 应用基座预设参数（需要 BasePresetSnapshot 模型）
        _currentPresetName = preset.Name;
        _isAppliedPresetPersonal = preset.IsPersonal;
        _isPresetModified = false;
        UpdatePresetDisplay();
        Debug.WriteLine($"[BaseControl] 预设已应用: {preset.Name}");
    }

    private void ActionButton_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid) return;
        var w = grid.ActualWidth;
        if (w <= 0) return;
        if (grid.Children.OfType<Canvas>().FirstOrDefault()?.Children.OfType<Path>().FirstOrDefault() is { } path)
        {
            path.Width = w;
            path.Data = Geometry.Parse($"M{w},5 H11 L5,11 V42 H5.32 H{w - 6} L{w},36 V5 Z");
        }
    }

    /// <summary>"设备类型 型号" 仅设备类型在语言切换时可刷新</summary>
    private string BuildDeviceDisplayName()
    {
        _deviceTypeName = LocalizationService.Instance["Status.DeviceTypeBase"];
        return string.IsNullOrEmpty(_deviceModel) ? _deviceTypeName : $"{_deviceTypeName} {_deviceModel}";
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null && DeviceModelName != null)
            DeviceModelName.Text = BuildDeviceDisplayName();
    }
}
