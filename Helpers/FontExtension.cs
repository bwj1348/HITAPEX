using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using HITAPEX.Services;

namespace HITAPEX.Helpers;

/// <summary>
/// XAML 标记扩展，将当前语言对应的字体绑定到 FontFamily 属性。
/// 底层绑定到 LocalizationService.Instance.CurrentFontFamily，
/// 语言切换时自动更新所有使用该扩展的控件字体。
///
/// 用法示例：
///   xmlns:lex="clr-namespace:HITAPEX.Helpers"
///   <TextBlock FontFamily="{lex:Font}" />
/// </summary>
public class FontExtension : MarkupExtension
{
    // ═══════════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 无参构造函数（XAML 标记扩展要求）
    /// </summary>
    public FontExtension() { }

    // ═══════════════════════════════════════════════════════════════
    // MarkupExtension 核心方法
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 在 XAML 加载时被调用，返回一个绑定对象或设计时占位值。
    /// 设计模式下返回 "Microsoft YaHei" 防止设计器错误
    /// </summary>
    /// <param name="serviceProvider">XAML 服务提供者</param>
    /// <returns>绑定对象或设计时占位字符串</returns>
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

    // ═══════════════════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 判断当前是否处于 Visual Studio 或 Blend 的设计模式。
    /// 设计模式下返回占位值以避免运行时代码在设计器中出错
    /// </summary>
    /// <param name="serviceProvider">XAML 服务提供者</param>
    /// <returns>true 表示处于设计模式</returns>
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
