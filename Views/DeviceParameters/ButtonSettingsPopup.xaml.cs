using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HITAPEX.Views.DeviceParameters;

/// <summary>
/// 按键设置弹窗控件，用于配置单个按键的 LED 灯颜色、遥测触发条件和闪烁速度。
/// 提供颜色选择、遥测功能开关、灯效模式及速度档位等完整设置界面，
/// 并支持全局颜色模式下的半透明禁用状态。
/// </summary>
public partial class ButtonSettingsPopup : UserControl
{
    // ════════════════════════════════════════════════════════════════
    // 事件与构造函数
    // ════════════════════════════════════════════════════════════════

    /// <summary>用户点击"确定"按钮时触发</summary>
    public event EventHandler? Confirmed;

    /// <summary>用户点击"取消"按钮时触发</summary>
    public event EventHandler? Cancelled;

    /// <summary>
    /// 初始化按键设置弹窗，注册控件加载后的默认初始化和事件绑定。
    /// 当遥测灯效为"无"（索引0）时，速度滑块默认归零。
    /// </summary>
    public ButtonSettingsPopup()
    {
        InitializeComponent();

        // 若默认选中"无灯效"，则将速度滑块归零
        if (TelemetryLightEffectComboBox.SelectedIndex == 0)
            PopupSpeedSlider.Value = 0;

        // 控件加载完成后绑定速度滑块填充更新
        PopupSpeedSlider.Loaded += (_, _) => UpdateSpeedSliderFill(PopupSpeedSlider);

        // 控件加载完成后绑定遥测开关的勾选/取消事件
        Loaded += (_, _) =>
        {
            TelemetryToggle.Checked += (_, _) => OnTelemetryToggled();
            TelemetryToggle.Unchecked += (_, _) => OnTelemetryToggled();
        };
    }

