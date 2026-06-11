using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace HITAPEX.Views.DeviceParameters;

public partial class RpmSettingsPopup : UserControl
{
    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    public RpmSettingsPopup()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateCapLinePosition();
            CapLineCanvas.SizeChanged += (_, _) => UpdateCapLinePosition();
            UpdateSpeedSliderFill(RpmSpeedSlider1);
            UpdateSpeedSliderFill(RpmSpeedSlider2);
            RpmTelemetryToggle.Checked += (_, _) => UpdateRightSideMaskedControls();
            RpmTelemetryToggle.Unchecked += (_, _) => UpdateRightSideMaskedControls();
            UpdateRightSideMaskedControls();
        };
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
        AnimateIn();
        UpdateCapLinePosition();
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

    private int _activeLeftBlockIndex = 1;

    private void LeftColorBlock_Checked(object sender, RoutedEventArgs e)
    {
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

    private RadioButton?[] GetLightColorRadios() => new RadioButton?[]
        { RpmLightColor1, RpmLightColor2, RpmLightColor3, RpmLightColor4, RpmLightColor5,
          RpmLightColor6, RpmLightColor7, RpmLightColor8, RpmLightColor9 };

    private RadioButton?[] GetStrobeColorRadios() => new RadioButton?[]
        { StrobeColor1, StrobeColor2, StrobeColor3, StrobeColor4, StrobeColor5,
          StrobeColor6, StrobeColor7, StrobeColor8 };

    private void RightLightColor_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;

        Color color;
        if (rb.Background is SolidColorBrush brush)
            color = brush.Color;
        else
            color = Color.FromRgb(0x37, 0x37, 0x37);

        var leftBlock = _activeLeftBlockIndex switch
        {
            1 => ColorBlock1, 2 => ColorBlock2, 3 => ColorBlock3, 4 => ColorBlock4,
            5 => ColorBlock5, 6 => ColorBlock6, 7 => ColorBlock7, 8 => ColorBlock8,
            9 => ColorBlock9, 10 => ColorBlock10, 11 => ColorBlock11, 12 => ColorBlock12,
            _ => null
        };
        if (leftBlock != null)
            leftBlock.Background = new SolidColorBrush(color);

        var slider = _activeLeftBlockIndex switch
        {
            1 => RpmSlider1, 2 => RpmSlider2, 3 => RpmSlider3, 4 => RpmSlider4,
            5 => RpmSlider5, 6 => RpmSlider6, 7 => RpmSlider7, 8 => RpmSlider8,
            9 => RpmSlider9, 10 => RpmSlider10, 11 => RpmSlider11, 12 => RpmSlider12,
            _ => null
        };
        if (slider != null)
        {
            slider.Background = CreateGradient(color);
            slider.Foreground = new SolidColorBrush(color);
        }
    }

    private static LinearGradientBrush CreateGradient(Color color)
    {
        var bright = color;
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

    private static bool ColorsEqual(Color c, byte r, byte g, byte b)
        => c.R == r && c.G == g && c.B == b;

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

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is Slider slider)
            UpdateSpeedSliderFill(slider);
    }

    private double _capValue = 100;
    private bool _isDraggingCap;
    private bool _isClamping;

    private Slider[] AllRpmSliders => new[]
    {
        RpmSlider1, RpmSlider2, RpmSlider3, RpmSlider4, RpmSlider5, RpmSlider6,
        RpmSlider7, RpmSlider8, RpmSlider9, RpmSlider10, RpmSlider11, RpmSlider12
    };

    private double GetMaxSliderValue()
    {
        return AllRpmSliders.Max(s => s.Value);
    }

    private void UpdateCapLinePosition()
    {
        if (CapLineCanvas == null || CapDashedLine == null
            || CapTriangle == null || CapPercentLabel == null) return;

        var y = 315 * (1 - _capValue / 100);
        const double lineX1 = 26;
        const double lineWidth = 525;
        var lineX2 = lineX1 + lineWidth;

        CapDashedLine.X1 = lineX1;
        CapDashedLine.X2 = lineX2;
        CapDashedLine.Y1 = y;
        CapDashedLine.Y2 = y;

        Canvas.SetLeft(CapTriangle, lineX2 + 6);
        Canvas.SetTop(CapTriangle, y - 11);

        Canvas.SetLeft(CapPercentLabel, 0);
        Canvas.SetTop(CapPercentLabel, y - 7);
        CapPercentLabel.Text = $"{_capValue:F0}";
    }

    private void CapTriangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingCap = true;
        CapTriangle.CaptureMouse();
        e.Handled = true;
    }

    private void CapTriangle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingCap) return;

        var pos = e.GetPosition(CapLineCanvas);
        var y = Math.Clamp(pos.Y, 0, 315);
        // 步长设为 1，确保百分比为整数
        var newCap = Math.Round((1 - y / 315) * 100);

        var maxSlider = GetMaxSliderValue();
        _capValue = Math.Clamp(newCap, maxSlider, 100);

        UpdateCapLinePosition();

        foreach (var slider in AllRpmSliders)
        {
            if (slider.Value > _capValue)
                slider.Value = _capValue;
        }
    }

    private void CapTriangle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingCap = false;
        CapTriangle.ReleaseMouseCapture();
    }

    private void RpmSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isDraggingCap || _isClamping) return;

        // _capValue == 0 表示爆闪触发值未配置，不进行截断
        if (_capValue > 0 && e.NewValue > _capValue)
        {
            _isClamping = true;
            ((Slider)sender).Value = _capValue;
            _isClamping = false;
            return;
        }

        UpdateCapLinePosition();
    }

    private int _strobeMode; // 0=与转速灯颜色一致, 1=自定义, 2=关灯
    private int _strobeColor; // 12灯统一的爆闪颜色索引 (0=红~7=白)

    private void StrobeModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _strobeMode = StrobeModeComboBox.SelectedIndex;
        UpdateRightSideMaskedControls();
    }

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

    private void BaseLightModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RpmSpeedSlider2 == null) return;
        // 仅在切换到"恒亮"时强制设为 0，其他模式保持已加载的预设值
        if (BaseLightModeComboBox.SelectedIndex == 0)
            RpmSpeedSlider2.Value = 0;
    }

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

        // 曲线类型
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

        // 右侧面板参数
        if (RpmTelemetryToggle != null)
            RpmTelemetryToggle.IsChecked = rpmTelemetryEnabled;

        if (RpmLightModeCombo != null)
            RpmLightModeCombo.SelectedIndex = rpmLightMode;

        if (StrobeModeComboBox != null)
            StrobeModeComboBox.SelectedIndex = rpmStrobeMode;

        if (RpmSpeedSlider1 != null)
            RpmSpeedSlider1.Value = rpmSpeed;

        if (BaseLightModeComboBox != null)
            BaseLightModeComboBox.SelectedIndex = rpmBaseLightMode;

        if (RpmSpeedSlider2 != null)
            RpmSpeedSlider2.Value = rpmBaseLightSpeed;

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
