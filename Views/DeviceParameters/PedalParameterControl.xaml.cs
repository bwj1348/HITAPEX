using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using HITAPEX.Models.Usb;
using HITAPEX.Services;
using HITAPEX.Services.Usb;
using SharpVectors.Converters;

namespace HITAPEX.Views.DeviceParameters;

public partial class PedalParameterControl : UserControl
{
    // ────────── 离合器状态 ──────────
    private int _selectedCurveType = 1;
    private double _clutchDeadZoneLeft = 0;
    private double _clutchDeadZoneRight = 0;
    private bool _isClutchDraggingDeadZone = false;
    private string? _clutchDraggingDeadZoneThumb = null;
    private PointCollection _clutchCurvePoints = new PointCollection
    {
        new Point(0, 266), new Point(69, 205), new Point(138, 148),
        new Point(207, 91), new Point(276, 42), new Point(345, 0)
    };
    private bool _isClutchDragging = false;
    private Control? _clutchDraggingPoint = null;

    // ────────── 刹车状态 ──────────
    private int _brakeSelectedCurveType = 1;
    private double _brakeDeadZoneLeft = 0;
    private double _brakeDeadZoneRight = 0;
    private bool _isBrakeDraggingDeadZone = false;
    private string? _brakeDraggingDeadZoneThumb = null;
    private PointCollection _brakeCurvePoints = new PointCollection
    {
        new Point(0, 266), new Point(69, 205), new Point(138, 148),
        new Point(207, 91), new Point(276, 42), new Point(345, 0)
    };
    private bool _isBrakeDragging = false;
    private Control? _brakeDraggingPoint = null;

    // ────────── 油门状态 ──────────
    private int _throttleSelectedCurveType = 1;
    private double _throttleDeadZoneLeft = 0;
    private double _throttleDeadZoneRight = 0;
    private bool _isThrottleDraggingDeadZone = false;
    private string? _throttleDraggingDeadZoneThumb = null;
    private PointCollection _throttleCurvePoints = new PointCollection
    {
        new Point(0, 266), new Point(69, 205), new Point(138, 148),
        new Point(207, 91), new Point(276, 42), new Point(345, 0)
    };
    private bool _isThrottleDragging = false;
    private Control? _throttleDraggingPoint = null;

    // ────────── USB 设备通信状态 ──────────
    private UsbDeviceInfo? _connectedPedalDevice;
    private UsbDeviceInfo? _baseDevice;
    private bool _isPedalViaBase;
    private string _deviceTypeName = LocalizationService.Instance["Status.DeviceTypePedal"];
    private string _deviceModel = "";
    private string _connectionStatusText = LocalizationService.Instance["DeviceParam.ConnectedBase"];
    private string _connectionStatusColor = "#179548";
    private string _firmwareVersion = "v 1.0.0";
    private bool _isSendingParameters;
    private bool _isApplyingParameters; // 从设备同步参数时阻止下发
    private string? _latestApiFirmwareVersion;
    private int _pedalCount = 1; // 0=2踏板, 1=3踏板

    // ────────── 预设管理 ──────────
    private PedalPresetSnapshot? _appliedPresetParameters;
    private bool _isPresetModified;
    private bool _isApplyingPreset;
    private bool _isAppliedPresetPersonal;
    private string _currentPresetName = "Default";
    private string _devicePresetName = string.Empty;

    // HID 最新数据缓存（后台线程写入，UI 线程读取），始终反映设备最新状态
    private double _latestRawClutch;
    private double _latestRawBrake;
    private double _latestRawGas;
    private double _latestProcessedClutch;
    private double _latestProcessedBrake;
    private double _latestProcessedGas;

    // 待处理的 UI 更新标记（0=无, 1=已入队），防止 Dispatcher 队列堆积
    private int _pendingUiUpdate;

    // 上次已显示的值，用于跳过冗余 UI 更新
    private double _displayedRawClutch = -1;
    private double _displayedRawBrake = -1;
    private double _displayedRawGas = -1;
    private double _displayedProcessedClutch = -1;
    private double _displayedProcessedBrake = -1;
    private double _displayedProcessedGas = -1;

    // 曲线缓存（值类型数组，无线程亲和性，后台线程可安全访问）
    private Point[] _clutchCurvePointsCache = Array.Empty<Point>();
    private double[] _clutchCurveSlopesCache = Array.Empty<double>();
    private Point[] _brakeCurvePointsCache = Array.Empty<Point>();
    private double[] _brakeCurveSlopesCache = Array.Empty<double>();
    private Point[] _throttleCurvePointsCache = Array.Empty<Point>();
    private double[] _throttleCurveSlopesCache = Array.Empty<double>();

    // 校准弹窗
    private CalibrationDialog? _calibrationDialog;
    private bool _isInitialized;

    public PedalParameterControl()
    {
        InitializeComponent();
        Loaded += PedalParameterControl_Loaded;
    }

    private async void PedalParameterControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;

            LocalizationService.Instance.PropertyChanged += OnLanguageChanged;

            // 订阅 HID 踏板数据（保持常驻，不随 Unload 取消）
            SubscribeHidData();

            // 订阅 USB 串口设备连接/断开事件 — 设备随时插拔时 UI 实时响应
            SubscribeUsbSerialEvents();

            // 离合器初始化
            UpdateCurveTypeSelection();
            SetupClutchDraggablePoints();
            SetupClutchDeadZoneThumbs();
            UpdateClutchDeadZoneDisplay();

            // 刹车初始化
            UpdateBrakeCurveTypeSelection();
            SetupBrakeDraggablePoints();
            SetupBrakeDeadZoneThumbs();
            UpdateBrakeDeadZoneDisplay();

            // 油门初始化
            UpdateThrottleCurveTypeSelection();
            SetupThrottleDraggablePoints();
            SetupThrottleDeadZoneThumbs();
            UpdateThrottleDeadZoneDisplay();

