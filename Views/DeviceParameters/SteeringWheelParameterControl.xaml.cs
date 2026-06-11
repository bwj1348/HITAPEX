using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HITAPEX.Models.Usb;
using HITAPEX.Services.Usb;

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

    // ── 按键参数状态（数组长度19，索引对应 Btn1-Btn19）──
    // 14个可调按键参数（按顺序: B1,B2,B3,B6,B7,B8,B9,B11,B12,B13,B16,B17,B18,B19）
    private int[] _buttonColors = Enumerable.Repeat(0, 14).ToArray();
    private bool[] _buttonTelemetryEnabled = new bool[14];
    private int[] _buttonTelemetryLightEffect = Enumerable.Repeat(0, 14).ToArray();
    private int[] _buttonTelemetryFunc = Enumerable.Repeat(0, 14).ToArray();
    private int[] _buttonTelemetryTriggerColor = Enumerable.Repeat(0, 14).ToArray();
    private int[] _buttonSpeeds = Enumerable.Repeat(0, 14).ToArray();

    // ── 转速灯状态 ──
    private int[] _rpmColors = new int[12];
    private double[] _rpmValues = Enumerable.Repeat(0.0, 12).ToArray();
    private double _rpmCapValue = 100;
    private int _rpmCurveType;
    private int _rpmDisplayMode;
    private int _rpmLightMode;
    private int _rpmStrobeMode;
    private int _rpmStrobeColor;
    private int _rpmSpeed;
    private int _rpmBaseLightMode;
    private int _rpmBaseLightSpeed;
    private bool _rpmTelemetryEnabled;

    // ── 拨片状态 ──
    private int _clutchMode;
    private double _clutchPointValue = 50;
    private double _deferredClutchPoint = -1; // -1 表示无需延迟定位

    // ── 当前选中的按键索引（用于弹窗设置）──
    private int _currentButtonIndex;

    // ── 预设管理 ──
    private WheelPresetSnapshot? _appliedPresetParameters;
    private bool _isPresetModified;
    private bool _isApplyingPreset;
    private bool _isAppliedPresetPersonal;
    private string _currentPresetName = "Default";
    private string _devicePresetName = string.Empty;

    // ── USB 通信状态 ──
    private bool _isSendingParameters;
    private bool _isApplyingParameters; // 从设备同步参数时阻止下发

    // ── 组合状态（无独立 UI 控件，需字段缓存）──
    private int _keyBrightness = 80;
    private int _sleepLightDuration;
    private int _standbyLightEffect;
    private int _standbyLightSpeed;
    private int _singleButtonAdjIndex = -1; // -1=全发, >=0=只发该可调索引的按键灯

    // ── HID 按键响应 ──
    /// <summary>上次 HID 按键位图（防抖）</summary>
    private ulong _lastHidButtonMask;
    /// <summary>弹窗打开时抑制 HID 对视觉效果的更新</summary>
    private bool _isPopupOpen;
    /// <summary>19 个按键的 GlowCircle 引用（仅圆形按键有值，方向键为 null）</summary>
    private readonly Ellipse?[] _buttonGlows = new Ellipse?[19];
    /// <summary>19 个按键的 OuterRing 引用（仅圆形按键有值，方向键为 null）</summary>
    private readonly Ellipse?[] _buttonRings = new Ellipse?[19];
    /// <summary>是否已完成一次性初始化，防止事件处理器重复注册</summary>
    private bool _isInitialized;

    // ── 转速灯数据快照（弹窗打开前保存，防止弹窗内 cap=0 误截断导致数据永久破坏）──
    private double[] _rpmValuesBeforePopup = [];
    private double _rpmCapValueBeforePopup;

    public bool HasUnsavedChanges => _isPresetModified;

    public SteeringWheelParameterControl()
    {
        InitializeComponent();
        Loaded += SteeringWheelParameterControl_Loaded;
        SpeedSlider.Loaded += (_, _) => UpdateSpeedSliderFill(SpeedSlider);
    }

    private async void SteeringWheelParameterControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;

            // 首次 Load 时订阅 HID 数据，之后一直保持（不随 Unload 取消）
            SubscribeHidData();

            // 订阅 USB 串口设备连接/断开事件 — 设备随时插拔时 UI 实时响应
            SubscribeUsbSerialEvents();

            // 订阅 ComboBox 变更事件
            if (SleepTimeCombo != null)
                SleepTimeCombo.SelectionChanged += (_, _) => OnParameterModified(WheelSendMask.SleepAndPaddle);
            if (StandbyEffectCombo != null)
                StandbyEffectCombo.SelectionChanged += (_, _) => OnParameterModified(WheelSendMask.SleepAndPaddle);

            // 亮度滑块：滑动时只更新百分比标签，松开滑块后再下发数据
            if (KeyBrightnessSlider != null)
                KeyBrightnessSlider.AddHandler(
                    System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
                    new System.Windows.Controls.Primitives.DragCompletedEventHandler(BrightnessSlider_DragCompleted));
            if (RpmBrightnessSlider != null)
                RpmBrightnessSlider.AddHandler(
                    System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
                    new System.Windows.Controls.Primitives.DragCompletedEventHandler(BrightnessSlider_DragCompleted));

            // 预缓存圆形按键引用，用于 HID 快速驱动 IsChecked
            CacheCircularButtons();
        }

        await RefreshDeviceInfoAsync();
        UpdatePresetDisplay();

        // 切回界面时重置按键掩码，强制下一帧 HID 数据刷新所有按键视觉效果
        _lastHidButtonMask = 0;
        // 立即用当前缓存掩码刷新一次（如果设备仍在持续上报数据）
        // 如果 HID 轮询间隔较长，这个重置确保 UI 尽快响应

        // 推迟到所有布局/默认值设置完成后才抓取基线，防止 Slider.ValueChanged 等
        // 在 Loaded 之后触发的初始化事件把 _isPresetModified 再次设为 true
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            _appliedPresetParameters = CaptureCurrentParameters();
            _isPresetModified = false;
            UpdatePresetDisplay();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
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
                // 1b. 检查是否有面盘设备处于更新模式
                var updateModeDevice = connectedDevices.FirstOrDefault(d =>
                {
                    var descriptor = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                    return descriptor != null && descriptor.DeviceType == DeviceType.Wheel
                           && descriptor.IsUpdateMode(d.Vid, d.Pid);
                });

                if (updateModeDevice != null)
                {
                    SetDisconnected();
                    ShowUpdateModeRedirectDialog(updateModeDevice);
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
                } // end base device check
            } // end else { // 2. 检查是否通过基座连接
            } // end else { // 1b. 更新模式检查
        } // end original else
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 刷新设备信息异常: {ex.Message}");
            SetDisconnected();
        }

        UpdateConnectionStatusDisplay();
        // 固件版本检查改为 fire-and-forget：API 服务器不可达时会阻塞 15s+，
        // 不应延迟后续 USB 参数获取命令（FetchWheelParameters / FetchPresetName）
        _ = CheckFirmwareVersionAsync();

        // 获取面盘参数并同步 UI
        await FetchWheelParametersAsync();

        // 获取设备预设名称
        await FetchPresetNameAsync();

        // 尝试将设备预设匹配到本地预设
        TryMatchLocalPreset();
    }

    /// <summary>对比设备上报的预设名称和参数与本地预设，若完全匹配则视为本地预设</summary>
    private void TryMatchLocalPreset()
    {
        if (string.IsNullOrEmpty(_devicePresetName) || _appliedPresetParameters == null || App.PresetService == null)
            return;

        try
        {
            var officialPresets = App.PresetService.LoadOfficialPresets(DeviceType.Wheel);
            var personalPresets = App.PresetService.LoadPersonalPresets(DeviceType.Wheel);

            // 先查个人预设，再查官方预设
            PresetItem? matched = personalPresets.FirstOrDefault(p => p.Name == _devicePresetName);
            bool isPersonal = true;
            if (matched == null)
            {
                matched = officialPresets.FirstOrDefault(p => p.Name == _devicePresetName);
                isPersonal = false;
            }

            if (matched?.WheelParameters != null && _appliedPresetParameters.ParametersEqual(matched.WheelParameters))
            {
                _currentPresetName = matched.Name;
                _isAppliedPresetPersonal = isPersonal;
                _devicePresetName = string.Empty;
                Debug.WriteLine($"[SteeringWheelControl] 设备预设匹配到本地{(isPersonal ? "个人" : "官方")}预设: {matched.Name}");
                UpdatePresetDisplay();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 匹配本地预设异常: {ex.Message}");
        }
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
        _connectedWheelDevice = null;
        _baseDevice = null;
        _isWheelViaBase = false;
        _deviceModelName = "面盘";
        _connectionStatusText = "未连接";
        _connectionStatusColor = "#C60E0E";
        _firmwareVersion = "---";

        // 重置预设状态
        _appliedPresetParameters = null;
        _currentPresetName = "Default";
        _devicePresetName = string.Empty;
        _isPresetModified = false;
        _isAppliedPresetPersonal = false;

        // 清除 HID 按键视觉效果
        _lastHidButtonMask = 0;
        for (int i = 0; i < 19; i++)
        {
            if (_buttonGlows[i] != null) _buttonGlows[i]!.Visibility = Visibility.Collapsed;
            if (_buttonRings[i] != null) _buttonRings[i]!.Visibility = Visibility.Collapsed;
        }
        if (KeyResponseName != null)
            KeyResponseName.Text = "---";
    }

    /// <summary>设备处于固件更新模式时弹窗，引导用户前往固件更新页面</summary>
    private void ShowUpdateModeRedirectDialog(UsbDeviceInfo device)
    {
        var mainWindow = Window.GetWindow(this) as HITAPEX.MainWindow
                         ?? Application.Current.MainWindow as HITAPEX.MainWindow;
        if (mainWindow == null) return;

        var descriptor = DeviceRegistry.FindByVidPid(device.Vid, device.Pid);
        var deviceName = descriptor?.ModelName ?? "设备";

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = "设 备 更 新 模 式";
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = $"{deviceName}当前处于固件更新模式，参数设置功能不可用。\n请前往固件更新页面完成或恢复固件。",
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton("前 往", (_, _) =>
        {
            dialog.Hide();
            NavigateToFirmwareUpdate();
        }, isPrimary: true);

        dialog.AddButton("取 消", (_, _) =>
        {
            dialog.Hide();
        }, isPrimary: false);

        dialog.Show();
    }

    /// <summary>导航到设置界面的固件更新选项卡</summary>
    private void NavigateToFirmwareUpdate()
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
        if (!_isPresetModified || _appliedPresetParameters == null)
            return;

        _isApplyingPreset = true;
        ApplyPresetSnapshot(_appliedPresetParameters);
        _isApplyingPreset = false;
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

        // 控件未加载到可视化树时 Window.GetWindow 返回 null；回退到 Application.Current.MainWindow
        var mainWindow = Window.GetWindow(this) as HITAPEX.MainWindow
                          ?? Application.Current.MainWindow as HITAPEX.MainWindow;
        if (mainWindow == null)
        {
            // 兜底：无法弹窗时直接放弃修改并执行回调，避免 _isCheckingUnsavedNavigation 永久死锁
            _isPresetModified = false;
            onSaved?.Invoke();
            return;
        }

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
                TrySaveWithRetry(() => PerformSave(), () => onSaved?.Invoke());
            }, isPrimary: true);
        }
        else
        {
            dialog.AddButton("另 存 为", (_, _) =>
            {
                dialog.Hide();
                SaveAsInternal(onSaved);
            }, isPrimary: true);
        }

        dialog.AddButton("取 消", (_, _) =>
        {
            dialog.Hide();
            onCancelled?.Invoke();
        }, isPrimary: false);

        dialog.Show();
    }

    private bool PerformSave()
    {
        var popup = GetPresetListPopup();
        if (popup == null || App.PresetService == null) return false;

        try
        {
            var personalPresets = App.PresetService.LoadPersonalPresets(DeviceType.Wheel);
            var target = personalPresets.FirstOrDefault(p => p.Name == _currentPresetName);
            if (target == null) return false;

            target.WheelParameters = CaptureCurrentParameters();
            App.PresetService.SavePersonalPresets(personalPresets, DeviceType.Wheel);
            popup.RefreshPersonalPresets(personalPresets);

            _appliedPresetParameters = CaptureCurrentParameters();
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

    private void ShowSaveFailedDialog(Action? onRetry)
    {
        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = "保 存 失 败";
        dialog.ShowIcon = true;
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = "当前预设未能成功保存，请检查后重试。",
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton("重 试", (_, _) =>
        {
            dialog.Hide();
            onRetry?.Invoke();
        }, isPrimary: true);

        dialog.AddButton("取 消", (_, _) =>
        {
            dialog.Hide();
        }, isPrimary: false);

        dialog.Show();
    }

    private void TrySaveWithRetry(Func<bool> saveAction, Action onSuccess)
    {
        if (saveAction())
        {
            onSuccess();
            return;
        }

        ShowSaveFailedDialog(() => TrySaveWithRetry(saveAction, onSuccess));
    }

    private void ShowSuccessToast(string message)
    {
        var rootPanel = (Window.GetWindow(this)?.Content as Panel);
        if (rootPanel == null) return;

        var toast = new Grid
        {
            Width = 360,
            Height = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Panel.SetZIndex(toast, 2000);

        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M360 0H9L0 9V100H351L360 91V0Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
            Stretch = Stretch.Fill
        });

        toast.Children.Add(new SharpVectors.Converters.SvgViewbox
        {
            Source = new Uri("/Assets/Group126548867.svg", UriKind.Relative),
            Stretch = Stretch.Fill
        });

        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Width = 340,
            Height = 80,
            Data = Geometry.Parse("M339.5 0.5V73.793L333.793 79.5H0.5V6.20703L6.20703 0.5H339.5Z"),
            Stroke = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
            StrokeThickness = 1,
            Stretch = Stretch.Fill
        });

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconCanvas = new Canvas { Width = 22, Height = 22 };
        iconCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M6.13672 12.2886L9.29057 14.8117C9.37527 14.8814 9.47445 14.9314 9.5809 14.9581C9.68735 14.9847 9.79839 14.9872 9.90595 14.9655C10.0145 14.9452 10.1175 14.9016 10.2077 14.8379C10.298 14.7742 10.3735 14.6918 10.429 14.5963L15.3675 6.13477"),
            Stroke = new SolidColorBrush(Color.FromRgb(0x16, 0xC6, 0x42)),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        });
        iconCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M10.75 20.75C16.2728 20.75 20.75 16.2728 20.75 10.75C20.75 5.22715 16.2728 0.75 10.75 0.75C5.22715 0.75 0.75 5.22715 0.75 10.75C0.75 16.2728 5.22715 20.75 10.75 20.75Z"),
            Stroke = new SolidColorBrush(Color.FromRgb(0x16, 0xC6, 0x42)),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        });

        var iconViewbox = new Viewbox { Width = 22, Height = 22, Margin = new Thickness(0, 0, 20, 0), Child = iconCanvas };
        contentPanel.Children.Add(iconViewbox);

        contentPanel.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 30,
            Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        });

        toast.Children.Add(contentPanel);
        rootPanel.Children.Add(toast);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (rootPanel.Children.Contains(toast))
                rootPanel.Children.Remove(toast);
        };
        timer.Start();
    }

    private void SaveAsInternal(Action? onSaved)
    {
        if (App.PresetService == null) return;
        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var personalPresets = App.PresetService.LoadPersonalPresets(DeviceType.Wheel);
        var existingNames = personalPresets.Select(p => p.Name).ToList();

        var rootPanel = mainWindow.Content as Panel;
        if (rootPanel == null) return;

        var editPopup = new EditPresetPopup { DeviceType = DeviceType.Wheel };
        rootPanel.Children.Add(editPopup);

        editPopup.EditConfirmed += (_, edited) =>
        {
            var presetName = edited.Name;
            var newPreset = new PresetItem
            {
                Name = presetName,
                Description = edited.Description,
                Category = edited.Category,
                Games = edited.Games,
                WheelParameters = CaptureCurrentParameters(),
                IsPersonal = true,
                DeviceType = DeviceType.Wheel
            };

            var currentPersonal = App.PresetService.LoadPersonalPresets(DeviceType.Wheel);
            currentPersonal.Add(newPreset);
            App.PresetService.SavePersonalPresets(currentPersonal, DeviceType.Wheel);

            var popup = GetPresetListPopup();
            popup?.RefreshPersonalPresets(currentPersonal);

            _appliedPresetParameters = CaptureCurrentParameters();
            _currentPresetName = presetName;
            _isAppliedPresetPersonal = true;
            _isPresetModified = false;
            UpdatePresetDisplay();

            if (rootPanel.Children.Contains(editPopup))
                rootPanel.Children.Remove(editPopup);

            onSaved?.Invoke();
        };

        editPopup.EditCancelled += (_, _) =>
        {
            if (rootPanel.Children.Contains(editPopup))
                rootPanel.Children.Remove(editPopup);
        };

        editPopup.BeginSaveAs(existingNames);
        editPopup.Show();
    }

    private PresetListPopup? GetPresetListPopup()
    {
        if (Window.GetWindow(this) is HITAPEX.MainWindow mainWindow)
            return mainWindow.GetPresetListPopup(DeviceType.Wheel);
        return null;
    }

    /// <summary>需要下发的协议包掩码</summary>
    [Flags]
    private enum WheelSendMask
    {
        None = 0,
        RpmBaseMode = 1 << 0,   // 0x2103 转速灯基础模式
        RpmIndicator = 1 << 1,  // 0x2104 转速灯转速指示
        RpmMode = 1 << 2,       // 0x2105 转速灯模式等属性
        ButtonLight = 1 << 3,   // 0x2107 按键灯
        SleepAndPaddle = 1 << 4,// 0x2108 睡眠和拨片
        All = RpmBaseMode | RpmIndicator | RpmMode | ButtonLight | SleepAndPaddle,
    }

    /// <summary>任意参数修改后的统一入口，仅更新 UI 状态，不触发数据下发</summary>
    private void OnParameterModified(WheelSendMask sendMask = WheelSendMask.None)
    {
        // 控件尚未加载时忽略事件（Slider/ComboBox 初始化赋值也会触发 ValueChanged/SelectionChanged，
        // 此时并非用户操作，不应标记为已修改）
        if (!IsLoaded || _isApplyingParameters || _isApplyingPreset) return;
        _isPresetModified = true;
        UpdatePresetDisplay();
        if (sendMask != WheelSendMask.None)
            SendWheelParameters(sendMask);
    }

    /// <summary>将当前 UI 参数捕获为快照</summary>
    private WheelPresetSnapshot CaptureCurrentParameters()
    {
        return new WheelPresetSnapshot
        {
            KeyColorEnabled = KeyColorToggle?.IsChecked ?? true,
            GlobalKeyColor = GetSelectedGlobalKeyColor(),
            ShowKeyNumber = ShowKeyNumberToggle?.IsChecked ?? true,
            KeyBrightness = (int)(KeyBrightnessSlider?.Value ?? 80),
            RpmBrightness = (int)(RpmBrightnessSlider?.Value ?? 80),
            SleepLightDuration = SleepTimeCombo?.SelectedIndex ?? 0,
            StandbyLightEffect = StandbyEffectCombo?.SelectedIndex ?? 0,
            GlobalFlashSpeed = (int)(SpeedSlider?.Value ?? 0),

            ButtonColors = (int[])_buttonColors.Clone(),
            ButtonTelemetryEnabled = (bool[])_buttonTelemetryEnabled.Clone(),
            ButtonTelemetryLightEffect = (int[])_buttonTelemetryLightEffect.Clone(),
            ButtonTelemetryFunc = (int[])_buttonTelemetryFunc.Clone(),
            ButtonTelemetryTriggerColor = (int[])_buttonTelemetryTriggerColor.Clone(),
            ButtonSpeeds = (int[])_buttonSpeeds.Clone(),

            RpmColors = (int[])_rpmColors.Clone(),
            RpmValues = (double[])_rpmValues.Clone(),
            RpmCapValue = _rpmCapValue,
            RpmCurveType = _rpmCurveType,
            RpmDisplayMode = _rpmDisplayMode,
            RpmLightMode = _rpmLightMode,
            RpmStrobeMode = _rpmStrobeMode,
            RpmStrobeColor = _rpmStrobeColor,
            RpmSpeed = _rpmSpeed,
            RpmBaseLightMode = _rpmBaseLightMode,
            RpmBaseLightSpeed = _rpmBaseLightSpeed,
            RpmTelemetryEnabled = _rpmTelemetryEnabled,

            ClutchMode = GetSelectedClutchMode(),
            ClutchPointValue = GetClutchPointValue(),
        };
    }

    /// <summary>将预设快照应用到 UI 控件</summary>
    private void ApplyPresetSnapshot(WheelPresetSnapshot p)
    {
        // 全局按键颜色选择
        SetGlobalKeyColor(p.GlobalKeyColor);

        // 全局按键颜色开关
        if (KeyColorToggle != null)
        {
            KeyColorToggle.IsChecked = p.KeyColorEnabled;
            SetKeyColorBlocksEnabled(p.KeyColorEnabled);
        }

        // 显示按键编号
        if (ShowKeyNumberToggle != null)
            ShowKeyNumberToggle.IsChecked = p.ShowKeyNumber;

        // 亮度
        if (KeyBrightnessSlider != null)
            KeyBrightnessSlider.Value = p.KeyBrightness;
        if (RpmBrightnessSlider != null)
            RpmBrightnessSlider.Value = p.RpmBrightness;

        // 睡眠灯光时间
        if (SleepTimeCombo != null)
            SleepTimeCombo.SelectedIndex = p.SleepLightDuration;

        // 待机灯效
        if (StandbyEffectCombo != null)
            StandbyEffectCombo.SelectedIndex = p.StandbyLightEffect;

        // 闪烁速度
        if (SpeedSlider != null)
            SpeedSlider.Value = p.GlobalFlashSpeed;

        // 按键参数（14个可调按键）
        Array.Copy(p.ButtonColors, _buttonColors, 14);
        Array.Copy(p.ButtonTelemetryEnabled, _buttonTelemetryEnabled, 14);
        Array.Copy(p.ButtonTelemetryLightEffect, _buttonTelemetryLightEffect, 14);
        Array.Copy(p.ButtonTelemetryFunc, _buttonTelemetryFunc, 14);
        Array.Copy(p.ButtonTelemetryTriggerColor, _buttonTelemetryTriggerColor, 14);
        Array.Copy(p.ButtonSpeeds, _buttonSpeeds, 14);

        // 转速灯
        Array.Copy(p.RpmColors, _rpmColors, 12);
        Array.Copy(p.RpmValues, _rpmValues, 12);
        _rpmCapValue = p.RpmCapValue;
        _rpmCurveType = p.RpmCurveType;
        _rpmDisplayMode = p.RpmDisplayMode;
        _rpmLightMode = p.RpmLightMode;
        _rpmStrobeMode = p.RpmStrobeMode;
        _rpmStrobeColor = p.RpmStrobeColor;
        _rpmSpeed = p.RpmSpeed;
        _rpmBaseLightMode = p.RpmBaseLightMode;
        _rpmBaseLightSpeed = p.RpmBaseLightSpeed;
        _rpmTelemetryEnabled = p.RpmTelemetryEnabled;

        // 拨片
        SetClutchMode(p.ClutchMode);
        SetClutchPointValue(p.ClutchPointValue);
    }

    private int GetSelectedGlobalKeyColor()
    {
        if (ColorRed?.IsChecked == true) return 0;
        if (ColorOrange?.IsChecked == true) return 1;
        if (ColorYellow?.IsChecked == true) return 2;
        if (ColorGreen?.IsChecked == true) return 3;
        if (ColorCyan?.IsChecked == true) return 4;
        if (ColorBlue?.IsChecked == true) return 5;
        if (ColorPurple?.IsChecked == true) return 6;
        if (ColorWhite?.IsChecked == true) return 7;
        return 0;
    }

    private void SetGlobalKeyColor(int index)
    {
        var colorButtons = new RadioButton?[] { ColorRed, ColorOrange, ColorYellow, ColorGreen, ColorCyan, ColorBlue, ColorPurple, ColorWhite };
        if (index >= 0 && index < colorButtons.Length && colorButtons[index] != null)
        {
            colorButtons[index]!.IsChecked = true;
        }
    }

    private int GetSelectedClutchMode()
    {
        if (CombinedAxisRadio?.IsChecked == true) return 0;
        if (IndependentAxisRadio?.IsChecked == true) return 1;
        if (KeyModeRadio?.IsChecked == true) return 2;
        return 0;
    }

    private void SetClutchMode(int mode)
    {
        switch (mode)
        {
            case 0:
                CombinedAxisRadio!.IsChecked = true;
                break;
            case 1:
                IndependentAxisRadio!.IsChecked = true;
                break;
            case 2:
                KeyModeRadio!.IsChecked = true;
                break;
        }
    }

    private double GetClutchPointValue()
    {
        return _clutchPointValue;
    }

    private void SetClutchPointValue(double percent)
    {
        _clutchPointValue = percent;
        if (ClutchPointPercent != null)
            ClutchPointPercent.Text = $"{percent}%";

        PositionClutchPointIndicator();
    }

    private void PositionClutchPointIndicator()
    {
        if (ClutchPointIndicator == null) return;

        var parentCanvas = ClutchPointIndicator.Parent as Canvas;
        if (parentCanvas == null || parentCanvas.ActualWidth <= 0)
        {
            // Canvas 尚未布局完成，记录当前值等布局完成后定位
            _deferredClutchPoint = _clutchPointValue;
            parentCanvas!.SizeChanged += OnClutchCanvasSizeChanged;
            return;
        }

        var x = Math.Round(_clutchPointValue / 100.0 * parentCanvas.ActualWidth);
        Canvas.SetLeft(ClutchPointIndicator, x);

        // 同步移动滑块拇指位置
        var thumb = (parentCanvas.Children.Cast<UIElement>()
            .FirstOrDefault(c => c is Canvas canvas && canvas.Name == "ClutchPointThumb") as Canvas);
        if (thumb != null)
            Canvas.SetLeft(thumb, x - 8);

        _deferredClutchPoint = -1;
    }

    private void OnClutchCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_deferredClutchPoint < 0) return;
        var canvas = sender as Canvas;
        if (canvas == null || canvas.ActualWidth <= 0) return;
        canvas.SizeChanged -= OnClutchCanvasSizeChanged;
        SetClutchPointValue(_deferredClutchPoint);
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
            var isSaveEnabled = _isAppliedPresetPersonal && _isPresetModified;
            if (isSaveEnabled)
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

        var isExportEnabled = _isAppliedPresetPersonal;
        if (ExportButtonPath != null)
        {
            if (isExportEnabled)
            {
                ExportButtonPath.ClearValue(System.Windows.Shapes.Path.FillProperty);
                ExportButtonPath.Cursor = System.Windows.Input.Cursors.Hand;
            }
            else
            {
                ExportButtonPath.Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xEE, 0xEE, 0xEE));
                ExportButtonPath.Cursor = System.Windows.Input.Cursors.Arrow;
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
        if (!_isPresetModified || _appliedPresetParameters == null)
            return;

        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = "撤 回 更 改";
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = "所有未保存的调整将被恢复为上一次保存的状态。",
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton("撤 回", (_, _) =>
        {
            dialog.Hide();
            DiscardChanges();
        }, isPrimary: true);

        dialog.AddButton("取 消", (_, _) =>
        {
            dialog.Hide();
        }, isPrimary: false);

        dialog.Show();
    }

    private void SaveButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isAppliedPresetPersonal || !_isPresetModified) return;
        TrySaveWithRetry(() => PerformSave(), () =>
        {
            ShowSuccessToast("保 存 成 功");
        });
    }

    private void SaveAsButton_Click(object sender, MouseButtonEventArgs e)
    {
        SaveAsInternal(null);
    }

    private void ExportButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isAppliedPresetPersonal) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出预设",
            Filter = "预设文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = ".json",
            FileName = _currentPresetName == "Default" ? "wheel_preset" : _currentPresetName
        };

        if (dlg.ShowDialog() != true || App.PresetService == null) return;

        var fileName = dlg.FileName;
        TryExportWithRetry(fileName);
    }

    private void TryExportWithRetry(string fileName)
    {
        if (PerformExport(fileName))
        {
            ShowSuccessToast("导 出 成 功");
            return;
        }

        ShowExportFailedDialog(() => TryExportWithRetry(fileName));
    }

    private bool PerformExport(string fileName)
    {
        try
        {
            var snapshot = _appliedPresetParameters ?? CaptureCurrentParameters();
            var exportItem = new PresetItem
            {
                Name = _currentPresetName,
                WheelParameters = snapshot,
                IsPersonal = true,
                DeviceType = DeviceType.Wheel
            };
            App.PresetService!.ExportPreset(exportItem, fileName);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 导出预设失败: {ex.Message}");
            return false;
        }
    }

    private void ShowExportFailedDialog(Action? onRetry)
    {
        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = "导 出 失 败";
        dialog.ShowIcon = true;
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = "当前预设导出失败，请检查后重试。",
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton("重 试", (_, _) =>
        {
            dialog.Hide();
            onRetry?.Invoke();
        }, isPrimary: true);

        dialog.AddButton("取 消", (_, _) =>
        {
            dialog.Hide();
        }, isPrimary: false);

        dialog.Show();
    }

    private void PresetListButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var popup = mainWindow.ShowPresetListPopup(DeviceType.Wheel);
            popup.PresetApplied -= OnPresetApplied;
            popup.PresetApplied += OnPresetApplied;
        }
    }

    private void OnPresetApplied(object? sender, PresetItem preset)
    {
        if (preset.WheelParameters == null) return;

        if (_isPresetModified)
            ShowUnsavedDialog(() => ApplyPreset(preset), () => ApplyPreset(preset));
        else
            ApplyPreset(preset);
    }

    private void ApplyPreset(PresetItem preset)
    {
        _isApplyingPreset = true;
        ApplyPresetSnapshot(preset.WheelParameters!);
        _isApplyingPreset = false;

        _appliedPresetParameters = preset.WheelParameters;
        _currentPresetName = preset.Name;
        _isAppliedPresetPersonal = preset.IsPersonal;
        _isPresetModified = false;
        UpdatePresetDisplay();

        SendPresetName(preset.Name);
        SendWheelParameters();
    }

    // ════════════════════════════════════════════════════════════════
    //  USB 设备通信 — 下发 & 获取面盘参数
    // ════════════════════════════════════════════════════════════════

    /// <summary>获取目标通信设备（直连面盘 > 通过基座）</summary>
    private UsbDeviceInfo? GetTargetDevice()
    {
        if (_connectedWheelDevice != null)
            return _connectedWheelDevice;
        if (_isWheelViaBase && _baseDevice != null)
            return _baseDevice;
        return null;
    }

    /// <summary>从设备获取全部面盘参数并同步到 UI</summary>
    private async Task FetchWheelParametersAsync()
    {
        var targetDevice = GetTargetDevice();
        if (targetDevice == null || App.ProtocolService == null)
            return;

        _isApplyingParameters = true;
        try
        {
            // 必须顺序发送：SendCommandAsync 使用每设备键单例 TCS，
            // 并发调用会互相取消对方的 TaskCompletionSource，导致除最后一个外全部超时
            await FetchRpmBaseModeAsync(targetDevice);
            await FetchRpmIndicatorAsync(targetDevice);
            await FetchRpmModeAsync(targetDevice);
            await FetchButtonLightAsync(targetDevice);
            await FetchSleepAndPaddleAsync(targetDevice);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 获取面盘参数异常: {ex.Message}");
        }
        finally
        {
            _isApplyingParameters = false;
        }

        // 设备上报参数作为首次基线预设
        _appliedPresetParameters = CaptureCurrentParameters();
        _currentPresetName = "Default";
        _isAppliedPresetPersonal = false;
        _isPresetModified = false;
        UpdatePresetDisplay();
    }

    private async Task FetchRpmBaseModeAsync(UsbDeviceInfo device)
    {
        if (App.ProtocolService == null) return;

        try
        {
            var cmd = DeviceProtocolService.BuildGetWheelRpmBaseModeCommand();
            var response = await App.ProtocolService.SendCommandAsync(device.DeviceKey, cmd);
            if (response == null) return;

            var parsed = DeviceProtocolService.ParseWheelRpmBaseModeResponse(response);
            if (parsed == null) return;

            _rpmBaseLightMode = parsed.BaseMode;
            _rpmBaseLightSpeed = parsed.BaseSpeed;
            for (int i = 0; i < 12; i++)
            {
                var c = parsed.LedColors[i];
                _rpmColors[i] = DeviceProtocolService.RgbToColorIndex(c[0], c[1], c[2]);
            }

            Debug.WriteLine($"[SteeringWheelControl] 转速灯基础模式参数已同步: mode={_rpmBaseLightMode}, speed={_rpmBaseLightSpeed}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 获取转速灯基础模式异常: {ex.Message}");
        }
    }

    private async Task FetchRpmIndicatorAsync(UsbDeviceInfo device)
    {
        if (App.ProtocolService == null) return;

        try
        {
            var cmd = DeviceProtocolService.BuildGetWheelRpmIndicatorCommand();
            var response = await App.ProtocolService.SendCommandAsync(device.DeviceKey, cmd);
            if (response == null) return;

            var parsed = DeviceProtocolService.ParseWheelRpmIndicatorResponse(response);
            if (parsed == null) return;

            _rpmDisplayMode = parsed.TriggerMode;
            for (int i = 0; i < 12; i++)
            {
                _rpmValues[i] = parsed.TriggerValues[i];
                var c = parsed.LedColors[i];
                _rpmColors[i] = DeviceProtocolService.RgbToColorIndex(c[0], c[1], c[2]);
            }

            Debug.WriteLine($"[SteeringWheelControl] 转速灯转速指示参数已同步: triggerMode={_rpmDisplayMode}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 获取转速灯转速指示异常: {ex.Message}");
        }
    }

    private async Task FetchRpmModeAsync(UsbDeviceInfo device)
    {
        if (App.ProtocolService == null) return;

        try
        {
            var cmd = DeviceProtocolService.BuildGetWheelRpmModeCommand();
            var response = await App.ProtocolService.SendCommandAsync(device.DeviceKey, cmd);
            if (response == null) return;

            var parsed = DeviceProtocolService.ParseWheelRpmModeResponse(response);
            if (parsed == null) return;

            var rpmBrightness = (int)parsed.Brightness;
            // 协议: 0=遥测模式(遥测开启), 1=关闭遥测(基础模式)
            _rpmTelemetryEnabled = parsed.TelemetryOff == 0;
            _rpmLightMode = parsed.LightMode;
            _rpmStrobeMode = parsed.StrobeMode;
            _rpmSpeed = parsed.StrobeSpeed;
            _rpmStrobeColor = DeviceProtocolService.RgbToColorIndex(parsed.StrobeColorR, parsed.StrobeColorG, parsed.StrobeColorB);
            _rpmCapValue = parsed.StrobeTriggerValue; // 爆闪触发值

            // 同步到UI控件
            if (RpmBrightnessSlider != null)
                RpmBrightnessSlider.Value = rpmBrightness;

            Debug.WriteLine($"[SteeringWheelControl] 转速灯模式参数已同步: brightness={rpmBrightness}, telemetry={(parsed.TelemetryOff == 0 ? "开" : "关")}, strobeTrigger={parsed.StrobeTriggerValue}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 获取转速灯模式异常: {ex.Message}");
        }
    }

    private async Task FetchButtonLightAsync(UsbDeviceInfo device)
    {
        if (App.ProtocolService == null) return;

        try
        {
            // 先获取物理索引 0 (B1)，确定 LED 模式
            var cmd0 = DeviceProtocolService.BuildGetWheelButtonLightCommand(0);
            var resp0 = await App.ProtocolService.SendCommandAsync(device.DeviceKey, cmd0);
            if (resp0 == null) return;

            var p0 = DeviceProtocolService.ParseWheelButtonLightResponse(resp0);
            if (p0 == null) return;

            var ledMode = p0.LedMode; // 0=单独颜色常亮, 1=统一颜色常亮
            _keyBrightness = p0.Brightness;
            if (KeyBrightnessSlider != null)
                KeyBrightnessSlider.Value = _keyBrightness;

            // 同步 LED 模式到 KeyColorToggle 开关（触发 Checked/Unchecked 后会正确启用/禁用色块、设置颜色）
            if (KeyColorToggle != null)
                KeyColorToggle.IsChecked = ledMode == 1;

            // 统一颜色模式时，设置全局颜色（需在 KeyColorToggle.IsChecked 设置之后，
            // 因为 IsChecked 的 Checked 事件处理器会将 ColorRed 选中，这里再覆盖为设备实际的统一颜色）
            if (ledMode == 1)
            {
                var gColor = DeviceProtocolService.RgbToColorIndex(p0.ColorR, p0.ColorG, p0.ColorB);
                SetGlobalKeyColor(gColor);
            }

            // 可调索引0 = LED索引0（对应B1），直接应用
            ApplyButtonLightFromResponse(p0, 0, _buttonColors, _buttonTelemetryEnabled,
                _buttonTelemetryFunc, _buttonTelemetryLightEffect, _buttonSpeeds, _buttonTelemetryTriggerColor);

            // 单独颜色常亮模式：逐个顺序获取剩余13个可调按键的参数（LED索引 1-13）
            // SendCommandAsync 使用每设备键单例 TCS，必须顺序发送，不能并发
            if (ledMode == 0)
            {
                for (int i = 1; i < 14; i++)
                {
                    var cmd = DeviceProtocolService.BuildGetWheelButtonLightCommand((byte)i);
                    var resp = await App.ProtocolService.SendCommandAsync(device.DeviceKey, cmd);
                    if (resp == null) continue;
                    var parsed = DeviceProtocolService.ParseWheelButtonLightResponse(resp);
                    if (parsed != null)
                        ApplyButtonLightFromResponse(parsed, i, _buttonColors, _buttonTelemetryEnabled,
                            _buttonTelemetryFunc, _buttonTelemetryLightEffect, _buttonSpeeds, _buttonTelemetryTriggerColor);
                }
            }
            else // 统一颜色常亮模式：14个可调按键复用索引 0 的设置
            {
                var colorIdx = DeviceProtocolService.RgbToColorIndex(p0.ColorR, p0.ColorG, p0.ColorB);
                for (int i = 1; i < 14; i++)
                {
                    _buttonColors[i] = colorIdx;
                    _buttonTelemetryEnabled[i] = p0.TelemetryFunc != 0;
                    if (p0.TelemetryFunc != 0)
                        _buttonTelemetryFunc[i] = p0.TelemetryFunc - 1;
                    _buttonTelemetryLightEffect[i] = p0.FlashSpeed == 0xFF ? 0 : 1;
                    _buttonSpeeds[i] = p0.FlashSpeed == 0xFF ? 0 : Math.Min((int)p0.FlashSpeed, 5);
                    var tc = DeviceProtocolService.RgbToColorIndex(p0.TelemetryColorR, p0.TelemetryColorG, p0.TelemetryColorB);
                    _buttonTelemetryTriggerColor[i] = tc;
                }
            }

            Debug.WriteLine($"[SteeringWheelControl] 按键灯参数已同步(14可调按键): ledMode={ledMode}, brightness={_keyBrightness}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 获取按键灯参数异常: {ex.Message}");
        }
    }

    /// <summary>将单个按键灯的协议响应应用到对应索引的数组</summary>
    private static void ApplyButtonLightFromResponse(WheelButtonLightResponse p, int index,
        int[] buttonColors, bool[] buttonTelemetryEnabled, int[] buttonTelemetryFunc,
        int[] buttonTelemetryLightEffect, int[] buttonSpeeds, int[] buttonTelemetryTriggerColor)
    {
        var colorIdx = DeviceProtocolService.RgbToColorIndex(p.ColorR, p.ColorG, p.ColorB);
        buttonColors[index] = colorIdx;
        // 协议 TelemetryFunc: 0=关闭, 1=ABS介入, 2=TC介入, ... 7=车轮打滑
        // 关闭时不覆盖下拉框索引，保留用户上次选中的遥测功能
        if (p.TelemetryFunc != 0)
            buttonTelemetryFunc[index] = p.TelemetryFunc - 1;
        buttonTelemetryEnabled[index] = p.TelemetryFunc != 0;
        buttonTelemetryLightEffect[index] = p.FlashSpeed == 0xFF ? 0 : 1; // 0=常亮, 1=闪烁
        buttonSpeeds[index] = p.FlashSpeed == 0xFF ? 0 : Math.Min((int)p.FlashSpeed, 5);
        var tc = DeviceProtocolService.RgbToColorIndex(p.TelemetryColorR, p.TelemetryColorG, p.TelemetryColorB);
        buttonTelemetryTriggerColor[index] = tc;
    }

    private async Task FetchSleepAndPaddleAsync(UsbDeviceInfo device)
    {
        if (App.ProtocolService == null) return;

        try
        {
            var cmd = DeviceProtocolService.BuildGetWheelSleepAndPaddleCommand();
            var response = await App.ProtocolService.SendCommandAsync(device.DeviceKey, cmd);
            if (response == null) return;

            var parsed = DeviceProtocolService.ParseWheelSleepAndPaddleResponse(response);
            if (parsed == null) return;

            // UI 下拉框索引与协议值对应:
            // 协议: 0=从不, 1=5分钟, 2=10分钟, 3=15分钟, 4=30分钟, 5=60分钟
            // UI:   0=5分钟, 1=10分钟, 2=15分钟, 3=30分钟, 4=60分钟, 5=从不
            _sleepLightDuration = parsed.SleepTime switch
            {
                0 => 5, // 从不 -> UI index 5
                1 => 0, // 5分钟 -> UI index 0
                2 => 1, // 10分钟 -> UI index 1
                3 => 2, // 15分钟 -> UI index 2
                4 => 3, // 30分钟 -> UI index 3
                5 => 4, // 60分钟 -> UI index 4
                _ => 5
            };

            // 协议: 0=关灯, 1=呼吸; UI: index 0=呼吸, index 1=关灯 → 取反
            _standbyLightEffect = parsed.SleepEffect == 0 ? 1 : 0;
            _standbyLightSpeed = parsed.SleepEffectSpeed;

            // 离合拨片模式 (协议: 0=独立轴, 1=合成轴, 2=按键)
            // UI: CombinedAxisRadio=0(合成轴), IndependentAxisRadio=1(独立轴), KeyModeRadio=2(按键)
            _clutchMode = parsed.ClutchPaddleMode switch
            {
                0 => 1, // 独立轴 -> _clutchMode=1
                1 => 0, // 合成轴 -> _clutchMode=0
                2 => 2, // 按键 -> _clutchMode=2
                _ => 0
            };

            _clutchPointValue = parsed.ClutchBitePoint;

            // 同步到UI控件
            if (SleepTimeCombo != null)
                SleepTimeCombo.SelectedIndex = _sleepLightDuration;
            if (StandbyEffectCombo != null)
                StandbyEffectCombo.SelectedIndex = _standbyLightEffect;
            if (SpeedSlider != null)
                SpeedSlider.Value = _standbyLightSpeed;
            SetClutchMode(_clutchMode);
            SetClutchPointValue(_clutchPointValue);

            Debug.WriteLine($"[SteeringWheelControl] 睡眠和拨片参数已同步: sleep={_sleepLightDuration}, clutch={_clutchMode}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 获取睡眠和拨片参数异常: {ex.Message}");
        }
    }

    /// <summary>根据当前UI状态下发面盘设置命令</summary>
    /// <param name="sendMask">需下发的协议包掩码，None 时下发全部</param>
    private void SendWheelParameters(WheelSendMask sendMask = WheelSendMask.All)
    {
        if (_isSendingParameters || _isApplyingParameters)
            return;

        var targetDevice = GetTargetDevice();
        if (targetDevice == null) return;

        // 只在下发全部包时捕获快照（应用预设场景）
        WheelPresetSnapshot snapshot;
        if (sendMask == WheelSendMask.All)
        {
            snapshot = CaptureCurrentParameters();
        }
        else
        {
            // 仅下发变更包时，从当前字段即时读取（避免不必要的大数组克隆）
            snapshot = CaptureCurrentParameters();
        }

        try
        {
            _isSendingParameters = true;

            if ((sendMask & WheelSendMask.RpmBaseMode) != 0)
                SendWheelRpmBaseMode(targetDevice, snapshot);
            if ((sendMask & WheelSendMask.RpmIndicator) != 0)
                SendWheelRpmIndicator(targetDevice, snapshot);
            if ((sendMask & WheelSendMask.RpmMode) != 0)
                SendWheelRpmMode(targetDevice, snapshot);
            if ((sendMask & WheelSendMask.ButtonLight) != 0)
                SendWheelButtonLight(targetDevice, snapshot);
            if ((sendMask & WheelSendMask.SleepAndPaddle) != 0)
                SendWheelSleepAndPaddle(targetDevice, snapshot);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 发送面盘参数异常: {ex.Message}");
        }
        finally
        {
            _isSendingParameters = false;
        }
    }

    private void SendWheelRpmBaseMode(UsbDeviceInfo device, WheelPresetSnapshot s)
    {
        if (App.UsbManager == null) return;

        try
        {
            var ledColors = new byte[12][];
            for (int i = 0; i < 12; i++)
            {
                var idx = Math.Clamp(s.RpmColors[i], 0, 8);
                ledColors[i] = (byte[])DeviceProtocolService.ColorIndexToRgb[idx].Clone();
            }

            var cmd = DeviceProtocolService.BuildSetWheelRpmBaseModeCommand(
                (byte)s.RpmBaseLightMode, (byte)s.RpmBaseLightSpeed, ledColors);

            App.UsbManager.SendToDevice(device.DeviceKey, cmd);
            Debug.WriteLine($"[SteeringWheelControl] 转速灯基础模式已发送: mode={s.RpmBaseLightMode}, speed={s.RpmBaseLightSpeed}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 发送转速灯基础模式异常: {ex.Message}");
        }
    }

    private void SendWheelRpmIndicator(UsbDeviceInfo device, WheelPresetSnapshot s)
    {
        if (App.UsbManager == null) return;

        try
        {
            var triggerValues = new ushort[12];
            var ledColors = new byte[12][];
            for (int i = 0; i < 12; i++)
            {
                triggerValues[i] = (ushort)s.RpmValues[i];
                var idx = Math.Clamp(s.RpmColors[i], 0, 8);
                ledColors[i] = (byte[])DeviceProtocolService.ColorIndexToRgb[idx].Clone();
            }

            var cmd = DeviceProtocolService.BuildSetWheelRpmIndicatorCommand(
                (byte)s.RpmDisplayMode, triggerValues, ledColors);

            App.UsbManager.SendToDevice(device.DeviceKey, cmd);
            Debug.WriteLine($"[SteeringWheelControl] 转速灯转速指示已发送: mode={s.RpmDisplayMode}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 发送转速灯转速指示异常: {ex.Message}");
        }
    }

    private void SendWheelRpmMode(UsbDeviceInfo device, WheelPresetSnapshot s)
    {
        if (App.UsbManager == null) return;

        try
        {
            // 协议: 0=遥测模式(遥测开启), 1=关闭遥测(基础模式)
            byte telemetryOff = (byte)(s.RpmTelemetryEnabled ? 0 : 1);
            var lightMode = (byte)s.RpmLightMode;
            var strobeMode = (byte)s.RpmStrobeMode;
            var strobeSpeed = (byte)s.RpmSpeed;
            var strobeTriggerValue = (byte)s.RpmCapValue; // 爆闪触发值，与虚线封顶百分比绑定

            var strobeColorIdx = Math.Clamp(s.RpmStrobeColor, 0, 8);
            var strobeColor = DeviceProtocolService.ColorIndexToRgb[strobeColorIdx];

            var cmd = DeviceProtocolService.BuildSetWheelRpmModeCommand(
                (byte)s.RpmBrightness, telemetryOff, lightMode, strobeMode, strobeSpeed,
                strobeColor[0], strobeColor[1], strobeColor[2], strobeTriggerValue);

            App.UsbManager.SendToDevice(device.DeviceKey, cmd);
            Debug.WriteLine($"[SteeringWheelControl] 转速灯模式已发送: brightness={s.RpmBrightness}, telemetryOff={telemetryOff}, strobeTriggerValue={strobeTriggerValue}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 发送转速灯模式异常: {ex.Message}");
        }
    }

    private void SendWheelButtonLight(UsbDeviceInfo device, WheelPresetSnapshot s)
    {
        if (App.UsbManager == null) return;

        try
        {
            var ledMode = (byte)(s.KeyColorEnabled ? 1 : 0); // 1=统一颜色常亮, 0=单独颜色常亮

            if (ledMode == 1)
            {
                // 统一颜色常亮模式：只发一条，索引 0
                var globalColorIdx = Math.Clamp(s.GlobalKeyColor, 0, 8);
                var color = DeviceProtocolService.ColorIndexToRgb[globalColorIdx];
                var cmd = DeviceProtocolService.BuildSetWheelButtonLightCommand(
                    ledMode, 0, (byte)s.KeyBrightness, color[0], color[1], color[2],
                    0, 0xFF, 0, 0, 0);
                App.UsbManager.SendToDevice(device.DeviceKey, cmd);
                Debug.WriteLine($"[SteeringWheelControl] 按键灯(统一)已发送: brightness={s.KeyBrightness}, color={globalColorIdx}");
            }
            else
            {
                // 单独颜色常亮模式：14个可调按键，LED索引=0-13按顺序对应B1,B2,B3,B6,B7,B8,B9,B11,B12,B13,B16,B17,B18,B19
                int singleAdjIdx = _singleButtonAdjIndex;
                _singleButtonAdjIndex = -1; // 立即复位，避免影响后续操作

                int start = singleAdjIdx >= 0 ? singleAdjIdx : 0;
                int end = singleAdjIdx >= 0 ? singleAdjIdx + 1 : 14;

                for (int adjIdx = start; adjIdx < end; adjIdx++)
                {
                    var btnColorIdx = Math.Clamp(s.ButtonColors[adjIdx], 0, 8);
                    var btnColor = DeviceProtocolService.ColorIndexToRgb[btnColorIdx];
                    var telemetryFunc = s.ButtonTelemetryEnabled[adjIdx]
                        ? (byte)(s.ButtonTelemetryFunc[adjIdx] + 1) : (byte)0;
                    var flashSpeed = s.ButtonTelemetryLightEffect[adjIdx] == 0 ? (byte)0xFF : (byte)s.ButtonSpeeds[adjIdx];
                    var tcIdx = Math.Clamp(s.ButtonTelemetryTriggerColor[adjIdx], 0, 8);
                    var tcColor = DeviceProtocolService.ColorIndexToRgb[tcIdx];

                    var cmd = DeviceProtocolService.BuildSetWheelButtonLightCommand(
                        ledMode, (byte)adjIdx, (byte)s.KeyBrightness,
                        btnColor[0], btnColor[1], btnColor[2],
                        telemetryFunc, flashSpeed,
                        tcColor[0], tcColor[1], tcColor[2]);

                    App.UsbManager.SendToDevice(device.DeviceKey, cmd);
                }
                Debug.WriteLine($"[SteeringWheelControl] 按键灯(单独{(singleAdjIdx >= 0 ? $"adj#{singleAdjIdx}" : "×14")})已发送: brightness={s.KeyBrightness}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 发送按键灯参数异常: {ex.Message}");
        }
    }

    private void SendWheelSleepAndPaddle(UsbDeviceInfo device, WheelPresetSnapshot s)
    {
        if (App.UsbManager == null) return;

        try
        {
            // UI索引到协议值的转换
            var sleepTime = s.SleepLightDuration switch
            {
                0 => (byte)1, // 5分钟
                1 => (byte)2, // 10分钟
                2 => (byte)3, // 15分钟
                3 => (byte)4, // 30分钟
                4 => (byte)5, // 60分钟
                5 => (byte)0, // 从不
                _ => (byte)5
            };

            // UI index: 0=呼吸, 1=关灯; 协议: 0=关灯, 1=呼吸 → 取反
            var sleepEffect = s.StandbyLightEffect == 0 ? (byte)1 : (byte)0;
            var sleepEffectSpeed = (byte)s.GlobalFlashSpeed;

            // s.ClutchMode: 0=合成轴, 1=独立轴, 2=按键
            // 协议: 0=独立轴, 1=合成轴, 2=按键
            var clutchPaddleMode = s.ClutchMode switch
            {
                0 => (byte)1, // 合成轴->协议1
                1 => (byte)0, // 独立轴->协议0
                2 => (byte)2, // 按键->协议2
                _ => (byte)0
            };

            var clutchBitePoint = (byte)Math.Round(s.ClutchPointValue);

            var cmd = DeviceProtocolService.BuildSetWheelSleepAndPaddleCommand(
                sleepTime, sleepEffect, sleepEffectSpeed, clutchPaddleMode, clutchBitePoint);

            App.UsbManager.SendToDevice(device.DeviceKey, cmd);
            Debug.WriteLine($"[SteeringWheelControl] 睡眠和拨片参数已发送: sleep={sleepTime}, clutch={clutchPaddleMode}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteeringWheelControl] 发送睡眠和拨片参数异常: {ex.Message}");
        }
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

    // 非可调按键（方向键 + 中央大按键），这些按钮不显示弹窗也不参与参数存储
    private static readonly HashSet<string> NonAdjustableButtonNames = ["Btn4", "Btn5", "Btn10", "Btn14", "Btn15"];

    // 物理按键索引(0-18) → 可调参数索引(0-13)，-1表示不可调
    // 可调的14个按键: B1(0),B2(1),B3(2), B6(5),B7(6),B8(7),B9(8), B11(10),B12(11),B13(12), B16(15),B17(16),B18(17),B19(18)
    private static readonly int[] PhysicalToAdjustable =
    [
        // B1 B2 B3  B4  B5  B6 B7 B8 B9  B10 B11 B12 B13  B14  B15  B16 B17 B18 B19
           0, 1, 2, -1, -1,  3, 4, 5, 6,  -1,  7,  8,  9,  -1,  -1, 10, 11, 12, 13
    ];

    // ── 用户点击按键 → 打开按键灯设置弹窗 ──
    // 使用 PreviewMouseLeftButtonDown 替代 Checked 事件：
    //   Checked 仅在 IsChecked=false→true 时触发，已选中的按键再次点击无响应。
    //   PreviewMouseLeftButtonDown 每次点击都触发，与 IsChecked 状态无关。

    private void KeyButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not RadioButton radioButton) return;

        // 弹窗已打开时，先关闭旧的再打开新的（避免残留）
        if (_isPopupOpen && _buttonSettingsPopup?.Parent != null)
        {
            var rootPanel = (Window.GetWindow(this) as MainWindow)?.Content as Panel;
            if (rootPanel != null)
                CloseButtonSettingsPopup(rootPanel);
        }

        // 方向键 (B4/B5/B14/B15) 和中央键 (B10)：不打开弹窗
        if (NonAdjustableButtonNames.Contains(radioButton.Name))
        {
            return;
        }

        // 提取可调索引和按钮名称
        if (!TryGetButtonIndex(radioButton.Name, out var physicalIdx)) return;
        var adjIdx = PhysicalToAdjustable[physicalIdx];
        if (adjIdx < 0) return;
        _currentButtonIndex = adjIdx;

        var btnName = radioButton.Content?.ToString()
                       ?? radioButton.Tag?.ToString()
                       ?? $"B{physicalIdx + 1}";

        ShowButtonSettingsPopup(adjIdx, btnName);
    }

    private void ShowButtonSettingsPopup(int adjIdx, string keyName)
    {
        if (Window.GetWindow(this) is not MainWindow mainWindow) return;
        if (mainWindow.Content is not Panel rootPanel) return;

        if (_buttonSettingsPopup == null)
        {
            _buttonSettingsPopup = new ButtonSettingsPopup();
            _buttonSettingsPopup.Confirmed += (_, _) =>
            {
                SaveButtonPopupSettings(_currentButtonIndex);
                CloseButtonSettingsPopup(rootPanel);
            };
            _buttonSettingsPopup.Cancelled += (_, _) =>
            {
                CloseButtonSettingsPopup(rootPanel);
            };
        }

        _buttonSettingsPopup.SetKeyName(keyName);
        _buttonSettingsPopup.LoadSettings(
            _buttonColors[adjIdx],
            _buttonTelemetryEnabled[adjIdx],
            _buttonTelemetryLightEffect[adjIdx],
            _buttonTelemetryFunc[adjIdx],
            _buttonTelemetryTriggerColor[adjIdx],
            _buttonSpeeds[adjIdx]);

        if (_buttonSettingsPopup.Parent == null)
        {
            rootPanel.Children.Add(_buttonSettingsPopup);
            _buttonSettingsPopup.Show();
        }

        _isPopupOpen = true;
    }

    private void CloseButtonSettingsPopup(Panel rootPanel)
    {
        if (_buttonSettingsPopup != null && rootPanel.Children.Contains(_buttonSettingsPopup))
            rootPanel.Children.Remove(_buttonSettingsPopup);
        _isPopupOpen = false;
    }

    private void SaveButtonPopupSettings(int adjIndex)
    {
        if (_buttonSettingsPopup == null) return;

        _buttonColors[adjIndex] = _buttonSettingsPopup.GetSelectedKeyColorIndex();
        _buttonTelemetryEnabled[adjIndex] = _buttonSettingsPopup.GetTelemetryEnabled();
        _buttonTelemetryLightEffect[adjIndex] = _buttonSettingsPopup.GetTelemetryLightEffect();
        _buttonTelemetryFunc[adjIndex] = _buttonSettingsPopup.GetTelemetryFunc();
        _buttonTelemetryTriggerColor[adjIndex] = _buttonSettingsPopup.GetTelemetryTriggerColor();
        _buttonSpeeds[adjIndex] = _buttonSettingsPopup.GetSpeed();

        // 单独颜色模式时只下发被修改的按键那一条数据包
        _singleButtonAdjIndex = adjIndex;
        OnParameterModified(WheelSendMask.ButtonLight);
    }

    private static bool TryGetButtonIndex(string name, out int index)
    {
        index = -1;
        if (string.IsNullOrEmpty(name) || name.Length < 4) return false;
        if (name.StartsWith("Btn") && int.TryParse(name[3..], out var num) && num >= 1 && num <= 19)
        {
            index = num - 1;
            return true;
        }
        return false;
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
                SaveRpmSettings();
                RemoveRpmSettingsPopup(rootPanel);
            };
            _rpmSettingsPopup.Cancelled += (_, _) =>
            {
                RemoveRpmSettingsPopup(rootPanel);
            };
        }

        if (_rpmSettingsPopup.Parent == null)
        {
            // 弹窗打开前保存当前数据快照，防止 cap=0 误截断导致数据永久破坏
            Array.Copy(_rpmValues, _rpmValuesBeforePopup = new double[12], 12);
            _rpmCapValueBeforePopup = _rpmCapValue;

            _rpmSettingsPopup.LoadSettings(
                _rpmColors, _rpmValues, _rpmCapValue, _rpmCurveType,
                _rpmDisplayMode, _rpmLightMode, _rpmStrobeMode,
                _rpmStrobeColor, _rpmSpeed, _rpmBaseLightMode, _rpmBaseLightSpeed,
                _rpmTelemetryEnabled);
            rootPanel.Children.Add(_rpmSettingsPopup);
            _rpmSettingsPopup.Show();
        }
    }

    private void RemoveRpmSettingsPopup(Panel rootPanel)
    {
        if (_rpmSettingsPopup != null && rootPanel.Children.Contains(_rpmSettingsPopup))
            rootPanel.Children.Remove(_rpmSettingsPopup);
    }

    private void SaveRpmSettings()
    {
        if (_rpmSettingsPopup == null) return;

        var popupValues = _rpmSettingsPopup.GetRpmValues();
        var popupCap = _rpmSettingsPopup.GetRpmCapValue();

        // 防护：若 12 个滑块值全为 0 而弹窗打开前 _rpmValues 有非零数据，
        // 说明 _capValue 截断错误或数据已损坏，恢复到打开前的值，避免永久破坏
        bool popupAllZero = popupValues.All(v => v == 0);
        bool hadData = _rpmValuesBeforePopup.Any(v => v != 0);
        if (popupAllZero && hadData)
        {
            Debug.WriteLine("[SteeringWheelControl] SaveRpmSettings 检测到滑块全零异常，恢复为弹窗前数据");
            Array.Copy(_rpmValuesBeforePopup, _rpmValues, 12);
        }
        else
        {
            Array.Copy(popupValues, _rpmValues, 12);
        }

        _rpmColors = _rpmSettingsPopup.GetRpmColors();
        _rpmCapValue = popupCap;
        // 同样防护 cap：若为 0 但打开前非零则恢复
        if (_rpmCapValue == 0 && _rpmCapValueBeforePopup > 0)
            _rpmCapValue = _rpmCapValueBeforePopup;

        _rpmCurveType = _rpmSettingsPopup.GetRpmCurveType();
        _rpmDisplayMode = 0; // (暂时禁用) _rpmSettingsPopup.GetRpmDisplayMode();
        _rpmLightMode = _rpmSettingsPopup.GetRpmLightMode();
        _rpmStrobeMode = _rpmSettingsPopup.GetRpmStrobeMode();
        _rpmStrobeColor = _rpmSettingsPopup.GetRpmStrobeColor();
        _rpmSpeed = _rpmSettingsPopup.GetRpmSpeed();
        _rpmBaseLightMode = _rpmSettingsPopup.GetRpmBaseLightMode();
        _rpmBaseLightSpeed = _rpmSettingsPopup.GetRpmBaseLightSpeed();
        _rpmTelemetryEnabled = _rpmSettingsPopup.GetRpmTelemetryEnabled();

        OnParameterModified(WheelSendMask.RpmBaseMode | WheelSendMask.RpmIndicator | WheelSendMask.RpmMode);
    }

    private bool _suppressColorChecked;

    // 色块选中 → 影响按键灯统一颜色模式下的常亮颜色
    private void KeyColor_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressColorChecked) return;
        OnParameterModified(WheelSendMask.ButtonLight);
    }

    // 按键颜色开关
    private void KeyColorToggle_Checked(object sender, RoutedEventArgs e)
    {
        SetKeyColorBlocksEnabled(true);
        _suppressColorChecked = true;
        if (ColorRed != null)
            ColorRed.IsChecked = true;
        _suppressColorChecked = false;
        OnParameterModified(WheelSendMask.ButtonLight);
    }

    private void KeyColorToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        SetKeyColorBlocksEnabled(false);
        _suppressColorChecked = true;
        // 取消所有色块的选中状态
        if (ColorRed != null) ColorRed.IsChecked = false;
        if (ColorOrange != null) ColorOrange.IsChecked = false;
        if (ColorYellow != null) ColorYellow.IsChecked = false;
        if (ColorGreen != null) ColorGreen.IsChecked = false;
        if (ColorCyan != null) ColorCyan.IsChecked = false;
        if (ColorBlue != null) ColorBlue.IsChecked = false;
        if (ColorPurple != null) ColorPurple.IsChecked = false;
        if (ColorWhite != null) ColorWhite.IsChecked = false;
        _suppressColorChecked = false;
        OnParameterModified(WheelSendMask.ButtonLight);
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
        OnParameterModified();
    }

    private void ShowKeyNumberToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        HideKeyButtonLabels();
        OnParameterModified();
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
        // 记录最后操作的亮度滑块，供 DragCompleted 使用
        _lastBrightnessSliderSender = sender as Slider;
        // 不在此处下发数据，等滑动结束后由 Thumb.DragCompleted 触发
    }

    private Slider? _lastBrightnessSliderSender;

    private void BrightnessSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        var mask = _lastBrightnessSliderSender == RpmBrightnessSlider
            ? WheelSendMask.RpmMode   // 转速灯亮度在 0x2105 中
            : WheelSendMask.ButtonLight; // 按键灯亮度在 0x2107 中
        OnParameterModified(mask);
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
        OnParameterModified(WheelSendMask.SleepAndPaddle);
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
        // 先算百分比再取整，再反算像素位置，确保精确 1% 步长
        var rawPercent = Math.Max(0, Math.Min(pos.X / maxX * 100, 100));
        var percent = Math.Round(rawPercent, MidpointRounding.AwayFromZero);

        _clutchPointValue = percent;
        var x = percent / 100 * maxX;

        Canvas.SetLeft(ClutchPointIndicator, x);
        Canvas.SetLeft(thumb, x - 8);

        if (ClutchPointPercent != null && maxX > 0)
        {
            ClutchPointPercent.Text = $"{percent}%";
        }
    }

    private void ClutchPointThumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas thumb) return;
        thumb.ReleaseMouseCapture();
        _isDraggingClutchPoint = false;
        OnParameterModified(WheelSendMask.SleepAndPaddle);
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

        OnParameterModified(WheelSendMask.SleepAndPaddle);
    }

    /// <summary>拨片按键按下（B20/B21）-- 仅 UI 选择，无需下发</summary>
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

    // ════════════════════════════════════════════════════════════════
    //  HID 按键响应 — 由面盘 USB 上报的 HID 按键位图驱动
    // ════════════════════════════════════════════════════════════════

    /// <summary>订阅面盘 HID 数据</summary>
    /// <summary>
    /// 订阅 USB 串口设备连接/断开事件，设备随时插拔时 UI 实时响应。
    /// 始终保持订阅，不随 Unload 取消。
    /// </summary>
    private void SubscribeUsbSerialEvents()
    {
        if (App.UsbManager == null) return;
        App.UsbManager.DeviceConnected += OnUsbDeviceConnected;
        App.UsbManager.DeviceDisconnected += OnUsbDeviceDisconnected;
    }

    private async void OnUsbDeviceConnected(UsbDeviceInfo device)
    {
        var descriptor = DeviceRegistry.FindByVidPid(device.Vid, device.Pid);
        if (descriptor == null || descriptor.DeviceType != DeviceType.Wheel)
            return;

        Debug.WriteLine($"[SteeringWheelControl] 面盘串口设备已连接: {device.DeviceKey}");
        await Application.Current.Dispatcher.InvokeAsync(async () => await RefreshDeviceInfoAsync());
    }

    private void OnUsbDeviceDisconnected(UsbDeviceInfo device)
    {
        if (_connectedWheelDevice == null && _baseDevice == null) return;

        var descriptor = DeviceRegistry.FindByVidPid(device.Vid, device.Pid);
        if (descriptor == null || descriptor.DeviceType != DeviceType.Wheel)
            return;

        Debug.WriteLine($"[SteeringWheelControl] 面盘串口设备已断开: {device.DeviceKey}");
        Application.Current.Dispatcher.Invoke(() =>
        {
            SetDisconnected();
            UpdateConnectionStatusDisplay();
            UpdatePresetDisplay();
            if (NewVersionAvailableBorder != null)
                NewVersionAvailableBorder.Visibility = Visibility.Collapsed;
        });
    }

    private void SubscribeHidData()
    {
        if (App.HidService == null) return;
        App.HidService.WheelDataReceived -= OnWheelHidDataReceived;
        App.HidService.WheelDataReceived += OnWheelHidDataReceived;
    }

    /// <summary>取消订阅面盘 HID 数据</summary>
    private void UnsubscribeHidData()
    {
        if (App.HidService == null) return;
        App.HidService.WheelDataReceived -= OnWheelHidDataReceived;
    }

    /// <summary>面盘 HID 数据 → 圆形按键的 IsChecked（复用模板中已有的 Glow+OuterRing 触发器动画）</summary>
    private void OnWheelHidDataReceived(UsbDeviceInfo device, HidWheelData data)
    {
        // 仅当面盘直连 USB 时处理
        if (_connectedWheelDevice == null || device.Vid != _connectedWheelDevice.Vid || device.Pid != _connectedWheelDevice.Pid)
            return;

        // 构建 64 位按键掩码（面盘 HID 协议：字节 14-21）
        ulong mask = 0;
        int byteCount = Math.Min(data.ButtonBits.Length, 8);
        for (int i = 0; i < byteCount; i++)
            mask |= (ulong)data.ButtonBits[i] << (i * 8);

        // 防抖：位图未变则跳过
        if (mask == _lastHidButtonMask) return;
        _lastHidButtonMask = mask;

        // 弹窗打开时暂不更新 IsChecked，避免视觉上闪烁
        // 弹窗关闭后下一帧 HID 数据到达时会自动恢复
        if (_isPopupOpen) return;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            if (_isPopupOpen) return; // 双重检查

            string? firstPressed = null;

            for (int physicalIdx = 0; physicalIdx < 19; physicalIdx++)
            {
                bool pressed = (mask & (1UL << physicalIdx)) != 0;

                // 直接控制 GlowCircle / OuterRing 的 Visibility，绕过 IsChecked
                // 这样点击按钮不会产生选中效果，仅 HID 上报数据驱动视觉效果
                var glow = _buttonGlows[physicalIdx];
                var ring = _buttonRings[physicalIdx];
                var vis = pressed ? Visibility.Visible : Visibility.Collapsed;
                if (glow != null) glow.Visibility = vis;
                if (ring != null) ring.Visibility = vis;

                if (pressed && firstPressed == null)
                    firstPressed = _buttonNames[physicalIdx];
            }

            if (KeyResponseName != null)
                KeyResponseName.Text = firstPressed ?? "---";
        });
    }

    /// <summary>按钮名称缓存（索引 0=B1 … 18=B19），用于按键响应指示器</summary>
    private static readonly string[] _buttonNames = ["B1","B2","B3","B4","B5","B6","B7","B8","B9","B10","B11","B12","B13","B14","B15","B16","B17","B18","B19"];

    /// <summary>预缓存圆形按键的 GlowCircle/OuterRing 模板部件引用，供 HID 回调直接操作 Visibility</summary>
    private void CacheCircularButtons()
    {
        var allButtons = new RadioButton?[] { Btn1, Btn2, Btn3, Btn4, Btn5, Btn6, Btn7, Btn8, Btn9,
                                              Btn10, Btn11, Btn12, Btn13, Btn14, Btn15, Btn16, Btn17, Btn18, Btn19 };

        for (int i = 0; i < 19; i++)
        {
            var btn = allButtons[i];
            if (btn?.Template == null) continue;

            _buttonGlows[i] = btn.Template.FindName("GlowCircle", btn) as Ellipse;
            _buttonRings[i] = btn.Template.FindName("OuterRing", btn) as Ellipse;
        }
    }
}
