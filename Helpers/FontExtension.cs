using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using HITAPEX.Services;

namespace HITAPEX.Helpers;

/// <summary>
/// XAML 标记扩展，绑定当前语言对应的字体。
///
/// 用法：
///   <TextBlock FontFamily="{lex:Font}" />
///
/// 底层绑定到 LocalizationService.Instance.CurrentFontFamily，
/// 语言切换时自动更新。
/// </summary>
public class FontExtension : MarkupExtension
{
    public FontExtension() { }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (IsInDesignMode(serviceProvider))
            return "Microsoft YaHei";

        var binding = new Binding
        {
            Source = LocalizationService.Instance,
            Path = new PropertyPath(nameof(LocalizationService.CurrentFontFamily)),
            Mode = BindingMode.OneWay,
            FallbackValue = "Microsoft YaHei"
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
