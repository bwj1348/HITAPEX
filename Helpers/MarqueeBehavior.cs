using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HITAPEX.Helpers;

/// <summary>
/// 悬停滚动播放附加属性：用于 <see cref="TextTrimming.CharacterEllipsis"/> 截断的文本。
/// 鼠标悬停在文本所在的裁剪容器上时，截断文本以跑马灯方式横向无缝循环滚动展示全文，移出后恢复。
/// </summary>
/// <remarks>
/// <para>使用前提：TextBlock 外层必须套一个固定宽度且 <see cref="UIElement.ClipToBounds"/> 为 true 的容器，
/// 否则滚动时文字会溢出到卡片之外。事件挂在容器上（尺寸稳定），避免文本滚动时命中区域漂移导致
/// MouseEnter/MouseLeave 抖动。</para>
/// <para>循环原理：滚动期间把文本临时拼成"原文＋间隙＋原文"两份，动画只平移一个副本的步长
/// （= 总长 − 单份长）。循环到终点后跳回起点，由于第二份与第一份内容相同，接头视觉无缝，永不折返。</para>
/// </remarks>
public static class MarqueeBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(MarqueeBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    private static readonly DependencyProperty _hookedProperty =
        DependencyProperty.RegisterAttached("Hooked", typeof(bool), typeof(MarqueeBehavior));

    /// <summary>滚动期间保存 TextBlock 的原始固定宽度，恢复时用。</summary>
    private static readonly DependencyProperty _originalWidthProperty =
        DependencyProperty.RegisterAttached("OriginalWidth", typeof(double), typeof(MarqueeBehavior));

    /// <summary>滚动期间保存原始 Text 绑定，结束恢复绑定。</summary>
    private static readonly DependencyProperty _originalBindingProperty =
        DependencyProperty.RegisterAttached("OriginalBinding", typeof(BindingExpression), typeof(MarqueeBehavior));

    /// <summary>滚动期间保存无绑定时的原始文本。</summary>
    private static readonly DependencyProperty _originalTextProperty =
        DependencyProperty.RegisterAttached("OriginalText", typeof(string), typeof(MarqueeBehavior));

    /// <summary>是否正在滚动（防止重复进入导致文本重复拼接）。</summary>
    private static readonly DependencyProperty _scrollingProperty =
        DependencyProperty.RegisterAttached("Scrolling", typeof(bool), typeof(MarqueeBehavior));

    /// <summary>循环时两份副本之间的间隙（3 个全角空格）。</summary>
    private const string GapText = "\u3000\u3000\u3000";

    /// <summary>滚动速度（像素/秒）。</summary>
    private const double SpeedPxPerSecond = 60.0;

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue != true) return;

        TextBlock? text = d as TextBlock;
        if (text == null && d is FrameworkElement fe)
        {
            // 兼容附加属性挂在容器上的用法：在子树中查找 TextBlock
            text = FindTextBlock(fe);
        }
        if (text == null) return;

        // 延迟到元素挂载后再取父容器（DataTemplate 应用时父容器可能尚未存在）
        text.Loaded += (_, _) => HookContainerEvents(text);
    }

    /// <summary>在可视树中递归查找第一个 TextBlock。</summary>
    private static TextBlock? FindTextBlock(DependencyObject root)
    {
        for (int i = 0, count = VisualTreeHelper.GetChildrenCount(root); i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock tb) return tb;
            var found = FindTextBlock(child);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>把悬停事件挂到裁剪容器上，容器才是稳定的命中区域。</summary>
    /// <remarks>防重复订阅标记按 TextBlock 记录：同一容器内可有多个文本（如辉光+前景两层）
    /// 各自滚动，由同一 MouseEnter/Leave 驱动（参数一致、同一时钟起始，滚动保持同步）。</remarks>
    private static void HookContainerEvents(TextBlock text)
    {
        if (VisualTreeHelper.GetParent(text) is not UIElement host) return;
        if (text.GetValue(_hookedProperty) is true) return; // 同一文本只挂一次

        text.SetValue(_hookedProperty, true);
        host.MouseEnter += (_, _) => StartMarquee(text);
        host.MouseLeave += (_, _) => StopMarquee(text);
    }

    private static void StartMarquee(TextBlock text)
    {
        if (text.GetValue(_scrollingProperty) is true) return; // 已在滚动中
        if (VisualTreeHelper.GetParent(text) is not FrameworkElement host) return;

        // 1. 用 FormattedText 纯计算量取单份文本宽度与间隙宽度——
        //    不修改任何布局属性、不触发重排，从而彻底避免悬停时的高度/细微抖动。
        var src = text.Text;
        var dpi = VisualTreeHelper.GetDpi(text).PixelsPerDip;
        var typeface = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
        var fontSize = double.IsNaN(text.FontSize) ? 18.0 : text.FontSize;

        var ftSrc = new FormattedText(src, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, fontSize, text.Foreground, dpi);
        var ftGap = new FormattedText(GapText, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, fontSize, text.Foreground, dpi);

        var singleWidth = ftSrc.WidthIncludingTrailingWhitespace;
        var step = singleWidth + ftGap.WidthIncludingTrailingWhitespace; // 无缝循环步长

        var viewport = host.ActualWidth - text.Margin.Left - text.Margin.Right;

        // 2. 未溢出：不做任何改动，直接返回（悬停无任何视觉变化）
        if (singleWidth <= viewport) return;

        // 3. 进入滚动：保存原宽度/绑定/文本，置自动宽 + 解除截断 + 拼接两份。
        //    自动宽使整行字形全部落入 TextBlock 自身边界（TextBlock 会裁掉超出自身布局宽度的
        //    墨迹），再由外层 Canvas 的 ClipToBounds 裁出固定窗口。
        text.SetValue(_originalWidthProperty, text.Width);
        text.SetValue(_originalBindingProperty, text.GetBindingExpression(TextBlock.TextProperty));
        text.SetValue(_originalTextProperty, src);

        text.Width = double.NaN;
        text.TextTrimming = TextTrimming.None;
        text.Text = src + GapText + src;

        if (text.RenderTransform is not TranslateTransform transform)
            text.RenderTransform = transform = new TranslateTransform();

        text.SetValue(_scrollingProperty, true);
        transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation
        {
            From = 0,
            To = -step,
            Duration = new Duration(TimeSpan.FromSeconds(step / SpeedPxPerSecond)),
            // 恒速单向循环：循环到终点跳回起点时，第二份与第一份内容相同，接头视觉无缝
            RepeatBehavior = RepeatBehavior.Forever
        });
    }

    private static void StopMarquee(TextBlock text)
    {
        if (text.GetValue(_scrollingProperty) is not true) return;

        if (text.RenderTransform is TranslateTransform transform)
        {
            // 停止动画后属性值回落到基本值（X=0），无需手动归位
            transform.BeginAnimation(TranslateTransform.XProperty, null);
        }

        text.SetValue(_scrollingProperty, false);
        RestoreText(text);
    }

    /// <summary>恢复固定宽度、省略号与原始文本（重新挂绑定）。</summary>
    private static void RestoreText(TextBlock text)
    {
        text.TextTrimming = TextTrimming.CharacterEllipsis;

        var original = (double)text.GetValue(_originalWidthProperty);
        if (!double.IsNaN(original))
        {
            text.Width = original;
            text.InvalidateMeasure();
        }

        if (text.GetValue(_originalBindingProperty) is BindingExpression binding)
        {
            text.SetBinding(TextBlock.TextProperty, binding.ParentBinding);
        }
        else if (text.GetValue(_originalTextProperty) is string raw)
        {
            text.Text = raw;
        }
    }
}