    // ════════════════════════════════════════════════════════════════
    // 遥测开关切换逻辑
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 遥测开关状态变化时的处理逻辑：
    /// 关闭时清除所有触发颜色选择并将速度归零；
    /// 开启时自动选中第一个触发颜色并设置默认速度档位(3)。
    /// </summary>
    private void OnTelemetryToggled()
    {
        if (TelemetryToggle.IsChecked != true)
        {
            // 遥测关闭：取消所有触发颜色的选中状态，速度归零
            foreach (var rb in TeleColorPanel.Children.OfType<RadioButton>())
                rb.IsChecked = false;
            PopupSpeedSlider.Value = 0;
        }
        else
        {
            // 遥测开启：自动选中第一个可用颜色
            var first = TeleColorPanel.Children.OfType<RadioButton>().FirstOrDefault();
            if (first != null)
                first.IsChecked = true;
            // 设置默认速度档位为 3（中速）
            PopupSpeedSlider.Value = 3;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 公开方法：按键信息
    // ════════════════════════════════════════════════════════════════

    /// <summary>设置弹窗标题中显示的按键名称</summary>
    /// <param name="keyName">按键名称，如 "W"、"A" 等</param>
    public void SetKeyName(string keyName)
    {
        KeyNameText.Text = keyName;
    }

    // ════════════════════════════════════════════════════════════════
    // 依赖属性：全局颜色模式
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 全局颜色模式依赖属性。
    /// 当全局颜色模式开启时，按键颜色行变为半透明且不可交互，
    /// 颜色选择面板中所有已选中的按钮会被自动取消选中。
    /// </summary>
    public static readonly DependencyProperty IsGlobalColorModeProperty =
        DependencyProperty.Register(nameof(IsGlobalColorMode), typeof(bool), typeof(ButtonSettingsPopup),
            new PropertyMetadata(false, OnIsGlobalColorModeChanged));

    /// <summary>获取或设置是否启用全局颜色模式</summary>
    public bool IsGlobalColorMode
    {
        get => (bool)GetValue(IsGlobalColorModeProperty);
        set => SetValue(IsGlobalColorModeProperty, value);
    }

    /// <summary>
    /// 全局颜色模式属性变更回调。
    /// 当模式开启（true）时，遍历按键颜色面板的所有单选按钮并取消选中，
    /// 确保全局颜色与独立按键颜色不会同时生效。
    /// </summary>
    private static void OnIsGlobalColorModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var popup = (ButtonSettingsPopup)d;
        if ((bool)e.NewValue && popup.KeyColorPanel != null)
        {
            // 全局颜色模式开启时取消所有按键颜色选中
            foreach (var rb in popup.KeyColorPanel.Children.OfType<RadioButton>())
                rb.IsChecked = false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 公开方法：加载与获取设置
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 加载已有设置到弹窗的各个控件中。
    /// 初始化按键基础颜色、遥测开关状态、触发颜色、功能索引、灯效和速度。
    /// 全局颜色模式下会立即取消所有按键颜色选中，不依赖 XAML 异步触发器。
    /// </summary>
    /// <param name="colorIndex">按键基础颜色索引（0=红 ~ 8=无）</param>
    /// <param name="telemetryEnabled">遥测是否启用</param>
    /// <param name="lightEffect">灯效索引</param>
    /// <param name="func">遥测功能索引</param>
    /// <param name="triggerColor">遥测触发颜色索引</param>
    /// <param name="speed">闪烁速度档位</param>
    public void LoadSettings(int colorIndex, bool telemetryEnabled, int lightEffect, int func, int triggerColor, int speed)
    {
        // 同步遥测开关状态
        if (TelemetryToggle != null)
            TelemetryToggle.IsChecked = telemetryEnabled;

        // 按键灯颜色（基础颜色）选择
        var keyColorButtons = KeyColorPanel?.Children.OfType<RadioButton>().ToList();
        if (keyColorButtons != null && colorIndex >= 0 && colorIndex < keyColorButtons.Count)
            keyColorButtons[colorIndex].IsChecked = true;

        // 全局颜色模式开启时立即取消选中，不依赖 XAML DataTrigger（异步），
        // 确保 LoadSettings 调用期间无论 DataTrigger 处理进度如何，颜色始终未选中
        if (IsGlobalColorMode && keyColorButtons != null)
        {
            foreach (var rb in keyColorButtons)
                rb.IsChecked = false;
        }

        // 遥测触发颜色选择
        var triggerColorButtons = TeleColorPanel?.Children.OfType<RadioButton>().ToList();
        if (triggerColorButtons != null && triggerColor >= 0 && triggerColor < triggerColorButtons.Count)
            triggerColorButtons[triggerColor].IsChecked = true;

        // 遥测功能下拉框
        if (TeleFuncCombo != null)
            TeleFuncCombo.SelectedIndex = func;

        // 遥测灯效下拉框
        if (TelemetryLightEffectComboBox != null)
            TelemetryLightEffectComboBox.SelectedIndex = lightEffect;

        // 闪烁速度滑块
        if (PopupSpeedSlider != null)
            PopupSpeedSlider.Value = speed;
    }

    /// <summary>获取弹窗中选中的按键灯基础颜色索引（0=红, 1=橙, ..., 8=无）</summary>
    /// <returns>选中的颜色索引，未选择时返回 0</returns>
    public int GetSelectedKeyColorIndex()
    {
        var colorButtons = KeyColorPanel?.Children.OfType<RadioButton>().ToList();
        if (colorButtons != null)
        {
            var selected = colorButtons.FirstOrDefault(rb => rb.IsChecked == true);
            if (selected != null) return colorButtons.IndexOf(selected);
        }
        return 0;
    }

    /// <summary>获取弹窗中选中的遥测触发颜色索引</summary>
    /// <returns>选中的遥测触发颜色索引，未选择时返回 0</returns>
    public int GetSelectedColorIndex()
    {
        var colorButtons = TeleColorPanel?.Children.OfType<RadioButton>().ToList();
        if (colorButtons != null)
        {
            var selected = colorButtons.FirstOrDefault(rb => rb.IsChecked == true);
            if (selected != null) return colorButtons.IndexOf(selected);
        }
        return 0;
    }

    /// <summary>获取遥测功能是否已启用</summary>
    /// <returns>遥测开关处于勾选状态时返回 true</returns>
    public bool GetTelemetryEnabled() => TelemetryToggle?.IsChecked == true;

    /// <summary>获取遥测功能索引</summary>
    /// <returns>功能下拉框的选中索引，空值时返回 0</returns>
    public int GetTelemetryFunc() => TeleFuncCombo?.SelectedIndex ?? 0;

    /// <summary>获取遥测灯效索引</summary>
    /// <returns>灯效下拉框的选中索引，空值时返回 0</returns>
    public int GetTelemetryLightEffect() => TelemetryLightEffectComboBox?.SelectedIndex ?? 0;

    /// <summary>
    /// 获取遥测触发颜色索引。
    /// 如果遥测未启用则直接返回 0，无需检查颜色选择。
    /// </summary>
    public int GetTelemetryTriggerColor()
    {
        if (TelemetryToggle?.IsChecked != true) return 0;
        return GetSelectedColorIndex();
    }

    /// <summary>获取闪烁速度档位（0~5 共六档）</summary>
    /// <returns>速度滑块当前值取整，空值时返回 0</returns>
    public int GetSpeed() => (int)(PopupSpeedSlider?.Value ?? 0);

    // ════════════════════════════════════════════════════════════════
    // 公开方法：显示与隐藏
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 显示弹窗，设置 Visibility 为 Visible 并播放淡入+缩放入场动画。
    /// 动画包含遮罩层淡入（180ms）、面板淡入（220ms）和缩放（260ms），
    /// 使用 CubicEase EaseOut 缓动曲线实现平滑过渡。
    /// </summary>
    public void Show()
    {
        Visibility = Visibility.Visible;
        AnimateIn();
    }

    /// <summary>
    /// 隐藏弹窗，播放淡出+缩放出场动画后将 Visibility 设为 Collapsed。
    /// 动画包含遮罩层淡出（160ms）、面板淡出（180ms）和缩放（240ms），
    /// 使用 CubicEase EaseIn 缓动曲线实现快速退出效果。
    /// </summary>
    public void Hide()
    {
        AnimateOut(() => Visibility = Visibility.Collapsed);
    }

    // ════════════════════════════════════════════════════════════════
    // 按钮点击事件
    // ════════════════════════════════════════════════════════════════

    /// <summary>确定按钮点击：触发 Confirmed 事件后关闭弹窗</summary>
    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    /// <summary>取消按钮点击：触发 Cancelled 事件后关闭弹窗</summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    // ════════════════════════════════════════════════════════════════
    // 速度滑块填充逻辑
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 速度档位（0~5）对应的渐变填充偏移量。
    /// 每个档位对应滑块轨迹上一个固定的填充位置百分比，
    /// 用于通过 LinearGradientBrush 控制滑块已填充区域的视觉宽度。
    /// 档位越高，填充比例越大（0=0%, 1≈20.6%, 2≈40.9%, ..., 5=100%）。
    /// </summary>
    private static readonly double[] SpeedStepOffsets = { 0, 0.2063, 0.4091, 0.6084, 0.8112, 1.0 };

    /// <summary>
    /// 更新速度滑块的填充视觉宽度。
    /// 通过修改滑块模板中 LinearGradientBrush（TrackFillBrush）的 GradientStops
    /// 偏移量来实现分段填充效果。当滑块处于最大档位时，填充完全覆盖轨道；
    /// 其他档位根据 SpeedStepOffsets 查找表映射到对应的填充比例。
    /// </summary>
    /// <param name="slider">需要更新填充的速度滑块控件</param>
    private static void UpdateSpeedSliderFill(Slider slider)
    {
        // 模板未加载时直接返回
        if (slider.Template == null) return;

        // 从滑块模板中查找名为 "TrackFillBrush" 的渐变画刷
        var brush = slider.Template.FindName("TrackFillBrush", slider) as LinearGradientBrush;
        if (brush == null || brush.GradientStops.Count < 4) return;

        // 将滑块值舍入到整数档位（0~5）
        var step = (int)Math.Round(slider.Value);
        step = Math.Clamp(step, 0, (int)slider.Maximum);

        // 根据档位查询对应的填充偏移比例
        var fraction = SpeedStepOffsets[step];

        if (step >= slider.Maximum)
        {
            // 最大档位：填充完全覆盖轨道
            brush.GradientStops[1].Offset = 1.0;
            brush.GradientStops[2].Offset = 2.0;
            brush.GradientStops[3].Offset = 2.0;
        }
        else
        {
            // 中间档位：根据 fraction 设置填充边界
            // GradientStops[1] 和 [2] 都设置为 fraction，形成填充与非填充的分界线
            brush.GradientStops[1].Offset = fraction;
            brush.GradientStops[2].Offset = fraction;
            // GradientStops[3] 标记轨道终点
            brush.GradientStops[3].Offset = 1.0;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 控件事件处理
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 遥测灯效下拉框选项改变时的处理逻辑。
    /// 选择"无灯效"（索引0）时速度归零；否则在遥测开启状态下设为默认档位3。
    /// </summary>
    private void TelemetryLightEffectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PopupSpeedSlider == null) return;

        if (TelemetryLightEffectComboBox.SelectedIndex == 0)
        {
            // 无灯效：速度归零
            PopupSpeedSlider.Value = 0;
        }
        else if (TelemetryToggle.IsChecked == true)
        {
            // 有灯效且遥测开启：默认速度档位 3
            PopupSpeedSlider.Value = 3;
        }
    }

    /// <summary>速度滑块值变化时，实时更新轨道的已填充区域宽度</summary>
    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider)
            UpdateSpeedSliderFill(slider);
    }

    // ════════════════════════════════════════════════════════════════
    // 入场/出场动画
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 播放入场动画：遮罩层淡入 + 面板从 94% 缩放到 100% + 面板淡入。
    /// 动画开始前将面板缩放到 0.94 倍并设置 BitmapCache 提升动画性能，
    /// 动画完成后移除 CacheMode 并恢复命中测试，使面板可交互。
    /// </summary>
    private void AnimateIn()
    {
        // 初始状态：遮罩不可见、面板不可见、面板缩小至 94%
        OverlayBackground.Opacity = 0;
        PopupPanel.Opacity = 0;
        // 以面板中心为原点进行缩放（CenterX = Width/2, CenterY = Height/2）
        PopupPanel.RenderTransform = new ScaleTransform(0.94, 0.94,
            PopupPanel.Width / 2, PopupPanel.Height / 2);

        // 开启 BitmapCache 将面板缓存为位图，提升缩放+淡入动画的渲染性能
        PopupPanel.CacheMode = new BitmapCache();
        // 动画期间禁用命中测试，防止用户在动画过程中误触控件
        PopupPanel.IsHitTestVisible = false;

        // 遮罩层淡入动画：0→1，180ms，CubicEase EaseOut 缓出
        var overlayFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // 面板淡入动画：0→1，220ms，CubicEase EaseOut 缓出
        var panelFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // 水平缩放动画：0.94→1.0，260ms，CubicEase EaseOut 缓出
        var scaleX = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // 垂直缩放动画：0.94→1.0，260ms，CubicEase EaseOut 缓出
        var scaleY = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // 缩放动画完成后清理缓存并恢复交互
        scaleX.Completed += (_, _) =>
        {
            PopupPanel.CacheMode = null;
            PopupPanel.IsHitTestVisible = true;
        };

        // 启动所有入场动画
        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        PopupPanel.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        PopupPanel.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }

    /// <summary>
    /// 播放出场动画：遮罩层淡出 + 面板从 100% 缩放到 94% + 面板淡出。
    /// 动画开始前确保 RenderTransform 为 ScaleTransform 并开启 BitmapCache，
    /// 面板淡出完成后执行回调（设置为 Collapsed），然后清理缓存。
    /// </summary>
    /// <param name="onCompleted">动画全部完成后的回调，通常用于设置 Visibility = Collapsed</param>
    private void AnimateOut(Action onCompleted)
    {
        // 确保 RenderTransform 是 ScaleTransform，以面板中心为缩放原点
        if (PopupPanel.RenderTransform is not ScaleTransform st)
            PopupPanel.RenderTransform = st = new ScaleTransform(1, 1,
                PopupPanel.Width / 2, PopupPanel.Height / 2);

        // 开启 BitmapCache 提升动画性能，禁用命中测试
        PopupPanel.CacheMode = new BitmapCache();
        PopupPanel.IsHitTestVisible = false;

        // 遮罩层淡出动画：1→0，160ms，CubicEase EaseIn 缓入
        var overlayFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        // 面板淡出动画：1→0，180ms，CubicEase EaseIn 缓入
        var panelFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        // 水平缩放动画：1→0.94，240ms，CubicEase EaseIn 缓入
        var scaleX = new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        // 垂直缩放动画：1→0.94，240ms，CubicEase EaseIn 缓入
        var scaleY = new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        // 面板淡出完成后：移除缓存、执行回调（例如设置 Collapsed）
        panelFade.Completed += (_, _) =>
        {
            PopupPanel.CacheMode = null;
            onCompleted();
        };

        // 启动所有出场动画
        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }
}
