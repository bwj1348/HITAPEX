using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace HITAPEX;

public partial class SplashWindow : Window
{
    private DispatcherTimer? _dotsTimer;
    private int _dotCount = 1;

    public SplashWindow()
    {
        InitializeComponent();
        StartAnimations();
    }

    private void StartAnimations()
    {
        Loaded += (_, _) =>
        {
            StartGlowPulse();
            StartDotsAnimation();
        };
    }

    /// <summary>
    /// 品牌名发光呼吸 + 副标题透明度呼吸
    /// </summary>
    private void StartGlowPulse()
    {
        // 品牌名文字光晕呼吸
        var textGlowAnim = new DoubleAnimation
        {
            From = 0.3,
            To = 0.8,
            Duration = new Duration(TimeSpan.FromMilliseconds(1800)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        BrandTextGlow.BeginAnimation(
            System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, textGlowAnim);

        // 品牌名文字透明度呼吸
        var textOpacityAnim = new DoubleAnimation
        {
            From = 0.75,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(1800)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        BrandText.BeginAnimation(OpacityProperty, textOpacityAnim);

        // 副标题透明度呼吸
        var subtitleAnim = new DoubleAnimation
        {
            From = 0.35,
            To = 0.7,
            Duration = new Duration(TimeSpan.FromMilliseconds(1800)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        SubtitleText.BeginAnimation(OpacityProperty, subtitleAnim);
    }

    protected override void OnClosed(EventArgs e)
    {
        _dotsTimer?.Stop();
        _dotsTimer = null;
        base.OnClosed(e);
    }

    /// <summary>
    /// 省略号循环：loading. → loading.. → loading... → loading.
    /// </summary>
    private void StartDotsAnimation()
    {
        _dotsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _dotsTimer.Tick += (_, _) =>
        {
            _dotCount = _dotCount >= 3 ? 1 : _dotCount + 1;
            // 每个点之间加间距，前面补一个空格避免粘住 loading
            LoadingDots.Text = " " + string.Join("  ", Enumerable.Repeat(".", _dotCount));
        };
        _dotsTimer.Start();
    }
}