            // 初始化位置显示为 0，清除 XAML 设计时占位值
            UpdatePedalPositionDisplay(0, 0, 0, 0, 0, 0);
        }

        // 每次 Load 都刷新设备连接状态和固件信息
        await RefreshDeviceInfoAsync();

        // 强制刷新 HID 位置显示（重置 _displayed* 使 HasDisplayChanged 返回 true，
        // 将后台累积的最新缓存值立即更新到 UI）
        _displayedRawClutch = -1;
        _displayedRawBrake = -1;
        _displayedRawGas = -1;
        _displayedProcessedClutch = -1;
        _displayedProcessedBrake = -1;
        _displayedProcessedGas = -1;
        ForceRefreshPedalDisplay();

        // 刷新撤销按钮状态
        UpdatePresetDisplay();
    }

    // ════════════════════════════════════════════════════════════════
    //  离合器 — 曲线类型选择
    // ════════════════════════════════════════════════════════════════

    private void UpdateCurveTypeSelection()
    {
        ApplyCurveTypeSelection(_selectedCurveType, "CurveType", Color.FromRgb(255, 200, 0), Color.FromRgb(153, 120, 0));
        UpdateClutchCurve();
    }

    private static string SelectedBorderData = "M45.5 0.5V39.793L39.793 45.5H0.5V6.24023L6.20801 0.5H45.5Z";
    private static string UnselectedBorderData = "M0,0 L46,0 L46,46 L0,46 Z";

    private void ApplyCurveTypeSelection(int selectedType, string prefix, Color highlightColor, Color darkColor)
    {
        for (int i = 1; i <= 5; i++)
        {
            var grid = this.FindName($"{prefix}{i}Grid") as Grid;
            var border = this.FindName($"{prefix}{i}Border") as Path;

            if (grid != null && border != null)
            {
                if (i == selectedType)
                {
                    border.Data = Geometry.Parse(SelectedBorderData);
                    border.Stroke = new SolidColorBrush(highlightColor);
                    border.StrokeThickness = 1;
                    border.Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(102, highlightColor.R, highlightColor.G, highlightColor.B), 0),
                            new GradientStop(Color.FromArgb(102, darkColor.R, darkColor.G, darkColor.B), 1)
                        }
                    };
                }
                else
                {
                    border.Data = Geometry.Parse(UnselectedBorderData);
                    border.Stroke = new SolidColorBrush(Color.FromArgb(102, 238, 238, 238));
                    border.StrokeThickness = 1.0;
                    border.Fill = new SolidColorBrush(Color.FromArgb(51, 238, 238, 238));
                }
            }
        }
    }

    private void UpdateClutchCurve()
    {
        if (ClutchCurveLine == null || ClutchFillArea == null) return;

        _clutchCurvePoints = GetCurvePointsForType(_selectedCurveType);
        ApplySmoothCurve(ClutchCurveLine, ClutchFillArea, _clutchCurvePoints);
        RepositionCurvePoints(_clutchCurvePoints, ClutchPoint1, ClutchPoint2, ClutchPoint3, ClutchPoint4);
        RebuildCurveCaches();
    }

    // ════════════════════════════════════════════════════════════════
    //  刹车 — 曲线类型选择
    // ════════════════════════════════════════════════════════════════

    private void UpdateBrakeCurveTypeSelection()
    {
        ApplyCurveTypeSelection(_brakeSelectedCurveType, "BrakeCurveType", Color.FromRgb(198, 14, 14), Color.FromRgb(96, 7, 7));
        UpdateBrakeCurve();
    }

    private void UpdateBrakeCurve()
    {
        if (BrakeCurveLine == null || BrakeFillArea == null) return;

        _brakeCurvePoints = GetCurvePointsForType(_brakeSelectedCurveType);
        ApplySmoothCurve(BrakeCurveLine, BrakeFillArea, _brakeCurvePoints);
        RepositionCurvePoints(_brakeCurvePoints, BrakePoint1, BrakePoint2, BrakePoint3, BrakePoint4);
        RebuildCurveCaches();
    }

    // ════════════════════════════════════════════════════════════════
    //  油门 — 曲线类型选择
    // ════════════════════════════════════════════════════════════════

    private void UpdateThrottleCurveTypeSelection()
    {
        ApplyCurveTypeSelection(_throttleSelectedCurveType, "ThrottleCurveType", Color.FromRgb(22, 198, 66), Color.FromRgb(10, 96, 32));
        UpdateThrottleCurve();
    }

    private void UpdateThrottleCurve()
    {
        if (ThrottleCurveLine == null || ThrottleFillArea == null) return;

        _throttleCurvePoints = GetCurvePointsForType(_throttleSelectedCurveType);
        ApplySmoothCurve(ThrottleCurveLine, ThrottleFillArea, _throttleCurvePoints);
        RepositionCurvePoints(_throttleCurvePoints, ThrottlePoint1, ThrottlePoint2, ThrottlePoint3, ThrottlePoint4);
        RebuildCurveCaches();
    }

    // ════════════════════════════════════════════════════════════════
    //  共用 — 曲线数据与渲染
    // ════════════════════════════════════════════════════════════════

    private static PointCollection GetCurvePointsForType(int type)
    {
        PointCollection Linear() => new PointCollection
        {
            new Point(0, 266), new Point(69, 212.8), new Point(138, 159.6),
            new Point(207, 106.4), new Point(276, 53.2), new Point(345, 0)
        };

        return type switch
        {
            1 => Linear(),
            2 => new PointCollection
            {
                new Point(0, 266), new Point(69, 232), new Point(138, 188),
                new Point(207, 128), new Point(276, 58), new Point(345, 0)
            },
            3 => new PointCollection
            {
                new Point(0, 266), new Point(69, 192), new Point(138, 128),
                new Point(207, 76), new Point(276, 38), new Point(345, 0)
            },
            4 => new PointCollection
            {
                new Point(0, 266), new Point(69, 232), new Point(138, 195),
                new Point(207, 120), new Point(276, 45), new Point(345, 0)
            },
            5 => Linear(),
            _ => Linear()
        };
    }

    private static void ApplySmoothCurve(Path curveLine, Path fillArea, PointCollection points)
    {
        curveLine.Data = CreateSmoothCurveGeometry(points);
        fillArea.Data = CreateSmoothFillGeometry(points);
    }

    private static void RepositionCurvePoints(PointCollection points, Control p1, Control p2, Control p3, Control p4)
    {
        if (points.Count >= 5)
        {
            Canvas.SetLeft(p1, points[1].X - 7.5);
            Canvas.SetTop(p1, points[1].Y - 7.5);
            Canvas.SetLeft(p2, points[2].X - 7.5);
            Canvas.SetTop(p2, points[2].Y - 7.5);
            Canvas.SetLeft(p3, points[3].X - 7.5);
            Canvas.SetTop(p3, points[3].Y - 7.5);
            Canvas.SetLeft(p4, points[4].X - 7.5);
            Canvas.SetTop(p4, points[4].Y - 7.5);
        }
    }

    private static PathGeometry CreateSmoothCurveGeometry(PointCollection points)
    {
        var geometry = new PathGeometry();
        int n = points.Count;
        if (n < 2) return geometry;

        double[] m = ComputeMonotonicSlopes(points);
        var figure = new PathFigure { StartPoint = points[0] };

        for (int i = 0; i < n - 1; i++)
        {
            Point p0 = points[i];
            Point p1 = points[i + 1];
            double dx = p1.X - p0.X;
            double d = dx / 3.0;

            Point cp1 = new Point(p0.X + d, p0.Y + m[i] * d);
            Point cp2 = new Point(p1.X - d, p1.Y - m[i + 1] * d);
            figure.Segments.Add(new BezierSegment(cp1, cp2, p1, true));
        }

        geometry.Figures.Add(figure);
        return geometry;
    }

    private static PathGeometry CreateSmoothFillGeometry(PointCollection points)
    {
        var geometry = new PathGeometry();
        int n = points.Count;
        if (n < 2) return geometry;

        double[] m = ComputeMonotonicSlopes(points);
        var figure = new PathFigure { StartPoint = new Point(0, 266) };

        for (int i = 0; i < n - 1; i++)
        {
            Point p0 = points[i];
            Point p1 = points[i + 1];
            double dx = p1.X - p0.X;
            double d = dx / 3.0;

            Point cp1 = new Point(p0.X + d, p0.Y + m[i] * d);
            Point cp2 = new Point(p1.X - d, p1.Y - m[i + 1] * d);
            figure.Segments.Add(new BezierSegment(cp1, cp2, p1, true));
        }

        figure.Segments.Add(new LineSegment(new Point(345, 266), true));
        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>使用 Fritsch-Carlson 算法计算单调三次样条的各节点斜率</summary>
    private static double[] ComputeMonotonicSlopes(PointCollection points)
    {
        int n = points.Count;
        double[] m = new double[n];
        double[] delta = new double[n - 1];

        for (int i = 0; i < n - 1; i++)
        {
            double dx = points[i + 1].X - points[i].X;
            delta[i] = dx != 0 ? (points[i + 1].Y - points[i].Y) / dx : 0;
        }

        for (int i = 1; i < n - 1; i++)
        {
            if (delta[i - 1] * delta[i] <= 0)
                m[i] = 0;
            else
            {
                double sum = delta[i - 1] + delta[i];
                m[i] = sum != 0 ? 2.0 * delta[i - 1] * delta[i] / sum : 0;
            }
        }

        m[0] = delta[0];
        m[n - 1] = delta[n - 2];

        for (int i = 0; i < n - 1; i++)
        {
            if (Math.Abs(delta[i]) < 1e-10)
            {
                m[i] = 0;
                m[i + 1] = 0;
            }
            else
            {
                double alpha = m[i] / delta[i];
                double beta = m[i + 1] / delta[i];
                if (alpha < 0) alpha = 0;
                if (beta < 0) beta = 0;
                double sq = alpha * alpha + beta * beta;
                if (sq > 9.0)
                {
                    double tau = 3.0 / Math.Sqrt(sq);
                    m[i] = tau * alpha * delta[i];
                    m[i + 1] = tau * beta * delta[i];
                }
            }
        }

        return m;
    }

    // ════════════════════════════════════════════════════════════════
    //  离合器 — 曲线点拖拽
    // ════════════════════════════════════════════════════════════════

    private void SetupClutchDraggablePoints()
    {
        AttachPointHandlers(ClutchPoint1, ClutchPoint_MouseLeftButtonDown, ClutchPoint_MouseMove, ClutchPoint_MouseLeftButtonUp);
        AttachPointHandlers(ClutchPoint2, ClutchPoint_MouseLeftButtonDown, ClutchPoint_MouseMove, ClutchPoint_MouseLeftButtonUp);
        AttachPointHandlers(ClutchPoint3, ClutchPoint_MouseLeftButtonDown, ClutchPoint_MouseMove, ClutchPoint_MouseLeftButtonUp);
        AttachPointHandlers(ClutchPoint4, ClutchPoint_MouseLeftButtonDown, ClutchPoint_MouseMove, ClutchPoint_MouseLeftButtonUp);
    }

    private static void AttachPointHandlers(Control? point, MouseButtonEventHandler down, MouseEventHandler move, MouseButtonEventHandler up)
    {
        if (point == null) return;
        point.MouseLeftButtonDown += down;
        point.MouseMove += move;
        point.MouseLeftButtonUp += up;
    }

    private void ClutchPoint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selectedCurveType != 5)
        {
            _selectedCurveType = 5;
            ApplyCurveTypeSelection(5, "CurveType", Color.FromRgb(255, 200, 0), Color.FromRgb(153, 120, 0));
        }
        StartPointDrag(sender, ref _isClutchDragging, ref _clutchDraggingPoint);
        e.Handled = true;
    }

    private void ClutchPoint_MouseMove(object sender, MouseEventArgs e)
    {
        HandlePointDrag(sender, e, ClutchCurveLine, ref _isClutchDragging, ref _clutchDraggingPoint,
            _clutchCurvePoints, ClutchPoint1, ClutchPoint2, ClutchPoint3, ClutchPoint4,
            v => { _clutchCurvePoints = v; ApplySmoothCurve(ClutchCurveLine, ClutchFillArea, _clutchCurvePoints); RebuildCurveCaches(); });
    }

    private void ClutchPoint_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndPointDrag(ref _isClutchDragging, ref _clutchDraggingPoint);
        SendPedalParameters();
    }

    // ════════════════════════════════════════════════════════════════
    //  刹车 — 曲线点拖拽
    // ════════════════════════════════════════════════════════════════

    private void SetupBrakeDraggablePoints()
    {
        AttachPointHandlers(BrakePoint1, BrakePoint_MouseLeftButtonDown, BrakePoint_MouseMove, BrakePoint_MouseLeftButtonUp);
        AttachPointHandlers(BrakePoint2, BrakePoint_MouseLeftButtonDown, BrakePoint_MouseMove, BrakePoint_MouseLeftButtonUp);
        AttachPointHandlers(BrakePoint3, BrakePoint_MouseLeftButtonDown, BrakePoint_MouseMove, BrakePoint_MouseLeftButtonUp);
        AttachPointHandlers(BrakePoint4, BrakePoint_MouseLeftButtonDown, BrakePoint_MouseMove, BrakePoint_MouseLeftButtonUp);
    }

    private void BrakePoint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_brakeSelectedCurveType != 5)
        {
            _brakeSelectedCurveType = 5;
            ApplyCurveTypeSelection(5, "BrakeCurveType", Color.FromRgb(198, 14, 14), Color.FromRgb(96, 7, 7));
        }
        StartPointDrag(sender, ref _isBrakeDragging, ref _brakeDraggingPoint);
        e.Handled = true;
    }

    private void BrakePoint_MouseMove(object sender, MouseEventArgs e)
    {
        HandlePointDrag(sender, e, BrakeCurveLine, ref _isBrakeDragging, ref _brakeDraggingPoint,
            _brakeCurvePoints, BrakePoint1, BrakePoint2, BrakePoint3, BrakePoint4,
            v => { _brakeCurvePoints = v; ApplySmoothCurve(BrakeCurveLine, BrakeFillArea, _brakeCurvePoints); RebuildCurveCaches(); });
    }

    private void BrakePoint_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndPointDrag(ref _isBrakeDragging, ref _brakeDraggingPoint);
        SendPedalParameters();
    }

    // ════════════════════════════════════════════════════════════════
    //  油门 — 曲线点拖拽
    // ════════════════════════════════════════════════════════════════

    private void SetupThrottleDraggablePoints()
    {
        AttachPointHandlers(ThrottlePoint1, ThrottlePoint_MouseLeftButtonDown, ThrottlePoint_MouseMove, ThrottlePoint_MouseLeftButtonUp);
        AttachPointHandlers(ThrottlePoint2, ThrottlePoint_MouseLeftButtonDown, ThrottlePoint_MouseMove, ThrottlePoint_MouseLeftButtonUp);
        AttachPointHandlers(ThrottlePoint3, ThrottlePoint_MouseLeftButtonDown, ThrottlePoint_MouseMove, ThrottlePoint_MouseLeftButtonUp);
        AttachPointHandlers(ThrottlePoint4, ThrottlePoint_MouseLeftButtonDown, ThrottlePoint_MouseMove, ThrottlePoint_MouseLeftButtonUp);
    }

    private void ThrottlePoint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_throttleSelectedCurveType != 5)
        {
            _throttleSelectedCurveType = 5;
            ApplyCurveTypeSelection(5, "ThrottleCurveType", Color.FromRgb(22, 198, 66), Color.FromRgb(10, 96, 32));
        }
        StartPointDrag(sender, ref _isThrottleDragging, ref _throttleDraggingPoint);
        e.Handled = true;
    }

    private void ThrottlePoint_MouseMove(object sender, MouseEventArgs e)
    {
        HandlePointDrag(sender, e, ThrottleCurveLine, ref _isThrottleDragging, ref _throttleDraggingPoint,
            _throttleCurvePoints, ThrottlePoint1, ThrottlePoint2, ThrottlePoint3, ThrottlePoint4,
            v => { _throttleCurvePoints = v; ApplySmoothCurve(ThrottleCurveLine, ThrottleFillArea, _throttleCurvePoints); RebuildCurveCaches(); });
    }

    private void ThrottlePoint_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndPointDrag(ref _isThrottleDragging, ref _throttleDraggingPoint);
        SendPedalParameters();
    }

    // ════════════════════════════════════════════════════════════════
    //  共用 — 曲线点拖拽逻辑
    // ════════════════════════════════════════════════════════════════

    private static void StartPointDrag(object sender, ref bool isDragging, ref Control? draggingPoint)
    {
        if (sender is Control point)
        {
            isDragging = true;
            draggingPoint = point;
            point.CaptureMouse();
        }
    }

    private static void HandlePointDrag(object sender, MouseEventArgs e, Path? curveLine,
        ref bool isDragging, ref Control? draggingPoint,
        PointCollection curvePoints, Control p1, Control p2, Control p3, Control p4,
        Action<PointCollection> applyCurve)
    {
        if (!isDragging || draggingPoint == null || curveLine == null) return;

        var position = e.GetPosition(curveLine.Parent as Canvas);
        var y = Math.Max(0, Math.Min(266, position.Y));

        if (draggingPoint == p1)
        {
            curvePoints[1] = new Point(69, y);
            Canvas.SetTop(p1, y - 7.5);
        }
        else if (draggingPoint == p2)
        {
            curvePoints[2] = new Point(138, y);
            Canvas.SetTop(p2, y - 7.5);
        }
        else if (draggingPoint == p3)
        {
            curvePoints[3] = new Point(207, y);
            Canvas.SetTop(p3, y - 7.5);
        }
        else if (draggingPoint == p4)
        {
            curvePoints[4] = new Point(276, y);
            Canvas.SetTop(p4, y - 7.5);
        }

        applyCurve(curvePoints);
    }

    private static void EndPointDrag(ref bool isDragging, ref Control? draggingPoint)
    {
        draggingPoint?.ReleaseMouseCapture();
        isDragging = false;
        draggingPoint = null;
    }

    // ════════════════════════════════════════════════════════════════
    //  离合器 — 曲线类型点击
    // ════════════════════════════════════════════════════════════════

    private void CurveType1_Click(object sender, MouseButtonEventArgs e) { _selectedCurveType = 1; UpdateCurveTypeSelection(); SendPedalParameters(); }
    private void CurveType2_Click(object sender, MouseButtonEventArgs e) { _selectedCurveType = 2; UpdateCurveTypeSelection(); SendPedalParameters(); }
    private void CurveType3_Click(object sender, MouseButtonEventArgs e) { _selectedCurveType = 3; UpdateCurveTypeSelection(); SendPedalParameters(); }
    private void CurveType4_Click(object sender, MouseButtonEventArgs e) { _selectedCurveType = 4; UpdateCurveTypeSelection(); SendPedalParameters(); }
    private void CurveType5_Click(object sender, MouseButtonEventArgs e) { _selectedCurveType = 5; UpdateCurveTypeSelection(); SendPedalParameters(); }

    // ════════════════════════════════════════════════════════════════
    //  刹车 — 曲线类型点击
    // ════════════════════════════════════════════════════════════════

    private void BrakeCurveType1_Click(object sender, MouseButtonEventArgs e) { _brakeSelectedCurveType = 1; UpdateBrakeCurveTypeSelection(); SendPedalParameters(); }
    private void BrakeCurveType2_Click(object sender, MouseButtonEventArgs e) { _brakeSelectedCurveType = 2; UpdateBrakeCurveTypeSelection(); SendPedalParameters(); }
    private void BrakeCurveType3_Click(object sender, MouseButtonEventArgs e) { _brakeSelectedCurveType = 3; UpdateBrakeCurveTypeSelection(); SendPedalParameters(); }
    private void BrakeCurveType4_Click(object sender, MouseButtonEventArgs e) { _brakeSelectedCurveType = 4; UpdateBrakeCurveTypeSelection(); SendPedalParameters(); }
    private void BrakeCurveType5_Click(object sender, MouseButtonEventArgs e) { _brakeSelectedCurveType = 5; UpdateBrakeCurveTypeSelection(); SendPedalParameters(); }

    // ════════════════════════════════════════════════════════════════
    //  油门 — 曲线类型点击
    // ════════════════════════════════════════════════════════════════

    private void ThrottleCurveType1_Click(object sender, MouseButtonEventArgs e) { _throttleSelectedCurveType = 1; UpdateThrottleCurveTypeSelection(); SendPedalParameters(); }
    private void ThrottleCurveType2_Click(object sender, MouseButtonEventArgs e) { _throttleSelectedCurveType = 2; UpdateThrottleCurveTypeSelection(); SendPedalParameters(); }
    private void ThrottleCurveType3_Click(object sender, MouseButtonEventArgs e) { _throttleSelectedCurveType = 3; UpdateThrottleCurveTypeSelection(); SendPedalParameters(); }
    private void ThrottleCurveType4_Click(object sender, MouseButtonEventArgs e) { _throttleSelectedCurveType = 4; UpdateThrottleCurveTypeSelection(); SendPedalParameters(); }
    private void ThrottleCurveType5_Click(object sender, MouseButtonEventArgs e) { _throttleSelectedCurveType = 5; UpdateThrottleCurveTypeSelection(); SendPedalParameters(); }

    // ════════════════════════════════════════════════════════════════
    //  预设管理
    // ════════════════════════════════════════════════════════════════

    public bool HasUnsavedChanges => _isPresetModified;

    /// <summary>放弃当前修改，恢复到已应用预设的状态</summary>
    public void DiscardChanges()
    {
        if (!_isPresetModified || _appliedPresetParameters == null)
            return;

        _isApplyingPreset = true;
        ApplyPresetSnapshot(_appliedPresetParameters);
        SendPedalParameters();
        _isApplyingPreset = false;
        _isPresetModified = false;
        UpdatePresetDisplay();
    }

    /// <summary>弹出未保存确认弹窗，保存后执行 onSaved，取消后执行 onCancelled</summary>
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
                TrySaveWithRetry(() => PerformSave(), () => onSaved?.Invoke());
            }, isPrimary: true);
        }
        else
        {
            dialog.AddButton(LocalizationService.Instance["Common.SaveAs"], (_, _) =>
            {
                dialog.Hide();
                SaveAsInternal(onSaved);
            }, isPrimary: true);
        }

        dialog.AddButton(LocalizationService.Instance["Common.Cancel"], (_, _) =>
        {
            dialog.Hide();
            onCancelled?.Invoke();
        }, isPrimary: false);

        dialog.Show();
    }

    private bool PerformSave()
    {
        var popup = GetPresetListPopup();
        if (App.PresetService == null) return false;

        try
        {
            var personalPresets = App.PresetService.LoadPersonalPresets(Models.Usb.DeviceType.Pedal);
            var target = personalPresets.FirstOrDefault(p => p.Name == _currentPresetName);
            if (target == null) return false;

            target.Parameters = CaptureCurrentParameters();
            App.PresetService.SavePersonalPresets(personalPresets, Models.Usb.DeviceType.Pedal);
            popup?.RefreshPersonalPresets(personalPresets);

            _appliedPresetParameters = CaptureCurrentParameters();
            _isPresetModified = false;
            UpdatePresetDisplay();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PedalControl] 保存预设失败: {ex.Message}");
            return false;
        }
    }

    private void ShowSaveFailedDialog(Action? onRetry)
    {
        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = LocalizationService.Instance["Dialog.SaveFailed"];
        dialog.ShowIcon = true;
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = LocalizationService.Instance["Dialog.SaveFailedMessage"],
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton(LocalizationService.Instance["Common.Retry"], (_, _) =>
        {
            dialog.Hide();
            onRetry?.Invoke();
        }, isPrimary: true);

        dialog.AddButton(LocalizationService.Instance["Common.Cancel"], (_, _) =>
        {
            dialog.Hide();
        }, isPrimary: false);

        dialog.Show();
    }

    private void UndoButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isPresetModified || _appliedPresetParameters == null)
            return;

        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = LocalizationService.Instance["Dialog.RevertChanges"];
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = LocalizationService.Instance["Dialog.RevertChangesMessage"],
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton(LocalizationService.Instance["Common.Undo"], (_, _) =>
        {
            dialog.Hide();
            DiscardChanges();
        }, isPrimary: true);

        dialog.AddButton(LocalizationService.Instance["Common.Cancel"], (_, _) =>
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
            ShowSuccessToast(LocalizationService.Instance["Preset.SaveSuccess"]);
        });
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

        // 背景形状
        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M360 0H9L0 9V100H351L360 91V0Z"),
            Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
            Stretch = Stretch.Fill
        });

        // SVG 装饰图形
        toast.Children.Add(new SvgViewbox
        {
            Source = new Uri("/Assets/Group126548867.svg", UriKind.Relative),
            Stretch = Stretch.Fill
        });

        // 边框
        toast.Children.Add(new System.Windows.Shapes.Path
        {
            Width = 340,
            Height = 80,
            Data = Geometry.Parse("M339.5 0.5V73.793L333.793 79.5H0.5V6.20703L6.20703 0.5H339.5Z"),
            Stroke = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
            StrokeThickness = 1,
            Stretch = Stretch.Fill
        });

        // 内容：图标 + 文字
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

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (rootPanel.Children.Contains(toast))
                rootPanel.Children.Remove(toast);
        };
        timer.Start();
    }

    private void SaveAsButton_Click(object sender, MouseButtonEventArgs e)
    {
        SaveAsInternal(null);
    }

    private void SaveAsInternal(Action? onSaved)
    {
        if (App.PresetService == null) return;
        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var personalPresets = App.PresetService.LoadPersonalPresets(Models.Usb.DeviceType.Pedal);
        var existingNames = personalPresets.Select(p => p.Name).ToList();

        var rootPanel = mainWindow.Content as Panel;
        if (rootPanel == null) return;

        var editPopup = new EditPresetPopup { DeviceType = Models.Usb.DeviceType.Pedal };
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
                Parameters = CaptureCurrentParameters(),
                IsPersonal = true,
                DeviceType = Models.Usb.DeviceType.Pedal
            };

            var currentPersonal = App.PresetService.LoadPersonalPresets(Models.Usb.DeviceType.Pedal);
            currentPersonal.Add(newPreset);
            App.PresetService.SavePersonalPresets(currentPersonal, Models.Usb.DeviceType.Pedal);

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

    private void ExportButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isAppliedPresetPersonal) return;

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = LocalizationService.Instance["Preset.ExportPreset"],
            Filter = LocalizationService.Instance["Preset.PresetFileFilter"],
            DefaultExt = ".json",
            FileName = _currentPresetName == "Default" ? "pedal_preset" : _currentPresetName
        };

        if (dlg.ShowDialog() != true || App.PresetService == null) return;

        var fileName = dlg.FileName;
        TryExportWithRetry(fileName);
    }

    private void TryExportWithRetry(string fileName)
    {
        if (PerformExport(fileName))
        {
            ShowSuccessToast(LocalizationService.Instance["Preset.ExportSuccess"]);
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
                Parameters = snapshot,
                IsPersonal = true,
                DeviceType = Models.Usb.DeviceType.Pedal
            };
            App.PresetService!.ExportPreset(exportItem, fileName);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PedalControl] 导出预设失败: {ex.Message}");
            return false;
        }
    }

    private void ShowExportFailedDialog(Action? onRetry)
    {
        if (Window.GetWindow(this) is not HITAPEX.MainWindow mainWindow) return;

        var dialog = mainWindow.GlobalDialog;
        dialog.Title = LocalizationService.Instance["Preset.ExportFailed"];
        dialog.ShowIcon = true;
        dialog.ClearButtons();

        dialog.DialogContent = new TextBlock
        {
            Text = LocalizationService.Instance["Preset.ExportFailedMessage"],
            FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238)),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        dialog.AddButton(LocalizationService.Instance["Common.Retry"], (_, _) =>
        {
            dialog.Hide();
            onRetry?.Invoke();
        }, isPrimary: true);

        dialog.AddButton(LocalizationService.Instance["Common.Cancel"], (_, _) =>
        {
            dialog.Hide();
        }, isPrimary: false);

        dialog.Show();
    }

    private PresetListPopup? GetPresetListPopup()
    {
        if (Window.GetWindow(this) is HITAPEX.MainWindow mainWindow)
            return mainWindow.GetPresetListPopup(Models.Usb.DeviceType.Pedal);
        return null;
    }

    private void PresetListButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is HITAPEX.MainWindow mainWindow)
        {
            var popup = mainWindow.ShowPresetListPopup(Models.Usb.DeviceType.Pedal);
            popup.PresetApplied -= OnPresetApplied;
            popup.PresetApplied += OnPresetApplied;
        }
    }

    private void OnPresetApplied(object? sender, PresetItem preset)
    {
        if (preset.Parameters == null) return;

        if (_isPresetModified)
            ShowUnsavedDialog(() => ApplyPreset(preset), () => ApplyPreset(preset));
        else
            ApplyPreset(preset);
    }

    private void ApplyPreset(PresetItem preset)
    {
        _isApplyingPreset = true;
        ApplyPresetSnapshot(preset.Parameters!);
        SendPedalParameters();
        _isApplyingPreset = false;

        _appliedPresetParameters = preset.Parameters;
        _currentPresetName = preset.Name;
        _isAppliedPresetPersonal = preset.IsPersonal;
        _isPresetModified = false;
        UpdatePresetDisplay();

        SendPresetName(preset.Name);
    }

    /// <summary>任意参数修改后的统一入口，标记已修改状态并刷新 UI</summary>
    private void OnParameterModified()
    {
        if (!IsLoaded || _isApplyingParameters || _isApplyingPreset) return;
        _isPresetModified = true;
        UpdatePresetDisplay();
    }

    /// <summary>更新预设名称、已更改提示、撤回按钮状态</summary>
    private void UpdatePresetDisplay()
    {
        var isDeviceConnected = _connectedPedalDevice != null || _isPedalViaBase;
        var isOnboard = _currentPresetName == "Default" && isDeviceConnected;

        Debug.WriteLine($"[PedalControl.UpdatePresetDisplay] isDeviceConnected={isDeviceConnected}, isOnboard={isOnboard}, _currentPresetName='{_currentPresetName}', _devicePresetName='{_devicePresetName}', _isAppliedPresetPersonal={_isAppliedPresetPersonal}, _isPresetModified={_isPresetModified}");

        if (PresetNameText != null)
        {
            var newText = PresetNameText.Text;
            if (isOnboard && !string.IsNullOrEmpty(_devicePresetName))
                newText = $"{_devicePresetName}_{LocalizationService.Instance["DeviceParam.Onboard"]}";
            else if (isOnboard)
                newText = LocalizationService.Instance["DeviceParam.Onboard"];
            else
                newText = _currentPresetName;

            Debug.WriteLine($"[PedalControl.UpdatePresetDisplay] PresetNameText: '{PresetNameText.Text}' -> '{newText}'");
            PresetNameText.Text = newText;
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

        var isSaveEnabled = _isAppliedPresetPersonal && _isPresetModified;
        if (SaveButtonPath != null)
        {
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

        if (OnboardPresetIcon != null)
            OnboardPresetIcon.Visibility = isOnboard ? Visibility.Visible : Visibility.Collapsed;
        if (OfficialPresetIcon != null)
            OfficialPresetIcon.Visibility = (!isOnboard && !_isAppliedPresetPersonal) ? Visibility.Visible : Visibility.Collapsed;
        if (PersonalPresetIcon != null)
            PersonalPresetIcon.Visibility = (!isOnboard && _isAppliedPresetPersonal) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>将当前 UI 参数捕获为快照</summary>
    private PedalPresetSnapshot CaptureCurrentParameters()
    {
        var clutchPoints = GetCurvePointsAsProtocolBytes(_clutchCurvePoints);
        var brakePoints = GetCurvePointsAsProtocolBytes(_brakeCurvePoints);
        var throttlePoints = GetCurvePointsAsProtocolBytes(_throttleCurvePoints);

        return new PedalPresetSnapshot
        {
            ClutchCurveType = _selectedCurveType,
            ClutchDirection = (byte)(ClutchReverseToggle?.IsChecked == true ? 1 : 0),
            ClutchPoint1Y = clutchPoints[0], ClutchPoint1X = clutchPoints[1],
            ClutchPoint2Y = clutchPoints[2], ClutchPoint2X = clutchPoints[3],
            ClutchPoint3Y = clutchPoints[4], ClutchPoint3X = clutchPoints[5],
            ClutchPoint4Y = clutchPoints[6], ClutchPoint4X = clutchPoints[7],
            ClutchDeadZoneFront = (byte)Math.Round(_clutchDeadZoneLeft),
            ClutchDeadZoneRear = (byte)Math.Round(_clutchDeadZoneRight),

            BrakeCurveType = _brakeSelectedCurveType,
            BrakeDirection = (byte)(BrakeReverseToggle?.IsChecked == true ? 1 : 0),
            BrakePoint1Y = brakePoints[0], BrakePoint1X = brakePoints[1],
            BrakePoint2Y = brakePoints[2], BrakePoint2X = brakePoints[3],
            BrakePoint3Y = brakePoints[4], BrakePoint3X = brakePoints[5],
            BrakePoint4Y = brakePoints[6], BrakePoint4X = brakePoints[7],
            BrakeDeadZoneFront = (byte)Math.Round(_brakeDeadZoneLeft),
            BrakeDeadZoneRear = (byte)Math.Round(_brakeDeadZoneRight),

            ThrottleCurveType = _throttleSelectedCurveType,
            ThrottleDirection = (byte)(ThrottleReverseToggle?.IsChecked == true ? 1 : 0),
            ThrottlePoint1Y = throttlePoints[0], ThrottlePoint1X = throttlePoints[1],
            ThrottlePoint2Y = throttlePoints[2], ThrottlePoint2X = throttlePoints[3],
            ThrottlePoint3Y = throttlePoints[4], ThrottlePoint3X = throttlePoints[5],
            ThrottlePoint4Y = throttlePoints[6], ThrottlePoint4X = throttlePoints[7],
            ThrottleDeadZoneFront = (byte)Math.Round(_throttleDeadZoneLeft),
            ThrottleDeadZoneRear = (byte)Math.Round(_throttleDeadZoneRight),
        };
    }

    /// <summary>将预设快照应用到 UI 控件</summary>
    private void ApplyPresetSnapshot(PedalPresetSnapshot p)
    {
        // 离合方向
        if (ClutchReverseToggle != null)
            ClutchReverseToggle.IsChecked = p.ClutchDirection == 1;

        // 离合死区
        _clutchDeadZoneLeft = p.ClutchDeadZoneFront;
        _clutchDeadZoneRight = p.ClutchDeadZoneRear;
        UpdateClutchDeadZoneDisplay();

        // 离合曲线
        _selectedCurveType = p.ClutchCurveType;
        ApplyCurveTypeSelection(p.ClutchCurveType, "CurveType", Color.FromRgb(255, 200, 0), Color.FromRgb(153, 120, 0));
        _clutchCurvePoints = new PointCollection
        {
            new Point(0, 266),
            PointFromProtocol(p.ClutchPoint1X, p.ClutchPoint1Y),
            PointFromProtocol(p.ClutchPoint2X, p.ClutchPoint2Y),
            PointFromProtocol(p.ClutchPoint3X, p.ClutchPoint3Y),
            PointFromProtocol(p.ClutchPoint4X, p.ClutchPoint4Y),
            new Point(345, 0)
        };
        ApplySmoothCurve(ClutchCurveLine, ClutchFillArea, _clutchCurvePoints);
        RepositionCurvePoints(_clutchCurvePoints, ClutchPoint1, ClutchPoint2, ClutchPoint3, ClutchPoint4);

        // 刹车方向
        if (BrakeReverseToggle != null)
            BrakeReverseToggle.IsChecked = p.BrakeDirection == 1;

        // 刹车死区
        _brakeDeadZoneLeft = p.BrakeDeadZoneFront;
        _brakeDeadZoneRight = p.BrakeDeadZoneRear;
        UpdateBrakeDeadZoneDisplay();

        // 刹车曲线
        _brakeSelectedCurveType = p.BrakeCurveType;
        ApplyCurveTypeSelection(p.BrakeCurveType, "BrakeCurveType", Color.FromRgb(198, 14, 14), Color.FromRgb(96, 7, 7));
        _brakeCurvePoints = new PointCollection
        {
            new Point(0, 266),
            PointFromProtocol(p.BrakePoint1X, p.BrakePoint1Y),
            PointFromProtocol(p.BrakePoint2X, p.BrakePoint2Y),
            PointFromProtocol(p.BrakePoint3X, p.BrakePoint3Y),
            PointFromProtocol(p.BrakePoint4X, p.BrakePoint4Y),
            new Point(345, 0)
        };
        ApplySmoothCurve(BrakeCurveLine, BrakeFillArea, _brakeCurvePoints);
        RepositionCurvePoints(_brakeCurvePoints, BrakePoint1, BrakePoint2, BrakePoint3, BrakePoint4);

        // 油门方向
        if (ThrottleReverseToggle != null)
            ThrottleReverseToggle.IsChecked = p.ThrottleDirection == 1;

        // 油门死区
        _throttleDeadZoneLeft = p.ThrottleDeadZoneFront;
        _throttleDeadZoneRight = p.ThrottleDeadZoneRear;
        UpdateThrottleDeadZoneDisplay();

        // 油门曲线
        _throttleSelectedCurveType = p.ThrottleCurveType;
        ApplyCurveTypeSelection(p.ThrottleCurveType, "ThrottleCurveType", Color.FromRgb(22, 198, 66), Color.FromRgb(10, 96, 32));
        _throttleCurvePoints = new PointCollection
        {
            new Point(0, 266),
            PointFromProtocol(p.ThrottlePoint1X, p.ThrottlePoint1Y),
            PointFromProtocol(p.ThrottlePoint2X, p.ThrottlePoint2Y),
            PointFromProtocol(p.ThrottlePoint3X, p.ThrottlePoint3Y),
            PointFromProtocol(p.ThrottlePoint4X, p.ThrottlePoint4Y),
            new Point(345, 0)
        };
        ApplySmoothCurve(ThrottleCurveLine, ThrottleFillArea, _throttleCurvePoints);
        RepositionCurvePoints(_throttleCurvePoints, ThrottlePoint1, ThrottlePoint2, ThrottlePoint3, ThrottlePoint4);

        RebuildCurveCaches();
    }

    // ════════════════════════════════════════════════════════════════
    //  离合器 — 死区调整
    // ════════════════════════════════════════════════════════════════

    private void SetupClutchDeadZoneThumbs()
    {
        AttachDeadZoneHandlers(ClutchDeadZoneLeftThumb, ClutchDeadZoneThumb_MouseLeftButtonDown, ClutchDeadZoneThumb_MouseMove, ClutchDeadZoneThumb_MouseLeftButtonUp);
        AttachDeadZoneHandlers(ClutchDeadZoneRightThumb, ClutchDeadZoneThumb_MouseLeftButtonDown, ClutchDeadZoneThumb_MouseMove, ClutchDeadZoneThumb_MouseLeftButtonUp);
    }

    private static void AttachDeadZoneHandlers(Ellipse? thumb, MouseButtonEventHandler down, MouseEventHandler move, MouseButtonEventHandler up)
    {
        if (thumb == null) return;
        thumb.MouseLeftButtonDown += down;
        thumb.MouseMove += move;
        thumb.MouseLeftButtonUp += up;
    }

    private void ClutchDeadZoneThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartDeadZoneDrag(sender, ClutchDeadZoneRightThumb, ref _isClutchDraggingDeadZone, ref _clutchDraggingDeadZoneThumb);
        e.Handled = true;
    }

    private void ClutchDeadZoneThumb_MouseMove(object sender, MouseEventArgs e)
    {
        HandleDeadZoneDrag(sender, e, DeadZoneTrackCanvas,
            ref _isClutchDraggingDeadZone, ref _clutchDraggingDeadZoneThumb,
            ref _clutchDeadZoneLeft, ref _clutchDeadZoneRight,
            UpdateClutchDeadZoneDisplay);
    }

    private void ClutchDeadZoneThumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDeadZoneDrag(sender, ref _isClutchDraggingDeadZone, ref _clutchDraggingDeadZoneThumb);
        SendPedalParameters();
    }

    private void UpdateClutchDeadZoneDisplay()
    {
        UpdateDeadZoneDisplay(_clutchDeadZoneLeft, _clutchDeadZoneRight,
            ClutchDeadZoneLeftThumb, ClutchDeadZoneRightThumb,
            ClutchDeadZoneLeftProgress, ClutchDeadZoneRightProgress,
            ClutchDeadZoneLeftLabel, ClutchDeadZoneRightLabel);
    }

    // ════════════════════════════════════════════════════════════════
    //  刹车 — 死区调整
    // ════════════════════════════════════════════════════════════════

    private void SetupBrakeDeadZoneThumbs()
    {
        AttachDeadZoneHandlers(BrakeDeadZoneLeftThumb, BrakeDeadZoneThumb_MouseLeftButtonDown, BrakeDeadZoneThumb_MouseMove, BrakeDeadZoneThumb_MouseLeftButtonUp);
        AttachDeadZoneHandlers(BrakeDeadZoneRightThumb, BrakeDeadZoneThumb_MouseLeftButtonDown, BrakeDeadZoneThumb_MouseMove, BrakeDeadZoneThumb_MouseLeftButtonUp);
    }

    private void BrakeDeadZoneThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartDeadZoneDrag(sender, BrakeDeadZoneRightThumb, ref _isBrakeDraggingDeadZone, ref _brakeDraggingDeadZoneThumb);
        e.Handled = true;
    }

    private void BrakeDeadZoneThumb_MouseMove(object sender, MouseEventArgs e)
    {
        HandleDeadZoneDrag(sender, e, BrakeDeadZoneTrackCanvas,
            ref _isBrakeDraggingDeadZone, ref _brakeDraggingDeadZoneThumb,
            ref _brakeDeadZoneLeft, ref _brakeDeadZoneRight,
            UpdateBrakeDeadZoneDisplay);
    }

    private void BrakeDeadZoneThumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDeadZoneDrag(sender, ref _isBrakeDraggingDeadZone, ref _brakeDraggingDeadZoneThumb);
        SendPedalParameters();
    }

    private void UpdateBrakeDeadZoneDisplay()
    {
        UpdateDeadZoneDisplay(_brakeDeadZoneLeft, _brakeDeadZoneRight,
            BrakeDeadZoneLeftThumb, BrakeDeadZoneRightThumb,
            BrakeDeadZoneLeftProgress, BrakeDeadZoneRightProgress,
            BrakeDeadZoneLeftLabel, BrakeDeadZoneRightLabel);
    }

    // ════════════════════════════════════════════════════════════════
    //  油门 — 死区调整
    // ════════════════════════════════════════════════════════════════

    private void SetupThrottleDeadZoneThumbs()
    {
        AttachDeadZoneHandlers(ThrottleDeadZoneLeftThumb, ThrottleDeadZoneThumb_MouseLeftButtonDown, ThrottleDeadZoneThumb_MouseMove, ThrottleDeadZoneThumb_MouseLeftButtonUp);
        AttachDeadZoneHandlers(ThrottleDeadZoneRightThumb, ThrottleDeadZoneThumb_MouseLeftButtonDown, ThrottleDeadZoneThumb_MouseMove, ThrottleDeadZoneThumb_MouseLeftButtonUp);
    }

    private void ThrottleDeadZoneThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartDeadZoneDrag(sender, ThrottleDeadZoneRightThumb, ref _isThrottleDraggingDeadZone, ref _throttleDraggingDeadZoneThumb);
        e.Handled = true;
    }

    private void ThrottleDeadZoneThumb_MouseMove(object sender, MouseEventArgs e)
    {
        HandleDeadZoneDrag(sender, e, ThrottleDeadZoneTrackCanvas,
            ref _isThrottleDraggingDeadZone, ref _throttleDraggingDeadZoneThumb,
            ref _throttleDeadZoneLeft, ref _throttleDeadZoneRight,
            UpdateThrottleDeadZoneDisplay);
    }

    private void ThrottleDeadZoneThumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDeadZoneDrag(sender, ref _isThrottleDraggingDeadZone, ref _throttleDraggingDeadZoneThumb);
        SendPedalParameters();
    }

    private void UpdateThrottleDeadZoneDisplay()
    {
        UpdateDeadZoneDisplay(_throttleDeadZoneLeft, _throttleDeadZoneRight,
            ThrottleDeadZoneLeftThumb, ThrottleDeadZoneRightThumb,
            ThrottleDeadZoneLeftProgress, ThrottleDeadZoneRightProgress,
            ThrottleDeadZoneLeftLabel, ThrottleDeadZoneRightLabel);
    }

    // ════════════════════════════════════════════════════════════════
    //  共用 — 死区拖拽逻辑
    // ════════════════════════════════════════════════════════════════

    private static void StartDeadZoneDrag(object sender, Ellipse? rightThumb,
        ref bool isDragging, ref string? draggingThumb)
    {
        if (sender is not UIElement element) return;
        draggingThumb = sender == rightThumb ? "Right" : "Left";
        isDragging = true;
        element.CaptureMouse();
    }

    private static void HandleDeadZoneDrag(object sender, MouseEventArgs e, Canvas? trackCanvas,
        ref bool isDragging, ref string? draggingThumb,
        ref double deadZoneLeft, ref double deadZoneRight,
        Action updateDisplay)
    {
        if (!isDragging || draggingThumb == null || trackCanvas == null) return;

        var position = e.GetPosition(trackCanvas);
        var x = Math.Max(0, Math.Min(227, position.X));

        if (draggingThumb == "Left")
        {
            double centerX = Math.Max(0, Math.Min(100, x));
            deadZoneLeft = Math.Round(centerX / 100.0 * 15.0, 1);
        }
        else
        {
            double centerX = Math.Max(124, Math.Min(227, x));
            deadZoneRight = Math.Round((227.0 - centerX) / 103.0 * 15.0, 1);
        }

        updateDisplay();
    }

    private static void EndDeadZoneDrag(object sender, ref bool isDragging, ref string? draggingThumb)
    {
        if (sender is UIElement element)
            element.ReleaseMouseCapture();
        isDragging = false;
        draggingThumb = null;
    }

    private static void UpdateDeadZoneDisplay(double deadZoneLeft, double deadZoneRight,
        Ellipse? leftThumb, Ellipse? rightThumb,
        Rectangle? leftProgress, Rectangle? rightProgress,
        TextBlock? leftLabel, TextBlock? rightLabel)
    {
        if (leftThumb == null || rightThumb == null
            || leftProgress == null || rightProgress == null
            || leftLabel == null || rightLabel == null) return;

        double leftProgressWidth = deadZoneLeft / 15.0 * 100.0;
        double leftThumbPos = deadZoneLeft / 15.0 * 100.0 - 4.5;
        // 左侧进度条向左扩展 0.5px，遮盖 SVG 轨道左边界抗锯齿缝隙
        Canvas.SetLeft(leftProgress, -0.5);
        leftProgress.Width = leftProgressWidth + 0.5;
        Canvas.SetLeft(leftThumb, leftThumbPos);
        leftLabel.Text = $"{deadZoneLeft:F0}%";

        double rightProgressWidth = deadZoneRight / 15.0 * 103.0;
        double rightThumbPos = 222.5 - rightProgressWidth;
        Canvas.SetLeft(rightThumb, rightThumbPos);
        // 右侧进度条向右扩展 0.5px，遮盖 SVG 轨道右边界抗锯齿缝隙
        Canvas.SetLeft(rightProgress, 227.0 - rightProgressWidth);
        rightProgress.Width = rightProgressWidth + 0.5;
        rightLabel.Text = $"{100.0 - deadZoneRight:F0}%";
    }

    // ════════════════════════════════════════════════════════════════
    //  USB 设备通信
    // ════════════════════════════════════════════════════════════════

    /// <summary>供外部调用的设备信息刷新入口</summary>
    public async Task RefreshDeviceInfoAsync()
    {
        try
        {
            var connectedDevices = App.UsbManager?.ConnectedDevices
                ?? System.Collections.ObjectModel.ReadOnlyCollection<UsbDeviceInfo>.Empty;

            _connectedPedalDevice = null;
            _isPedalViaBase = false;

            // 1. 查找直连的踏板 USB 设备
            _connectedPedalDevice = connectedDevices.FirstOrDefault(d =>
            {
                var descriptor = DeviceRegistry.FindByVidPid(d.Vid, d.Pid);
                return descriptor != null && descriptor.DeviceType == DeviceType.Pedal
                       && descriptor.IsNormalMode(d.Vid, d.Pid);
            });

            if (_connectedPedalDevice != null)
            {
                // 直连方式
                var descriptor = DeviceRegistry.FindByVidPid(_connectedPedalDevice.Vid, _connectedPedalDevice.Pid);
                _deviceModel = descriptor?.ModelName ?? "";
                _connectionStatusText = LocalizationService.Instance["DeviceParam.ConnectedDirect"];
                _connectionStatusColor = "#179548";

                if (App.ProtocolService != null && App.FirmwareUpdater != null)
                {
                    var deviceInfo = await App.FirmwareUpdater.GetDeviceInfoAsync(
                        _connectedPedalDevice, DeviceType.Pedal);
                    if (deviceInfo != null)
                    {
                        _firmwareVersion = deviceInfo.VersionString;
                        _pedalCount = deviceInfo.PedalCount;
                    }
                    else
                    {
                        _firmwareVersion = "未知";
                    }
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
                    if (baseInfo != null && baseInfo.IsPedalConnected)
                    {
                        _isPedalViaBase = true;
                        _baseDevice = baseDevice;
                        _deviceModel = GetPedalModelFromConnectionStatus(baseInfo.PedalConnectionStatus);
                        _connectionStatusText = LocalizationService.Instance["DeviceParam.ConnectedBase"];
                        _connectionStatusColor = "#179548";
                        _firmwareVersion = baseInfo.PedalVersionString;
                        _pedalCount = baseInfo.PedalCount;
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
            } // end else { // 检查基座连接
        } // end original outer else
        catch (Exception ex)
        {
            Debug.WriteLine($"[PedalControl] 刷新设备信息异常: {ex.Message}");
            SetDisconnected();
        }

        UpdateConnectionStatusDisplay();

        // 固件版本检查改为 fire-and-forget：API 服务器不可达时会阻塞 15s+，
        // 不应延迟后续 USB 参数获取命令
        _ = CheckFirmwareVersionAsync();

        // 获取踏板参数并同步 UI
        await FetchPedalParametersAsync();

        // 获取设备预设名称
        await FetchPresetNameAsync();

        // 尝试将设备预设匹配到本地预设
        TryMatchLocalPreset();
    }

    /// <summary>从设备获取预设名称</summary>
    private async Task FetchPresetNameAsync()
    {
        UsbDeviceInfo? targetDevice = null;
        if (_connectedPedalDevice != null)
            targetDevice = _connectedPedalDevice;
        else if (_isPedalViaBase && _baseDevice != null)
            targetDevice = _baseDevice;

        if (targetDevice == null || App.ProtocolService == null)
            return;

        try
        {
            var name = await App.ProtocolService.GetPresetNameAsync(targetDevice.DeviceKey, DeviceType.Pedal);
            Debug.WriteLine($"[PedalControl.FetchPresetName] 设备返回名称='{name ?? "(null)"}', _currentPresetName='{_currentPresetName}', _isPresetModified={_isPresetModified}");
            if (name != null)
            {
                _devicePresetName = name;
                Debug.WriteLine($"[PedalControl.FetchPresetName] 设置 _devicePresetName='{_devicePresetName}'");
                Debug.WriteLine($"[PedalControl] 设备预设名称: {name}");
                UpdatePresetDisplay();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PedalControl] 获取预设名称异常: {ex.Message}");
        }
    }

    /// <summary>下发预设名称到设备</summary>
    private void SendPresetName(string name)
    {
        UsbDeviceInfo? targetDevice = null;
        if (_connectedPedalDevice != null)
            targetDevice = _connectedPedalDevice;
        else if (_isPedalViaBase && _baseDevice != null)
            targetDevice = _baseDevice;

        if (targetDevice == null || App.ProtocolService == null)
            return;

        try
        {
            App.ProtocolService.SetPresetName(targetDevice.DeviceKey, DeviceType.Pedal, name);
            Debug.WriteLine($"[PedalControl] 预设名称已下发: {name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PedalControl] 下发预设名称异常: {ex.Message}");
        }
    }

    /// <summary>对比设备上报的预设名称和参数与本地预设，若完全匹配则视为本地预设</summary>
    private void TryMatchLocalPreset()
    {
        if (string.IsNullOrEmpty(_devicePresetName) || _appliedPresetParameters == null || App.PresetService == null)
            return;

        try
        {
            var officialPresets = App.PresetService.LoadOfficialPresets(Models.Usb.DeviceType.Pedal);
            var personalPresets = App.PresetService.LoadPersonalPresets(Models.Usb.DeviceType.Pedal);

            // 先查个人预设，再查官方预设
            PresetItem? matched = personalPresets.FirstOrDefault(p => p.Name == _devicePresetName);
            bool isPersonal = true;
            if (matched == null)
            {
                matched = officialPresets.FirstOrDefault(p => p.Name == _devicePresetName);
                isPersonal = false;
            }

            if (matched?.Parameters != null && _appliedPresetParameters.ParametersEqual(matched.Parameters))
            {
                _currentPresetName = matched.Name;
                _isAppliedPresetPersonal = isPersonal;
                _devicePresetName = string.Empty;
                Debug.WriteLine($"[PedalControl] 设备预设匹配到本地{(isPersonal ? "个人" : "官方")}预设: {matched.Name}");
                UpdatePresetDisplay();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PedalControl] 匹配本地预设异常: {ex.Message}");
        }
    }

    private void SetDisconnected()
    {
        _connectedPedalDevice = null;
        _baseDevice = null;
        _isPedalViaBase = false;
        _deviceModel = "";
        _connectionStatusText = LocalizationService.Instance["DeviceParam.NotConnected"];
        _connectionStatusColor = "#C60E0E";
        _firmwareVersion = "---";
        _pedalCount = 1;

        // 重置预设状态
        _appliedPresetParameters = null;
        _currentPresetName = "Default";
        _devicePresetName = string.Empty;
        _isPresetModified = false;
        _isAppliedPresetPersonal = false;

        // 踏板位置归零
        _latestRawClutch = 0;
        _latestRawBrake = 0;
        _latestRawGas = 0;
        _latestProcessedClutch = 0;
        _latestProcessedBrake = 0;
        _latestProcessedGas = 0;
        _displayedRawClutch = -1;
        _displayedRawBrake = -1;
        _displayedRawGas = -1;
        _displayedProcessedClutch = -1;
        _displayedProcessedBrake = -1;
        _displayedProcessedGas = -1;
        UpdatePedalPositionDisplay(0, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// 根据基座上报的踏板连接状态字节，返回踏板型号名称。
    /// 0x01 = A1, 0x02 = A2, ...
    /// </summary>
    private static string GetPedalModelFromConnectionStatus(int status)
    {
        return status switch
        {
            0x01 => "A1踏板",
            0x02 => "A2踏板",
            0x03 => "A3踏板",
            0x04 => "A4踏板",
            _ => LocalizationService.Instance["Status.DeviceTypePedal"]
        };
    }

    /// <summary>
    /// 从 API 获取踏板最新固件版本并与设备当前版本比对，控制“新版本可用”的显隐。
    /// </summary>
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
            if (!_isPedalViaBase && _connectedPedalDevice != null)
            {
                vid = _connectedPedalDevice.Vid;
                pid = _connectedPedalDevice.Pid;
            }
            else
            {
                // 踏板通过基座连接时，使用 A1 踏板的默认 VID/PID 查询 API
                var descriptor = DeviceRegistry.Devices.FirstOrDefault(d => d.DeviceType == DeviceType.Pedal);
                if (descriptor == null) return;
                vid = descriptor.NormalMode.Vid;
                pid = descriptor.NormalMode.Pid;
            }

            var firmwareList = await App.FirmwareApi.GetFirmwareVersionsAsync();
            var matched = App.FirmwareApi.FindFirmwareForDevice(firmwareList, vid, pid);

            if (matched != null && FirmwareUpdateService.IsNewerVersion(_firmwareVersion, matched.Version))
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
            Debug.WriteLine($"[PedalControl] 固件版本检查异常: {ex.Message}");
        }
    }

    /// <summary>点击“新版本可用”跳转到设置界面的固件更新选项卡</summary>
    private void NewVersionAvailable_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            var vm = mainWindow.DataContext as ViewModels.MainWindowViewModel;
            if (vm != null)
            {
                // 导航到设置界面
                var settingsItem = vm.NavigationItems.FirstOrDefault(n => n.Name == "Settings");
                if (settingsItem != null)
                {
                    vm.SelectedNavigationItem = settingsItem;
                    // 延迟切换到固件更新选项卡，等待 SettingsUserControl 加载
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                    {
                        var settingsView = vm.CurrentView as SettingsUserControl;
                        settingsView?.SwitchToFirmwareUpdateTab();
                    });
                }
            }
        }
        e.Handled = true;
    }

    /// <summary>向设备下发获取踏板参数命令，并根据响应更新 UI</summary>
    private async Task FetchPedalParametersAsync()
    {
        UsbDeviceInfo? targetDevice = null;
        if (_connectedPedalDevice != null)
            targetDevice = _connectedPedalDevice;
        else if (_isPedalViaBase && _baseDevice != null)
            targetDevice = _baseDevice;

        if (targetDevice == null || App.ProtocolService == null)
            return;

        try
        {
            var cmd = DeviceProtocolService.BuildGetPedalParametersCommand();
            var response = await App.ProtocolService.SendCommandAsync(targetDevice.DeviceKey, cmd);
            if (response == null) return;

            var parameters = DeviceProtocolService.ParsePedalParametersResponse(response);
            if (parameters != null)
                ApplyPedalParameters(parameters);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PedalControl] 获取踏板参数异常: {ex.Message}");
        }
    }

    /// <summary>将协议解析的踏板参数应用到 UI 控件</summary>
    private void ApplyPedalParameters(PedalParametersResponse p)
    {
        _isApplyingParameters = true;

        try
        {
            // 离合方向
            if (ClutchReverseToggle != null)
                ClutchReverseToggle.IsChecked = p.ClutchDirection == 1;

        // 离合死区
        _clutchDeadZoneLeft = p.ClutchDeadZoneFront;
        _clutchDeadZoneRight = p.ClutchDeadZoneRear;
        UpdateClutchDeadZoneDisplay();

        // 离合曲线点
        _selectedCurveType = 5;
        ApplyCurveTypeSelection(5, "CurveType", Color.FromRgb(255, 200, 0), Color.FromRgb(153, 120, 0));
        _clutchCurvePoints = new PointCollection
        {
            new Point(0, 266),
            PointFromProtocol(p.ClutchPoint1X, p.ClutchPoint1Y),
            PointFromProtocol(p.ClutchPoint2X, p.ClutchPoint2Y),
            PointFromProtocol(p.ClutchPoint3X, p.ClutchPoint3Y),
            PointFromProtocol(p.ClutchPoint4X, p.ClutchPoint4Y),
            new Point(345, 0)
        };
        ApplySmoothCurve(ClutchCurveLine, ClutchFillArea, _clutchCurvePoints);
        RepositionCurvePoints(_clutchCurvePoints, ClutchPoint1, ClutchPoint2, ClutchPoint3, ClutchPoint4);
        RebuildCurveCaches();

        // 刹车方向
        if (BrakeReverseToggle != null)
            BrakeReverseToggle.IsChecked = p.BrakeDirection == 1;

        // 刹车死区
        _brakeDeadZoneLeft = p.BrakeDeadZoneFront;
        _brakeDeadZoneRight = p.BrakeDeadZoneRear;
        UpdateBrakeDeadZoneDisplay();

        // 刹车曲线点
        _brakeSelectedCurveType = 5;
        ApplyCurveTypeSelection(5, "BrakeCurveType", Color.FromRgb(198, 14, 14), Color.FromRgb(96, 7, 7));
        _brakeCurvePoints = new PointCollection
        {
            new Point(0, 266),
            PointFromProtocol(p.BrakePoint1X, p.BrakePoint1Y),
            PointFromProtocol(p.BrakePoint2X, p.BrakePoint2Y),
            PointFromProtocol(p.BrakePoint3X, p.BrakePoint3Y),
            PointFromProtocol(p.BrakePoint4X, p.BrakePoint4Y),
            new Point(345, 0)
        };
        ApplySmoothCurve(BrakeCurveLine, BrakeFillArea, _brakeCurvePoints);
        RepositionCurvePoints(_brakeCurvePoints, BrakePoint1, BrakePoint2, BrakePoint3, BrakePoint4);
        RebuildCurveCaches();

        // 油门方向
        if (ThrottleReverseToggle != null)
            ThrottleReverseToggle.IsChecked = p.ThrottleDirection == 1;

        // 油门死区
        _throttleDeadZoneLeft = p.ThrottleDeadZoneFront;
        _throttleDeadZoneRight = p.ThrottleDeadZoneRear;
        UpdateThrottleDeadZoneDisplay();

        // 油门曲线点
        _throttleSelectedCurveType = 5;
        ApplyCurveTypeSelection(5, "ThrottleCurveType", Color.FromRgb(22, 198, 66), Color.FromRgb(10, 96, 32));
        _throttleCurvePoints = new PointCollection
        {
            new Point(0, 266),
            PointFromProtocol(p.ThrottlePoint1X, p.ThrottlePoint1Y),
            PointFromProtocol(p.ThrottlePoint2X, p.ThrottlePoint2Y),
            PointFromProtocol(p.ThrottlePoint3X, p.ThrottlePoint3Y),
            PointFromProtocol(p.ThrottlePoint4X, p.ThrottlePoint4Y),
            new Point(345, 0)
        };
        ApplySmoothCurve(ThrottleCurveLine, ThrottleFillArea, _throttleCurvePoints);
        RepositionCurvePoints(_throttleCurvePoints, ThrottlePoint1, ThrottlePoint2, ThrottlePoint3, ThrottlePoint4);
        RebuildCurveCaches();

        // 设备上报参数作为首次基线预设
        _appliedPresetParameters = CaptureCurrentParameters();
        _currentPresetName = "Default";
        _isAppliedPresetPersonal = false;
        _isPresetModified = false;
        UpdatePresetDisplay();

        Debug.WriteLine($"[PedalControl] 踏板参数已从设备同步到 UI");
        }
        finally
        {
            _isApplyingParameters = false;
        }
    }

    /// <summary>将协议字节值（X/Y 0-100）转为画布坐标 Point</summary>
    private static Point PointFromProtocol(byte x, byte y)
    {
        var canvasX = x / 100.0 * 345.0;
        var canvasY = (100 - y) / 100.0 * 266.0;
        return new Point(canvasX, canvasY);
    }

    private void UpdateConnectionStatusDisplay()
    {
        if (DeviceModelName != null)
            DeviceModelName.Text = BuildDeviceDisplayName();

        if (ConnectionStatusText != null)
            ConnectionStatusText.Text = _connectionStatusText;

        if (FirmwareVersionText != null)
            FirmwareVersionText.Text = _firmwareVersion == "未知" ? LocalizationService.Instance["DeviceParam.Unknown"]
                : _firmwareVersion == "---" ? LocalizationService.Instance["DeviceParam.UnknownVersion"]
                : _firmwareVersion;

        // 更新连接状态图标颜色
        var color = (Color)ColorConverter.ConvertFromString(_connectionStatusColor);
        var brush = new SolidColorBrush(color);
        var iconPaths = new[] { ConnStatusIcon1, ConnStatusIcon2, ConnStatusIcon3,
                                ConnStatusIcon4, ConnStatusIcon5, ConnStatusIcon6, ConnStatusIcon7 };
        foreach (var path in iconPaths)
        {
            if (path != null)
                path.Stroke = brush;
        }

        // 2踏板模式下离合不可用，显示遮罩
        if (ClutchOverlay != null)
            ClutchOverlay.Visibility = _pedalCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>将画布曲线点转换为协议格式的字节数组(每点Y,X，各0-100)</summary>
    private static byte[] GetCurvePointsAsProtocolBytes(PointCollection curvePoints)
    {
        // curvePoints 有6个点，取中间4个(索引1-4)，Y从266倒转为0-100
        var result = new byte[8];
        for (int i = 0; i < 4 && i + 1 < curvePoints.Count; i++)
        {
            var y = Math.Max(0, Math.Min(266, curvePoints[i + 1].Y));
            var x = Math.Max(0, Math.Min(345, curvePoints[i + 1].X));
            var yPercent = (byte)Math.Round((266.0 - y) / 266.0 * 100.0);
            var xPercent = (byte)Math.Round(x / 345.0 * 100.0);
            result[i * 2] = yPercent;
            result[i * 2 + 1] = xPercent;
        }
        return result;
    }

    /// <summary>构建并发送踏板参数命令到已连接的踏板设备</summary>
    private void SendPedalParameters()
    {
        // 通过基座连接的踏板使用基座设备作为通信目标
        var targetDevice = _connectedPedalDevice ?? (_isPedalViaBase ? _baseDevice : null);
        if (targetDevice == null || _isSendingParameters || _isApplyingParameters)
            return;

        // 用户主动修改参数时标记已更改
        OnParameterModified();

        try
        {
            _isSendingParameters = true;

            var clutchDir = (byte)(ClutchReverseToggle?.IsChecked == true ? 1 : 0);
            var brakeDir = (byte)(BrakeReverseToggle?.IsChecked == true ? 1 : 0);
            var throttleDir = (byte)(ThrottleReverseToggle?.IsChecked == true ? 1 : 0);

            var clutchPoints = GetCurvePointsAsProtocolBytes(_clutchCurvePoints);
            var brakePoints = GetCurvePointsAsProtocolBytes(_brakeCurvePoints);
            var throttlePoints = GetCurvePointsAsProtocolBytes(_throttleCurvePoints);

            var cmd = DeviceProtocolService.BuildSetPedalParametersCommand(
                clutchDir, clutchPoints, (byte)Math.Round(_clutchDeadZoneLeft), (byte)Math.Round(_clutchDeadZoneRight),
                brakeDir, brakePoints, (byte)Math.Round(_brakeDeadZoneLeft), (byte)Math.Round(_brakeDeadZoneRight),
                throttleDir, throttlePoints, (byte)Math.Round(_throttleDeadZoneLeft), (byte)Math.Round(_throttleDeadZoneRight));

            App.UsbManager?.SendToDevice(targetDevice.DeviceKey, cmd);
            Debug.WriteLine($"[PedalControl] 踏板参数已发送到 {targetDevice.DeviceKey}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PedalControl] 发送踏板参数异常: {ex.Message}");
        }
        finally
        {
            _isSendingParameters = false;
        }
    }

    /// <summary>轴反向开关变更处理</summary>
    private void AxisReverseToggle_Changed(object sender, RoutedEventArgs e)
    {
        SendPedalParameters();
    }

    // ════════════════════════════════════════════════════════════════
    //  HID 实时数据更新
    // ════════════════════════════════════════════════════════════════

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
        if (descriptor == null || descriptor.DeviceType != DeviceType.Pedal)
            return;
        // 更新模式由 MainWindow 统一处理，参数页面忽略
        if (descriptor.IsUpdateMode(device.Vid, device.Pid))
            return;

        Debug.WriteLine($"[PedalControl] 踏板串口设备已连接: {device.DeviceKey}");
        await Application.Current.Dispatcher.InvokeAsync(async () => await RefreshDeviceInfoAsync());
    }

    private void OnUsbDeviceDisconnected(UsbDeviceInfo device)
    {
        if (_connectedPedalDevice == null)
            return;

        // 检查断开的是否为当前连接的踏板设备
        var descriptor = DeviceRegistry.FindByVidPid(device.Vid, device.Pid);
        if (descriptor == null || descriptor.DeviceType != DeviceType.Pedal)
            return;

        Debug.WriteLine($"[PedalControl] 踏板串口设备已断开: {device.DeviceKey}");
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
        App.HidService.PedalDataReceived -= OnPedalDataReceived;
        App.HidService.PedalDataReceived += OnPedalDataReceived;
    }

    private void UnsubscribeHidData()
    {
        if (App.HidService == null) return;
        App.HidService.PedalDataReceived -= OnPedalDataReceived;
    }

    /// <summary>
    /// 将最新的 HID 缓存值强制刷新到 UI（绕过防抖/去重检查）。
    /// 用于界面重新加载时立刻显示正确的踏板位置。
    /// </summary>
    private void ForceRefreshPedalDisplay()
    {
        var rawClutch = _latestRawClutch;
        var rawBrake = _latestRawBrake;
        var rawGas = _latestRawGas;
        var pClutch = _latestProcessedClutch;
        var pBrake = _latestProcessedBrake;
        var pGas = _latestProcessedGas;

        _displayedRawClutch = rawClutch;
        _displayedRawBrake = rawBrake;
        _displayedRawGas = rawGas;
        _displayedProcessedClutch = pClutch;
        _displayedProcessedBrake = pBrake;
        _displayedProcessedGas = pGas;

        UpdatePedalPositionDisplay(rawClutch, pClutch, rawBrake, pBrake, rawGas, pGas);
    }

    private void OnPedalDataReceived(UsbDeviceInfo device, HidPedalData data)
    {
        if (_connectedPedalDevice == null || device.Vid != _connectedPedalDevice.Vid || device.Pid != _connectedPedalDevice.Pid)
            return;

        // 始终缓存最新原始值
        _latestRawClutch = data.ClutchPercent;
        _latestRawBrake = data.BrakePercent;
        _latestRawGas = data.GasPercent;

        // 使用预缓存的 Point[] 数组在后台线程完成曲线变换，避免跨线程访问 PointCollection
        _latestProcessedClutch = ApplyCurveTransform(_clutchCurvePointsCache, _clutchCurveSlopesCache, data.ClutchPercent);
        _latestProcessedBrake = ApplyCurveTransform(_brakeCurvePointsCache, _brakeCurveSlopesCache, data.BrakePercent);
        _latestProcessedGas = ApplyCurveTransform(_throttleCurvePointsCache, _throttleCurveSlopesCache, data.GasPercent);

        // Render 优先级 + 防抖：同一渲染帧内只入队一次回调
        if (Interlocked.Exchange(ref _pendingUiUpdate, 1) == 0)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
            {
                _pendingUiUpdate = 0;

                var rawClutch = _latestRawClutch;
                var rawBrake = _latestRawBrake;
                var rawGas = _latestRawGas;
                var pClutch = _latestProcessedClutch;
                var pBrake = _latestProcessedBrake;
                var pGas = _latestProcessedGas;

                // 跳过与上次已显示值相同的更新，避免无效 WPF 布局重排
                if (HasDisplayChanged(rawClutch, rawBrake, rawGas, pClutch, pBrake, pGas))
                {
                    _displayedRawClutch = rawClutch;
                    _displayedRawBrake = rawBrake;
                    _displayedRawGas = rawGas;
                    _displayedProcessedClutch = pClutch;
                    _displayedProcessedBrake = pBrake;
                    _displayedProcessedGas = pGas;
                    UpdatePedalPositionDisplay(rawClutch, pClutch, rawBrake, pBrake, rawGas, pGas);
                }
            });
        }
    }

    private bool HasDisplayChanged(double rc, double rb, double rg, double pc, double pb, double pg)
    {
        return Math.Abs(rc - _displayedRawClutch) > 0.05
            || Math.Abs(rb - _displayedRawBrake) > 0.05
            || Math.Abs(rg - _displayedRawGas) > 0.05
            || Math.Abs(pc - _displayedProcessedClutch) > 0.05
            || Math.Abs(pb - _displayedProcessedBrake) > 0.05
            || Math.Abs(pg - _displayedProcessedGas) > 0.05;
    }

    /// <summary>将踏板原始位置百分比通过曲线映射为处理后百分比</summary>
    private static double ApplyCurveTransform(PointCollection curvePoints, double positionPercent)
    {
        if (curvePoints == null || curvePoints.Count < 2)
            return positionPercent;

        var canvasX = positionPercent / 100.0 * 345.0;
        canvasX = Math.Max(0, Math.Min(345, canvasX));

        int n = curvePoints.Count;
        var slopes = ComputeMonotonicSlopes(curvePoints);

        if (canvasX <= curvePoints[0].X)
            return (266.0 - curvePoints[0].Y) / 266.0 * 100.0;
        if (canvasX >= curvePoints[n - 1].X)
            return (266.0 - curvePoints[n - 1].Y) / 266.0 * 100.0;

        for (int i = 0; i < n - 1; i++)
        {
            double x0 = curvePoints[i].X;
            double x1 = curvePoints[i + 1].X;
            if (canvasX < x0 || canvasX > x1)
                continue;

            double y0 = curvePoints[i].Y;
            double y1 = curvePoints[i + 1].Y;
            double dx = x1 - x0;
            if (dx < 1e-10) return positionPercent;

            double t = (canvasX - x0) / dx;
            double m0 = slopes[i] * dx;
            double m1 = slopes[i + 1] * dx;

            double t2 = t * t;
            double t3 = t2 * t;
            double y = (2 * t3 - 3 * t2 + 1) * y0
                     + (t3 - 2 * t2 + t) * m0
                     + (-2 * t3 + 3 * t2) * y1
                     + (t3 - t2) * m1;

            return (266.0 - y) / 266.0 * 100.0;
        }

        return positionPercent;
    }

    /// <summary>用 Point[] 缓存做曲线变换，可从任何线程安全调用</summary>
    private static double ApplyCurveTransform(Point[] points, double[] slopes, double positionPercent)
    {
        if (points == null || points.Length < 2)
            return positionPercent;

        var canvasX = positionPercent / 100.0 * 345.0;
        canvasX = Math.Max(0, Math.Min(345, canvasX));

        int n = points.Length;

        if (canvasX <= points[0].X)
            return (266.0 - points[0].Y) / 266.0 * 100.0;
        if (canvasX >= points[n - 1].X)
            return (266.0 - points[n - 1].Y) / 266.0 * 100.0;

        for (int i = 0; i < n - 1; i++)
        {
            double x0 = points[i].X;
            double x1 = points[i + 1].X;
            if (canvasX < x0 || canvasX > x1)
                continue;

            double y0 = points[i].Y;
            double y1 = points[i + 1].Y;
            double dx = x1 - x0;
            if (dx < 1e-10) return positionPercent;

            double t = (canvasX - x0) / dx;
            double m0 = slopes[i] * dx;
            double m1 = slopes[i + 1] * dx;

            double t2 = t * t;
            double t3 = t2 * t;
            double y = (2 * t3 - 3 * t2 + 1) * y0
                     + (t3 - 2 * t2 + t) * m0
                     + (-2 * t3 + 3 * t2) * y1
                     + (t3 - t2) * m1;

            return (266.0 - y) / 266.0 * 100.0;
        }

        return positionPercent;
    }

    /// <summary>从 PointCollection 构建值类型数组缓存并预计算斜率，供后台线程安全访问</summary>
    private static void BuildCurveCache(PointCollection source, ref Point[] pointsCache, ref double[] slopesCache)
    {
        var arr = new Point[source.Count];
        source.CopyTo(arr, 0);
        var slopes = ComputeMonotonicSlopes(arr);
        pointsCache = arr;
        slopesCache = slopes;
    }

    /// <summary>Fritsch-Carlson 单调三次样条斜率计算（Point[] 版）</summary>
    private static double[] ComputeMonotonicSlopes(Point[] points)
    {
        int n = points.Length;
        double[] m = new double[n];
        double[] delta = new double[n - 1];

        for (int i = 0; i < n - 1; i++)
        {
            double dx = points[i + 1].X - points[i].X;
            delta[i] = dx != 0 ? (points[i + 1].Y - points[i].Y) / dx : 0;
        }

        for (int i = 1; i < n - 1; i++)
        {
            if (delta[i - 1] * delta[i] <= 0)
                m[i] = 0;
            else
            {
                double sum = delta[i - 1] + delta[i];
                m[i] = sum != 0 ? 2.0 * delta[i - 1] * delta[i] / sum : 0;
            }
        }

        m[0] = delta[0];
        m[n - 1] = delta[n - 2];

        for (int i = 0; i < n - 1; i++)
        {
            if (Math.Abs(delta[i]) < 1e-10)
            {
                m[i] = 0;
                m[i + 1] = 0;
            }
            else
            {
                double alpha = m[i] / delta[i];
                double beta = m[i + 1] / delta[i];
                if (alpha < 0) alpha = 0;
                if (beta < 0) beta = 0;
                double sq = alpha * alpha + beta * beta;
                if (sq > 9.0)
                {
                    double tau = 3.0 / Math.Sqrt(sq);
                    m[i] = tau * alpha * delta[i];
                    m[i + 1] = tau * beta * delta[i];
                }
            }
        }

        return m;
    }

    /// <summary>更新所有曲线缓存（曲线点变化时调用）</summary>
    private void RebuildCurveCaches()
    {
        BuildCurveCache(_clutchCurvePoints, ref _clutchCurvePointsCache, ref _clutchCurveSlopesCache);
        BuildCurveCache(_brakeCurvePoints, ref _brakeCurvePointsCache, ref _brakeCurveSlopesCache);
        BuildCurveCache(_throttleCurvePoints, ref _throttleCurvePointsCache, ref _throttleCurveSlopesCache);
    }

    private void CalibrationButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow mainWindow) return;
        if (mainWindow.Content is not Panel rootPanel) return;

        if (_calibrationDialog == null)
        {
            _calibrationDialog = new CalibrationDialog();
            _calibrationDialog.CloseRequested += (_, _) =>
            {
                if (rootPanel.Children.Contains(_calibrationDialog))
                    rootPanel.Children.Remove(_calibrationDialog);
                _calibrationDialog = null;
            };
            _calibrationDialog.CompleteRequested += (_, _) =>
            {
                SendPedalCalibration(
                    DeviceProtocolService.CalibrationComplete,
                    DeviceProtocolService.CalibrationComplete,
                    DeviceProtocolService.CalibrationComplete);

                if (rootPanel.Children.Contains(_calibrationDialog))
                    rootPanel.Children.Remove(_calibrationDialog);
                _calibrationDialog = null;
            };
            _calibrationDialog.StartCalibrationRequested += (_, _) =>
            {
                SendPedalCalibration(
                    DeviceProtocolService.CalibrationStart,
                    DeviceProtocolService.CalibrationStart,
                    DeviceProtocolService.CalibrationStart);
            };
        }

        rootPanel.Children.Add(_calibrationDialog);
        _calibrationDialog.Show();
        e.Handled = true;
    }

    private void SendPedalCalibration(byte clutch, byte brake, byte throttle)
    {
        var targetDevice = _connectedPedalDevice ?? (_isPedalViaBase ? _baseDevice : null);
        if (targetDevice == null) return;

        var cmd = DeviceProtocolService.BuildPedalCalibrationCommand(clutch, brake, throttle);
        App.UsbManager?.SendToDevice(targetDevice.DeviceKey, cmd);
    }

    private void UpdatePedalPositionDisplay(
        double rawClutch, double processedClutch,
        double rawBrake, double processedBrake,
        double rawGas, double processedGas)
    {
        if (ClutchProgressGreen != null)
            ClutchProgressGreen.Width = new GridLength(processedClutch, GridUnitType.Star);
        if (ClutchProgressRed != null)
            ClutchProgressRed.Width = new GridLength(100 - processedClutch, GridUnitType.Star);

        if (ClutchProgressGreen2 != null)
            ClutchProgressGreen2.Width = new GridLength(rawClutch, GridUnitType.Star);
        if (ClutchProgressRed2 != null)
            ClutchProgressRed2.Width = new GridLength(100 - rawClutch, GridUnitType.Star);

        if (BrakeProgressGreen != null)
            BrakeProgressGreen.Width = new GridLength(processedBrake, GridUnitType.Star);
        if (BrakeProgressRed != null)
            BrakeProgressRed.Width = new GridLength(100 - processedBrake, GridUnitType.Star);

        if (BrakeProgressGreen2 != null)
            BrakeProgressGreen2.Width = new GridLength(rawBrake, GridUnitType.Star);
        if (BrakeProgressRed2 != null)
            BrakeProgressRed2.Width = new GridLength(100 - rawBrake, GridUnitType.Star);

        if (ThrottleProgressGreen != null)
            ThrottleProgressGreen.Width = new GridLength(processedGas, GridUnitType.Star);
        if (ThrottleProgressRed != null)
            ThrottleProgressRed.Width = new GridLength(100 - processedGas, GridUnitType.Star);

        if (ThrottleProgressGreen2 != null)
            ThrottleProgressGreen2.Width = new GridLength(rawGas, GridUnitType.Star);
        if (ThrottleProgressRed2 != null)
            ThrottleProgressRed2.Width = new GridLength(100 - rawGas, GridUnitType.Star);

        if (ClutchCurrentPosition != null)
            ClutchCurrentPosition.Text = $"{processedClutch:F0}%";
        if (BrakeCurrentPosition != null)
            BrakeCurrentPosition.Text = $"{processedBrake:F0}%";
        if (ThrottleCurrentPosition != null)
            ThrottleCurrentPosition.Text = $"{processedGas:F0}%";

        // 同步更新校准弹窗进度条
        if (_calibrationDialog != null)
        {
            _calibrationDialog.UpdateClutchProgress(rawClutch);
            _calibrationDialog.UpdateBrakeProgress(rawBrake);
            _calibrationDialog.UpdateThrottleProgress(rawGas);
        }
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

    private void CalibrationButton_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid) return;
        var w = grid.ActualWidth;
        if (w <= 0) return;
        CalibrationButtonBg.Width = w;
        CalibrationButtonBg.Data = Geometry.Parse($"M6,0 H{w} V29 L{w - 6},35 H0 V6 Z");
    }

    private string BuildDeviceDisplayName()
    {
        _deviceTypeName = LocalizationService.Instance["Status.DeviceTypePedal"];
        return string.IsNullOrEmpty(_deviceModel) ? _deviceTypeName : $"{_deviceTypeName} {_deviceModel}";
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null && DeviceModelName != null)
            DeviceModelName.Text = BuildDeviceDisplayName();
    }
}
