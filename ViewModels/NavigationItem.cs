using HITAPEX.Services;

namespace HITAPEX.ViewModels;

/// <summary>
/// 导航项视图模型，表示左侧导航栏中的单个导航条目。
/// 封装导航项的名称、图标路径、本地化键和选中状态，
/// 支持语言切换时自动刷新显示文本。
/// </summary>
public class NavigationItem : ViewModelBase
{
    // ═══════════════════════════════════════════════════════════════
    // 私有字段
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 是否选中
    /// </summary>
    private bool _isSelected;

    /// <summary>
    /// 当前语言的显示标签文本
    /// </summary>
    private string _label;

    // ═══════════════════════════════════════════════════════════════
    // 公共属性
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 导航项的内部名称，用于视图匹配（如 "Home"、"Device"）
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 导航项图标的 SVG 资源路径
    /// </summary>
    public string IconPath { get; }

    /// <summary>
    /// 本地化键，用于从语言资源中查找对应文本
    /// </summary>
    public string LocKey { get; }

    /// <summary>
    /// 当前语言的显示标签文本，绑定到导航按钮的 Content
    /// </summary>
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    /// <summary>
    /// 导航项是否处于选中状态，控制高亮样式
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    // ═══════════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建导航项并初始化其基本属性
    /// </summary>
    /// <param name="name">内部名称</param>
    /// <param name="iconPath">图标路径</param>
    /// <param name="locKey">本地化键</param>
    public NavigationItem(string name, string iconPath, string locKey)
    {
        Name = name;
        IconPath = iconPath;
        LocKey = locKey;
        _label = LocalizationService.Instance[locKey];
    }

    // ═══════════════════════════════════════════════════════════════
    // 公共方法
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 语言切换时刷新标签文本，从本地化服务重新获取当前语言的显示文本
    /// </summary>
    public void RefreshLabel()
    {
        Label = LocalizationService.Instance[LocKey];
    }
}
