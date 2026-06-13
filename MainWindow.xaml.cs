using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HITAPEX.Helpers;
using HITAPEX.Models.Usb;
using HITAPEX.ViewModels;
using HITAPEX.Controls;
using HITAPEX.Views;
using HITAPEX.Views.DeviceParameters;

namespace HITAPEX;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Dictionary<DeviceType, PresetListPopup> _presetListPopups = new();
    private TrayIcon? _trayIcon;
    private bool _isCheckingUnsavedNavigation;
    private bool _isShowingUpdateModeDialog;

    public ModalDialog GlobalDialogControl => GlobalDialog;

    public PresetListPopup? GetPresetListPopup(DeviceType deviceType)
    {
        _presetListPopups.TryGetValue(deviceType, out var popup);
        return popup;
    }

    public PresetListPopup ShowPresetListPopup(DeviceType deviceType)
    {
        if (!_presetListPopups.TryGetValue(deviceType, out var popup))
        {
            popup = new PresetListPopup { DeviceType = deviceType };
            _presetListPopups[deviceType] = popup;
            if (Content is Panel rootPanel)
                rootPanel.Children.Add(popup);
        }
        popup.Show();
        return popup;
    }

    public MainWindow()
    {
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        InitializeComponent();
        InitializeTrayIcon();

        Loaded += OnMainWindowLoaded;
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnMainWindowLoaded;

        // 1. 处理启动时已连接且处于更新模式的设备
        CheckAndShowUpdateModeDevicesOnStartup();

        // 2. 运行时设备连接事件：每当有新设备连接，检查是否为更新模式
        if (App.UsbManager != null)
        {
            App.UsbManager.DeviceConnected += OnUsbDeviceConnectedForUpdateMode;
        }
    }

    /// <summary>
    /// 启动时检查所有已连接的设备，如果存在更新模式设备，弹出强制更新对话框。
    /// </summary>
    private void CheckAndShowUpdateModeDevicesOnStartup()
    {
        var connectedDevices = App.UsbManager?.ConnectedDevices
                               ?? new List<UsbDeviceInfo>().AsReadOnly();
        var updateModeDevices = connectedDevices
            .Where(d => DeviceRegistry.IsUpdateMode(d.Vid, d.Pid))
            .ToList();

        if (updateModeDevices.Count > 0)
        {
            ShowUpdateModeDialog(updateModeDevices);
        }
    }

    /// <summary>
    /// 运行时设备连接回调：如果设备处于更新模式，弹出强制更新对话框。
    /// 如果当前正在执行固件更新（FirmwareUpdateService 主动切换的设备），不弹窗。
    /// </summary>
    private void OnUsbDeviceConnectedForUpdateMode(UsbDeviceInfo device)
    {
        if (!DeviceRegistry.IsUpdateMode(device.Vid, device.Pid))
            return;

        // 固件更新流程中主动切换的更新模式设备不弹窗
        if (App.FirmwareUpdater?.IsUpdating == true)
            return;

        // 必须在 UI 线程弹窗
        Dispatcher.Invoke(() =>
        {
            ShowUpdateModeDialog(new List<UsbDeviceInfo> { device });
        });
    }

    /// <summary>
    /// 显示更新模式强制弹窗。
    /// 列出所有处于更新模式的设备名称，用户点击"前往更新"后跳转固件更新界面并自动开始更新。
    /// </summary>
    private void ShowUpdateModeDialog(List<UsbDeviceInfo> updateModeDevices)
    {
        if (updateModeDevices.Count == 0)
            return;

        // 防止重复弹窗
        if (_isShowingUpdateModeDialog)
        {
            GlobalDialog.Hide();
        }
        _isShowingUpdateModeDialog = true;

        GlobalDialog.Title = "设 备 异 常";
        GlobalDialog.ClearButtons();

        var deviceNames = updateModeDevices
            .Select(d =>
            {
                var desc = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                return desc?.ModelName ?? $"未知设备 (VID={d.Vid:X4} PID={d.Pid:X4})";
            })
            .Distinct()
            .ToList();

        var namesText = string.Join("、", deviceNames);
        var messageText = $"检测到{namesText}设备固件异常，需进行固件更新才可正常使用。";

        var messageBlock = new TextBlock
        {
            Text = messageText,
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 30)
        };

        var button = BuildPrimaryButton("前 往 更 新");
        button.Click += (_, _) =>
        {
            _isShowingUpdateModeDialog = false;
            GlobalDialog.Hide();
            NavigateToFirmwareUpdate(updateModeDevices);
        };

        var contentPanel = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        contentPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        contentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        messageBlock.VerticalAlignment = VerticalAlignment.Center;
        contentPanel.Children.Add(messageBlock);

        Grid.SetRow(button, 1);
        contentPanel.Children.Add(button);

        GlobalDialog.DialogContent = contentPanel;
        GlobalDialog.Show();
    }

    private Button BuildPrimaryButton(string text)
    {
        var button = new Button
        {
            Content = text,
            Width = 172,
            Height = 32,
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var template = new ControlTemplate(typeof(Button));
        var gridFactory = new FrameworkElementFactory(typeof(Grid));
        var pathFactory = new FrameworkElementFactory(typeof(Path));
        pathFactory.SetValue(Path.DataProperty, Geometry.Parse("M0 6V32H166L172 26V0H6L0 6Z"));
        pathFactory.SetValue(Path.StretchProperty, Stretch.Fill);
        pathFactory.SetValue(Path.WidthProperty, 172.0);
        pathFactory.SetValue(Path.HeightProperty, 32.0);

        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            Opacity = 0.8
        };
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(198, 14, 14), 0));
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(96, 7, 7), 1));
        pathFactory.SetValue(Path.FillProperty, gradient);

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(238, 238, 238)));
        contentFactory.SetValue(TextBlock.FontSizeProperty, 18.0);
        contentFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);

        gridFactory.AppendChild(pathFactory);
        gridFactory.AppendChild(contentFactory);
        template.VisualTree = gridFactory;
        button.Template = template;
        return button;
    }

    /// <summary>
    /// 导航到设置界面的固件更新选项卡，并传入待更新设备列表以自动开始批量更新。
    /// </summary>
    private void NavigateToFirmwareUpdate(List<UsbDeviceInfo> updateModeDevices)
    {
        var settingsItem = _viewModel.NavigationItems.FirstOrDefault(n => n.Name == "Settings");
        if (settingsItem == null) return;

        _viewModel.SelectedNavigationItem = settingsItem;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            var settingsView = _viewModel.CurrentView as SettingsUserControl;
            settingsView?.SwitchToFirmwareUpdateTab(updateModeDevices);
        });
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new TrayIcon(this);
        _trayIcon.SetTooltip("HITAPEX");
        _trayIcon.DoubleClick += RestoreFromTray;
        _trayIcon.ExitRequested += ExitApplication;

        Closing += (s, e) =>
        {
            if (App.IsSessionEnding)
                return;

            if (Properties.Settings.Default.CloseMinimizedToTray)
            {
                e.Cancel = true;
                MinimizeToTray();
            }
        };
    }

    public void MinimizeToTray()
    {
        Hide();
        if (_trayIcon != null)
            _trayIcon.Visible = true;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        Application.Current.Shutdown();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (Properties.Settings.Default.CloseMinimizedToTray)
        {
            MinimizeToTray();
        }
        else
        {
            Application.Current.Shutdown();
        }
    }

    private void NavigationItem_Checked(object sender, RoutedEventArgs e)
    {
        if (_isCheckingUnsavedNavigation) return;

        if (sender is RadioButton radioButton && radioButton.DataContext is NavigationItem navItem)
        {
            // 检查设备参数是否有未保存的更改
            if (_viewModel.CurrentView is DeviceUserControl deviceControl)
            {
                // IsLoaded 保证控件已渲染到可视化树中 — 未加载的控件因初始化事件
                // 产生假修改，实则无用户操作，弹窗也无法正常显示（Window.GetWindow 返回 null）
                if (deviceControl.PedalControl is { IsLoaded: true, HasUnsavedChanges: true })
                {
                    _isCheckingUnsavedNavigation = true;
                    deviceControl.PedalControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        },
                        onCancelled: () =>
                        {
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        });
                    return;
                }

                if (deviceControl.SteeringWheelControl is { IsLoaded: true, HasUnsavedChanges: true })
                {
                    _isCheckingUnsavedNavigation = true;
                    deviceControl.SteeringWheelControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        },
                        onCancelled: () =>
                        {
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        });
                    return;
                }

                if (deviceControl.BaseControl is { IsLoaded: true, HasUnsavedChanges: true })
                {
                    _isCheckingUnsavedNavigation = true;
                    deviceControl.BaseControl.ShowUnsavedDialog(
                        onSaved: () =>
                        {
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        },
                        onCancelled: () =>
                        {
                            _isCheckingUnsavedNavigation = false;
                            _viewModel.SelectedNavigationItem = navItem;
                        });
                    return;
                }
            }

            _viewModel.SelectedNavigationItem = navItem;
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _trayIcon?.Dispose();
    }
}
