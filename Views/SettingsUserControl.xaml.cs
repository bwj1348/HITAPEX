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
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Input;
using System.Windows.Shapes;
using Microsoft.Win32;
using HITAPEX.Models;
using HITAPEX.Models.Usb;
using HITAPEX.Services.Usb;

namespace HITAPEX.Views;

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

public partial class SettingsUserControl : UserControl
{
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
    public ObservableCollection<DeviceItem> DeviceList { get; set; }

    public SettingsUserControl()
    {
        InitializeComponent();
        SetupKeyboardNavigation();
        InitializeDeviceList();
    }

    private void InitializeDeviceList()
    {
        DeviceList = new ObservableCollection<DeviceItem>();
        DeviceListItems.ItemsSource = DeviceList;
    }

    private void ShowUpdateDialog(DeviceItem device)
    {
        if (device == null || device.FirmwareInfo == null) return;
        if (device.Status == "已是最新版本") return;

        var parentWindow = Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
        {
            var dialog = mainWindow.GlobalDialog;
            var deviceName = device.FirmwareInfo?.DeviceName ?? device.Model;
            dialog.Title = $"{deviceName} 更新提示";
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

            dialog.AddButton("立即更新", async (s, e) =>
            {
                dialog.Hide();
                await StartDeviceUpdateAsync(device);
            }, true);

            dialog.AddButton("稍后再说", (s, e) =>
            {
                dialog.Hide();
            }, false);

            dialog.Show();
        }
    }

    private async Task StartDeviceUpdateAsync(DeviceItem device)
    {
        if (device?.UsbDevice == null || device.FirmwareInfo == null) return;
        if (_isFirmwareUpdating) return;

        _isFirmwareUpdating = true;
        _updateCts = new CancellationTokenSource();

        var usbDevice = device.UsbDevice;
        var firmwareInfo = device.FirmwareInfo;
        var deviceName = firmwareInfo.DeviceName ?? device.Model;

        device.Status = "更新中...";

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
            Text = $"{deviceName} 更新中...",
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
            Text = $"{deviceName} 更新中...",
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
            Text = "固件更新中，请勿关闭软件、断开设备或关闭设备电源，否则可能导致软件更新失败！",
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
                device.Status = "下载失败";
                return;
            }

            Debug.WriteLine($"[FirmwareUI] 固件下载完成: {firmwareData.Length} 字节");
            SetProgress(20);

            // Step 2: Start firmware update via FirmwareUpdateService
            if (App.FirmwareUpdater == null)
            {
                progressDialog.Hide();
                device.Status = "服务不可用";
                return;
            }

            // Update progress: 20% to 100% mapped from update progress (80% range)
            void OnUpdateProgress(FirmwareUpdateProgress progress)
            {
                Dispatcher.Invoke(() => SetProgress(20 + progress.ProgressPercent * 80 / 100));
            }

            App.FirmwareUpdater.ProgressChanged += OnUpdateProgress;

            var result = await App.FirmwareUpdater.UpdateFirmwareAsync(
                usbDevice, firmwareInfo, firmwareData, _updateCts.Token);

            // Clean up progress event
            App.FirmwareUpdater.ProgressChanged -= OnUpdateProgress;

            progressDialog.Hide();

            var disabledBrush = new SolidColorBrush(Color.FromArgb(77, 238, 238, 238));

