using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using HITAPEX.Services;

namespace HITAPEX.Helpers;

/// <summary>
/// XAML 标记扩展，绑定导航栏图标与文本之间的间距。
/// 间距值从 JSON 的 Nav.IconTextSpacing 读取，语言切换时自动更新。
///
/// 用法：
///   <TextBlock Margin="{lex:NavSpacing}" />
/// </summary>
public class NavSpacingExtension : MarkupExtension
{
    public NavSpacingExtension() { }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (IsInDesignMode(serviceProvider))
            return new Thickness(31, 0, 0, 0);

        var binding = new Binding
        {
            Source = LocalizationService.Instance,
            Path = new PropertyPath(nameof(LocalizationService.NavSpacing)),
            Mode = BindingMode.OneWay,
            FallbackValue = new Thickness(31, 0, 0, 0)
        };

        return binding.ProvideValue(serviceProvider);
    }

    private static bool IsInDesignMode(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget pvt)
        {
            if (pvt.TargetObject is DependencyObject d)
            {
                return DesignerProperties.GetIsInDesignMode(d);
            }
        }
        return false;
    }
}
