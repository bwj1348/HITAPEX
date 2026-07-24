using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using HITAPEX.Models;
using HITAPEX.Models.Usb;
using HITAPEX.Services;
using HITAPEX.Services.Usb;

namespace HITAPEX.Views;

/// <summary>
/// 固件更新设备列表项的数据模型。
/// 每个实例表示设备列表中的一行，包含设备类型、型号、序列号、当前版本、
/// 更新状态、按钮样式、更新描述等信息，同时保存固件更新流程所需的内部字段。
/// </summary>
public class DeviceItem
{
    public string DeviceType { get; set; }
    public string Model { get; set; }
    public string SerialNumber { get; set; }
    public string CurrentVersion { get; set; }
    public string Status { get; set; }
    public Brush ButtonBackground { get; set; }
    public ICommand UpdateCommand { get; set; }
    public string UpdateDescription { get; set; }

    // Internal fields for firmware update flow
    public UsbDeviceInfo? UsbDevice { get; set; }
    public FirmwareVersionInfo? FirmwareInfo { get; set; }
    public byte[]? FirmwareData { get; set; }
    public int DeviceIndex { get; set; }
}

/// <summary>
/// 设置页面的用户控件，包含两个选项卡：
///   1. SystemSettings（系统设置） - 开机自启动、语言切换、软件版本更新
///   2. FirmwareUpdate（固件更新） - 已连接设备列表的固件版本检查与 OTA 刷写
/// </summary>
public partial class SettingsUserControl : UserControl
{
    // ════════════════════════════════════════════════════════════════
    // 字段
    // ════════════════════════════════════════════════════════════════

    private DateTime? _lastCheckUpdateTime;
    private DateTime? _firmwareLastCheckTime;
    private bool _isNewVersionDetected;
    private bool _isUpdating;
    private int _updateProgress;
    private bool _isFirmwareChecking;
    private bool _isFirmwareUpdating;
    private CancellationTokenSource? _updateCts;
    private bool _hasCheckedFirmware;
    private List<FirmwareVersionInfo> _cachedFirmwareList = new();
    private ClientInstallerInfo? _latestInstaller;
    private string? _installerPath;
    public ObservableCollection<DeviceItem> DeviceList { get; set; }

    // ════════════════════════════════════════════════════════════════
    // 构造函数与初始化
    // ════════════════════════════════════════════════════════════════

    public SettingsUserControl()
    {
        InitializeComponent();
        SetupKeyboardNavigation();
        InitializeDeviceList();
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>
    /// 语言切换时清空缓存数据并重置更新状态，确保下次检查时获取正确语言的日志。
    /// </summary>
    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 语言切换时清空所有与语言相关的缓存数据
        _latestInstaller = null;
        _installerPath = null;
        _cachedFirmwareList.Clear();
        _isNewVersionDetected = false;
        _isDownloaded = false;
        _isUpdating = false;
        _updateProgress = 0;

        // 仅当控件已加载时才更新 UI（首次加载前不操作 UI 元素）
        if (IsLoaded)
        {
            NewVersionPanel.Visibility = Visibility.Collapsed;
            UpdateButtonText("Settings.CheckUpdate");
            UpdateProgress(100, false);
            UpdateLastCheckTimeDisplay();
            UpdateFirmwareLastCheckTimeDisplay();

            // 如果之前已检查过固件，重新构建列表以显示正确语言的标签
            if (_hasCheckedFirmware)
            {
                _ = Dispatcher.BeginInvoke(new Func<Task>(CheckFirmwareUpdatesAsync),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 设备列表管理
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 初始化固件更新页面的设备列表 ObservableCollection，并绑定到 ItemsControl。
    /// </summary>
    private void InitializeDeviceList()
    {
        DeviceList = new ObservableCollection<DeviceItem>();
        DeviceListItems.ItemsSource = DeviceList;
    }

    /// <summary>
    /// 弹出固件更新确认对话框，显示设备型号和更新日志，用户可选择立即更新或稍后更新。
    /// </summary>
    private void ShowUpdateDialog(DeviceItem device)
    {
        if (device == null || device.FirmwareInfo == null) return;
        if (device.Status == LocalizationService.Instance["Firmware.AlreadyLatest"]) return;

        var parentWindow = Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
        {
            var dialog = mainWindow.GlobalDialog;
            var deviceName = device.FirmwareInfo?.DeviceName ?? device.Model;
            dialog.Title = string.Format(LocalizationService.Instance["Firmware.UpdatePrompt"], deviceName);
            dialog.ClearButtons();

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 20, 0, 20)
            };

            var descText = new TextBlock
            {
                Text = device.UpdateDescription,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                FontSize = 22,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                LineHeight = 32,
                Margin = new Thickness(0, 0, 0, 0)
            };

            scrollViewer.Content = descText;
            dialog.DialogContent = scrollViewer;

            dialog.AddButton(LocalizationService.Instance["Firmware.UpdateNow"], async (s, e) =>
            {
                dialog.Hide();
                await StartDeviceUpdateAsync(device);
            }, true);

            dialog.AddButton(LocalizationService.Instance["Firmware.UpdateLater"], (s, e) =>
            {
                dialog.Hide();
            }, false);

            dialog.Show();
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 固件更新对话框与刷写流程
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 单设备固件更新的入口，转发到内部方法并以非批量模式执行。
    /// </summary>
    private async Task StartDeviceUpdateAsync(DeviceItem device) =>
        await StartDeviceUpdateInternalAsync(device, fromBatch: false);

    /// <summary>
    /// 固件更新核心流程（单设备）：
    ///   1. 构建进度对话框 UI（标题、进度条、警告文本，全部以代码动态创建）
    ///   2. 下载固件（占整体进度的前 20%）
    ///   3. 通过 FirmwareUpdater 刷写固件（20%-100%）
    ///   4. 完成后等待设备以正常模式重新连接（最多 15 秒），并刷新设备列表
    /// 参数 fromBatch 为 true 时表示批量更新中某一步，不在此方法内设置/清除全局锁。
    /// </summary>
    private async Task StartDeviceUpdateInternalAsync(DeviceItem device, bool fromBatch)
    {
        if (device?.UsbDevice == null || device.FirmwareInfo == null) return;
        if (!fromBatch && _isFirmwareUpdating) return;

        if (!fromBatch)
        {
            _isFirmwareUpdating = true;
            _updateCts = new CancellationTokenSource();
        }

        var usbDevice = device.UsbDevice;
        var firmwareInfo = device.FirmwareInfo;
        var deviceName = device.Model ?? firmwareInfo.DeviceName ?? LocalizationService.Instance["Settings.Device"];

        device.Status = LocalizationService.Instance["Firmware.Updating"];

        var parentWindow = Window.GetWindow(this);
        if (parentWindow is not MainWindow mainWindow) return;

        // Build the progress dialog UI (保持原始样式)
        var progressDialog = mainWindow.GlobalDialog;
        progressDialog.ClearButtons();
        progressDialog.Title = string.Empty;
        progressDialog.ShowIcon = false;

        var mainContainer = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top
        };
        mainContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Title
        var titleCanvas = new Canvas { Height = 50, Margin = new Thickness(0, 0, 0, 64), ClipToBounds = false };
        var shadowGradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 0), MappingMode = BrushMappingMode.Absolute
        };
        shadowGradient.GradientStops.Add(new GradientStop(Color.FromRgb(198, 14, 14), 0));
        shadowGradient.GradientStops.Add(new GradientStop(Color.FromRgb(96, 7, 7), 1));

        var titleShadow = new TextBlock
        {
            Text = string.Format(LocalizationService.Instance["Firmware.UpdatingDevice"], deviceName),
            FontSize = 36, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic,
            FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Orbitron"),
            Margin = new Thickness(4, 4, 0, 0), Foreground = shadowGradient
        };
        titleShadow.SizeChanged += (s, e) =>
        {
            shadowGradient.EndPoint = new Point(e.NewSize.Width, 0);
        };
        Canvas.SetLeft(titleShadow, 0); Canvas.SetTop(titleShadow, 0);
        titleCanvas.Children.Add(titleShadow);

        var titleMain = new TextBlock
        {
            Text = string.Format(LocalizationService.Instance["Firmware.UpdatingDevice"], deviceName),
            FontSize = 36, FontWeight = FontWeights.Black, FontStyle = FontStyles.Italic,
            FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Orbitron"),
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238))
        };
        Canvas.SetLeft(titleMain, 0); Canvas.SetTop(titleMain, 0);
        titleCanvas.Children.Add(titleMain);

