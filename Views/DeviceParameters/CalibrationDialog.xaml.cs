using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HITAPEX.Views.DeviceParameters;

public partial class CalibrationDialog : UserControl
{
    public event EventHandler? StartCalibrationRequested;
    public event EventHandler? CompleteRequested;
    public event EventHandler? CloseRequested;

    public CalibrationDialog()
    {
        InitializeComponent();
    }

    public void UpdateClutchProgress(double percentage)
    {
        var clamped = Math.Clamp(percentage, 0, 100);
        ClutchProgressGreen.Width = new GridLength(clamped, GridUnitType.Star);
        ClutchProgressRed.Width = new GridLength(100 - clamped, GridUnitType.Star);
        ClutchPercentText.Text = $"{clamped:F0}%";
    }

    public void UpdateBrakeProgress(double percentage)
    {
        var clamped = Math.Clamp(percentage, 0, 100);
        BrakeProgressGreen.Width = new GridLength(clamped, GridUnitType.Star);
        BrakeProgressRed.Width = new GridLength(100 - clamped, GridUnitType.Star);
        BrakePercentText.Text = $"{clamped:F0}%";
    }

    public void UpdateThrottleProgress(double percentage)
    {
        var clamped = Math.Clamp(percentage, 0, 100);
        ThrottleProgressGreen.Width = new GridLength(clamped, GridUnitType.Star);
        ThrottleProgressRed.Width = new GridLength(100 - clamped, GridUnitType.Star);
        ThrottlePercentText.Text = $"{clamped:F0}%";
    }

    private void SetStartButtonDisabled()
    {
        CompleteButton.IsEnabled = true;
        StartCalibrationButton.Cursor = Cursors.Arrow;
        StartButtonMask.Visibility = Visibility.Visible;
        InstructionText.Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x6F, 0x6F));
    }

    public void ResetState()
    {
        CompleteButton.IsEnabled = false;
        StartCalibrationButton.Cursor = Cursors.Hand;
        StartButtonMask.Visibility = Visibility.Collapsed;
        InstructionText.Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));

        UpdateClutchProgress(0);
        UpdateBrakeProgress(0);
        UpdateThrottleProgress(0);
    }

    private void StartCalibrationButton_Click(object sender, MouseButtonEventArgs e)
    {
        SetStartButtonDisabled();
        StartCalibrationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CompleteButton_Click(object sender, RoutedEventArgs e)
    {
        CompleteRequested?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    public void Show()
    {
        ResetState();
        Visibility = Visibility.Visible;
        AnimateIn();
    }

    public void Hide()
    {
        AnimateOut(() => Visibility = Visibility.Collapsed);
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
