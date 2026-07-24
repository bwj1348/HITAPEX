using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using HITAPEX.Services;

namespace HITAPEX.Helpers;

/// <summary>
/// XAML 标记扩展，用于将本地化字符串绑定到 UI 属性（如 TextBlock.Text、Button.Content 等）。
/// 底层创建一个到 LocalizationService.Instance[key] 的单向绑定，
/// 语言切换时所有使用此扩展的文本自动刷新。
///
/// 用法示例：
///   xmlns:lex="clr-namespace:HITAPEX.Helpers"
///   <TextBlock Text="{lex:Loc Nav.Home}" />
///   <Button Content="{lex:Loc Common.Confirm}" />
/// </summary>
public class LocExtension : MarkupExtension
{
    // ═══════════════════════════════════════════════════════════════
    // 公共属性
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 翻译键，对应语言 JSON 文件中的 key。
    /// 例如 "Nav.Home"、"Common.Confirm"
    /// </summary>
    public string Key { get; set; } = string.Empty;

    // ═══════════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 无参构造函数（XAML 标记扩展要求）
    /// </summary>
    public LocExtension() { }

    /// <summary>
    /// 带本地化键的构造函数。
    /// XAML 用法：{lex:Loc SomeKey}
    /// </summary>
    /// <param name="key">本地化键</param>
    public LocExtension(string key)
    {
        Key = key;
    }

    // ═══════════════════════════════════════════════════════════════
    // MarkupExtension 核心方法
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 在 XAML 加载时被调用，返回一个绑定对象或设计时占位值。
    /// 设计模式下返回 "[Key]" 形式的占位文本，方便在 XAML 设计器中识别绑定位置
    /// </summary>
    /// <param name="serviceProvider">XAML 服务提供者</param>
    /// <returns>绑定对象或设计时占位字符串</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return string.Empty;

        // 设计模式下返回 key 本身，避免设计器报错
        if (IsInDesignMode(serviceProvider))
            return $"[{Key}]";

        var binding = new Binding
        {
            Source = LocalizationService.Instance,
            Path = new PropertyPath($"[{Key}]"),
            Mode = BindingMode.OneWay,
            FallbackValue = Key
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
