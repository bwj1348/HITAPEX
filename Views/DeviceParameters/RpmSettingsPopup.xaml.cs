using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HITAPEX.Views.DeviceParameters;

/// <summary>
/// 转速灯设置弹窗 (RPM LED Settings Popup)。
/// 用于配置设备端 12 颗 LED 转速灯条的完整参数，
/// 包括每颗灯的颜色、触发 RPM 阈值、爆闪模式、灯光模式、曲线类型等。
/// 弹窗通过预设快照加载已有配置，用户调整完毕后通过公共 Getter 方法回读参数写入设备。
/// 顶部 12 个色块 + 12 个滑块为一对一映射，右侧面板提供颜色选择、爆闪/恒亮参数控制。
/// </summary>
public partial class RpmSettingsPopup : UserControl
{
    /// <summary>用户点击"确认"按钮时触发</summary>
    public event EventHandler? Confirmed;
    /// <summary>用户点击"取消"按钮时触发</summary>
    public event EventHandler? Cancelled;

    public RpmSettingsPopup()
    {
        InitializeComponent();
        // 在控件加载完成后初始化盖子线位置、滑块渐变填充及右侧遮罩状态
        Loaded += (_, _) =>
        {
            // 初始化爆闪 cap 线（顶部虚线 + 三角拖拽手柄 + 百分比文字）
            UpdateCapLinePosition();
            // Canvas 尺寸变化时重新定位 cap 线
            CapLineCanvas.SizeChanged += (_, _) => UpdateCapLinePosition();
            // 初始化两个速度滑块的自定义渐变填充
            UpdateSpeedSliderFill(RpmSpeedSlider1);
            UpdateSpeedSliderFill(RpmSpeedSlider2);
            // 遥测开关切换时更新右侧面板控件遮罩状态
            RpmTelemetryToggle.Checked += (_, _) => UpdateRightSideMaskedControls();
            RpmTelemetryToggle.Unchecked += (_, _) => UpdateRightSideMaskedControls();
            UpdateRightSideMaskedControls();
        };
    }

    // ══════════════════════════════════════════
    //  可见性控制：显示 / 隐藏弹窗（带动画）
    // ══════════════════════════════════════════

    /// <summary>显示弹窗并播放入场动画</summary>
    public void Show()
    {
        Visibility = Visibility.Visible;
        AnimateIn();
        // 显示时确保 cap 线位置与当前 _capValue 同步
        UpdateCapLinePosition();
    }

    /// <summary>播放出场动画，动画结束后隐藏弹窗</summary>
    public void Hide()
    {
        AnimateOut(() => Visibility = Visibility.Collapsed);
    }

    // ══════════════════════════════════════════
    //  确认 / 取消按钮处理
    // ══════════════════════════════════════════

    /// <summary>确认按钮：触发 Confirmed 事件并隐藏弹窗</summary>
    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    /// <summary>取消按钮：触发 Cancelled 事件并隐藏弹窗</summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    // ══════════════════════════════════════════
    //  入场 / 出场动画
    // ══════════════════════════════════════════

    /// <summary>
    /// 播放入场动画：遮罩淡入 + 弹窗面板从 94% 缩放还原至 100% 并淡入。
    /// 动画期间使用 BitmapCache 优化渲染性能，完成后移除缓存以恢复交互。
    /// </summary>
    private void AnimateIn()
    {
        // 设置初始状态：完全透明 + 94% 缩放
        OverlayBackground.Opacity = 0;
        PopupPanel.Opacity = 0;
        PopupPanel.RenderTransform = new ScaleTransform(0.94, 0.94,
            PopupPanel.Width / 2, PopupPanel.Height / 2);
        // 动画期间缓存为位图以提高性能
        PopupPanel.CacheMode = new BitmapCache();
        PopupPanel.IsHitTestVisible = false;

        // 遮罩淡入 180ms
        var overlayFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        // 面板淡入 220ms
        var panelFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        // X 轴从 94% 缩放到 100%，260ms
        var scaleX = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        // Y 轴从 94% 缩放到 100%，260ms
        var scaleY = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // 动画完成后清除位图缓存，恢复命中测试
        scaleX.Completed += (_, _) =>
        {
            PopupPanel.CacheMode = null;
            PopupPanel.IsHitTestVisible = true;
        };

        // 同时启动四个动画
        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        PopupPanel.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        PopupPanel.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }

