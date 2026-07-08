using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using HITAPEX.Services;

namespace HITAPEX.Helpers;

/// <summary>
/// XAML 标记扩展，将 JSON 中指定的数值 key 绑定到 FontSize。
/// 语言切换时自动更新。
///
/// 用法：
///   <TextBlock FontSize="{lex:LocFontSize Home.ForceFeedbackFontSize}" />
/// </summary>
public class LocFontSizeExtension : MarkupExtension
{
    public LocFontSizeExtension() { }

    public LocFontSizeExtension(string key)
    {
        Key = key;
    }

    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return 14d;

        if (IsInDesignMode(serviceProvider))
            return 14d;

        var binding = new Binding
        {
            Source = LocalizationService.Instance,
            Path = new PropertyPath($"[{Key}]"),
            Mode = BindingMode.OneWay,
            Converter = new StringToDoubleConverter(),
            FallbackValue = 14d
        };

        return binding.ProvideValue(serviceProvider);
    }

    private static bool IsInDesignMode(IServiceProvider sp)
    {
        if (sp.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget pvt
            && pvt.TargetObject is DependencyObject d)
        {
            return DesignerProperties.GetIsInDesignMode(d);
        }
        return false;
    }

    private sealed class StringToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return parameter is double d ? d : 14d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
