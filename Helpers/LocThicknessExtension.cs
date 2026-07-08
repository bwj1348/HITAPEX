using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using HITAPEX.Services;

namespace HITAPEX.Helpers;

/// <summary>
/// XAML 标记扩展，将 JSON 中的 Margin 字符串绑定到 Thickness 属性。
/// 格式："left,top,right,bottom"，语言切换时自动更新。
///
/// 用法：
///   <TextBlock Margin="{lex:LocThickness WheelParam.ClutchModeSpacing}" />
/// </summary>
public class LocThicknessExtension : MarkupExtension
{
    public LocThicknessExtension() { }
    public LocThicknessExtension(string key) { Key = key; }
    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key)) return new Thickness(0);

        if (IsInDesignMode(serviceProvider))
            return new Thickness(0, 0, 20, 0);

        var binding = new Binding
        {
            Source = LocalizationService.Instance,
            Path = new PropertyPath($"[{Key}]"),
            Mode = BindingMode.OneWay,
            Converter = new ThicknessConverter(),
            FallbackValue = new Thickness(0, 0, 20, 0)
        };

        return binding.ProvideValue(serviceProvider);
    }

    private static bool IsInDesignMode(IServiceProvider sp)
    {
        if (sp.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget pvt
            && pvt.TargetObject is DependencyObject d)
            return DesignerProperties.GetIsInDesignMode(d);
        return false;
    }

    private sealed class ThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s) return Parse(s);
            return new Thickness(0, 0, 20, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static Thickness Parse(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 4) return new Thickness(0);
            return new Thickness(
                double.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
                double.Parse(parts[2].Trim(), CultureInfo.InvariantCulture),
                double.Parse(parts[3].Trim(), CultureInfo.InvariantCulture));
        }
    }
}
