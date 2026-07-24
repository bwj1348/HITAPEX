using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using HITAPEX.Services;

namespace HITAPEX.Helpers;

/// <summary>
/// XAML 标记扩展，绑定导航栏图标与文本之间的间距。
/// 间距值从语言 JSON 的 Nav.IconTextSpacing 读取，
/// 语言切换时自动更新，确保不同语言下图标和文本的对齐效果一致。
///
/// 用法示例：
///   <TextBlock Margin="{lex:NavSpacing}" />
/// </summary>
public class NavSpacingExtension : MarkupExtension
{
    // ═══════════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 无参构造函数（XAML 标记扩展要求）
    /// </summary>
    public NavSpacingExtension() { }

    // ═══════════════════════════════════════════════════════════════
    // MarkupExtension 核心方法
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 在 XAML 加载时被调用，返回一个绑定到 LocalizationService.NavSpacing 属性的绑定对象。
    /// 设计模式下返回默认间距值 Thickness(31, 0, 0, 0)
    /// </summary>
    /// <param name="serviceProvider">XAML 服务提供者</param>
    /// <returns>绑定对象或设计时默认 Thickness</returns>
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

    // ═══════════════════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 判断当前是否处于 Visual Studio 或 Blend 的设计模式。
    /// 设计模式下返回默认间距值以避免运行时代码在设计器中出错
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
