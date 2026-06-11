using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
