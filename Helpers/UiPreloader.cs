using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace HITAPEX.Helpers;

/// <summary>
/// UI 预热工具：在离屏（未连接任何 PresentationSource）状态下对 FrameworkElement
/// 执行一次 Measure/Arrange/UpdateLayout，提前完成模板实例化、首次布局、绑定初求值和
/// SVG/资源解析，把"首次构图"的重开销转移到启动 Splash 阶段消化。
/// </summary>
/// <remarks>
/// 原理：WPF 渲染成本中"首次构图"（ApplyTemplate + Measure/Arrange + 绑定首帧求值）
/// 与元素是否被 Show 无关，只要在布局管线上走一次 Measure/Arrange 即会执行。
/// 预热后元素再挂入真实窗口时，WPF 复用缓存布局与模板实例，切换瞬时不卡。
/// 注意：Loaded 事件需要 PresentationSource 才会触发，因此离屏预热不会提前触发
/// 各控件的 Loaded 逻辑（如 HID/USB 订阅），这些仍在其真正显示时按原流程执行。
/// </summary>
public static class UiPreloader
{
    /// <summary>预热使用的参考布局尺寸（与主窗口设计尺寸一致，保证相对/比例布局正确构图）</summary>
    private static readonly Size DesignSize = new(1342, 924);

    /// <summary>
    /// 对指定元素执行一次完整的离屏构图。
    /// </summary>
    /// <param name="element">待预热的 UI 元素；null 时静默跳过。</param>
    public static void WarmUp(FrameworkElement? element)
    {
        if (element == null) return;

        try
        {
            element.Measure(DesignSize);
            element.Arrange(new Rect(0, 0, DesignSize.Width, DesignSize.Height));
            element.UpdateLayout();
        }
        catch (Exception ex)
        {
            // 预热是尽力而为的优化，失败不应影响程序运行
            Debug.WriteLine($"[UiPreloader] 预热失败: {element.GetType().Name}: {ex.Message}");
        }
    }
}