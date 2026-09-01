using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Input;
using HITAPEX.Models.Usb;
using HITAPEX.Views.DeviceParameters;
using SharpVectors.Converters;

namespace HITAPEX.Views;

/// <summary>
/// 设备参数页面容器，管理三个子视图（基座/面盘/踏板）的切换、淡入淡出动画和未保存变更检查。
/// 通过 RadioButton 导航栏在 BaseParameterControl、SteeringWheelParameterControl、
/// PedalParameterControl 之间切换，支持键盘快捷键（1/2/3、上/下箭头）导航。
///
/// 设备连接状态驱动 UI：
/// - 某类型未连接 → 对应导航按钮图标半透明且不可点击；
/// - 全部设备未连接 → 右侧不显示参数页，居中显示"设备未连接"占位内容（含手动刷新按钮）；
/// - 多类设备连接时按顺序（基座 → 面盘 → 踏板）默认展示最上方的参数页，
///   断开当前设备时自动跳转到其他已连接的参数页，全部断开则回到占位内容。
/// </summary>
public partial class DeviceUserControl : UserControl
{
    // ═══ 子控件实例 ═══
    private BaseParameterControl? _baseControl;
    private SteeringWheelParameterControl? _steeringWheelControl;
    private PedalParameterControl? _pedalControl;

    // ═══ 导航状态 ═══
    /// <summary>当前显示的子控件</summary>
    private UserControl? _currentControl;
    /// <summary>当前选中的导航索引（0=基座, 1=面盘, 2=踏板，-1=未选中）</summary>
    private int _currentIndex = 0;
    /// <summary>正在检查未保存变更标志，阻止导航按钮重复触发</summary>
    private bool _isCheckingUnsaved;
    /// <summary>自动切换标志：设备插拔触发的页面跳转跳过未保存确认</summary>
    private bool _autoSwitching;
    /// <summary>设备连接事件是否已订阅（视图缓存复用，只需订阅一次）</summary>
    private bool _eventsSubscribed;

    // ═══ 各类型设备连接状态 ═══
    private bool _baseConnected;
    private bool _wheelConnected;
    private bool _pedalConnected;

    /// <summary>未连接状态下导航图标的透明度</summary>
    private const double DisconnectedIconOpacity = 0.4;

    // ═══ 公开属性：供外部获取子控件引用 ═══
    public BaseParameterControl? BaseControl => _baseControl;
    public PedalParameterControl? PedalControl => _pedalControl;
    public SteeringWheelParameterControl? SteeringWheelControl => _steeringWheelControl;

    /// <summary>
    /// 导航到指定设备子页（供外部调用，如首页 Group 图标点击跳转）。
    /// 目标类型未连接时自动跳转到最上方已连接的设备页；全部未连接则显示占位内容。
    /// </summary>
    /// <param name="index">0=基座, 1=面盘, 2=踏板</param>
    public void NavigateToTab(int index)
    {
        index = Math.Clamp(index, 0, 2);

        // 目标类型未连接 → 跳转到最上方已连接的设备页；全部未连接 → 显示占位
        if (!IsConnectedAt(index))
        {
            int first = GetFirstConnectedIndex();
            if (first < 0)
            {
                RefreshDeviceStates();
                return;
            }
            index = first;
        }
        UpdateNavigationSelection(index);
    }

    public DeviceUserControl()
    {
        InitializeComponent();
        InitializeControls();
        SetupKeyboardShortcuts();
        SubscribeDeviceEvents();
    }

    /// <summary>创建三个设备参数子控件的实例</summary>
    private void InitializeControls()
    {
        _baseControl = new BaseParameterControl();
        _steeringWheelControl = new SteeringWheelParameterControl();
        _pedalControl = new PedalParameterControl();
    }

    /// <summary>注册键盘快捷键（1/2/3 切换子页，上/下箭头导航）</summary>
    private void SetupKeyboardShortcuts()
    {
        KeyDown += DeviceUserControl_KeyDown;
    }

