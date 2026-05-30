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
            InitStrobeOverlay();
            UpdateCapLinePosition();
            CapLineCanvas.SizeChanged += (_, _) => UpdateCapLinePosition();
            UpdateSpeedSliderFill(RpmSpeedSlider1);
            UpdateSpeedSliderFill(RpmSpeedSlider2);
            RpmTelemetryToggle.Checked += (_, _) => UpdateRightSideMaskedControls();
            RpmTelemetryToggle.Unchecked += (_, _) => UpdateRightSideMaskedControls();
            UpdateRightSideMaskedControls();
            RpmSpeedSlider2.Value = BaseLightModeComboBox.SelectedIndex == 0 ? 0 : 3;
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

        var rightBlock = _activeLeftBlockIndex switch
        {
            1 => RpmLightColor1, 2 => RpmLightColor2, 3 => RpmLightColor3,
            4 => RpmLightColor4, 5 => RpmLightColor5, 6 => RpmLightColor6,
            7 => RpmLightColor7, 8 => RpmLightColor8, 9 => RpmLightColor9,
            10 => RpmLightColor1, 11 => RpmLightColor2, 12 => RpmLightColor3,
            _ => null
        };
        if (rightBlock != null)
            rightBlock.IsChecked = true;

        var strobeBlock = _activeLeftBlockIndex switch
        {
            1 => StrobeColor1, 2 => StrobeColor2, 3 => StrobeColor3,
            4 => StrobeColor4, 5 => StrobeColor5, 6 => StrobeColor6,
            7 => StrobeColor7, 8 => StrobeColor8, 9 => StrobeColor9,
            10 => StrobeColor1, 11 => StrobeColor2, 12 => StrobeColor3,
            _ => null
        };
        if (strobeBlock != null)
            strobeBlock.IsChecked = true;
    }

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

        UpdateStrobeOverlay();
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
        var newCap = (1 - y / 315) * 100;

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

        if (e.NewValue > _capValue)
        {
            _isClamping = true;
            ((Slider)sender).Value = _capValue;
            _isClamping = false;
            return;
        }

        UpdateCapLinePosition();
    }

    private int _strobeMode; // 0=与转速灯颜色一致, 1=自定义, 2=关灯
    private readonly Color[] _strobeColors =
    {
        Color.FromRgb(0xC6, 0x0E, 0x0E), // 1: red
        Color.FromRgb(0xFF, 0x6A, 0x00), // 2: orange
        Color.FromRgb(0xFF, 0xC8, 0x00), // 3: yellow
        Color.FromRgb(0x16, 0xC6, 0x42), // 4: green
        Color.FromRgb(0x28, 0xF9, 0xDD), // 5: cyan
        Color.FromRgb(0x28, 0x40, 0xF9), // 6: blue
        Color.FromRgb(0xC1, 0x28, 0xF9), // 7: purple
        Color.FromRgb(0xEE, 0xEE, 0xEE), // 8: white
        Color.FromRgb(0x37, 0x37, 0x37), // 9: dark gray
        Color.FromRgb(0xC6, 0x0E, 0x0E), // 10: red
        Color.FromRgb(0xFF, 0x6A, 0x00), // 11: orange
        Color.FromRgb(0xFF, 0xC8, 0x00), // 12: yellow
    };
    private readonly Rectangle?[] _strobeOverlays = new Rectangle?[12];

    private void InitStrobeOverlay()
    {
        if (StrobeOverlayCanvas == null) return;

        for (int i = 0; i < 12; i++)
        {
            var rect = new Rectangle
            {
                Width = 6,
                RadiusX = 3,
                RadiusY = 3,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            _strobeOverlays[i] = rect;
            StrobeOverlayCanvas.Children.Add(rect);
        }
    }

    private void UpdateStrobeOverlay()
    {
        if (_strobeOverlays[0] == null || StrobeOverlayCanvas == null) return;

        if (_strobeMode != 1) // not custom mode
        {
            foreach (var overlay in _strobeOverlays)
                overlay!.Visibility = Visibility.Collapsed;
            return;
        }

        var capY = 315 * (1 - _capValue / 100);

        Slider[] allSliders = { RpmSlider1, RpmSlider2, RpmSlider3, RpmSlider4,
            RpmSlider5, RpmSlider6, RpmSlider7, RpmSlider8,
            RpmSlider9, RpmSlider10, RpmSlider11, RpmSlider12 };

        for (int i = 0; i < 12; i++)
        {
            var overlay = _strobeOverlays[i]!;
            var slider = allSliders[i];

            try
            {
                var parentGrid = StrobeOverlayCanvas.Parent as UIElement;
                if (parentGrid == null) continue;

                var sliderPos = slider.TransformToAncestor(parentGrid)
                    .Transform(new Point(0, 0));
                Canvas.SetLeft(overlay, sliderPos.X + 5);
            }
            catch
            {
                var x = i * 46;
                Canvas.SetLeft(overlay, x + 5);
            }

            Canvas.SetTop(overlay, 0);
            overlay.Height = capY + 3;

            Geometry baseClip = new RectangleGeometry(new Rect(0, 0, 6, capY));
            var sliderValue = slider.Value;
            var thumbCenterY = 315 * (1 - sliderValue / 100);
            var thumbTop = thumbCenterY - 8;
            if (thumbTop < capY)
            {
                var holeY = Math.Max(0, thumbTop);
                var holeHeight = Math.Min(16, capY - holeY);
                if (holeHeight > 0)
                {
                    var hole = new RectangleGeometry(new Rect(0, holeY, 6, holeHeight));
                    baseClip = new CombinedGeometry(GeometryCombineMode.Exclude, baseClip, hole);
                }
            }
            overlay.Clip = baseClip;

            overlay.Fill = new SolidColorBrush(_strobeColors[i]);
            overlay.Visibility = Visibility.Visible;
        }
    }

    private void StrobeModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _strobeMode = StrobeModeComboBox.SelectedIndex;
        UpdateStrobeOverlay();
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
            StrobeColor9.IsChecked = false;
        }
        else
        {
            StrobeColor1.IsChecked = true;
        }

        var speedMasked = telemetryOff || strobeMode == 2;
        RpmSpeedSlider1.Value = speedMasked ? 0 : 3;
    }

    private void BaseLightModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RpmSpeedSlider2 == null) return;
        RpmSpeedSlider2.Value = BaseLightModeComboBox.SelectedIndex == 0 ? 0 : 3;
    }

    private void StrobeColor_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;

        var idx = _activeLeftBlockIndex - 1;
        if (rb.Background is SolidColorBrush brush)
            _strobeColors[idx] = brush.Color;
        else
            _strobeColors[idx] = Color.FromRgb(0x37, 0x37, 0x37);

        if (_strobeMode == 1)
            UpdateStrobeOverlay();
    }
}