    /// <summary>
    /// 播放出场动画：遮罩淡出 + 弹窗面板缩放至 94% 并淡出。
    /// 动画完成后回调 onCompleted 执行清理（隐藏弹窗）。
    /// </summary>
    private void AnimateOut(Action onCompleted)
    {
        // 确保 RenderTransform 是 ScaleTransform；若已丢失则重建
        if (PopupPanel.RenderTransform is not ScaleTransform st)
            PopupPanel.RenderTransform = st = new ScaleTransform(1, 1,
                PopupPanel.Width / 2, PopupPanel.Height / 2);

        // 动画期间缓存为位图，禁止交互
        PopupPanel.CacheMode = new BitmapCache();
        PopupPanel.IsHitTestVisible = false;

        // 遮罩淡出 160ms
        var overlayFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        // 面板淡出 180ms
        var panelFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        // X 轴从 100% 缩放到 94%，240ms
        var scaleX = new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        // Y 轴从 100% 缩放到 94%，240ms
        var scaleY = new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        // 面板淡出完成后清理缓存并执行回调
        panelFade.Completed += (_, _) =>
        {
            PopupPanel.CacheMode = null;
            onCompleted();
        };

        // 同时启动四个动画
        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }

    // ══════════════════════════════════════════
    //  左侧 12 个色块选择逻辑
    // ══════════════════════════════════════════

    /// <summary>当前选中的左侧色块索引 (1-12)，默认为第 1 个</summary>
    private int _activeLeftBlockIndex = 1;

    /// <summary>
    /// 左侧 12 个色块 (RadioButton) 的 Checked 事件处理。
    /// 记录当前选中色块索引，并根据其当前颜色同步右侧灯光色板与爆闪色板的选中状态。
    /// </summary>
    private void LeftColorBlock_Checked(object sender, RoutedEventArgs e)
    {
        // 根据 sender 更新当前选中色块索引
        if (sender == ColorBlock1) _activeLeftBlockIndex = 1;
        else if (sender == ColorBlock2) _activeLeftBlockIndex = 2;
        else if (sender == ColorBlock3) _activeLeftBlockIndex = 3;
        else if (sender == ColorBlock4) _activeLeftBlockIndex = 4;
        else if (sender == ColorBlock5) _activeLeftBlockIndex = 5;
        else if (sender == ColorBlock6) _activeLeftBlockIndex = 6;
        else if (sender == ColorBlock7) _activeLeftBlockIndex = 7;
        else if (sender == ColorBlock8) _activeLeftBlockIndex = 8;
        else if (sender == ColorBlock9) _activeLeftBlockIndex = 9;
        else if (sender == ColorBlock10) _activeLeftBlockIndex = 10;
        else if (sender == ColorBlock11) _activeLeftBlockIndex = 11;
        else if (sender == ColorBlock12) _activeLeftBlockIndex = 12;

        // 根据当前选中色块的实际颜色同步右侧灯光色板
        if (sender is RadioButton rb && rb.Background is SolidColorBrush brush)
        {
            var colorIdx = ColorToIndex(brush.Color);
            var lightColors = GetLightColorRadios();
            if (colorIdx >= 0 && colorIdx < lightColors.Length && lightColors[colorIdx] != null)
                lightColors[colorIdx].IsChecked = true;
        }

        // 同步统一的爆闪颜色到右侧爆闪色板
        var scIdx = Math.Clamp(_strobeColor, 0, 7);
        var strobeRadios = GetStrobeColorRadios();
        if (scIdx < strobeRadios.Length && strobeRadios[scIdx] != null)
            strobeRadios[scIdx].IsChecked = true;
    }

