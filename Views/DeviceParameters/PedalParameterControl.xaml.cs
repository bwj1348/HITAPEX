using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HITAPEX.Views.DeviceParameters;

public partial class PedalParameterControl : UserControl
{
    // ────────── 离合器状态 ──────────
    private int _selectedCurveType = 1;
    private double _clutchDeadZoneLeft = 0;
    private double _clutchDeadZoneRight = 0;
    private bool _isClutchDraggingDeadZone = false;
    private string? _clutchDraggingDeadZoneThumb = null;
    private double _clutchCurrentPosition = 0;
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
    private double _brakeCurrentPosition = 0;
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
    private double _throttleCurrentPosition = 0;
    private PointCollection _throttleCurvePoints = new PointCollection
    {
        new Point(0, 266), new Point(69, 205), new Point(138, 148),
        new Point(207, 91), new Point(276, 42), new Point(345, 0)
    };
    private bool _isThrottleDragging = false;
    private Control? _throttleDraggingPoint = null;

    public PedalParameterControl()
    {
        InitializeComponent();
        Loaded += PedalParameterControl_Loaded;
    }

    private void PedalParameterControl_Loaded(object sender, RoutedEventArgs e)
    {
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
            v => { _clutchCurvePoints = v; ApplySmoothCurve(ClutchCurveLine, ClutchFillArea, _clutchCurvePoints); });
    }

    private void ClutchPoint_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndPointDrag(ref _isClutchDragging, ref _clutchDraggingPoint);
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
            v => { _brakeCurvePoints = v; ApplySmoothCurve(BrakeCurveLine, BrakeFillArea, _brakeCurvePoints); });
    }

    private void BrakePoint_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndPointDrag(ref _isBrakeDragging, ref _brakeDraggingPoint);
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
            v => { _throttleCurvePoints = v; ApplySmoothCurve(ThrottleCurveLine, ThrottleFillArea, _throttleCurvePoints); });
    }

    private void ThrottlePoint_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndPointDrag(ref _isThrottleDragging, ref _throttleDraggingPoint);
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

    private void CurveType1_Click(object sender, MouseButtonEventArgs e) { _selectedCurveType = 1; UpdateCurveTypeSelection(); }
    private void CurveType2_Click(object sender, MouseButtonEventArgs e) { _selectedCurveType = 2; UpdateCurveTypeSelection(); }
    private void CurveType3_Click(object sender, MouseButtonEventArgs e) { _selectedCurveType = 3; UpdateCurveTypeSelection(); }
    private void CurveType4_Click(object sender, MouseButtonEventArgs e) { _selectedCurveType = 4; UpdateCurveTypeSelection(); }
    private void CurveType5_Click(object sender, MouseButtonEventArgs e) { _selectedCurveType = 5; UpdateCurveTypeSelection(); }

    // ════════════════════════════════════════════════════════════════
    //  刹车 — 曲线类型点击
    // ════════════════════════════════════════════════════════════════

    private void BrakeCurveType1_Click(object sender, MouseButtonEventArgs e) { _brakeSelectedCurveType = 1; UpdateBrakeCurveTypeSelection(); }
    private void BrakeCurveType2_Click(object sender, MouseButtonEventArgs e) { _brakeSelectedCurveType = 2; UpdateBrakeCurveTypeSelection(); }
    private void BrakeCurveType3_Click(object sender, MouseButtonEventArgs e) { _brakeSelectedCurveType = 3; UpdateBrakeCurveTypeSelection(); }
    private void BrakeCurveType4_Click(object sender, MouseButtonEventArgs e) { _brakeSelectedCurveType = 4; UpdateBrakeCurveTypeSelection(); }
    private void BrakeCurveType5_Click(object sender, MouseButtonEventArgs e) { _brakeSelectedCurveType = 5; UpdateBrakeCurveTypeSelection(); }

    // ════════════════════════════════════════════════════════════════
    //  油门 — 曲线类型点击
    // ════════════════════════════════════════════════════════════════

    private void ThrottleCurveType1_Click(object sender, MouseButtonEventArgs e) { _throttleSelectedCurveType = 1; UpdateThrottleCurveTypeSelection(); }
    private void ThrottleCurveType2_Click(object sender, MouseButtonEventArgs e) { _throttleSelectedCurveType = 2; UpdateThrottleCurveTypeSelection(); }
    private void ThrottleCurveType3_Click(object sender, MouseButtonEventArgs e) { _throttleSelectedCurveType = 3; UpdateThrottleCurveTypeSelection(); }
    private void ThrottleCurveType4_Click(object sender, MouseButtonEventArgs e) { _throttleSelectedCurveType = 4; UpdateThrottleCurveTypeSelection(); }
    private void ThrottleCurveType5_Click(object sender, MouseButtonEventArgs e) { _throttleSelectedCurveType = 5; UpdateThrottleCurveTypeSelection(); }

    // ════════════════════════════════════════════════════════════════
    //  预设管理按钮
    // ════════════════════════════════════════════════════════════════

    private void UndoButton_Click(object sender, MouseButtonEventArgs e)
    {
        MessageBox.Show("撤回上一步操作", "撤回", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveButton_Click(object sender, MouseButtonEventArgs e)
    {
        MessageBox.Show("预设已保存", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveAsButton_Click(object sender, MouseButtonEventArgs e)
    {
        MessageBox.Show("请输入新的预设名称...", "另存为", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportButton_Click(object sender, MouseButtonEventArgs e)
    {
        MessageBox.Show("预设配置已导出", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PresetListButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is HITAPEX.MainWindow mainWindow)
        {
            mainWindow.ShowPresetListPopup();
        }
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
        var x = Math.Max(0, Math.Min(240, position.X));

        if (draggingThumb == "Left")
        {
            double thumbLeft = Math.Max(0, Math.Min(103, x - 5.5));
            deadZoneLeft = Math.Round(thumbLeft / 103.0 * 15.0, 1);
        }
        else
        {
            double thumbLeft = Math.Max(126, Math.Min(229, x - 5.5));
            deadZoneRight = Math.Round((229.0 - thumbLeft) / 103.0 * 15.0, 1);
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

        double leftProgressWidth = deadZoneLeft / 15.0 * 114.0;
        double leftThumbPos = deadZoneLeft / 15.0 * 103.0;
        leftProgress.Width = leftProgressWidth;
        Canvas.SetLeft(leftThumb, leftThumbPos);
        leftLabel.Text = $"{deadZoneLeft:F0}%";

        double rightProgressWidth = deadZoneRight / 15.0 * 114.0;
        double rightThumbPos = 229.0 - deadZoneRight / 15.0 * 103.0;
        rightProgress.Width = rightProgressWidth;
        Canvas.SetLeft(rightProgress, 240.0 - rightProgressWidth);
        Canvas.SetLeft(rightThumb, rightThumbPos);
        rightLabel.Text = $"{100.0 - deadZoneRight:F0}%";
    }
}
