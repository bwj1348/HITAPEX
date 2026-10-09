using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HITAPEX.Services;

namespace HITAPEX.Views;

/// <summary>
/// 帮助页面视图：展示常见问题（FAQ）。
/// 顶部为图标 + 标题 + 分隔线，下方四个选项卡（基座 / 方向盘 / 脚踏板 / HITAPEX软件），
/// 每个选项卡内为一问一答形式的滚动内容，滚动条显示在背景外部的右侧。
/// 问答内容由 FaqService 从程序目录 Resources/FAQ/faq.{language}.json 读取，切换语言时自动重新加载。
/// </summary>
public partial class HelpUserControl : UserControl
{
    public HelpUserControl()
    {
        InitializeComponent();
        Loaded += (_, _) => RebuildFaqPanels();
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>语言切换（SetLanguage 触发 PropertyChanged(null)）时重新从对应语言的 FAQ 文件构建问答列表。</summary>
    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null && IsLoaded)
            RebuildFaqPanels();
    }

    /// <summary>
    /// 从 FAQ 文件读取当前语言内容，按类别（Base/Wheel/Pedal/Software）
    /// 重建四个选项卡内的 Q&A 文本块（沿用 FaqQuestionStyle / FaqAnswerStyle）。
    /// </summary>
    private void RebuildFaqPanels()
    {
        var data = FaqService.Load(LocalizationService.Instance.CurrentLanguage);

        var panels = new (StackPanel panel, string category)[]
        {
            (BaseFaqPanel, "Base"),
            (WheelFaqPanel, "Wheel"),
            (PedalFaqPanel, "Pedal"),
            (SoftwareFaqPanel, "Software"),
        };

        var questionStyle = Resources["FaqQuestionStyle"] as Style;
        var answerStyle = Resources["FaqAnswerStyle"] as Style;

        foreach (var (panel, category) in panels)
        {
            panel.Children.Clear();

            if (!data.TryGetValue(category, out var items)) continue;

            foreach (var item in items)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = item.Q,
                    Style = questionStyle,
                });
                panel.Children.Add(new TextBlock
                {
                    Text = item.A,
                    Style = answerStyle,
                });
            }
        }
    }

    /// <summary>选项卡选中：仅新内容淡入切换（0→1，0.3s，CubicEase EaseOut）。</summary>
    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radio && radio.Tag != null)
            SwitchTab(radio.Tag.ToString());
    }

    private void SwitchTab(string? tabName)
    {
        if (BaseScrollViewer == null) return;

        foreach (var sv in new[] { BaseScrollViewer, WheelScrollViewer, PedalScrollViewer, SoftwareScrollViewer })
            sv.Visibility = Visibility.Collapsed;

        ScrollViewer? target = tabName switch
        {
            "Base" => BaseScrollViewer,
            "Wheel" => WheelScrollViewer,
            "Pedal" => PedalScrollViewer,
            "Software" => SoftwareScrollViewer,
            _ => BaseScrollViewer
        };

        if (target != null)
        {
            target.Visibility = Visibility.Visible;
            target.Opacity = 0;
            if (Resources["FadeInStoryboard"] is Storyboard fadeIn)
                fadeIn.Begin(target);
        }
    }

    /// <summary>背景区域尺寸变化时，按实际尺寸重建倒角裁剪形状（设计尺寸 1279×766、倒角 15 等比换算）。</summary>
    private void FaqBackground_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (FaqBackground == null) return;

        double w = FaqBackground.ActualWidth;
        double h = FaqBackground.ActualHeight;
        if (w <= 0 || h <= 0) return;

        double cx = 15.0 * w / 1279.0;
        double cy = 15.0 * h / 766.0;
        FaqBackground.Clip = Geometry.Parse(
            $"M0,{cy:F3} V{h:F3} H{w - cx:F3} L{w:F3},{h - cy:F3} V0 H{cx:F3} Z");
    }
}