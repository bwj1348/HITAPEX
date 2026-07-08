using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using HITAPEX.Services;

namespace HITAPEX.Helpers;

/// <summary>
/// XAML 标记扩展，用于将本地化字符串绑定到 UI 属性。
///
/// 用法：
///   xmlns:lex="clr-namespace:HITAPEX.Helpers"
///   <TextBlock Text="{lex:Loc Nav.Home}" />
///   <Button Content="{lex:Loc Common.Confirm}" />
///
/// 底层创建一个到 LocalizationService.Instance[key] 的单向绑定，
/// 语言切换时自动刷新。
/// </summary>
public class LocExtension : MarkupExtension
{
    /// <summary>
    /// 翻译键，对应 JSON 中的 key
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 无参构造函数（XAML 需要）
    /// </summary>
    public LocExtension() { }

    /// <summary>
    /// 带 key 的构造函数（XAML: {lex:Loc SomeKey}）
    /// </summary>
    public LocExtension(string key)
    {
        Key = key;
    }

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