    /// <summary>获取右侧"灯光颜色"面板的 9 个颜色 RadioButton 引用</summary>
    private RadioButton?[] GetLightColorRadios() => new RadioButton?[]
        { RpmLightColor1, RpmLightColor2, RpmLightColor3, RpmLightColor4, RpmLightColor5,
          RpmLightColor6, RpmLightColor7, RpmLightColor8, RpmLightColor9 };

    /// <summary>获取右侧"爆闪颜色"面板的 8 个颜色 RadioButton 引用</summary>
    private RadioButton?[] GetStrobeColorRadios() => new RadioButton?[]
        { StrobeColor1, StrobeColor2, StrobeColor3, StrobeColor4, StrobeColor5,
          StrobeColor6, StrobeColor7, StrobeColor8 };

    /// <summary>
    /// 右侧灯光颜色 RadioButton 的 Checked 事件处理。
    /// 将选中颜色应用到当前左侧色块，并更新对应滑块的渐变背景与前景色。
    /// </summary>
    private void RightLightColor_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;

        // 提取颜色：优先从 Background 获取纯色画刷，否则默认为"无"(深灰)
        Color color;
        if (rb.Background is SolidColorBrush brush)
            color = brush.Color;
        else
            color = Color.FromRgb(0x37, 0x37, 0x37);

        // 根据当前选中色块索引，将颜色应用到对应的左侧色块
        var leftBlock = _activeLeftBlockIndex switch
        {
            1 => ColorBlock1, 2 => ColorBlock2, 3 => ColorBlock3, 4 => ColorBlock4,
            5 => ColorBlock5, 6 => ColorBlock6, 7 => ColorBlock7, 8 => ColorBlock8,
            9 => ColorBlock9, 10 => ColorBlock10, 11 => ColorBlock11, 12 => ColorBlock12,
            _ => null
        };
        if (leftBlock != null)
            leftBlock.Background = new SolidColorBrush(color);