            if (result.Success)
            {
                device.CurrentVersion = $"v{result.NewVersion}";
                device.Status = "更新完成，等待设备重启...";
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
                device.Status = $"更新失败: {result.ErrorMessage}";
                Debug.WriteLine($"[FirmwareUI] {deviceName} 更新失败: {result.ErrorMessage}");
            }
        }
        catch (OperationCanceledException)
        {
            progressDialog.Hide();
            device.Status = "更新已取消";
            Debug.WriteLine($"[FirmwareUI] {deviceName} 更新已取消");
        }
        catch (Exception ex)
        {
            progressDialog.Hide();
            device.Status = $"更新异常: {ex.Message}";
            Debug.WriteLine($"[FirmwareUI] {deviceName} 更新异常: {ex.Message}");
        }
        finally
        {
            _isFirmwareUpdating = false;
            _updateCts?.Dispose();
            _updateCts = null;
        }
    }

    private void SettingsUserControl_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        UpdateLastCheckTimeDisplay();
        InitializeUpdateButton();
        UpdateFirmwareLastCheckTimeDisplay();
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
                FirmwareLastCheckTimeText.Text = "未检查";
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

            var theme = GetThemeSetting();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (ComboBoxItem item in ThemeComboBox.Items)
                {
                    if (item.Tag?.ToString() == theme)
                    {
                        ThemeComboBox.SelectedItem = item;
                        break;
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);

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

    private string GetThemeSetting()
    {
        return Properties.Settings.Default.Theme ?? "Dark";
    }

    private void SetThemeSetting(string theme)
    {
        Properties.Settings.Default.Theme = theme;
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
            LastCheckTimeText.Text = "未检查";
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
            var currentLanguage = GetLanguageSetting();

            if (newLanguage != currentLanguage)
            {
                SetLanguageSetting(newLanguage);
                ShowRestartPrompt("语言设置已更改，重启应用程序后生效。");
            }
        }
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
        {
            var newTheme = item.Tag.ToString() ?? "Dark";
            var currentTheme = GetThemeSetting();

            if (newTheme != currentTheme)
            {
                SetThemeSetting(newTheme);
                ShowRestartPrompt("主题设置已更改，重启应用程序后生效。");
            }
        }
    }

    private void ShowRestartPrompt(string message)
    {
        var parentWindow = Window.GetWindow(this);
        if (parentWindow is MainWindow mainWindow)
        {
            var dialog = mainWindow.GlobalDialog;
            dialog.Title = "提示";
            dialog.ClearButtons();

            dialog.AddButton("稍后重启", (s, e) =>
            {
                dialog.Hide();
            }, false);

            dialog.AddButton("立即重启", (s, e) =>
            {
                dialog.Hide();
                RestartApplication();
            }, true);

            var content = new TextBlock
            {
                Text = message,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 238, 238)),
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            dialog.DialogContent = content;
            dialog.Show();
        }
    }

    private void RestartApplication()
    {
        var appPath = Environment.ProcessPath;
        if (appPath != null)
        {
            Process.Start(appPath);
            Application.Current.Shutdown();
        }
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;

        if (!_isNewVersionDetected)
        {
            CheckUpdateButton.IsEnabled = false;
            UpdateButtonText("检 查 中...");

            try
            {
                await Task.Delay(1500);

                _lastCheckUpdateTime = DateTime.Now;
                UpdateLastCheckTimeDisplay();

                NewVersionPanel.Visibility = Visibility.Visible;
                NewVersionText.Text = "V 1.1.1";
                _isNewVersionDetected = true;
                UpdateButtonText("立 即 更 新");

                // 阶段一结束，确保按钮呈现完整红色（瞬间拉满）
                UpdateProgress(100, false);
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
            }
        }
        else
        {
            _isUpdating = true;
            CheckUpdateButton.IsEnabled = false;
            _updateProgress = 0;

            // 阶段二开始：瞬间将进度归零，为动画做准备
            UpdateProgress(0, false);

            for (int i = 0; i <= 100; i += 2)
            {
                await Task.Delay(50);
                _updateProgress = i;
                // 启动带有平滑过渡的进度推进
                UpdateProgress(i, true);
                UpdateButtonText($"{i}%");
            }
            
            UpdateButtonText("已完成");

            // 确保最终精度完美贴合 122px
            UpdateProgress(100, true);

            await Task.Delay(1000);
            UpdateButtonText("立即更新");
            _isUpdating = false;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void UpdateButtonText(string text)
    {
        if (CheckUpdateButton.Template.FindName("ButtonText", CheckUpdateButton) is TextBlock buttonText)
        {
            CheckUpdateButton.Content = text;
        }
    }

    private void UpdateProgress(int progress, bool smooth = true)
    {
        double width = 122 * progress / 100.0;
        SetProgressClip(width, smooth);
    }

    private void SetProgressClip(double width, bool smooth = true)
    {
        if (CheckUpdateButton.Template.FindName("ProgressClipTransform", CheckUpdateButton) is TranslateTransform transform)
        {
            if (smooth)
            {
                // 启用 WPF 硬件加速动画，让 50ms 一次的循环数值跳跃变得极致平滑
                var animation = new DoubleAnimation
                {
                    To = width,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                transform.BeginAnimation(TranslateTransform.XProperty, animation);
            }
            else
            {
                // 瞬间改变：必须先清除绑定的动画，否则直接赋值会失效
                transform.BeginAnimation(TranslateTransform.XProperty, null);
                transform.X = width;
            }
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

    private async void FirmwareCheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        await CheckFirmwareUpdatesAsync();
    }

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
                    ? $"{descriptor?.ModelName ?? "未知设备"} (更新模式)"
                    : DeviceRegistry.GetDisplayName(usbDevice.Vid, usbDevice.Pid);
                var deviceTypeName = deviceType switch
                {
                    DeviceType.Base => "基座",
                    DeviceType.Pedal => "踏板",
                    DeviceType.Wheel => "面盘",
                    _ => "未知设备"
                };

                // Try to get device info (firmware version) from the device.
                // Skip for update-mode devices — they don't respond to normal commands.
                string currentVersion = isUpdateMode ? "更新模式" : "未知";
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
                        status = $"v{matchedFirmware.Version} 新版本";
                        buttonBg = updateGradient;
                        updateDesc = matchedFirmware.UpdateLog;
                    }
                    else
                    {
                        status = "已是最新版本";
                        buttonBg = disabledBrush;
                    }
                }
                else
                {
                    status = isUpdateMode ? "无可用固件" : "已是最新版本";
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
                    DeviceType = "提示",
                    Model = "未检测到设备",
                    SerialNumber = "-",
                    CurrentVersion = "-",
                    Status = "请连接设备",
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
        }
    }

    /// <summary>供外部调用，切换到固件更新选项卡</summary>
    public void SwitchToFirmwareUpdateTab()
    {
        if (FirmwareUpdateTab != null)
            FirmwareUpdateTab.IsChecked = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Tab)
        {
            e.Handled = false;
        }
    }
}

public class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    private readonly Func<object, bool> _canExecute;

    public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object parameter)
    {
        return _canExecute == null || _canExecute(parameter);
    }

    public void Execute(object parameter)
    {
        _execute(parameter);
    }
}
