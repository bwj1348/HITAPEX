using System.Windows.Input;

namespace HITAPEX.ViewModels;

/// <summary>
/// 可绑定的中继命令，实现 ICommand 接口，
/// 将视图中的事件（如按钮点击）转发到视图模型中的操作方法。
/// 支持可选的执行条件判断（CanExecute）。
/// </summary>
public class RelayCommand : ICommand
{
    // ═══════════════════════════════════════════════════════════════
    // 私有字段
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 命令执行时要调用的委托，接收可选参数
    /// </summary>
    private readonly Action<object?> _execute;

    /// <summary>
    /// 判断命令是否可执行的委托，为 null 时始终可执行
    /// </summary>
    private readonly Func<object?, bool>? _canExecute;

    // ═══════════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建带参数的中继命令
    /// </summary>
    /// <param name="execute">执行委托，接收一个 object? 参数</param>
    /// <param name="canExecute">可执行性判定委托，可选</param>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// 创建无参数的中继命令（最常用的便捷重载）
    /// </summary>
    /// <param name="execute">执行委托，无参数</param>
    /// <param name="canExecute">可执行性判定委托，可选</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
    {
    }

    // ═══════════════════════════════════════════════════════════════
    // ICommand 接口实现
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 当命令的可执行状态发生变化时触发。
    /// 通过挂接 CommandManager.RequerySuggested 实现自动刷新
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// 判定命令当前是否可执行
    /// </summary>
    /// <param name="parameter">命令参数</param>
    /// <returns>如果未提供 canExecute 委托则始终返回 true</returns>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// 执行命令逻辑
    /// </summary>
    /// <param name="parameter">命令参数</param>
    public void Execute(object? parameter) => _execute(parameter);
}