        // 同步更新对应滑块的渐变底色和前景指示色
        var slider = _activeLeftBlockIndex switch
        {
            1 => RpmSlider1, 2 => RpmSlider2, 3 => RpmSlider3, 4 => RpmSlider4,
            5 => RpmSlider5, 6 => RpmSlider6, 7 => RpmSlider7, 8 => RpmSlider8,
            9 => RpmSlider9, 10 => RpmSlider10, 11 => RpmSlider11, 12 => RpmSlider12,
            _ => null
        };
        if (slider != null)
        {
            // 滑块的 Background 为自绘渐变底色，Foreground 为滑块圆点颜色
            slider.Background = CreateGradient(color);
            slider.Foreground = new SolidColorBrush(color);
        }
    }

    /// <summary>
    /// 根据给定颜色创建垂直线性渐变的滑块底色画刷。
    /// 上端为亮色 (bright)，下端为对应暗色 (dark)——每种颜色有预定义的暗色映射。
    /// 未知颜色默认为暗红色。
    /// </summary>
    public static LinearGradientBrush CreateGradient(Color color)
    {
        var bright = color;
        // 根据亮色查表得到对应暗色，用于滑块的渐变底色
        var dark = color switch
        {
            _ when ColorsEqual(color, 0xC6, 0x0E, 0x0E) => Color.FromRgb(0x60, 0x07, 0x07),
            _ when ColorsEqual(color, 0xFF, 0x6A, 0x00) => Color.FromRgb(0x99, 0x40, 0x00),
            _ when ColorsEqual(color, 0xFF, 0xC8, 0x00) => Color.FromRgb(0x99, 0x78, 0x00),
            _ when ColorsEqual(color, 0x16, 0xC6, 0x42) => Color.FromRgb(0x0A, 0x60, 0x20),
            _ when ColorsEqual(color, 0x28, 0xF9, 0xDD) => Color.FromRgb(0x18, 0x93, 0x82),
            _ when ColorsEqual(color, 0x28, 0x40, 0xF9) => Color.FromRgb(0x18, 0x26, 0x93),
            _ when ColorsEqual(color, 0xC1, 0x28, 0xF9) => Color.FromRgb(0x72, 0x18, 0x93),
            _ when ColorsEqual(color, 0xEE, 0xEE, 0xEE) => Color.FromRgb(0x88, 0x88, 0x88),
            _ when ColorsEqual(color, 0x37, 0x37, 0x37) => Color.FromRgb(0x1B, 0x1B, 0x1B),
            _ => Color.FromRgb(0x60, 0x07, 0x07),
        };

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        brush.GradientStops.Add(new GradientStop(bright, 0));
        brush.GradientStops.Add(new GradientStop(dark, 1));
        return brush;
    }

    /// <summary>颜色匹配工具方法：判断 Color 是否与给定的 RGB 分量的颜色相同</summary>
    private static bool ColorsEqual(Color c, byte r, byte g, byte b)
        => c.R == r && c.G == g && c.B == b;

    // ══════════════════════════════════════════
    //  速度滑块渐变填充逻辑
    // ══════════════════════════════════════════

    /// <summary>
    /// 速度滑块的 6 个档位 (0~5) 对应的渐变断点偏移量。
    /// 用于控制 TrackFillBrush 中亮色区域覆盖比例，模拟进度条效果。
    /// </summary>
    private static readonly double[] SpeedStepOffsets = { 0, 0.2063, 0.4091, 0.6084, 0.8112, 1.0 };

    /// <summary>
    /// 更新速度滑块的渐变填充进度 (TrackFillBrush)。
    /// 滑块模板中的 LinearGradientBrush 有 4 个 GradientStop：
    ///   [0] 起始透明 -> [1][2] 亮色断点 -> [3] 终点暗色
    /// 根据当前滑块档位移动亮色断点位置，模拟滚动进度条效果。
    /// 到达最大值时将所有断点推到末端使滑块显示为全亮。
    /// </summary>
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

    /// <summary>速度滑块值变化时刷新渐变填充</summary>
    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider)
            UpdateSpeedSliderFill(slider);
    }

    // ══════════════════════════════════════════
    //  爆闪 cap 线：拖拽 + 滑块截断逻辑
    // ══════════════════════════════════════════

    /// <summary>当前爆闪 cap 百分比值 (0-100)，用于截断所有 12 路转速滑块上限</summary>
    private double _capValue = 100;
    /// <summary>用户正在拖拽 cap 三角手柄的标志</summary>
    private bool _isDraggingCap;
    /// <summary>防止 ValueChanged 回调中重复触发截断的标志</summary>
    private bool _isClamping;

    /// <summary>获取全部 12 个转速滑块的引用数组</summary>
    private Slider[] AllRpmSliders => new[]
    {
        RpmSlider1, RpmSlider2, RpmSlider3, RpmSlider4, RpmSlider5, RpmSlider6,
        RpmSlider7, RpmSlider8, RpmSlider9, RpmSlider10, RpmSlider11, RpmSlider12
    };

    /// <summary>获取 12 个滑块中的最大值</summary>
    private double GetMaxSliderValue()
    {
        return AllRpmSliders.Max(s => s.Value);
    }

    /// <summary>
    /// 更新爆闪 cap 线在 Canvas 上的位置。
    /// Y 坐标随 _capValue 增大而减小 (顶部=100, 底部=0)，
    /// Canvas 总高度为 315 像素。同时更新虚线位置、三角拖拽手柄位置和百分比文字。
    /// </summary>
    private void UpdateCapLinePosition()
    {
        if (CapLineCanvas == null || CapDashedLine == null
            || CapTriangle == null || CapPercentLabel == null) return;

        // Y = 总高度 * (1 - 百分比/100)，即百分比越大线越靠上
        var y = 315 * (1 - _capValue / 100);
        // 虚线的左右边界，Canvas 宽约 577
        const double lineX1 = 26;
        const double lineWidth = 525;
        var lineX2 = lineX1 + lineWidth;

        // 更新虚线位置（水平线）
        CapDashedLine.X1 = lineX1;
        CapDashedLine.X2 = lineX2;
        CapDashedLine.Y1 = y;
        CapDashedLine.Y2 = y;

        // 三角拖拽手柄位于虚线右侧 +6 像素偏移
        Canvas.SetLeft(CapTriangle, lineX2 + 6);
        Canvas.SetTop(CapTriangle, y - 11);

        // 百分比文字标签位于左侧，高度偏移 -7 像素以垂直居中

        Canvas.SetLeft(CapPercentLabel, 0);
        Canvas.SetTop(CapPercentLabel, y - 7);
        CapPercentLabel.Text = $"{_capValue:F0}";
    }

    /// <summary>按下三角拖拽手柄时开始拖拽 cap 线</summary>
    private void CapTriangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingCap = true;
        // 捕获鼠标，确保拖拽过程中即使鼠标移出控件也能收到 MouseMove 事件
        CapTriangle.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// 拖拽 cap 三角手柄时实时更新 cap 线位置。
    /// 核心逻辑：Y 坐标越小 cap 值越大。
    /// 拖拽过程中会同时将超过 cap 值的滑块截断到 cap 值，保证约束一致。
    /// </summary>
    private void CapTriangle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingCap) return;

        var pos = e.GetPosition(CapLineCanvas);
        // Y 坐标限制在 Canvas 高度范围内 (0~315)
        var y = Math.Clamp(pos.Y, 0, 315);
        // 步长设为 1，确保百分比为整数
        var newCap = Math.Round((1 - y / 315) * 100);

        // cap 值不能低于当前滑块的最大值，确保截断线与实际数据一致
        var maxSlider = GetMaxSliderValue();
        _capValue = Math.Clamp(newCap, maxSlider, 100);

        // 更新所有 cap 线相关 UI 元素位置
        UpdateCapLinePosition();

        // 将超过 cap 值的所有滑块强制截断到 cap
        foreach (var slider in AllRpmSliders)
        {
            if (slider.Value > _capValue)
                slider.Value = _capValue;
        }
    }

    /// <summary>释放三角手柄鼠标捕获，结束拖拽</summary>
    private void CapTriangle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingCap = false;
        CapTriangle.ReleaseMouseCapture();
    }

    /// <summary>
    /// 12 路转速滑块值变化处理。
    /// 当滑块值超过 cap 值时自动截断到 cap，同时使用 _isClamping 防止重复触发。
    /// _capValue == 0 表示未设置爆闪触发值，不进行截断。
    /// 拖拽 cap 手柄期间忽略滑块变化，避免相互干扰。
    /// </summary>
    private void RpmSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 拖拽 cap 手柄时或正在执行截断操作时跳过
        if (_isDraggingCap || _isClamping) return;

        // _capValue == 0 表示爆闪触发值未配置，不进行截断
        if (_capValue > 0 && e.NewValue > _capValue)
        {
            _isClamping = true;
            ((Slider)sender).Value = _capValue;
            _isClamping = false;
            return;
        }

        // 滑块值变化后可能需要重新绘制 cap 线（因为滑块最大值可能变了）
        UpdateCapLinePosition();
    }

    // ══════════════════════════════════════════
    //  爆闪模式处理
    // ══════════════════════════════════════════

    /// <summary>爆闪模式：0=与转速灯颜色一致, 1=自定义, 2=关灯</summary>
    private int _strobeMode; // 0=与转速灯颜色一致, 1=自定义, 2=关灯
    /// <summary>12 灯统一的爆闪颜色索引 (0=红~7=白)</summary>
    private int _strobeColor; // 12灯统一的爆闪颜色索引 (0=红~7=白)

    /// <summary>爆闪模式下拉框选择变化处理，切换后刷新右侧面板遮罩</summary>
    private void StrobeModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _strobeMode = StrobeModeComboBox.SelectedIndex;
        UpdateRightSideMaskedControls();
    }

    /// <summary>
    /// 根据遥测开关状态和爆闪模式更新右侧面板控件的启用/禁用状态。
    /// 控制逻辑：
    ///   - 遥测关闭 或 爆闪模式为"与转速灯一致"或"关灯"时：清空爆闪颜色选中
    ///   - 遥测关闭 或 爆闪模式为"关灯"时：爆闪速度滑块强制归零
    /// 同时按 _strobeColor 同步爆闪色板选中状态。
    /// </summary>
    private void UpdateRightSideMaskedControls()
    {
        if (StrobeColor1 == null || RpmSpeedSlider1 == null) return;

        var telemetryOff = RpmTelemetryToggle.IsChecked != true;
        var strobeMode = StrobeModeComboBox.SelectedIndex;

        var strobeColorMasked = telemetryOff || strobeMode == 0 || strobeMode == 2;
        if (strobeColorMasked)
        {
            StrobeColor1.IsChecked = false;
            StrobeColor2.IsChecked = false;
            StrobeColor3.IsChecked = false;
            StrobeColor4.IsChecked = false;
            StrobeColor5.IsChecked = false;
            StrobeColor6.IsChecked = false;
            StrobeColor7.IsChecked = false;
            StrobeColor8.IsChecked = false;
        }
        else
        {
            // 按统一爆闪颜色选中对应色板
            var colorIdx = Math.Clamp(_strobeColor, 0, 7);
            var radios = GetStrobeColorRadios();
            if (colorIdx < radios.Length && radios[colorIdx] != null)
                radios[colorIdx].IsChecked = true;
        }

        var speedMasked = telemetryOff || strobeMode == 2;
        if (speedMasked)
            RpmSpeedSlider1.Value = 0;
    }

    /// <summary>
    /// 基础灯光模式下拉框切换处理。
    /// "恒亮"(索引 0)时不需要速度控制，强制将底层灯光速度滑块归零。
    /// </summary>
    private void BaseLightModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RpmSpeedSlider2 == null) return;
        // 仅在切换到"恒亮"时强制设为 0，其他模式保持已加载的预设值
        if (BaseLightModeComboBox.SelectedIndex == 0)
            RpmSpeedSlider2.Value = 0;
    }

    /// <summary>爆闪颜色 RadioButton Checked 处理：从选中颜色提取颜色索引写入 _strobeColor</summary>
    private void StrobeColor_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;

        if (rb.Background is SolidColorBrush brush)
            _strobeColor = ColorToIndex(brush.Color);
        else
            _strobeColor = 0;
    }

    // ══════════════════════════════════════════
    //  预设绑定：加载 / 读取转速灯参数
    // ══════════════════════════════════════════

    /// <summary>
    /// RPM 内置颜色映射表 (9 色)。
    /// 索引 0=红, 1=橙, 2=黄, 3=绿, 4=青, 5=蓝, 6=紫, 7=白, 8=无(深灰/关灯)。
    /// 存储在设备中的颜色值即为此表中的索引。
    /// </summary>
    private static readonly Color[] RpmColorMap =
    {
        Color.FromRgb(0xC6, 0x0E, 0x0E), // 0: 红
        Color.FromRgb(0xFF, 0x6A, 0x00), // 1: 橙
        Color.FromRgb(0xFF, 0xC8, 0x00), // 2: 黄
        Color.FromRgb(0x16, 0xC6, 0x42), // 3: 绿
        Color.FromRgb(0x28, 0xF9, 0xDD), // 4: 青
        Color.FromRgb(0x28, 0x40, 0xF9), // 5: 蓝
        Color.FromRgb(0xC1, 0x28, 0xF9), // 6: 紫
        Color.FromRgb(0xEE, 0xEE, 0xEE), // 7: 白
        Color.FromRgb(0x37, 0x37, 0x37), // 8: 无
    };

    /// <summary>
    /// 将 Color 值映射为 RpmColorMap 中的索引 (0~8)。
    /// 映射关系：0=红, 1=橙, 2=黄, 3=绿, 4=青, 5=蓝, 6=紫, 7=白, 8=无(深灰)。
    /// 未匹配到任何颜色时默认返回 0 (红色)。
    /// </summary>
    private static int ColorToIndex(Color c)
    {
        for (int i = 0; i < RpmColorMap.Length; i++)
            if (ColorsEqual(c, RpmColorMap[i].R, RpmColorMap[i].G, RpmColorMap[i].B))
                return i;
        return 0;
    }

    /// <summary>从预设快照加载所有转速灯参数到弹窗 UI</summary>
    public void LoadSettings(int[] rpmColors, double[] rpmValues, double rpmCapValue,
        int rpmCurveType, int rpmDisplayMode, int rpmLightMode, int rpmStrobeMode,
        int rpmStrobeColor, int rpmSpeed, int rpmBaseLightMode, int rpmBaseLightSpeed,
        bool rpmTelemetryEnabled)
    {
        var allBlocks = new RadioButton?[] { ColorBlock1, ColorBlock2, ColorBlock3, ColorBlock4,
            ColorBlock5, ColorBlock6, ColorBlock7, ColorBlock8, ColorBlock9, ColorBlock10,
            ColorBlock11, ColorBlock12 };
        var allSliders = AllRpmSliders;

        // ── 爆闪 cap 值必须在滑块之前设置 ──
        // 滑块的 ValueChanged 处理器会检查 e.NewValue > _capValue 并将超限值截断到 _capValue。
        // 若 _capValue 残留上一次的 0，所有非零滑块值会被截断为 0。
        _capValue = rpmCapValue;
        UpdateCapLinePosition();

        // 转速灯颜色 & 滑块值（12 灯逐一设置）
        for (int i = 0; i < 12; i++)
        {
            var color = RpmColorMap[Math.Clamp(rpmColors[i], 0, RpmColorMap.Length - 1)];
            if (allBlocks[i] != null)
            {
                allBlocks[i]!.Background = new SolidColorBrush(color);
            }
            if (allSliders[i] != null)
            {
                allSliders[i].Value = rpmValues[i];
                allSliders[i].Background = CreateGradient(color);
                allSliders[i].Foreground = new SolidColorBrush(color);
            }
        }

        // 曲线类型 (对应 XAML 中四个曲线 RadioButton)
        switch (rpmCurveType)
        {
            case 0: CurveLinearRadio!.IsChecked = true; break;
            case 1: CurveConvexRadio!.IsChecked = true; break;
            case 2: CurveConcaveRadio!.IsChecked = true; break;
            case 3: CurveCustomRadio!.IsChecked = true; break;
        }

        // 显示模式 (暂时禁用)
        _ = rpmDisplayMode; // 暂时保留参数，后续可恢复
        // if (rpmDisplayMode == 0)
        //     PercentModeRadio!.IsChecked = true;
        // else
        //     RpmModeRadio!.IsChecked = true;

        // 右侧面板参数：依次恢复遥测开关、灯光模式下拉框、爆闪模式下拉框
        if (RpmTelemetryToggle != null)
            RpmTelemetryToggle.IsChecked = rpmTelemetryEnabled;

        if (RpmLightModeCombo != null)
            RpmLightModeCombo.SelectedIndex = rpmLightMode;

        if (StrobeModeComboBox != null)
            StrobeModeComboBox.SelectedIndex = rpmStrobeMode;

        // 爆闪速度滑块 (档位 0~5)
        if (RpmSpeedSlider1 != null)
            RpmSpeedSlider1.Value = rpmSpeed;

        // 底层灯光模式下拉框
        if (BaseLightModeComboBox != null)
            BaseLightModeComboBox.SelectedIndex = rpmBaseLightMode;

        // 底层灯光速度滑块 (档位 0~5)
        if (RpmSpeedSlider2 != null)
            RpmSpeedSlider2.Value = rpmBaseLightSpeed;

        // 参数加载完毕后统一刷新右侧遮罩状态
        UpdateRightSideMaskedControls();

        // 爆闪颜色 — 必须在 UpdateRightSideMaskedControls() 之后设置
        _strobeColor = Math.Clamp(rpmStrobeColor, 0, 7);

        // 同步当前选中块的右侧颜色选中状态
        if (_activeLeftBlockIndex >= 1 && _activeLeftBlockIndex <= 12)
        {
            var block = allBlocks[_activeLeftBlockIndex - 1];
            if (block?.Background is SolidColorBrush blockBrush)
            {
                var colorIdx = ColorToIndex(blockBrush.Color);
                var lightColors = GetLightColorRadios();
                if (colorIdx >= 0 && colorIdx < lightColors.Length && lightColors[colorIdx] != null)
                    lightColors[colorIdx].IsChecked = true;
            }
        }
    }

    /// <summary>获取 12 个转速灯的颜色索引</summary>
    public int[] GetRpmColors()
    {
        var allBlocks = new RadioButton?[] { ColorBlock1, ColorBlock2, ColorBlock3, ColorBlock4,
            ColorBlock5, ColorBlock6, ColorBlock7, ColorBlock8, ColorBlock9, ColorBlock10,
            ColorBlock11, ColorBlock12 };
        var result = new int[12];
        for (int i = 0; i < 12; i++)
        {
            if (allBlocks[i]?.Background is SolidColorBrush brush)
                result[i] = ColorToIndex(brush.Color);
        }
        return result;
    }

    /// <summary>获取 12 个转速灯滑块值</summary>
    public double[] GetRpmValues() => AllRpmSliders.Select(s => s.Value).ToArray();

    /// <summary>获取爆闪 cap 百分比</summary>
    public double GetRpmCapValue() => _capValue;

    /// <summary>获取曲线类型 (0=线性,1=外凸,2=内凹,3=自定义)</summary>
    public int GetRpmCurveType()
    {
        if (CurveLinearRadio?.IsChecked == true) return 0;
        if (CurveConvexRadio?.IsChecked == true) return 1;
        if (CurveConcaveRadio?.IsChecked == true) return 2;
        if (CurveCustomRadio?.IsChecked == true) return 3;
        return 0;
    }

    /// <summary>获取显示模式 (0=百分比,1=转速RPM) (暂时禁用)</summary>
    // public int GetRpmDisplayMode() => RpmModeRadio?.IsChecked == true ? 1 : 0;

    /// <summary>获取灯光模式 (0=序列,1=扩散,2=汇聚)</summary>
    public int GetRpmLightMode() => RpmLightModeCombo?.SelectedIndex ?? 0;

    /// <summary>获取爆闪模式 (0=与转速灯颜色一致,1=自定义,2=关灯)</summary>
    public int GetRpmStrobeMode() => StrobeModeComboBox?.SelectedIndex ?? 0;

    /// <summary>获取统一的爆闪颜色索引 (0=红~7=白)</summary>
    public int GetRpmStrobeColor() => Math.Clamp(_strobeColor, 0, 7);

    /// <summary>获取爆闪速度档位</summary>
    public int GetRpmSpeed() => (int)(RpmSpeedSlider1?.Value ?? 0);

    /// <summary>获取基础灯光模式 (0=恒亮,1=呼吸,2=彩色循环)</summary>
    public int GetRpmBaseLightMode() => BaseLightModeComboBox?.SelectedIndex ?? 0;

    /// <summary>获取基础灯光速度档位</summary>
    public int GetRpmBaseLightSpeed() => (int)(RpmSpeedSlider2?.Value ?? 0);

    /// <summary>获取遥测模式是否启用</summary>
    public bool GetRpmTelemetryEnabled() => RpmTelemetryToggle?.IsChecked == true;
}
