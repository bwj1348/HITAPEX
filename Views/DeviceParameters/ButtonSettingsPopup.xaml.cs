using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HITAPEX.Views.DeviceParameters;

public partial class ButtonSettingsPopup : UserControl
{
    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    public ButtonSettingsPopup()
    {
        InitializeComponent();
        if (TelemetryLightEffectComboBox.SelectedIndex == 0)
            PopupSpeedSlider.Value = 0;
        PopupSpeedSlider.Loaded += (_, _) => UpdateSpeedSliderFill(PopupSpeedSlider);
        Loaded += (_, _) =>
        {
            TelemetryToggle.Checked += (_, _) => OnTelemetryToggled();
            TelemetryToggle.Unchecked += (_, _) => OnTelemetryToggled();
        };
    }

    private void OnTelemetryToggled()
    {
        if (TelemetryToggle.IsChecked != true)
        {
            foreach (var rb in TeleColorPanel.Children.OfType<RadioButton>())
                rb.IsChecked = false;
            PopupSpeedSlider.Value = 0;
        }
        else
        {
            var first = TeleColorPanel.Children.OfType<RadioButton>().FirstOrDefault();
            if (first != null)
                first.IsChecked = true;
            PopupSpeedSlider.Value = 3;
        }
    }

    public void SetKeyName(string keyName)
    {
        KeyNameText.Text = keyName;
    }

    /// <summary>加载按键设置到弹窗</summary>
    public void LoadSettings(int colorIndex, bool telemetryEnabled, int lightEffect, int func, int triggerColor, int speed)
    {
        if (TelemetryToggle != null)
            TelemetryToggle.IsChecked = telemetryEnabled;

        // 按键灯颜色（基础颜色）
        var keyColorButtons = KeyColorPanel?.Children.OfType<RadioButton>().ToList();
        if (keyColorButtons != null && colorIndex >= 0 && colorIndex < keyColorButtons.Count)
            keyColorButtons[colorIndex].IsChecked = true;

        // 遥测触发颜色
        var triggerColorButtons = TeleColorPanel?.Children.OfType<RadioButton>().ToList();
        if (triggerColorButtons != null && triggerColor >= 0 && triggerColor < triggerColorButtons.Count)
            triggerColorButtons[triggerColor].IsChecked = true;

        if (TeleFuncCombo != null)
            TeleFuncCombo.SelectedIndex = func;

        if (TelemetryLightEffectComboBox != null)
            TelemetryLightEffectComboBox.SelectedIndex = lightEffect;

        if (PopupSpeedSlider != null)
            PopupSpeedSlider.Value = speed;
    }

    /// <summary>获取弹窗中选中的按键灯基础颜色索引 (0=红 ~ 8=无)</summary>
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

    /// <summary>获取遥测是否启用</summary>
    public bool GetTelemetryEnabled() => TelemetryToggle?.IsChecked == true;

    /// <summary>获取遥测功能索引</summary>
    public int GetTelemetryFunc() => TeleFuncCombo?.SelectedIndex ?? 0;

    /// <summary>获取遥测灯效索引</summary>
    public int GetTelemetryLightEffect() => TelemetryLightEffectComboBox?.SelectedIndex ?? 0;

    /// <summary>获取遥测触发颜色索引</summary>
    public int GetTelemetryTriggerColor()
    {
        if (TelemetryToggle?.IsChecked != true) return 0;
        return GetSelectedColorIndex();
    }

    /// <summary>获取闪烁速度档位</summary>
    public int GetSpeed() => (int)(PopupSpeedSlider?.Value ?? 0);

    public void Show()
    {
        Visibility = Visibility.Visible;
        AnimateIn();
    }

    public void Hide()
    {
        AnimateOut(() => Visibility = Visibility.Collapsed);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    private static readonly double[] SpeedStepOffsets = { 0, 0.2063, 0.4091, 0.6084, 0.8112, 1.0 };

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

    private void TelemetryLightEffectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PopupSpeedSlider == null) return;

        if (TelemetryLightEffectComboBox.SelectedIndex == 0)
        {
            PopupSpeedSlider.Value = 0;
        }
        else if (TelemetryToggle.IsChecked == true)
        {
            PopupSpeedSlider.Value = 3;
        }
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider)
            UpdateSpeedSliderFill(slider);
    }

    private void AnimateIn()
    {
        OverlayBackground.Opacity = 0;
        PopupPanel.Opacity = 0;
        PopupPanel.RenderTransform = new ScaleTransform(0.94, 0.94,
            PopupPanel.Width / 2, PopupPanel.Height / 2);
        PopupPanel.CacheMode = new BitmapCache();
        PopupPanel.IsHitTestVisible = false;

        var overlayFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var panelFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var scaleX = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var scaleY = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        scaleX.Completed += (_, _) =>
        {
            PopupPanel.CacheMode = null;
            PopupPanel.IsHitTestVisible = true;
        };

        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        PopupPanel.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        PopupPanel.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }

    private void AnimateOut(Action onCompleted)
    {
        if (PopupPanel.RenderTransform is not ScaleTransform st)
            PopupPanel.RenderTransform = st = new ScaleTransform(1, 1,
                PopupPanel.Width / 2, PopupPanel.Height / 2);

        PopupPanel.CacheMode = new BitmapCache();
        PopupPanel.IsHitTestVisible = false;

        var overlayFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var panelFade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var scaleX = new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var scaleY = new DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        panelFade.Completed += (_, _) =>
        {
            PopupPanel.CacheMode = null;
            onCompleted();
        };

        OverlayBackground.BeginAnimation(OpacityProperty, overlayFade);
        PopupPanel.BeginAnimation(OpacityProperty, panelFade);
        st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
    }
}
