using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HITAPEX.Models.Usb;

namespace HITAPEX.Views.DeviceParameters;

public partial class SteeringWheelParameterControl : UserControl
{
    // 设备通信状态
    private UsbDeviceInfo? _connectedWheelDevice;
    private UsbDeviceInfo? _baseDevice;
    private bool _isWheelViaBase;
    private string _deviceModelName = "面盘";
    private string _connectionStatusText = "未连接";
    private string _connectionStatusColor = "#C60E0E";
    private string _firmwareVersion = "---";
    private string? _latestApiFirmwareVersion;

    // 预设管理
    private bool _isPresetModified;
    private bool _isAppliedPresetPersonal;
    private string _currentPresetName = "Default";
    private string _devicePresetName = string.Empty;

    public bool HasUnsavedChanges => _isPresetModified;

    public SteeringWheelParameterControl()
    {
        InitializeComponent();
        Loaded += SteeringWheelParameterControl_Loaded;
        SpeedSlider.Loaded += (_, _) => UpdateSpeedSliderFill(SpeedSlider);
    }

    private async void SteeringWheelParameterControl_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshDeviceInfoAsync();
        UpdatePresetDisplay();
    }

    public async Task RefreshDeviceInfoAsync()
    {
        try
        {
            var connectedDevices = App.UsbManager?.ConnectedDevices
                ?? System.Collections.ObjectModel.ReadOnlyCollection<UsbDeviceInfo>.Empty;

            _connectedWheelDevice = null;
            _isWheelViaBase = false;

            // 1. 查找直连的面盘 USB 设备
            _connectedWheelDevice = connectedDevices.FirstOrDefault(d =>
            {
                var descriptor = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                return descriptor != null && descriptor.DeviceType == DeviceType.Wheel
                       && descriptor.IsNormalMode(d.Vid, d.Pid);
            });

            if (_connectedWheelDevice != null)
            {
                var descriptor = DeviceRegistry.FindByVidPid(_connectedWheelDevice.Vid, _connectedWheelDevice.Pid);
                _deviceModelName = descriptor?.ModelName ?? "面盘";
                _connectionStatusText = "已连接(直连)";
                _connectionStatusColor = "#179548";

                if (App.ProtocolService != null && App.FirmwareUpdater != null)
                {
                    var deviceInfo = await App.FirmwareUpdater.GetDeviceInfoAsync(
                        _connectedWheelDevice, DeviceType.Wheel);
                    _firmwareVersion = deviceInfo?.VersionString ?? "未知";
                }
            }
            else
            {
                // 2. 检查是否通过基座连接
                var baseDevice = connectedDevices.FirstOrDefault(d =>
                {
                    var descriptor = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                    return descriptor != null && descriptor.DeviceType == DeviceType.Base
                           && descriptor.IsNormalMode(d.Vid, d.Pid);
                });

                if (baseDevice != null && App.ProtocolService != null && App.FirmwareUpdater != null)
                {
                    var baseInfo = await App.FirmwareUpdater.GetDeviceInfoAsync(baseDevice, DeviceType.Base);
                    if (baseInfo != null && baseInfo.WheelConnectionStatus != 0x00)
                    {
                        _isWheelViaBase = true;
                        _baseDevice = baseDevice;
                        _deviceModelName = GetWheelModelFromConnectionStatus(baseInfo.WheelConnectionStatus);
                        _connectionStatusText = "已连接(基座)";
                        _connectionStatusColor = "#179548";
                        _firmwareVersion = $"v{baseInfo.WheelNormalFwVersion >> 8}.{baseInfo.WheelNormalFwVersion & 0xFF}";
                    }
                    else
                    {
                        SetDisconnected();
                    }
                }
                else
                {
                    SetDisconnected();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 刷新设备信息异常: {ex.Message}");
            SetDisconnected();
        }

        UpdateConnectionStatusDisplay();
        await CheckFirmwareVersionAsync();

        // 获取设备预设名称
        await FetchPresetNameAsync();
    }

    /// <summary>从设备获取预设名称</summary>
    private async Task FetchPresetNameAsync()
    {
        UsbDeviceInfo? targetDevice = null;
        if (_connectedWheelDevice != null)
            targetDevice = _connectedWheelDevice;
        else if (_isWheelViaBase && _baseDevice != null)
            targetDevice = _baseDevice;

        if (targetDevice == null || App.ProtocolService == null)
            return;

        try
        {
            var name = await App.ProtocolService.GetPresetNameAsync(targetDevice.DeviceKey, DeviceType.Wheel);
            if (name != null)
            {
                _devicePresetName = name;
                if (_currentPresetName == "Default" && !_isPresetModified)
                    UpdatePresetDisplay();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 获取预设名称异常: {ex.Message}");
        }
    }

    /// <summary>下发预设名称到设备</summary>
    private void SendPresetName(string name)
    {
        UsbDeviceInfo? targetDevice = null;
        if (_connectedWheelDevice != null)
            targetDevice = _connectedWheelDevice;
        else if (_isWheelViaBase && _baseDevice != null)
            targetDevice = _baseDevice;

        if (targetDevice == null || App.ProtocolService == null)
            return;

        try
        {
            App.ProtocolService.SetPresetName(targetDevice.DeviceKey, DeviceType.Wheel, name);
            Debug.WriteLine($"[SteeringWheelControl] 预设名称已下发: {name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 下发预设名称异常: {ex.Message}");
        }
    }

    private void SetDisconnected()
    {
        _baseDevice = null;
        _deviceModelName = "面盘";
        _connectionStatusText = "未连接";
        _connectionStatusColor = "#C60E0E";
        _firmwareVersion = "---";
    }

    /// <summary>
    /// 根据基座上报的面盘连接状态字节，返回面盘型号名称。
    /// 0x01 = S1, 0x02 = S2, ...
    /// </summary>
    private static string GetWheelModelFromConnectionStatus(int status)
    {
        return status switch
        {
            0x01 => "S1面盘",
            0x02 => "S2面盘",
            0x03 => "S3面盘",
            0x04 => "S4面盘",
            _ => "面盘"
        };
    }

    private void UpdateConnectionStatusDisplay()
    {
        if (DeviceModelName != null)
            DeviceModelName.Text = _deviceModelName;

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
            if (App.FirmwareApi == null || string.IsNullOrEmpty(_firmwareVersion) || _firmwareVersion == "---" || _firmwareVersion == "未知")
            {
                if (NewVersionAvailableBorder != null)
                    NewVersionAvailableBorder.Visibility = Visibility.Collapsed;
                return;
            }

            // 确定用于 API 匹配的 VID/PID
            int vid, pid;
            if (!_isWheelViaBase && _connectedWheelDevice != null)
            {
                vid = _connectedWheelDevice.Vid;
                pid = _connectedWheelDevice.Pid;
            }
            else
            {
                // 面盘通过基座连接时，使用面盘设备的默认 VID/PID 查询 API
                var descriptor = DeviceRegistry.Devices.FirstOrDefault(d => d.DeviceType == DeviceType.Wheel);
                if (descriptor == null) return;
                vid = descriptor.NormalMode.Vid;
                pid = descriptor.NormalMode.Pid;
            }

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
            Debug.WriteLine($"[SteeringWheelControl] 固件版本检查异常: {ex.Message}");
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

        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = "未 保 存";
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = "当前预设已更改，是否保存？",
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (_isAppliedPresetPersonal)
        {
            dialog.AddButton("保 存", (_, _) =>
            {
                dialog.Hide();
                SaveCurrentPreset();
                onSaved?.Invoke();
            });
        }

        dialog.AddButton("不保存", (_, _) =>
        {
            dialog.Hide();
            DiscardChanges();
            onSaved?.Invoke();
        });

        dialog.AddButton("取 消", (_, _) =>
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
            // TODO: 实现面盘预设保存逻辑（需要 WheelPresetSnapshot 模型）
            _isPresetModified = false;
            UpdatePresetDisplay();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 保存预设失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>任意参数修改后的统一入口</summary>
    private void OnParameterModified()
    {
        _isPresetModified = true;
        UpdatePresetDisplay();
    }

    /// <summary>更新预设名称、已更改提示、撤回按钮状态</summary>
    private void UpdatePresetDisplay()
    {
        var isDeviceConnected = _connectedWheelDevice != null || _isWheelViaBase;
        var isOnboard = _currentPresetName == "Default" && isDeviceConnected;

        if (PresetNameText != null)
        {
            if (isOnboard && !string.IsNullOrEmpty(_devicePresetName))
                PresetNameText.Text = $"{_devicePresetName}_板载";
            else if (isOnboard)
                PresetNameText.Text = "板载";
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

        if (!_isAppliedPresetPersonal && _currentPresetName != "Default")
        {
            SaveAsButton_Click(sender, e);
            return;
        }

        SaveCurrentPreset();
    }

    private void SaveAsButton_Click(object sender, MouseButtonEventArgs e)
    {
        // TODO: 实现面盘预设另存为（需要 EditPresetPopup 适配面盘参数）
        Debug.WriteLine("[SteeringWheelControl] 另存为功能待实现");
    }

    private void ExportButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isAppliedPresetPersonal) return;

        // TODO: 实现面盘预设导出（需要 WheelPresetSnapshot 模型）
        Debug.WriteLine("[SteeringWheelControl] 导出功能待实现");
    }

    private void PresetListButton_Click(object sender, MouseButtonEventArgs e)
    {
        // TODO: 实现面盘预设列表弹窗
        Debug.WriteLine("[SteeringWheelControl] 预设列表功能待实现");
    }

    // 选项卡切换
    private void SectionTab_Checked(object sender, RoutedEventArgs e)
    {
        if (ButtonContentPanel == null || PaddleContentPanel == null) return;

        if (ButtonTab?.IsChecked == true)
        {
            ButtonContentPanel.Visibility = Visibility.Visible;
            PaddleContentPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            ButtonContentPanel.Visibility = Visibility.Collapsed;
            PaddleContentPanel.Visibility = Visibility.Visible;
        }
    }

    private ButtonSettingsPopup? _buttonSettingsPopup;

    // 箭头按键名称，这些按键不显示弹窗
    private static readonly HashSet<string> ArrowButtonNames = ["Btn4", "Btn5", "Btn14", "Btn15"];

    // 按键选中
    private void KeyButton_Checked(object sender, RoutedEventArgs e)
    {
        if (KeyResponseName == null || sender is not RadioButton radioButton) return;

        var name = radioButton.Content?.ToString();
        if (string.IsNullOrEmpty(name))
            name = radioButton.Tag?.ToString();
        KeyResponseName.Text = string.IsNullOrEmpty(name) ? "---" : name;

        // 箭头按键不显示弹窗
        if (ArrowButtonNames.Contains(radioButton.Name)) return;

        // 在 MainWindow 层级显示按键设置弹窗
        if (Window.GetWindow(this) is not MainWindow mainWindow) return;
        if (mainWindow.Content is not Panel rootPanel) return;

        if (_buttonSettingsPopup == null)
        {
            _buttonSettingsPopup = new ButtonSettingsPopup();
            _buttonSettingsPopup.Confirmed += (_, _) =>
            {
                // TODO: 应用按键设置
                RemoveButtonSettingsPopup(rootPanel);
            };
            _buttonSettingsPopup.Cancelled += (_, _) =>
            {
                RemoveButtonSettingsPopup(rootPanel);
            };
        }

        if (_buttonSettingsPopup.Parent == null)
        {
            _buttonSettingsPopup.SetKeyName(KeyResponseName.Text);
            rootPanel.Children.Add(_buttonSettingsPopup);
            _buttonSettingsPopup.Show();
        }
        else
        {
            _buttonSettingsPopup.SetKeyName(KeyResponseName.Text);
        }
    }

    private void RemoveButtonSettingsPopup(Panel rootPanel)
    {
        if (_buttonSettingsPopup != null && rootPanel.Children.Contains(_buttonSettingsPopup))
            rootPanel.Children.Remove(_buttonSettingsPopup);
    }

    // ══════════════════════════════════════════
    //  转速灯设置弹窗
    // ══════════════════════════════════════════

    private RpmSettingsPopup? _rpmSettingsPopup;

    private void RpmSettingsTrigger_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow mainWindow) return;
        if (mainWindow.Content is not Panel rootPanel) return;

        if (_rpmSettingsPopup == null)
        {
            _rpmSettingsPopup = new RpmSettingsPopup();
            _rpmSettingsPopup.Confirmed += (_, _) =>
            {
                // TODO: 应用转速灯设置
                RemoveRpmSettingsPopup(rootPanel);
            };
            _rpmSettingsPopup.Cancelled += (_, _) =>
            {
                RemoveRpmSettingsPopup(rootPanel);
            };
        }

        if (_rpmSettingsPopup.Parent == null)
        {
            rootPanel.Children.Add(_rpmSettingsPopup);
            _rpmSettingsPopup.Show();
        }
    }

    private void RemoveRpmSettingsPopup(Panel rootPanel)
    {
        if (_rpmSettingsPopup != null && rootPanel.Children.Contains(_rpmSettingsPopup))
            rootPanel.Children.Remove(_rpmSettingsPopup);
    }

    // 色块选中
    private void KeyColor_Checked(object sender, RoutedEventArgs e)
    {
        // 选中色块后更新按键颜色（TODO: 发送到设备）
        if (sender is RadioButton rb)
            Debug.WriteLine($"[SteeringWheelControl] 按键颜色切换: {rb.Name}");
    }

    // 按键颜色开关
    private void KeyColorToggle_Checked(object sender, RoutedEventArgs e)
    {
        SetKeyColorBlocksEnabled(true);
        if (ColorRed != null)
            ColorRed.IsChecked = true;
    }

    private void KeyColorToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        SetKeyColorBlocksEnabled(false);
        // 取消所有色块的选中状态
        if (ColorRed != null) ColorRed.IsChecked = false;
        if (ColorOrange != null) ColorOrange.IsChecked = false;
        if (ColorYellow != null) ColorYellow.IsChecked = false;
        if (ColorGreen != null) ColorGreen.IsChecked = false;
        if (ColorCyan != null) ColorCyan.IsChecked = false;
        if (ColorBlue != null) ColorBlue.IsChecked = false;
        if (ColorPurple != null) ColorPurple.IsChecked = false;
        if (ColorWhite != null) ColorWhite.IsChecked = false;
    }

    private void SetKeyColorBlocksEnabled(bool enabled)
    {
        if (ColorRed != null) ColorRed.IsEnabled = enabled;
        if (ColorOrange != null) ColorOrange.IsEnabled = enabled;
        if (ColorYellow != null) ColorYellow.IsEnabled = enabled;
        if (ColorGreen != null) ColorGreen.IsEnabled = enabled;
        if (ColorCyan != null) ColorCyan.IsEnabled = enabled;
        if (ColorBlue != null) ColorBlue.IsEnabled = enabled;
        if (ColorPurple != null) ColorPurple.IsEnabled = enabled;
        if (ColorWhite != null) ColorWhite.IsEnabled = enabled;
    }

    // 显示按键编号开关
    private void ShowKeyNumberToggle_Checked(object sender, RoutedEventArgs e)
    {
        RestoreKeyButtonLabels();
    }

    private void ShowKeyNumberToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        HideKeyButtonLabels();
    }

    private void HideKeyButtonLabels()
    {
        ClearButtonContent(Btn1);
        ClearButtonContent(Btn2);
        ClearButtonContent(Btn3);
        ClearButtonContent(Btn4);
        ClearButtonContent(Btn5);
        ClearButtonContent(Btn6);
        ClearButtonContent(Btn7);
        ClearButtonContent(Btn8);
        ClearButtonContent(Btn9);
        ClearButtonContent(Btn10);
        ClearButtonContent(Btn11);
        ClearButtonContent(Btn12);
        ClearButtonContent(Btn13);
        ClearButtonContent(Btn14);
        ClearButtonContent(Btn15);
        ClearButtonContent(Btn16);
        ClearButtonContent(Btn17);
        ClearButtonContent(Btn18);
        ClearButtonContent(Btn19);
    }

    private void RestoreKeyButtonLabels()
    {
        RestoreButtonContent(Btn1, "B1");
        RestoreButtonContent(Btn2, "B2");
        RestoreButtonContent(Btn3, "B3");
        RestoreButtonContent(Btn4, "B4");
        RestoreButtonContent(Btn5, "B5");
        RestoreButtonContent(Btn6, "B6");
        RestoreButtonContent(Btn7, "B7");
        RestoreButtonContent(Btn8, "B8");
        RestoreButtonContent(Btn9, "B9");
        RestoreButtonContent(Btn10, "B10");
        RestoreButtonContent(Btn11, "B11");
        RestoreButtonContent(Btn12, "B12");
        RestoreButtonContent(Btn13, "B13");
        RestoreButtonContent(Btn14, "B14");
        RestoreButtonContent(Btn15, "B15");
        RestoreButtonContent(Btn16, "B16");
        RestoreButtonContent(Btn17, "B17");
        RestoreButtonContent(Btn18, "B18");
        RestoreButtonContent(Btn19, "B19");
    }

    private static void ClearButtonContent(ContentControl button)
    {
        if (button != null && button.Content is string s && !string.IsNullOrEmpty(s))
        {
            button.Tag = s;
            button.Content = "";
        }
    }

    private static void RestoreButtonContent(ContentControl button, string defaultLabel)
    {
        if (button == null) return;
        var saved = button.Tag as string;
        button.Content = !string.IsNullOrEmpty(saved) ? saved : defaultLabel;
        button.Tag = null;
    }

    // 亮度滑块值变化
    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender == KeyBrightnessSlider && KeyBrightnessPercent != null)
            KeyBrightnessPercent.Text = $"{(int)e.NewValue}%";
        else if (sender == RpmBrightnessSlider && RpmBrightnessPercent != null)
            RpmBrightnessPercent.Text = $"{(int)e.NewValue}%";
    }

    private static readonly double[] SpeedStepOffsets = { 0, 0.2063, 0.4091, 0.6084, 0.8112, 1.0 };

    private static void UpdateSpeedSliderFill(Slider slider)
    {
        if (slider.Template == null) return;

        var brush = slider.Template.FindName("TrackFillBrush", slider) as LinearGradientBrush;
        if (brush == null || brush.GradientStops.Count < 4) return;

        var step = (int)Math.Round(slider.Value);
        step = Math.Clamp(step, 0, (int)slider.Maximum);
        var fraction = SpeedStepOffsets[step];

        if (step >= slider.Maximum)
        {
            brush.GradientStops[1].Offset = 1.0;
            brush.GradientStops[2].Offset = 2.0;
            brush.GradientStops[3].Offset = 2.0;
        }
        else
        {
            brush.GradientStops[1].Offset = fraction;
            brush.GradientStops[2].Offset = fraction;
            brush.GradientStops[3].Offset = 1.0;
        }
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider)
            UpdateSpeedSliderFill(slider);
    }

    private bool _isPlaying;

    // 遥测效果 开始/暂停
    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (PlayPauseButton == null) return;

        var playIcon = PlayPauseButton.Template.FindName("PlayIcon", PlayPauseButton) as System.Windows.Shapes.Path;
        var pauseIcon = PlayPauseButton.Template.FindName("PauseIcon", PlayPauseButton) as System.Windows.Shapes.Path;

        _isPlaying = !_isPlaying;
        if (_isPlaying)
        {
            if (playIcon != null) playIcon.Visibility = Visibility.Collapsed;
            if (pauseIcon != null) pauseIcon.Visibility = Visibility.Visible;
            Debug.WriteLine("[SteeringWheelControl] 遥测效果预览 开始");
        }
        else
        {
            if (playIcon != null) playIcon.Visibility = Visibility.Visible;
            if (pauseIcon != null) pauseIcon.Visibility = Visibility.Collapsed;
            Debug.WriteLine("[SteeringWheelControl] 遥测效果预览 暂停");
        }
    }

    // ────────── 离合点滑块拖拽 ──────────

    private bool _isDraggingClutchPoint;

    private void ClutchPointThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas thumb) return;
        thumb.CaptureMouse();
        _isDraggingClutchPoint = true;
        e.Handled = true;
    }

    private void ClutchPointThumb_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingClutchPoint) return;
        if (sender is not Canvas thumb) return;

        var parentCanvas = thumb.Parent as Canvas;
        if (parentCanvas == null || ClutchPointIndicator == null) return;

        var pos = e.GetPosition(parentCanvas);
        var maxX = parentCanvas.ActualWidth - 4;
        var x = Math.Max(0, Math.Min(pos.X, maxX));

        Canvas.SetLeft(ClutchPointIndicator, x);
        Canvas.SetLeft(thumb, x - 8);

        if (ClutchPointPercent != null && maxX > 0)
        {
            var percent = Math.Round(x / maxX * 100);
            ClutchPointPercent.Text = $"{percent}%";
        }
    }

    private void ClutchPointThumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas thumb) return;
        thumb.ReleaseMouseCapture();
        _isDraggingClutchPoint = false;
        e.Handled = true;
    }

    // ────────── 拨片选项卡事件处理 ──────────

    /// <summary>离合拨片模式切换</summary>
    private void ClutchMode_Checked(object sender, RoutedEventArgs e)
    {
        if (CombinedAxisPanel == null || IndependentAxisPanel == null || KeyModePanel == null) return;

        if (sender == CombinedAxisRadio)
        {
            CombinedAxisPanel.Visibility = Visibility.Visible;
            IndependentAxisPanel.Visibility = Visibility.Collapsed;
            KeyModePanel.Visibility = Visibility.Collapsed;
        }
        else if (sender == IndependentAxisRadio)
        {
            CombinedAxisPanel.Visibility = Visibility.Collapsed;
            IndependentAxisPanel.Visibility = Visibility.Visible;
            KeyModePanel.Visibility = Visibility.Collapsed;
        }
        else if (sender == KeyModeRadio)
        {
            CombinedAxisPanel.Visibility = Visibility.Collapsed;
            IndependentAxisPanel.Visibility = Visibility.Collapsed;
            KeyModePanel.Visibility = Visibility.Visible;
        }
    }

    /// <summary>拨片按键按下（B20/B21）</summary>
    private void PaddleKeyButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (KeyResponseName == null) return;

        if (sender is Grid grid)
        {
            var textBlock = grid.Children.OfType<TextBlock>().FirstOrDefault();
            if (textBlock != null)
                KeyResponseName.Text = textBlock.Text;
        }
    }

    /// <summary>换挡拨片校准</summary>
    private void ShiftPaddleCalibrate_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Grid grid) return;

        // 判断是左拨片还是右拨片
        var parentGrid = grid.Parent as Grid;
        var isLeftPaddle = parentGrid?.Children.OfType<TextBlock>()
            .Any(tb => tb.Text.Contains("左 拨 片")) ?? false;

        var dotName = isLeftPaddle ? "LeftShiftPaddleStatusDot" : "RightShiftPaddleStatusDot";
        var statusName = isLeftPaddle ? "LeftShiftPaddleStatus" : "RightShiftPaddleStatus";
        var pathName = isLeftPaddle ? "LeftShiftPaddleCalibratePath" : "RightShiftPaddleCalibratePath";

        var dot = parentGrid?.FindName(dotName) as System.Windows.Shapes.Ellipse;
        var statusText = parentGrid?.FindName(statusName) as TextBlock;
        var calibratePath = parentGrid?.FindName(pathName) as System.Windows.Shapes.Path;

        if (statusText != null && statusText.Text == "等待输入...")
        {
            // 切换到已输入状态
            statusText.Text = "已输入";
            statusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEEEEE"));
            if (dot != null)
                dot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8016C642"));
            if (calibratePath != null)
            {
                calibratePath.Fill = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#C60E0E"),
                    (Color)ColorConverter.ConvertFromString("#600707"),
                    90);
            }
        }

        Debug.WriteLine($"[SteeringWheelControl] {(isLeftPaddle ? "左" : "右")}拨片校准触发");
        e.Handled = true;
    }

    /// <summary>开始拨片校准</summary>
    private void PaddleStartCalibration_Click(object sender, MouseButtonEventArgs e)
    {
        Debug.WriteLine("[SteeringWheelControl] 开始拨片校准");
        e.Handled = true;
    }
}
