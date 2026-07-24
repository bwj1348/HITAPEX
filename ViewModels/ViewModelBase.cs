using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HITAPEX.ViewModels;

/// <summary>
/// 所有视图模型的抽象基类，实现 INotifyPropertyChanged 接口。
/// 提供属性变更通知和属性设置辅助方法，
/// 使视图能自动响应视图模型属性的变化。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    // ═══════════════════════════════════════════════════════════════
    // INotifyPropertyChanged 实现
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 当属性值发生变更时触发，通知绑定目标刷新
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 触发 PropertyChanged 事件。调用方成员名由编译器自动填充
    /// </summary>
    /// <param name="propertyName">变更的属性名称（自动填充）</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ═══════════════════════════════════════════════════════════════
    // 属性设置辅助
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 设置属性值并在值发生变化时自动触发 PropertyChanged 通知。
    /// 使用 EqualityComparer 比较新旧值，避免不必要的 UI 刷新
    /// </summary>
    /// <typeparam name="T">属性类型</typeparam>
    /// <param name="field">属性对应的后备字段引用</param>
    /// <param name="value">新值</param>
    /// <param name="propertyName">属性名称（自动填充）</param>
    /// <returns>值是否发生了变化（true 表示已更新）</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
