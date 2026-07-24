using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using HITAPEX.Services;

namespace HITAPEX.Helpers;

/// <summary>
/// XAML 标记扩展，将语言 JSON 中的 Margin 字符串绑定到 Thickness 属性。
/// 字符串格式为 "left,top,right,bottom"（逗号分隔的四个数值），
/// 适用于需要根据语言/区域调整控件边距的场景。
/// 语言切换时自动更新所有使用该扩展的控件边距。
///
/// 用法示例：
///   <TextBlock Margin="{lex:LocThickness WheelParam.ClutchModeSpacing}" />
/// </summary>
public class LocThicknessExtension : MarkupExtension
{
    // ═══════════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 无参构造函数（XAML 标记扩展要求）
    /// </summary>
    public LocThicknessExtension() { }

    /// <summary>
    /// 带本地化键的构造函数
    /// </summary>
    /// <param name="key">语言 JSON 中的边距键</param>
    public LocThicknessExtension(string key) { Key = key; }

    // ═══════════════════════════════════════════════════════════════
    // 公共属性
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 语言 JSON 中对应边距值的键名
    /// </summary>
    public string? Key { get; set; }

    // ═══════════════════════════════════════════════════════════════
    // MarkupExtension 核心方法
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 在 XAML 加载时被调用，返回一个绑定对象或默认 Thickness。
    /// 使用内部的 ThicknessConverter 将逗号分隔的字符串转换为 Thickness 对象
    /// </summary>
    /// <param name="serviceProvider">XAML 服务提供者</param>
    /// <returns>绑定对象或默认 Thickness</returns>
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
            return DesignerProperties.GetIsInDesignMode(d);
        return false;
    }

    // ═══════════════════════════════════════════════════════════════
    // 内部转换器
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 字符串到 Thickness 的转换器。
    /// 将 "left,top,right,bottom" 格式的字符串解析为 WPF Thickness 对象
    /// </summary>
    private sealed class ThicknessConverter : IValueConverter
    {
        /// <summary>
        /// 将逗号分隔的字符串转换为 Thickness 值
        /// </summary>
        /// <param name="value">源值（"left,top,right,bottom" 格式字符串）</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换器参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>解析后的 Thickness，解析失败返回默认值</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s) return Parse(s);
            return new Thickness(0, 0, 20, 0);
        }

        /// <summary>
        /// 反向转换不支持
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        /// <summary>
        /// 解析 "left,top,right,bottom" 格式字符串为 Thickness。
        /// 各部分使用 InvariantCulture 解析以确保小数点格式一致
        /// </summary>
        /// <param name="s">边距字符串</param>
        /// <returns>解析后的 Thickness，格式不符时返回零值 Thickness</returns>
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
