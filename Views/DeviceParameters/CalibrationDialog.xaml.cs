using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HITAPEX.Views.DeviceParameters;

/// <summary>
/// 踏板校准对话框用户控件，用于显示离合器、刹车和油门踏板的校准进度条。
/// 支持弹出/隐藏动画、开始校准和完成校准操作。
/// </summary>
public partial class CalibrationDialog : UserControl
{
    // ═══════════════════════════════════════════════════════════════════
    // 事件定义
    // ═══════════════════════════════════════════════════════════════════

    public event EventHandler? StartCalibrationRequested;
    public event EventHandler? CompleteRequested;
    public event EventHandler? CloseRequested;

    // ═══════════════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════════════

    public CalibrationDialog()
    {
        InitializeComponent();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 进度更新方法
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 更新离合器踏板校准进度。
    /// </summary>
    /// <param name="percentage">进度百分比，取值范围 0～100。</param>
    public void UpdateClutchProgress(double percentage)
    {
        // 将百分比钳制在 0～100 范围内，防止越界
        var clamped = Math.Clamp(percentage, 0, 100);
        // 使用 Star 比例分配：绿色区域占 clamped 份，红色区域占剩余份数
        // 例如 30% 时绿色列占 30★，红色列占 70★，总 100★
        ClutchProgressGreen.Width = new GridLength(clamped, GridUnitType.Star);
        ClutchProgressRed.Width = new GridLength(100 - clamped, GridUnitType.Star);
        // 显示百分比文本（保留整数）
        ClutchPercentText.Text = $"{clamped:F0}%";
    }

    /// <summary>
    /// 更新刹车踏板校准进度。
    /// </summary>
    /// <param name="percentage">进度百分比，取值范围 0～100。</param>
    public void UpdateBrakeProgress(double percentage)
    {
        var clamped = Math.Clamp(percentage, 0, 100);
        BrakeProgressGreen.Width = new GridLength(clamped, GridUnitType.Star);
        BrakeProgressRed.Width = new GridLength(100 - clamped, GridUnitType.Star);
        BrakePercentText.Text = $"{clamped:F0}%";
    }

    /// <summary>
    /// 更新油门踏板校准进度。
    /// </summary>
    /// <param name="percentage">进度百分比，取值范围 0～100。</param>
    public void UpdateThrottleProgress(double percentage)
    {
        var clamped = Math.Clamp(percentage, 0, 100);
        ThrottleProgressGreen.Width = new GridLength(clamped, GridUnitType.Star);
        ThrottleProgressRed.Width = new GridLength(100 - clamped, GridUnitType.Star);
        ThrottlePercentText.Text = $"{clamped:F0}%";
    }

    // ═══════════════════════════════════════════════════════════════════
    // 状态管理
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 将开始校准按钮设置为禁用状态（校准已开始）。
    /// </summary>
    private void SetStartButtonDisabled()
    {
        CompleteButton.IsEnabled = true;
        StartCalibrationButton.Cursor = Cursors.Arrow;
        StartButtonMask.Visibility = Visibility.Visible;
        InstructionText.Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x6F, 0x6F));
    }

    /// <summary>
    /// 重置对话框状态：恢复按钮可用性并将所有踏板进度清零。
    /// </summary>
    public void ResetState()
    {
        // 将完成按钮设为不可用，直到校准开始后才可点击
        CompleteButton.IsEnabled = false;
        StartCalibrationButton.Cursor = Cursors.Hand;
        StartButtonMask.Visibility = Visibility.Collapsed;
        InstructionText.Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));

        // 将所有踏板的进度条重置为 0%
        UpdateClutchProgress(0);
        UpdateBrakeProgress(0);
        UpdateThrottleProgress(0);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 按钮事件处理
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 点击开始校准按钮：禁用自身并触发 StartCalibrationRequested 事件。
    /// </summary>
    private void StartCalibrationButton_Click(object sender, MouseButtonEventArgs e)
    {
        SetStartButtonDisabled();
        StartCalibrationRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 点击完成按钮：触发 CompleteRequested 事件并隐藏对话框。
    /// </summary>
    private void CompleteButton_Click(object sender, RoutedEventArgs e)
    {
        CompleteRequested?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    /// <summary>
    /// 点击关闭按钮：触发 CloseRequested 事件并隐藏对话框。
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 显示/隐藏
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 显示对话框：先重置状态为初始值，再将控件设为可见并播放弹入动画。
    /// </summary>
    public void Show()
    {
        ResetState();
        Visibility = Visibility.Visible;
        AnimateIn();
    }

    /// <summary>
    /// 隐藏对话框：播放弹出动画，动画结束后将控件设为不可见。
    /// </summary>
    public void Hide()
    {
        AnimateOut(() => Visibility = Visibility.Collapsed);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 动画效果
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 弹入动画：遮罩层淡入 + 面板从 94% 缩放到 100% 同时淡入。
    /// 使用 CubicEase 缓动函数，动画时长 180～260ms。
    /// </summary>
    private void AnimateIn()
    {
        // 设置动画初始状态：遮罩和面板完全透明，面板缩小至 94%
        OverlayBackground.Opacity = 0;
        PopupPanel.Opacity = 0;
        // 以面板中心为缩放原点，从 94% 开始放大
        PopupPanel.RenderTransform = new ScaleTransform(0.94, 0.94,
            PopupPanel.Width / 2, PopupPanel.Height / 2);
        // 启用位图缓存以提升动画性能
        PopupPanel.CacheMode = new BitmapCache();
        // 动画期间禁用点击，防止用户误操作
        PopupPanel.IsHitTestVisible = false;

        // 遮罩层淡入动画：0 → 1，180ms，EaseOut 缓动
        var overlayFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        // 面板淡入动画：0 → 1，220ms，EaseOut 缓动
        var panelFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        // 水平缩放动画：0.94 → 1，260ms，EaseOut 缓动
        var scaleX = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        // 垂直缩放动画：0.94 → 1，260ms，EaseOut 缓动
        var scaleY = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // 缩放动画完成后：清除位图缓存以节省内存，恢复点击交互
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
    /// 弹出动画：遮罩层淡出 + 面板从 100% 缩小到 94% 同时淡出。
    /// 使用 CubicEase 缓动函数，动画时长 160～240ms。
    /// </summary>
    /// <param name="onCompleted">动画完全结束后的回调操作。</param>
    private void AnimateOut(Action onCompleted)
    {
        // 确保 RenderTransform 为 ScaleTransform 类型，否则重新创建
        if (PopupPanel.RenderTransform is not ScaleTransform st)
            PopupPanel.RenderTransform = st = new ScaleTransform(1, 1,
                PopupPanel.Width / 2, PopupPanel.Height / 2);

        // 启用位图缓存以提升动画性能，动画期间禁用点击
        PopupPanel.CacheMode = new BitmapCache();
        PopupPanel.IsHitTestVisible = false;

        // 遮罩层淡出动画：1 → 0，160ms，EaseIn 缓动
        var overlayFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        // 面板淡出动画：1 → 0，180ms，EaseIn 缓动
        var panelFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        // 水平缩放动画：1 → 0.94，240ms，EaseIn 缓动
        var scaleX = new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        // 垂直缩放动画：1 → 0.94，240ms，EaseIn 缓动
        var scaleY = new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        // 面板淡出完成后：清除缓存并执行回调（如设置 Visibility = Collapsed）
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

    // ═══════════════════════════════════════════════════════════════════
    // 布局适配
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 开始按钮尺寸变化时，动态重新生成按钮外形几何路径（带切角的异形按钮）。
    /// </summary>
    private void StartCalibrationButton_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Grid grid) return;
        var w = grid.ActualWidth;
        if (w <= 0) return;

        // 根据当前宽度生成带右上斜切角的几何形状
        // 路径：左上(6,0) → 右上(w,0) → 右上内(w,29) → 斜切点(w-6,35) → 左下(0,35) → 左上(0,6)
        var geom = Geometry.Parse($"M6,0 H{w} V29 L{w - 6},35 H0 V6 Z");
        StartButtonBg.Width = w;
        StartButtonBg.Data = geom;
        StartButtonMask.Width = w;
        StartButtonMask.Data = geom;
    }
}