        Grid.SetRow(titleCanvas, 0);
        mainContainer.Children.Add(titleCanvas);

        // Progress bar border
        var progressBorder = new Grid { Width = 560, Height = 127 };
        var borderPath = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M559.9 0.0996094V117.959L550.959 126.9H0.0996094V9.04102L9.04102 0.0996094H559.9Z"),
            Stretch = Stretch.Fill,
            Stroke = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            StrokeThickness = 0.5,
            Fill = new SolidColorBrush(Color.FromArgb(3, 0, 0, 0))
        };
        progressBorder.Children.Add(borderPath);

        var progressContent = new Grid { Margin = new Thickness(10, 10, 10, 10) };
        progressContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        progressContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        progressContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Row 0: Percentage with icon
        var percentBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(217, 217, 217)),
            BorderThickness = new Thickness(0, 0, 0, 3),
            Margin = new Thickness(0, 0, 0, 4),
            Width = 125, Height = 36, HorizontalAlignment = HorizontalAlignment.Left
        };
        var percentPanel = new Grid();

        var combinedGradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(7.18, 0), MappingMode = BrushMappingMode.Absolute
        };
        combinedGradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 112, 114), 0));
        combinedGradient.GradientStops.Add(new GradientStop(Color.FromRgb(198, 14, 14), 1));

        var iconPath = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M21.5508 11.5859H14.3673L14.3673 17.379H21.5508L21.5508 11.5859Z M7.18359 11.5859H0.000145912L0.000145435 17.379H7.18359L7.18359 11.5859Z M7.18359 0H0.000145912L0.000145435 5.7931H7.18359L7.18359 0Z M14.3691 5.79297H7.18569L7.18569 11.5861H14.3691L14.3691 5.79297Z"),
            Fill = combinedGradient, Width = 22, Height = 18, Stretch = Stretch.Uniform,
            Margin = new Thickness(14, 0, 10, 0), Opacity = 0.8, HorizontalAlignment = HorizontalAlignment.Left
        };
        percentPanel.Children.Add(iconPath);

        var percentText = new TextBlock
        {
            Text = "0%", FontSize = 26, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Orbitron"),
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 14, 0)
        };
        percentPanel.Children.Add(percentText);

        percentBorder.Child = percentPanel;
        Grid.SetRow(percentBorder, 0);
        progressContent.Children.Add(percentBorder);

        // Row 1: Middle section with progress bar
        var middleSectionGrid = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 0) };
        middleSectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        middleSectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        middleSectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var cornerSize = 10.0;

        // Row 1a: Left top corner
        var leftTopCorner = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M9.65527 0V4.82715H4.82812V9.65527H0V0H9.65527Z"),
            Fill = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            Width = cornerSize, Height = cornerSize, Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom
        };
        Grid.SetRow(leftTopCorner, 0);
        middleSectionGrid.Children.Add(leftTopCorner);

        var updatingText = new TextBlock
        {
            Text = "UPDATING...",
            FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(198, 14, 14)),
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Orbitron"),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(15.8, 0, 0, 0)
        };
        Grid.SetRow(updatingText, 0);
        middleSectionGrid.Children.Add(updatingText);

        var rightTopCorner = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M9.65527 0V9.65527H4.82715V4.82715H0V0H9.65527Z"),
            Fill = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            Width = cornerSize, Height = cornerSize, Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom
        };
        Grid.SetRow(rightTopCorner, 0);
        middleSectionGrid.Children.Add(rightTopCorner);

        // Row 1b: Progress bar
        var progressBarContainer = new Grid { Width = 541, Height = 12, Background = Brushes.Transparent };

        var overlayGrid = new Grid { Height = 7.72, VerticalAlignment = VerticalAlignment.Center };
        var colGreen = new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) };
        var colRed = new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) };
        overlayGrid.ColumnDefinitions.Add(colGreen);
        overlayGrid.ColumnDefinitions.Add(colRed);

        var greenBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        greenBrush.GradientStops.Add(new GradientStop(Color.FromRgb(17, 98, 47), 0));
        greenBrush.GradientStops.Add(new GradientStop(Color.FromRgb(76, 224, 131), 1));
        var greenRect = new Border { Background = greenBrush };
        Grid.SetColumn(greenRect, 0);
        overlayGrid.Children.Add(greenRect);

        var redBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        redBrush.GradientStops.Add(new GradientStop(Color.FromRgb(198, 14, 14), 0));
        redBrush.GradientStops.Add(new GradientStop(Color.FromRgb(96, 7, 7), 1));
        var redRect = new Border { Background = redBrush };
        Grid.SetColumn(redRect, 1);
        overlayGrid.Children.Add(redRect);

        progressBarContainer.Children.Add(overlayGrid);

        string gridData = "M7.4493 9.65529H3.50586L10.0783 1.93115H14.0217Z M0.438559 9.65529H-3.50488L3.06752 1.93115H7.01096Z M14.46 9.65529H10.5166L17.089 1.93115H21.0324Z M21.4669 9.65529H17.5234L24.0958 1.93115H28.0393Z M28.4796 9.65529H24.5361L31.1085 1.93115H35.052Z M35.4913 9.65529H31.5479L38.1203 1.93115H42.0637Z M42.502 9.65529H38.5586L45.131 1.93115H49.0744Z M49.5128 9.65529H45.5693L52.1417 1.93115H56.0852Z M56.5235 9.65529H52.5801L59.1525 1.93115H63.0959Z M63.5323 9.65529H59.5889L66.1613 1.93115H70.1047Z M70.546 9.65529H66.6025L73.1749 1.93115H77.1184Z M77.5548 9.65529H73.6113L80.1837 1.93115H84.1272Z M84.5655 9.65529H80.6221L87.1945 1.93115H91.1379Z M91.5743 9.65529H87.6309L94.2033 1.93115H98.1467Z M98.585 9.65529H94.6416L101.214 1.93115H105.157Z M105.596 9.65529H101.652L108.225 1.93115H112.168Z M112.608 9.65529H108.664L115.236 1.93115H119.18Z M119.62 9.65529H115.677L122.249 1.93115H126.193Z M126.627 9.65529H122.684L129.256 1.93115H133.199Z M133.638 9.65529H129.694L136.267 1.93115H140.21Z M140.65 9.65529H136.707L143.279 1.93115H147.223Z M147.66 9.65529H143.717L150.289 1.93115H154.233Z M154.669 9.65529H150.726L157.298 1.93115H161.241Z M161.682 9.65529H157.738L164.311 1.93115H168.254Z M168.691 9.65529H164.747L171.319 1.93115H175.263Z M175.701 9.65529H171.758L178.33 1.93115H182.274Z M182.714 9.65529H178.771L185.343 1.93115H189.286Z M189.726 9.65529H185.782L192.355 1.93115H196.298Z M196.734 9.65529H192.791L199.363 1.93115H203.307Z M203.743 9.65529H199.8L206.372 1.93115H210.316Z M210.756 9.65529H206.812L213.385 1.93115H217.328Z M217.765 9.65529H213.821L220.394 1.93115H224.337Z M224.774 9.65529H220.831L227.403 1.93115H231.347Z M231.787 9.65529H227.844L234.416 1.93115H238.36Z M238.798 9.65529H234.854L241.427 1.93115H245.37Z M245.807 9.65529H241.863L248.436 1.93115H252.379Z M252.819 9.65529H248.876L255.448 1.93115H259.392Z M259.828 9.65529H255.885L262.457 1.93115H266.401Z M266.84 9.65529H262.896L269.469 1.93115H273.412Z M273.851 9.65529H269.907L276.48 1.93115H280.423Z M280.861 9.65529H276.918L283.49 1.93115H287.434Z M287.872 9.65529H283.929L290.501 1.93115H294.445Z M294.883 9.65529H290.939L297.512 1.93115H301.455Z M301.893 9.65529H297.949L304.522 1.93115H308.465Z M308.903 9.65529H304.96L311.532 1.93115H315.476Z M315.912 9.65529H311.969L318.541 1.93115H322.485Z M322.923 9.65529H318.979L325.552 1.93115H329.495Z M329.934 9.65529H325.99L332.563 1.93115H336.506Z M336.942 9.65529H332.999L339.571 1.93115H343.515Z M343.956 9.65529H340.013L346.585 1.93115H350.529Z M350.965 9.65529H347.021L353.594 1.93115H357.537Z M357.978 9.65529H354.034L360.607 1.93115H364.55Z M364.99 9.65529H361.047L367.619 1.93115H371.563Z M371.997 9.65529H368.054L374.626 1.93115H378.57Z M379.009 9.65529H375.065L381.638 1.93115H385.581Z M386.02 9.65529H382.076L388.649 1.93115H392.592Z M393.028 9.65529H389.085L395.657 1.93115H399.601Z M400.037 9.65529H396.094L402.666 1.93115H406.61Z M407.05 9.65529H403.106L409.679 1.93115H413.622Z M414.061 9.65529H410.117L416.69 1.93115H420.633Z M421.072 9.65529H417.129L423.701 1.93115H427.645Z M428.081 9.65529H424.138L430.71 1.93115H434.654Z M435.096 9.65529H431.152L437.725 1.93115H441.668Z M442.107 9.65529H438.163L444.735 1.93115H448.679Z M449.113 9.65529H445.17L451.742 1.93115H455.686Z M456.124 9.65529H452.181L458.753 1.93115H462.697Z M463.134 9.65529H459.19L465.763 1.93115H469.706Z M470.143 9.65529H466.199L472.772 1.93115H476.715Z M477.155 9.65529H473.212L479.784 1.93115H483.728Z M484.166 9.65529H480.223L486.795 1.93115H490.739Z M491.177 9.65529H487.233L493.806 1.93115H497.749Z M498.191 9.65529H494.247L500.819 1.93115H504.763Z M505.199 9.65529H501.256L507.828 1.93115H511.772Z M512.21 9.65529H508.267L514.839 1.93115H518.782Z M519.221 9.65529H515.277L521.85 1.93115H525.793Z M526.232 9.65529H522.288L528.86 1.93115H532.804Z M533.239 9.65529H529.296L535.868 1.93115H539.812Z M540.252 9.65529H536.309L542.881 1.93115H546.824Z";
        var trackPath = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(gridData),
            Fill = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            Opacity = 0.3, Stretch = Stretch.None
        };
        progressBarContainer.Children.Add(trackPath);

        Grid.SetRow(progressBarContainer, 1);
        middleSectionGrid.Children.Add(progressBarContainer);

        // Row 1c: Left bottom corner + Please Wait + Right bottom corner
        var leftBottomCorner = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M4.82812 0V4.82715H9.65527V9.65527H0V0H4.82812Z"),
            Fill = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            Width = cornerSize, Height = cornerSize, Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(leftBottomCorner, 2);
        middleSectionGrid.Children.Add(leftBottomCorner);

        var pleaseWaitPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(15.8, 0, 0, 0)
        };
        var pleaseWaitText = new TextBlock
        {
            Text = "Please Wait",
            FontSize = 12, FontWeight = FontWeights.Regular,
            FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Orbitron"),
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        pleaseWaitPanel.Children.Add(pleaseWaitText);
        var pleaseWaitLine = new Border
        {
            Width = 135, Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(4.3, 0, 0, 0)
        };
        pleaseWaitPanel.Children.Add(pleaseWaitLine);
        Grid.SetRow(pleaseWaitPanel, 2);
        middleSectionGrid.Children.Add(pleaseWaitPanel);

        var rightBottomCorner = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M9.65527 9.65527H0V4.82715H4.82715V0H9.65527V9.65527Z"),
            Fill = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            Width = cornerSize, Height = cornerSize, Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(rightBottomCorner, 2);
        middleSectionGrid.Children.Add(rightBottomCorner);

        Grid.SetRow(middleSectionGrid, 1);
        progressContent.Children.Add(middleSectionGrid);

        // Row 2: Warning text
        var warningText = new TextBlock
        {
            Text = LocalizationService.Instance["Firmware.WarningMessage"],
            FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(10, 10, 10, 0)
        };
        Grid.SetRow(warningText, 2);
        progressContent.Children.Add(warningText);

        progressBorder.Children.Add(progressContent);
        Grid.SetRow(progressBorder, 1);
        mainContainer.Children.Add(progressBorder);

        progressDialog.DialogContent = mainContainer;
        progressDialog.Show();

        try
        {
            Debug.WriteLine($"[FirmwareUI] 开始更新 {deviceName}...");

            // Download is first 20% of overall progress
            void SetProgress(int pct)
            {
                percentText.Text = $"{pct}%";
                colGreen.Width = new GridLength(pct, GridUnitType.Star);
                colRed.Width = new GridLength(100 - pct, GridUnitType.Star);
            }

            SetProgress(0);

            var downloadProgress = new Progress<int>(p =>
            {
                Dispatcher.Invoke(() => SetProgress(p * 20 / 100));
            });

            byte[]? firmwareData = null;
            if (firmwareInfo.UpdateFile?.Url != null && App.FirmwareApi != null)
            {
                firmwareData = await App.FirmwareApi.DownloadFirmwareAsync(
                    firmwareInfo.UpdateFile.Url, downloadProgress, _updateCts.Token);
            }

            if (firmwareData == null || firmwareData.Length == 0)
            {
                Debug.WriteLine("[FirmwareUI] 固件下载失败");
                progressDialog.Hide();
                device.Status = LocalizationService.Instance["Firmware.DownloadFailed"];
                return;
            }

            Debug.WriteLine($"[FirmwareUI] 固件下载完成: {firmwareData.Length} 字节");
            SetProgress(20);

            // Step 2: Start firmware update via FirmwareUpdateService
            var firmwareUpdater = App.FirmwareUpdater;
            if (firmwareUpdater == null)
            {
                progressDialog.Hide();
                device.Status = LocalizationService.Instance["Firmware.ServiceUnavailable"];
                return;
            }

            // Update progress: 20% to 100% mapped from update progress (80% range)
            void OnUpdateProgress(FirmwareUpdateProgress progress)
            {
                Dispatcher.Invoke(() => SetProgress(20 + progress.ProgressPercent * 80 / 100));
            }

            firmwareUpdater.ProgressChanged += OnUpdateProgress;

            FirmwareUpdateResult result;
            try
            {
                result = await firmwareUpdater.UpdateFirmwareAsync(
                    usbDevice, firmwareInfo, firmwareData, _updateCts.Token);
            }
            finally
            {
                // 确保即使异常也会清理事件 handler
                firmwareUpdater.ProgressChanged -= OnUpdateProgress;
            }

            progressDialog.Hide();

            var disabledBrush = new SolidColorBrush(Color.FromArgb(77, 238, 238, 238));

            if (result.Success)
            {
                device.CurrentVersion = $"v{result.NewVersion}";
                device.Status = LocalizationService.Instance["Firmware.UpdateComplete"];
                device.ButtonBackground = disabledBrush;
                Debug.WriteLine($"[FirmwareUI] {deviceName} 更新成功 -> v{result.NewVersion}");

                // After successful update, wait for the device to reconnect
                // in normal mode, then refresh the entire device list.
                _ = Task.Run(async () =>
                {
                    // Wait for device reconnect (up to 15 seconds)
                    for (int i = 0; i < 15; i++)
                    {
                        await Task.Delay(1000);
                        var connected = App.UsbManager?.ConnectedDevices ?? new List<UsbDeviceInfo>().AsReadOnly();
                        // Check if the device (or a normal-mode device of same type) has appeared
                        if (connected.Any(d =>
                        {
                            var desc = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                            return desc != null && desc.DeviceType == DeviceRegistry.GetDeviceType(usbDevice.Vid, usbDevice.Pid)
                                   && desc.IsNormalMode(d.Vid, d.Pid);
                        }))
                        {
                            Debug.WriteLine($"[FirmwareUI] 设备已以正常模式重新连接，刷新列表");
                            break;
                        }
                    }

                    // Refresh the device list on UI thread
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await CheckFirmwareUpdatesAsync();
                    });
                });
            }
            else
            {
                device.Status = string.Format(LocalizationService.Instance["Firmware.UpdateFailed"], result.ErrorMessage);
                Debug.WriteLine($"[FirmwareUI] {deviceName} 更新失败: {result.ErrorMessage}");
            }
        }
        catch (OperationCanceledException)
        {
            progressDialog.Hide();
            device.Status = LocalizationService.Instance["Firmware.UpdateCancelled"];
            Debug.WriteLine($"[FirmwareUI] {deviceName} 更新已取消");
        }
        catch (Exception ex)
        {
            progressDialog.Hide();
            device.Status = string.Format(LocalizationService.Instance["Firmware.UpdateException"], ex.Message);
            Debug.WriteLine($"[FirmwareUI] {deviceName} 更新异常: {ex.Message}");
        }
        finally
        {
            if (!fromBatch)
            {
                _isFirmwareUpdating = false;
                _updateCts?.Dispose();
                _updateCts = null;
            }
        }
    }

    private void SettingsUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_firstLoaded)
        {
            _firstLoaded = false;
            LoadSettings();
            UpdateLastCheckTimeDisplay();
            InitializeUpdateButton();
            UpdateFirmwareLastCheckTimeDisplay();
        }
        else
        {
            // 切换回来时恢复按钮当前状态
            RestoreUpdateButtonState();
        }
    }

    /// <summary>
    /// 从内存字段恢复按钮 UI 状态（切换页面再回来时调用）。
    /// </summary>
    private void RestoreUpdateButtonState()
    {
        if (_isUpdating)
        {
            // 正在下载中，恢复当前进度
            CheckUpdateButton.IsEnabled = false;
            UpdateProgress(_updateProgress, false);
            UpdateButtonTextRaw($"{_updateProgress}%");
        }
        else if (_isDownloaded)
        {
            // 已下载完成
            CheckUpdateButton.IsEnabled = true;
            UpdateButtonText("Settings.InstallNow");
            UpdateProgress(100, false);
            NewVersionPanel.Visibility = _isNewVersionDetected ? Visibility.Visible : Visibility.Collapsed;
            if (_latestInstaller != null)
                NewVersionText.Text = $"V {_latestInstaller.Version}";
        }
        else if (_isNewVersionDetected)
        {
            // 检测到新版本但尚未下载
            CheckUpdateButton.IsEnabled = true;
            UpdateButtonText("Settings.UpdateNow");
            UpdateProgress(100, false);
            NewVersionPanel.Visibility = Visibility.Visible;
            if (_latestInstaller != null)
                NewVersionText.Text = $"V {_latestInstaller.Version}";
        }
        // 其他情况保持初始状态即可
    }

    private void UpdateFirmwareLastCheckTimeDisplay()
    {
        if (FirmwareLastCheckTimeText != null)
        {
            if (_firmwareLastCheckTime.HasValue)
            {
                FirmwareLastCheckTimeText.Text = _firmwareLastCheckTime.Value.ToString("yyyy-MM-dd HH:mm");
            }
            else
            {
                FirmwareLastCheckTimeText.Text = LocalizationService.Instance["Settings.NotChecked"];
            }
        }
    }

    private void InitializeUpdateButton()
    {
        _isNewVersionDetected = false;
        _isUpdating = false;
        _updateProgress = 0;
    }

    private void SetupKeyboardNavigation()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.Continue);
    }

    // ════════════════════════════════════════════════════════════════
    // 系统设置管理（开机启动、语言、版本号等）
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 从用户配置中加载系统设置：开机自启、最小化行为、当前语言、软件版本号。
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            AutoStartCheckBox.IsChecked = Properties.Settings.Default.AutoStart;
            StartMinimizedCheckBox.IsChecked = Properties.Settings.Default.StartMinimizedToTray;
            CloseMinimizedCheckBox.IsChecked = Properties.Settings.Default.CloseMinimizedToTray;

            var language = GetLanguageSetting();
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == language)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }

            var version = GetAssemblyVersion();
            CurrentVersionText.Text = version;
        }
        catch (Exception)
        {
        }
    }

    private bool GetAutoStartSetting()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            var appName = Assembly.GetExecutingAssembly().GetName().Name;
            return key?.GetValue(appName) != null;
        }
        catch
        {
            return false;
        }
    }

    private void SetAutoStartSetting(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            var appName = Assembly.GetExecutingAssembly().GetName().Name;
            var appPath = Environment.ProcessPath;

            if (enable && appPath != null)
            {
                key?.SetValue(appName, $"\"{appPath}\"");
            }
            else
            {
                key?.DeleteValue(appName, false);
            }
        }
        catch
        {
        }
    }

    private string GetLanguageSetting()
    {
        return Properties.Settings.Default.Language ?? "zh-CN";
    }

    private void SetLanguageSetting(string language)
    {
        Properties.Settings.Default.Language = language;
        Properties.Settings.Default.Save();
    }

    private string GetAssemblyVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version != null ? $"V {version.Major}.{version.Minor}.{version.Build}" : "V 1.0.0";
    }

    private void UpdateLastCheckTimeDisplay()
    {
        if (_lastCheckUpdateTime.HasValue)
        {
            LastCheckTimeText.Text = _lastCheckUpdateTime.Value.ToString("yyyy-MM-dd HH:mm");
        }
        else
        {
            LastCheckTimeText.Text = LocalizationService.Instance["Settings.NotChecked"];
        }
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radio && radio.Tag != null)
        {
            var tabName = radio.Tag.ToString();
            SwitchTab(tabName);
        }
    }

    /// <summary>
    /// 切换设置选项卡（系统设置 / 固件更新），使用淡入动画过渡。
    /// 首次切换到固件更新选项卡时自动触发固件版本检查。
    /// </summary>
    private async void SwitchTab(string? tabName)
    {
        if (SystemSettingsContent == null || FirmwareUpdateContent == null)
            return;

        var fadeIn = Resources["FadeInStoryboard"] as Storyboard;

        SystemSettingsContent.Visibility = Visibility.Collapsed;
        FirmwareUpdateContent.Visibility = Visibility.Collapsed;

        Grid? targetContent = tabName switch
        {
            "SystemSettings" => SystemSettingsContent,
            "FirmwareUpdate" => FirmwareUpdateContent,
            _ => SystemSettingsContent
        };

        if (targetContent != null)
        {
            targetContent.Visibility = Visibility.Visible;
            targetContent.Opacity = 0;
            fadeIn?.Begin(targetContent);
        }

        // 首次切换到固件更新选项卡时自动检查固件版本
        if (tabName == "FirmwareUpdate" && !_hasCheckedFirmware)
        {
            _hasCheckedFirmware = true;
            await Task.Delay(300); // 等待选项卡切换动画完成
            await CheckFirmwareUpdatesAsync();
        }
    }

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoStartCheckBox.IsChecked == true)
        {
            SetAutoStartSetting(true);
            Properties.Settings.Default.AutoStart = true;
        }
        else
        {
            SetAutoStartSetting(false);
            Properties.Settings.Default.AutoStart = false;
        }
        Properties.Settings.Default.Save();
    }

    private void StartMinimizedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        Properties.Settings.Default.StartMinimizedToTray = StartMinimizedCheckBox.IsChecked == true;
        Properties.Settings.Default.Save();
    }

    private void CloseMinimizedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        Properties.Settings.Default.CloseMinimizedToTray = CloseMinimizedCheckBox.IsChecked == true;
        Properties.Settings.Default.Save();
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            var newLanguage = item.Tag.ToString() ?? "zh-CN";
            var currentLanguage = LocalizationService.Instance.CurrentLanguage;

            if (newLanguage != currentLanguage)
            {
                // 即时切换语言，无需重启
                LocalizationService.Instance.SetLanguage(newLanguage);
            }
        }
    }

    private bool _isDownloaded;
    private bool _firstLoaded = true;
    private bool _keepProgressHidden;

    // ════════════════════════════════════════════════════════════════
    // 软件版本更新（检查更新 → 下载 → 安装）
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 软件更新按钮点击处理（两阶段流程）：
    ///   阶段一：检查是否有新版本。调用 API 获取最新安装包信息，与当前版本比较。
    ///     有更新则显示版本号和"立即更新"按钮；已最新则短暂提示后恢复初始状态。
    ///   阶段二：下载并安装。下载安装包并显示百分比进度，下载完成后自动启动安装程序。
    ///     若自动安装失败，按钮变为"点击安装"供用户手动执行。
    /// 已下载状态下再次点击则直接启动已下载的安装程序。
    /// </summary>
    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;

        if (!_isNewVersionDetected)
        {
            // 阶段一：检查更新
            CheckUpdateButton.IsEnabled = false;
            UpdateButtonText("Settings.Checking");

            try
            {
                _updateCts?.Cancel();
                _updateCts = new CancellationTokenSource();
                var ct = _updateCts.Token;

                if (App.ClientInstallerApi != null)
                {
                    _latestInstaller = await App.ClientInstallerApi.GetLatestInstallerAsync(ct);
                }

                _lastCheckUpdateTime = DateTime.Now;
                UpdateLastCheckTimeDisplay();

                if (_latestInstaller != null && !string.IsNullOrEmpty(_latestInstaller.Version))
                {
                    var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                    var latestVersion = _latestInstaller.ParsedVersion;

                    if (latestVersion > (currentVersion ?? new Version(0, 0, 0, 0)))
                    {
                        // 有新版本
                        NewVersionPanel.Visibility = Visibility.Visible;
                        NewVersionText.Text = $"V {_latestInstaller.Version}";
                        _isNewVersionDetected = true;
                        UpdateButtonText("Settings.UpdateNow");

                        // 阶段一结束，确保按钮呈现完整红色（瞬间拉满）
                        UpdateProgress(100, false);
                        CheckUpdateButton.IsEnabled = true;
                    }
                    else
                    {
                        // 已是最新版本，隐藏红色进度条，按钮不可点击，3秒后恢复
                        _keepProgressHidden = true;
                        UpdateButtonText("Settings.AlreadyLatest");
                        UpdateProgress(0, false);
                        await Task.Delay(3000, ct);
                        _keepProgressHidden = false;
                        UpdateButtonText("Settings.CheckUpdate");
                        UpdateProgress(100, false);
                        CheckUpdateButton.IsEnabled = true;
                    }
                }
                else
                {
                    // API 请求失败或者没有数据，恢复初始状态
                    UpdateButtonText("Settings.CheckUpdate");
                    UpdateProgress(0, false);
                    CheckUpdateButton.IsEnabled = true;
                }
            }
            catch (OperationCanceledException)
            {
                UpdateButtonText("Settings.CheckUpdate");
                UpdateProgress(0, false);
                CheckUpdateButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsUI] 检查更新异常: {ex.Message}");
                UpdateButtonText("Settings.CheckUpdate");
                UpdateProgress(0, false);
                CheckUpdateButton.IsEnabled = true;
            }
        }
        else if (_isDownloaded && !string.IsNullOrEmpty(_installerPath))
        {
            // 手动点击安装
            LaunchInstaller(_installerPath);
        }
        else
        {
            // 阶段二：下载并安装
            if (_latestInstaller?.Installer == null) return;

            _isUpdating = true;
            CheckUpdateButton.IsEnabled = false;
            _updateProgress = 0;

            // 阶段二开始：瞬间将进度归零，为动画做准备
            UpdateProgress(0, false);

            _updateCts?.Cancel();
            _updateCts = new CancellationTokenSource();
            var ct = _updateCts.Token;

            try
            {
                var progress = new Progress<int>(percent =>
                {
                    _updateProgress = percent;
                    // 启动带有平滑过渡的进度推进
                    UpdateProgress(percent, true);
                    UpdateButtonTextRaw($"{percent}%");
                });

                if (App.ClientInstallerApi != null)
                {
                    _installerPath = await App.ClientInstallerApi.DownloadInstallerAsync(
                        _latestInstaller.Installer.Url, progress, ct);
                }

                if (string.IsNullOrEmpty(_installerPath) || !File.Exists(_installerPath))
                {
                    // 下载失败，恢复按钮状态以便重试
                    UpdateButtonText("Settings.UpdateNow");
                    UpdateProgress(0, false);
                    _isUpdating = false;
                    CheckUpdateButton.IsEnabled = true;
                    return;
                }

                // 确保最终精度完美贴合
                UpdateProgress(100, true);

                // 尝试自动启动安装程序
                LaunchInstaller(_installerPath);

                // 下载完成，按钮变为"点击安装"，以便用户手动安装
                UpdateButtonText("Settings.InstallNow");
                _isDownloaded = true;
                _isUpdating = false;
                CheckUpdateButton.IsEnabled = true;
            }
            catch (OperationCanceledException)
            {
                UpdateButtonText("Settings.UpdateNow");
                UpdateProgress(0, false);
                _isUpdating = false;
                CheckUpdateButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsUI] 更新异常: {ex.Message}");
                UpdateButtonText("Settings.UpdateNow");
                UpdateProgress(0, false);
                _isUpdating = false;
                CheckUpdateButton.IsEnabled = true;
            }
        }
    }

    /// <summary>
    /// 启动下载的安装程序。
    /// </summary>
    private void LaunchInstaller(string installerPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SettingsUI] 启动安装程序失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 查看更新日志按钮点击 —— 弹出 ModalDialog 显示最新版本的更新日志。
    /// </summary>
    private void ViewUpdateLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestInstaller == null || string.IsNullOrEmpty(_latestInstaller.Log)) return;

        var parentWindow = Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
        {
            var dialog = mainWindow.GlobalDialog;
            dialog.Title = LocalizationService.Instance["Settings.UpdateLogTitle"];
            dialog.ShowCloseButton = true;
            dialog.ClearButtons();

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var logText = new TextBlock
            {
                Text = _latestInstaller.Log,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                FontSize = 22,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                LineHeight = 32,
                Margin = new Thickness(0, 0, 0, 0)
            };

            scrollViewer.Content = logText;
            dialog.DialogContent = scrollViewer;

            dialog.Show();
        }
    }

    // ════════════════════════════════════════════════════════════════
    // UI 辅助方法（按钮文字、进度条、形状重绘等）
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 通过绑定设置按钮文字，语言切换时自动刷新（适用本地化 key）。
    /// </summary>
    private void UpdateButtonText(string locKey)
    {
        if (CheckUpdateButton.Template?.FindName("ButtonText", CheckUpdateButton) is TextBlock buttonText)
        {
            buttonText.SetBinding(TextBlock.TextProperty, new Binding
            {
                Source = LocalizationService.Instance,
                Path = new PropertyPath($"[{locKey}]"),
                Mode = BindingMode.OneWay
            });
        }
        ScheduleShapeRedraw();
    }

    /// <summary>
    /// 直接设置按钮文字（适用非本地化的动态文本，如百分比 "50%"）。
    /// </summary>
    private void UpdateButtonTextRaw(string text)
    {
        if (CheckUpdateButton.Template?.FindName("ButtonText", CheckUpdateButton) is TextBlock buttonText)
        {
            buttonText.Text = text;
        }
        ScheduleShapeRedraw();
    }

    private void ScheduleShapeRedraw()
    {
        var button = CheckUpdateButton;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (button.Template.FindName("UpdateButtonGrid", button) is Grid grid)
                RedrawUpdateButtonShape(grid);
        }), DispatcherPriority.Loaded);
    }

    private void UpdateProgress(int progress, bool smooth = true)
    {
        var grid = CheckUpdateButton.Template.FindName("UpdateButtonGrid", CheckUpdateButton) as Grid;
        var buttonWidth = grid?.ActualWidth ?? 122;
        double width = buttonWidth * progress / 100.0;
        SetProgressClip(width, smooth);
    }

    private void SetProgressClip(double width, bool smooth = true)
    {
        if (CheckUpdateButton.Template.FindName("ProgressBackground", CheckUpdateButton) is System.Windows.Shapes.Path pg)
        {
            // 右侧切角和按钮形状一致：从 (p,21) 斜切到 (p-6,27)
            pg.Clip = Geometry.Parse($"M-100,0 L{width:F3},0 L{width:F3},21 L{width - 6:F3},27 L-100,27 Z");
        }
    }

    // SettingsUserControl.xaml.cs
    private void SocialMediaButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string platform)
        {
            var url = platform switch
            {
                "douyin" => "https://www.douyin.com/user/chengyou",
                "xiaohongshu" => "https://www.xiaohongshu.com/user/chengyou",
                "weibo" => "https://weibo.com/chengyou",
                "wechat" => "weixin://",
                "bilibili" => "https://space.bilibili.com/chengyou",
                _ => null
            };

            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch
                {
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 固件版本检查
    // ════════════════════════════════════════════════════════════════

    private async void FirmwareCheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateFirmwareButtonText("Settings.Checking");
        await CheckFirmwareUpdatesAsync();
    }

    /// <summary>
    /// 设置固件检查更新按钮文字，与软件更新按钮共用 UpdateButtonStyle 模板。
    /// </summary>
    private void UpdateFirmwareButtonText(string locKey)
    {
        if (FirmwareCheckUpdateButton.Template?.FindName("ButtonText", FirmwareCheckUpdateButton) is TextBlock buttonText)
        {
            buttonText.SetBinding(TextBlock.TextProperty, new Binding
            {
                Source = LocalizationService.Instance,
                Path = new PropertyPath($"[{locKey}]"),
                Mode = BindingMode.OneWay
            });
        }
    }

    /// <summary>
    /// 检查所有已连接设备的固件更新：
    ///   1. 调用 API 获取云端固件版本列表并缓存
    ///   2. 枚举 USB 已连接设备，通过 FirmwareUpdater 获取每台设备的当前固件版本
    ///   3. 将设备版本与云端版本逐一比较，更新模式下设备始终标记为可更新
    ///   4. 重建 DeviceList 集合并绑定到列表控件
    ///   5. 若无设备连接，则插入占位提示项
    /// </summary>
    private async Task CheckFirmwareUpdatesAsync()
    {
        if (_isFirmwareChecking || _isFirmwareUpdating) return;

        _isFirmwareChecking = true;
        FirmwareCheckUpdateButton.IsEnabled = false;

        try
        {
            Debug.WriteLine("[FirmwareUI] 开始检查固件更新...");

            // Step 1: Fetch firmware versions from API
            var firmwareList = App.FirmwareApi != null
                ? await App.FirmwareApi.GetFirmwareVersionsAsync()
                : new List<FirmwareVersionInfo>();

            _cachedFirmwareList = firmwareList;
            Debug.WriteLine($"[FirmwareUI] API返回 {firmwareList.Count} 条固件版本记录");

            // Step 2: Clear existing device list and rebuild from connected devices
            DeviceList.Clear();

            var connectedDevices = App.UsbManager?.ConnectedDevices ?? new List<UsbDeviceInfo>().AsReadOnly();
            Debug.WriteLine($"[FirmwareUI] 已连接设备数: {connectedDevices.Count}");

            var disabledBrush = new SolidColorBrush(Color.FromArgb(77, 238, 238, 238));
            var updateGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            updateGradient.GradientStops.Add(new GradientStop(Color.FromRgb(198, 14, 14), 0));
            updateGradient.GradientStops.Add(new GradientStop(Color.FromRgb(96, 7, 7), 1));

            for (int i = 0; i < connectedDevices.Count; i++)
            {
                var usbDevice = connectedDevices[i];
                var deviceIndex = i;

                var deviceType = DeviceRegistry.GetDeviceType(usbDevice.Vid, usbDevice.Pid);
                var descriptor = DeviceRegistry.FindByVidPid(usbDevice.Vid, usbDevice.Pid);
                var isUpdateMode = DeviceRegistry.IsUpdateMode(usbDevice.Vid, usbDevice.Pid);
                var displayName = isUpdateMode
                    ? string.Format(LocalizationService.Instance["Firmware.UpdateModeDevice"], descriptor?.ModelName ?? LocalizationService.Instance["Firmware.UnknownDevice"])
                    : DeviceRegistry.GetDisplayName(usbDevice.Vid, usbDevice.Pid);
                var deviceTypeName = deviceType switch
                {
                    DeviceType.Base => LocalizationService.Instance["Status.DeviceTypeBase"],
                    DeviceType.Pedal => LocalizationService.Instance["Status.DeviceTypePedal"],
                    DeviceType.Wheel => LocalizationService.Instance["Status.DeviceTypeWheel"],
                    _ => LocalizationService.Instance["Firmware.UnknownDevice"]
                };

                // Try to get device info (firmware version) from the device.
                // Skip for update-mode devices — they don't respond to normal commands.
                string currentVersion = isUpdateMode ? "LocalizationService.Instance[\"Firmware.UpdateMode\"]" : LocalizationService.Instance["DeviceParam.Unknown"];
                if (!isUpdateMode && App.FirmwareUpdater != null && App.ProtocolService != null)
                {
                    var deviceInfo = await App.FirmwareUpdater.GetDeviceInfoAsync(usbDevice, deviceType);
                    if (deviceInfo != null)
                    {
                        currentVersion = deviceInfo.VersionString;
                        Debug.WriteLine($"[FirmwareUI] 设备 {usbDevice.DeviceKey} 当前版本: {currentVersion}");
                    }
                    else
                    {
                        Debug.WriteLine($"[FirmwareUI] 无法获取设备 {usbDevice.DeviceKey} 的信息");
                    }
                }

                // For update-mode devices, use normal-mode VID/PID to match firmware
                // because API firmware entries are registered under normal-mode PIDs.
                var lookupVid = isUpdateMode && descriptor != null ? descriptor.NormalMode.Vid : usbDevice.Vid;
                var lookupPid = isUpdateMode && descriptor != null ? descriptor.NormalMode.Pid : usbDevice.Pid;
                var matchedFirmware = App.FirmwareApi?.FindFirmwareForDevice(firmwareList, lookupVid, lookupPid);
                Debug.WriteLine($"[FirmwareUI] 固件查找: VID={lookupVid:X4} PID={lookupPid:X4}, 匹配={(matchedFirmware != null ? matchedFirmware.Version : "无")}");

                string status;
                Brush buttonBg;
                string updateDesc = "";
                // Update-mode devices can always be flashed with available firmware
                bool hasUpdate = isUpdateMode && matchedFirmware != null;

                if (matchedFirmware != null)
                {
                    if (!isUpdateMode)
                    {
                        // Normal mode: compare versions
                        hasUpdate = FirmwareUpdateService.IsNewerVersion(currentVersion, matchedFirmware.Version);
                    }
                    if (hasUpdate)
                    {
                        status = string.Format(LocalizationService.Instance["Firmware.NewVersion"], matchedFirmware.Version);
                        buttonBg = updateGradient;
                        updateDesc = matchedFirmware.UpdateLog;
                    }
                    else
                    {
                        status = LocalizationService.Instance["Firmware.AlreadyLatest"];
                        buttonBg = disabledBrush;
                    }
                }
                else
                {
                    status = isUpdateMode ? LocalizationService.Instance["Firmware.NoFirmware"] : LocalizationService.Instance["Firmware.AlreadyLatest"];
                    buttonBg = disabledBrush;
                }

                Debug.WriteLine($"[FirmwareUI] 设备: {displayName}, 当前版本={currentVersion}, 状态={status}, 可更新={hasUpdate}");

                var deviceItem = new DeviceItem
                {
                    DeviceType = deviceTypeName,
                    Model = descriptor?.ModelName ?? displayName,
                    SerialNumber = usbDevice.SerialNumber,
                    CurrentVersion = currentVersion,
                    Status = status,
                    ButtonBackground = buttonBg,
                    UpdateDescription = updateDesc,
                    UsbDevice = usbDevice,
                    FirmwareInfo = matchedFirmware,
                    DeviceIndex = deviceIndex,
                };

                deviceItem.UpdateCommand = new RelayCommand(param =>
                {
                    if (hasUpdate)
                        ShowUpdateDialog(deviceItem);
                });

                DeviceList.Add(deviceItem);
            }

            // If no devices found, show placeholder
            if (DeviceList.Count == 0)
            {
                DeviceList.Add(new DeviceItem
                {
                    DeviceType = LocalizationService.Instance["Settings.Prompt"],
                    Model = LocalizationService.Instance["Firmware.NoDeviceDetected"],
                    SerialNumber = "-",
                    CurrentVersion = "-",
                    Status = LocalizationService.Instance["Firmware.ConnectDevice"],
                    ButtonBackground = disabledBrush,
                    UpdateCommand = new RelayCommand(_ => { }),
                });
            }

            _firmwareLastCheckTime = DateTime.Now;
            UpdateFirmwareLastCheckTimeDisplay();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FirmwareUI] 检查固件更新异常: {ex.Message}");
        }
        finally
        {
            _isFirmwareChecking = false;
            FirmwareCheckUpdateButton.IsEnabled = true;
            UpdateFirmwareButtonText("Settings.CheckUpdate");
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 批量固件更新
    // ════════════════════════════════════════════════════════════════

    /// <summary>供外部调用，切换到固件更新选项卡</summary>
    public void SwitchToFirmwareUpdateTab(List<UsbDeviceInfo>? updateModeDevices = null)
    {
        if (FirmwareUpdateTab != null)
            FirmwareUpdateTab.IsChecked = true;

        if (updateModeDevices != null && updateModeDevices.Count > 0)
        {
            // 延迟确保 tab 切换和 UI 加载完成后再启动批量更新
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                async () => await StartBatchUpdateAsync(updateModeDevices));
        }
    }

    /// <summary>
    /// 批量固件更新流程（通常从异常弹窗跳转而来）：
    ///   1. 获取/使用已缓存的云端固件列表
    ///   2. 为每台更新模式下的设备匹配固件
    ///   3. 按顺序对每台设备执行 StartDeviceUpdateInternalAsync（fromBatch=true）
    ///   4. 全部完成后弹出批量更新结果提示，并刷新设备列表
    /// </summary>
    private async Task StartBatchUpdateAsync(List<UsbDeviceInfo> updateModeDevices)
    {
        if (_isFirmwareUpdating) return;
        _isFirmwareUpdating = true;
        _updateCts = new CancellationTokenSource();
        var ct = _updateCts.Token;

        try
        {
            // Step 1: 获取云端固件列表
            var firmwareList = _cachedFirmwareList;
            if (firmwareList.Count == 0 && App.FirmwareApi != null)
            {
                firmwareList = await App.FirmwareApi.GetFirmwareVersionsAsync(ct);
                _cachedFirmwareList = firmwareList;
            }

            // Step 2: 为每台设备匹配固件并构建设备列表
            var updateList = new List<DeviceItem>();
            var missingFirmwareNames = new List<string>();
            foreach (var device in updateModeDevices)
            {
                var descriptor = DeviceRegistry.FindByVidPid(device.Vid, device.Pid);
                var deviceName = descriptor?.ModelName ?? LocalizationService.Instance["Settings.Device"];
                var lookupVid = descriptor?.NormalMode.Vid ?? device.Vid;
                var lookupPid = descriptor?.NormalMode.Pid ?? device.Pid;
                var matched = App.FirmwareApi?.FindFirmwareForDevice(firmwareList, lookupVid, lookupPid);
                if (matched != null)
                {
                    // 确保进度标题使用设备型号名称而非设备类别
                    if (!string.IsNullOrEmpty(descriptor?.ModelName))
                        matched.DeviceName = descriptor.ModelName;

                    updateList.Add(new DeviceItem
                    {
                        Model = descriptor?.ModelName ?? deviceName,
                        SerialNumber = device.SerialNumber,
                        CurrentVersion = "LocalizationService.Instance[\"Firmware.UpdateMode\"]",
                        Status = string.Format(LocalizationService.Instance["Firmware.NewVersion"], matched.Version),
                        UsbDevice = device,
                        FirmwareInfo = matched,
                        ButtonBackground = new SolidColorBrush(Color.FromArgb(77, 238, 238, 238))
                    });
                }
                else
                {
                    missingFirmwareNames.Add(deviceName);
                    Debug.WriteLine($"[FirmwareUI] 批量更新: {deviceName} 无可用固件，跳过");
                }
            }

            if (updateList.Count == 0)
            {
                var names = string.Join("、", missingFirmwareNames);
                ShowBatchResultDialog(string.Format(LocalizationService.Instance["Firmware.NoFirmwareAvailable"], names));
                return;
            }

            // Step 3: 依次更新每台设备，记录更新成功与失败的设备名
            var succeededNames = new List<string>();
            var failedNames = new List<string>();
            for (int i = 0; i < updateList.Count; i++)
            {
                if (ct.IsCancellationRequested) break;

                var item = updateList[i];
                var modelName = item.Model ?? LocalizationService.Instance["Firmware.UnknownDevice"];
                if (updateList.Count > 1)
                    item.Model = $"{modelName} ({i + 1}/{updateList.Count})";

                try
                {
                    await StartDeviceUpdateInternalAsync(item, fromBatch: true);
                    succeededNames.Add(modelName);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FirmwareUI] 批量更新: {modelName} 异常: {ex.Message}");
                    failedNames.Add(modelName);
                }
            }

            if (!ct.IsCancellationRequested)
            {
                var allNames = string.Join("、", updateList.Select(d => d.FirmwareInfo?.DeviceName ?? d.Model ?? ""));
                if (failedNames.Count == 0)
                    ShowBatchResultDialog(string.Format(LocalizationService.Instance["Firmware.UpdateSuccess"], allNames));
                else
                    ShowBatchResultDialog(string.Format(LocalizationService.Instance["Firmware.UpdatePartial"], allNames, string.Join("、", failedNames)));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FirmwareUI] 批量更新异常: {ex.Message}");
            ShowBatchResultDialog(string.Format(LocalizationService.Instance["Firmware.UpdateError"], ex.Message));
        }
        finally
        {
            _isFirmwareUpdating = false;
            _updateCts?.Dispose();
            _updateCts = null;
        }
    }

    /// <summary>
    /// 显示批量更新的结果提示弹窗，按钮居中显示在底部。
    /// </summary>
    private void ShowBatchResultDialog(string message)
    {
        var parentWindow = Window.GetWindow(this);
        if (parentWindow is not MainWindow mainWindow) return;

        Dispatcher.Invoke(() =>
        {
            var dialog = mainWindow.GlobalDialog;
            dialog.Title = LocalizationService.Instance["Firmware.FirmwareUpdate"];
            dialog.ClearButtons();

            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 22,
                Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var button = new Button
            {
                Content = LocalizationService.Instance["Common.Confirm"],
                Width = 172,
                Height = 32,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var template = new ControlTemplate(typeof(Button));
            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            var pathFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            pathFactory.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M0 6V32H166L172 26V0H6L0 6Z"));
            pathFactory.SetValue(System.Windows.Shapes.Path.StretchProperty, Stretch.Fill);
            pathFactory.SetValue(System.Windows.Shapes.Path.WidthProperty, 172.0);
            pathFactory.SetValue(System.Windows.Shapes.Path.HeightProperty, 32.0);

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(0, 1), Opacity = 0.8
            };
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(198, 14, 14), 0));
            gradient.GradientStops.Add(new GradientStop(Color.FromRgb(96, 7, 7), 1));
            pathFactory.SetValue(System.Windows.Shapes.Path.FillProperty, gradient);

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

            button.Click += (_, _) =>
            {
                dialog.Hide();
                _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                    async () => await CheckFirmwareUpdatesAsync());
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

            dialog.DialogContent = contentPanel;
            dialog.Show();
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Tab)
        {
            e.Handled = false;
        }
    }

    /// <summary>
    /// UpdateButtonStyle 模板中 Grid 的 SizeChanged 事件处理。
    /// 按钮宽度变化时重绘背景形状，并重置进度裁剪到新宽度。
    /// </summary>
    private void UpdateButtonGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Grid grid)
            RedrawUpdateButtonShape(grid);
    }

    /// <summary>
    /// 根据 Grid 实际宽度重绘背景 Path 的几何体，并重置渐变裁剪到满宽。
    /// 进度动画运行时跳过裁剪重置，避免打断动画。
    /// </summary>
    private void RedrawUpdateButtonShape(Grid grid)
    {
        var w = grid.ActualWidth;
        if (w <= 0) return;

        var geom = $"M0,6 V27 H{w - 6:F4} L{w},21 V0 H6 Z";

        if (grid.FindName("ButtonBackground") is System.Windows.Shapes.Path bg)
        {
            bg.Width = w;
            bg.Data = Geometry.Parse(geom);
        }
        if (grid.FindName("ProgressBackground") is System.Windows.Shapes.Path pg)
        {
            pg.Width = w;
            pg.Data = Geometry.Parse(geom);

            if (!_isUpdating && !_keepProgressHidden)
                pg.Clip = Geometry.Parse($"M-100,0 L{w:F3},0 L{w:F3},21 L{w - 6:F3},27 L-100,27 Z");
        }
    }
}

// ════════════════════════════════════════════════════════════════
// RelayCommand — 本地 ICommand 实现
// ════════════════════════════════════════════════════════════════

// NOTE: This is a local RelayCommand copy used within SettingsUserControl.
// Consider using the shared ViewModels.RelayCommand instead when refactoring.
/// <summary>
/// 本地的 ICommand 实现，用于 SettingsUserControl 内部按钮命令绑定。
/// 建议后续重构时改用共享的 ViewModels.RelayCommand。
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute(parameter);
    }

    public void Execute(object? parameter)
    {
        _execute(parameter);
    }
}
