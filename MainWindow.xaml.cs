using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HITAPEX.Helpers;
using HITAPEX.ViewModels;
using HITAPEX.Controls;
using HITAPEX.Views.DeviceParameters;

namespace HITAPEX;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private PresetListPopup? _presetListPopup;
    private TrayIcon? _trayIcon;

    public ModalDialog GlobalDialogControl => GlobalDialog;

    public void ShowPresetListPopup()
    {
        if (_presetListPopup == null)
        {
            _presetListPopup = new PresetListPopup();
            if (Content is Panel rootPanel)
                rootPanel.Children.Add(_presetListPopup);
        }
        _presetListPopup.Show();
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
        if (sender is RadioButton radioButton && radioButton.DataContext is NavigationItem navItem)
        {
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
