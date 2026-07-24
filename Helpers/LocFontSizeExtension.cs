using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using HITAPEX.Services;

namespace HITAPEX.Helpers;

/// <summary>
/// XAML 标记扩展，将语言 JSON 中配置的数值绑定到 FontSize 属性。
/// 适用于需要根据语言/区域调整字体大小的场景（例如中文和英文的默认字号不同）。
/// 语言切换时自动更新所有使用该扩展的控件字号。
///
/// 用法示例：
///   <TextBlock FontSize="{lex:LocFontSize Home.ForceFeedbackFontSize}" />
/// </summary>
public class LocFontSizeExtension : MarkupExtension
{
    // ═══════════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 无参构造函数（XAML 标记扩展要求）
    /// </summary>
    public LocFontSizeExtension() { }

    /// <summary>
    /// 带本地化键的构造函数
    /// </summary>
    /// <param name="key">语言 JSON 中的字体大小键</param>
    public LocFontSizeExtension(string key)
    {
        Key = key;
    }

    // ═══════════════════════════════════════════════════════════════
    // 公共属性
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 语言 JSON 中对应字体大小的键名
    /// </summary>
    public string? Key { get; set; }

    // ═══════════════════════════════════════════════════════════════
    // MarkupExtension 核心方法
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 在 XAML 加载时被调用，返回一个绑定对象或默认值 14。
    /// 使用 StringToDoubleConverter 将 JSON 中的字符串值转换为 double 类型
    /// </summary>
    /// <param name="serviceProvider">XAML 服务提供者</param>
    /// <returns>绑定对象或默认字号 14</returns>
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

    // ═══════════════════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 判断当前是否处于 Visual Studio 或 Blend 的设计模式
    /// </summary>
    /// <param name="sp">XAML 服务提供者</param>
    /// <returns>true 表示处于设计模式</returns>
    private static bool IsInDesignMode(IServiceProvider sp)
    {
        if (sp.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget pvt
            && pvt.TargetObject is DependencyObject d)
        {
            return DesignerProperties.GetIsInDesignMode(d);
        }
        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // 内部转换器
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 字符串到 double 的转换器，用于将 JSON 中的字体大小字符串值
    /// （如 "16"）转换为可用于 FontSize 属性的 double 值
    /// </summary>
    private sealed class StringToDoubleConverter : IValueConverter
    {
        /// <summary>
        /// 将字符串值转换为 double。解析失败时返回 parameter 指定的默认值或 14
        /// </summary>
        /// <param name="value">源值（字符串形式的数值）</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">默认值参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>转换后的 double 值</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (double.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
            return parameter is double d ? d : 14d;
        }

        /// <summary>
        /// 反向转换不支持
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