    /// <summary>订阅 USB 串口设备的连接/断开事件，实时刷新界面设备状态</summary>
    private void SubscribeDeviceEvents()
    {
        if (_eventsSubscribed) return;
        _eventsSubscribed = true;

        if (App.UsbManager != null)
        {
            App.UsbManager.DeviceConnected += OnAnyDeviceConnectionChanged;
            App.UsbManager.DeviceDisconnected += OnAnyDeviceConnectionChanged;
        }
    }

    /// <summary>
    /// 任意设备连接/断开事件处理：封送到 UI 线程后刷新设备状态。
    /// 事件可能来自 WMI 事件线程等非 UI 线程，必须切回 UI 线程操作控件。
    /// </summary>
    private void OnAnyDeviceConnectionChanged(UsbDeviceInfo device)
    {
        if (Dispatcher.CheckAccess())
            RefreshDeviceStates();
        else
            Dispatcher.BeginInvoke(RefreshDeviceStates);
    }

    /// <summary>键盘快捷键处理：1/2/3 切换子页，上/下箭头循环导航（仅限已连接的设备类型）</summary>
    private void DeviceUserControl_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.D1:
            case Key.NumPad1:
                if (_baseConnected)
                    BaseNavButton.IsChecked = true;
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                if (_wheelConnected)
                    SteeringWheelNavButton.IsChecked = true;
                e.Handled = true;
                break;
            case Key.D3:
            case Key.NumPad3:
                if (_pedalConnected)
                    PedalNavButton.IsChecked = true;
                e.Handled = true;
                break;
            case Key.Up:
                NavigatePrevious();
                e.Handled = true;
                break;
            case Key.Down:
                NavigateNext();
                e.Handled = true;
                break;
        }
    }

    /// <summary>切换到上一个已连接的子控件（循环）</summary>
    private void NavigatePrevious()
    {
        for (int scan = 3; scan > 0; scan--)
        {
            int index = (_currentIndex - scan + 3) % 3;
            if (IsConnectedAt(index))
            {
                UpdateNavigationSelection(index);
                return;
            }
        }
    }

    /// <summary>切换到下一个已连接的子控件（循环）</summary>
    private void NavigateNext()
    {
        for (int scan = 1; scan <= 3; scan++)
        {
            int index = (_currentIndex + scan) % 3;
            if (IsConnectedAt(index))
            {
                UpdateNavigationSelection(index);
                return;
            }
        }
    }

    /// <summary>根据索引更新导航按钮选中状态</summary>
    private void UpdateNavigationSelection(int index)
    {
        switch (index)
        {
            case 0:
                BaseNavButton.IsChecked = true;
                break;
            case 1:
                SteeringWheelNavButton.IsChecked = true;
                break;
            case 2:
                PedalNavButton.IsChecked = true;
                break;
        }
    }

    /// <summary>页面加载时根据设备连接状态刷新界面（而非总是显示基座）</summary>
    private void DeviceUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshDeviceStates();
    }

    /// <summary>
    /// 根据当前已检测到的设备刷新导航按钮状态与右侧内容：
    /// 1. 各类型无设备连接 → 对应按钮图标半透明且不可点击；
    /// 2. 全部无设备连接 → 显示"设备未连接"占位内容；
    /// 3. 有设备连接 → 隐藏占位内容；当前展示页对应类型仍连接则保持，
    ///    否则自动跳转到最上方已连接的设备参数页。
    /// </summary>
    private void RefreshDeviceStates()
    {
        if (BaseNavButton == null) return;

        // 与各参数页（BaseParameterControl / SteeringWheelParameterControl / PedalParameterControl）
        // 的检测口径保持一致：仅以串口已连接设备为准。
        // 注意不能合并 HID 通道（HidService.ConnectedHidDevices）：HID 与串口的摘除检测存在
        // 时间差（HID 轮询最长 2 秒），设备拔出后会出现"参数页已显示未连接、容器仍认为该类型
        // 已连接"的中间态，导致无法自动跳转到其他参数页、左侧按钮也不重置。
        var devices = App.UsbManager?.ConnectedDevices
                      ?? System.Collections.ObjectModel.ReadOnlyCollection<UsbDeviceInfo>.Empty;

        _baseConnected = HasConnectedDevice(devices, DeviceType.Base);
        _wheelConnected = HasConnectedDevice(devices, DeviceType.Wheel);
        _pedalConnected = HasConnectedDevice(devices, DeviceType.Pedal);

        UpdateNavButtonState(BaseNavButton, BaseNavIcon, _baseConnected);
        UpdateNavButtonState(SteeringWheelNavButton, SteeringWheelNavIcon, _wheelConnected);
        UpdateNavButtonState(PedalNavButton, PedalNavIcon, _pedalConnected);

        int firstConnected = GetFirstConnectedIndex();
        if (firstConnected < 0)
        {
            // 全部断开 → 显示占位内容
            ShowNoDeviceState();
            return;
        }

        NoDevicePanel.Visibility = Visibility.Collapsed;
        ContentHost.Visibility = Visibility.Visible;

        // 当前展示页对应类型仍连接 → 保持现状；否则跳转到最上方已连接设备页
        if (_currentControl == null || !IsConnectedAt(_currentIndex))
        {
            AutoSelectFirstConnected();
        }
    }

    /// <summary>当前是否存在指定索引对应类型的已连接设备</summary>
    private bool IsConnectedAt(int index) => index switch
    {
        0 => _baseConnected,
        1 => _wheelConnected,
        2 => _pedalConnected,
        _ => false
    };

    /// <summary>按顺序（基座 → 面盘 → 踏板）返回第一个已连接类型的索引，全部未连接返回 -1</summary>
    private int GetFirstConnectedIndex()
    {
        if (_baseConnected) return 0;
        if (_wheelConnected) return 1;
        if (_pedalConnected) return 2;
        return -1;
    }

    /// <summary>按索引返回对应的子控件实例</summary>
    private UserControl? GetControlForIndex(int index) => index switch
    {
        0 => _baseControl,
        1 => _steeringWheelControl,
        2 => _pedalControl,
        _ => null
    };

    /// <summary>判断设备列表中是否存在指定类型的已连接设备（仅正常模式）</summary>
    private static bool HasConnectedDevice(IEnumerable<UsbDeviceInfo> devices, DeviceType type)
    {
        return devices.Any(d =>
        {
            var descriptor = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
            return descriptor != null && descriptor.DeviceType == type
                   && descriptor.IsNormalMode(d.Vid, d.Pid);
        });
    }

    /// <summary>更新导航按钮的可用状态与图标透明度</summary>
    private static void UpdateNavButtonState(RadioButton button, SvgViewbox? icon, bool connected)
    {
        if (button == null) return;
        button.IsEnabled = connected;
        if (icon != null)
            icon.Opacity = connected ? 1.0 : DisconnectedIconOpacity;
    }

    /// <summary>全部设备断开：取消按钮选中、隐藏参数页，显示"设备未连接"占位内容</summary>
    private void ShowNoDeviceState()
    {
        BaseNavButton.IsChecked = false;
        SteeringWheelNavButton.IsChecked = false;
        PedalNavButton.IsChecked = false;

        ContentHost.Content = null;
        ContentHost.Visibility = Visibility.Collapsed;
        NoDevicePanel.Visibility = Visibility.Visible;

        _currentControl = null;
        _currentIndex = -1;
    }

    /// <summary>
    /// 自动跳转到最上方已连接的设备参数页。
    /// 绕过未保存确认（设备断开时页面状态已被重置）；若目标按钮已处于选中态
    /// （Checked 事件不触发），则手动直接显示对应子控件。
    /// </summary>
    private void AutoSelectFirstConnected()
    {
        int first = GetFirstConnectedIndex();
        if (first < 0) return;

        _autoSwitching = true;
        UpdateNavigationSelection(first);
        _autoSwitching = false;

        if (_currentControl != GetControlForIndex(first))
        {
            _currentIndex = first;
            ShowControl(GetControlForIndex(first), _currentControl != null);
        }
    }

    /// <summary>
    /// 导航按钮选中变更处理：检查当前子控件是否有未保存变更，
    /// 有则弹出确认对话框，无则直接切换并播放淡入淡出动画。
    /// 设备插拔触发的自动切换（_autoSwitching）跳过确认流程。
    /// </summary>
    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isCheckingUnsaved) return;

        if (sender is RadioButton button)
        {
            var targetControl = ResolveTargetControl(button, out int index);
            if (targetControl == null) return;
            _currentIndex = index;

            // 自动切换（设备插拔触发的页面跳转）→ 跳过未保存确认，直接显示
            if (_autoSwitching)
            {
                if (targetControl != _currentControl)
                    ShowControl(targetControl, true);
                return;
            }

            if (targetControl == _currentControl)
                return;

            ShowUnsavedAwareTransition(targetControl);
        }
    }

    /// <summary>根据导航按钮解析目标子控件与索引</summary>
    private UserControl? ResolveTargetControl(RadioButton button, out int index)
    {
        if (button == BaseNavButton)
        {
            index = 0;
            return _baseControl;
        }
        if (button == SteeringWheelNavButton)
        {
            index = 1;
            return _steeringWheelControl;
        }
        if (button == PedalNavButton)
        {
            index = 2;
            return _pedalControl;
        }
        index = -1;
        return null;
    }

    /// <summary>
    /// 切换子控件并检查未保存变更：当前页有未保存修改时弹出确认对话框，
    /// 用户确认（保存/放弃）后统一切换到目标子页面。
    /// </summary>
    private void ShowUnsavedAwareTransition(UserControl targetControl)
    {
        if (_currentControl == _pedalControl && _pedalControl is { HasUnsavedChanges: true })
        {
            _isCheckingUnsaved = true;
            _pedalControl.ShowUnsavedDialog(
                onSaved: () =>
                {
                    _isCheckingUnsaved = false;
                    ShowControl(targetControl, true);
                },
                onCancelled: () =>
                {
                    // 取消子导航 = 不保存修改，直接切换到目标子页面
                    _isCheckingUnsaved = false;
                    ShowControl(targetControl, true);
                });
        }
        else if (_currentControl == _steeringWheelControl && _steeringWheelControl is { HasUnsavedChanges: true })
        {
            _isCheckingUnsaved = true;
            _steeringWheelControl.ShowUnsavedDialog(
                onSaved: () =>
                {
                    _isCheckingUnsaved = false;
                    ShowControl(targetControl, true);
                },
                onCancelled: () =>
                {
                    _isCheckingUnsaved = false;
                    ShowControl(targetControl, true);
                });
        }
        else if (_currentControl == _baseControl && _baseControl is { HasUnsavedChanges: true })
        {
            _isCheckingUnsaved = true;
            _baseControl.ShowUnsavedDialog(
                onSaved: () =>
                {
                    _isCheckingUnsaved = false;
                    ShowControl(targetControl, true);
                },
                onCancelled: () =>
                {
                    _isCheckingUnsaved = false;
                    ShowControl(targetControl, true);
                });
        }
        else
        {
            ShowControl(targetControl, true);
        }
    }

    /// <summary>
    /// 显示目标子控件，可选播放淡入动画。
    /// 与主窗口顶级页面切换保持一致：直接替换内容，新页面从透明淡入（无旧页淡出）。
    /// </summary>
    private void ShowControl(UserControl? control, bool animate)
    {
        if (control == null) return;

        NoDevicePanel.Visibility = Visibility.Collapsed;
        ContentHost.Visibility = Visibility.Visible;
        ContentHost.Content = control;
        _currentControl = control;

        if (animate)
        {
            var fadeIn = (Storyboard)FindResource("FadeInAnimation");
            fadeIn.Begin(ContentHost);
        }
    }

    /// <summary>
    /// 手动刷新设备：设备已插入但未被检测到时触发一次完整重新扫描。
    /// 串口设备立即重新发现；HID 设备由服务每 2 秒自动轮询，稍后二次刷新状态兜底。
    /// </summary>
    private async void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDevicesButton.IsEnabled = false;
        try
        {
            App.UsbManager?.RediscoverDevices();
            RefreshDeviceStates();

            // 等待驱动/HID 枚举完成后再次刷新，捕获延迟出现的设备
            await Task.Delay(800);
            await Dispatcher.InvokeAsync(RefreshDeviceStates);
        }
        finally
        {
            RefreshDevicesButton.IsEnabled = true;
        }
    }
}