using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;
using HITAPEX.Services;
using HITAPEX.Views;

namespace HITAPEX.ViewModels;

/// <summary>
/// 主窗口的视图模型，管理导航项选择、视图切换和窗口标题。
/// 作为整个应用程序 UI 的核心协调者，响应导航变化并维护视图缓存。
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    // ═══════════════════════════════════════════════════════════════
    // 私有字段
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 当前选中的导航项
    /// </summary>
    private NavigationItem? _selectedNavigationItem;

    /// <summary>
    /// 当前显示的视图控件
    /// </summary>
    private UserControl? _currentView;

    /// <summary>
    /// 窗口标题文本
    /// </summary>
    private string _title = "HITAPEX Racing Simulator";

    /// <summary>
    /// 视图缓存字典，避免每次导航都重新创建视图实例，
    /// 从而保持设备连接事件订阅等状态有效
    /// </summary>
    private readonly Dictionary<string, UserControl> _viewCache = new();

    // ═══════════════════════════════════════════════════════════════
    // 公共属性
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 导航项集合，用于左侧导航栏的数据绑定
    /// </summary>
    public ObservableCollection<NavigationItem> NavigationItems { get; }

    /// <summary>
    /// 当前选中的导航项。设置时自动取消上一项的选中状态，
    /// 并触发当前视图的更新
    /// </summary>
    public NavigationItem? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (_selectedNavigationItem != null)
                _selectedNavigationItem.IsSelected = false;

            if (SetProperty(ref _selectedNavigationItem, value))
            {
                if (_selectedNavigationItem != null)
                    _selectedNavigationItem.IsSelected = true;
                UpdateCurrentView();
            }
        }
    }

    /// <summary>
    /// 当前显示的视图控件，绑定到主窗口的内容区域
    /// </summary>
    public UserControl? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    /// <summary>
    /// 窗口标题，支持语言切换时动态更新
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    // ═══════════════════════════════════════════════════════════════
    // 构造函数
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 初始化 MainWindowViewModel，创建所有导航项并订阅语言切换事件
    /// </summary>
    public MainWindowViewModel()
    {
        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Home", "/Assets/HomeIcon.svg", "Nav.Home"),
            new("Device", "/Assets/DeviceIcon.svg", "Nav.Device"),
            new("Game", "/Assets/GameIcon.svg", "Nav.Game"),
            new("Help", "/Assets/HelpIcon.svg", "Nav.Help"),
            new("Settings", "/Assets/SettingsIcon.svg", "Nav.Settings")
        };

        SelectedNavigationItem = NavigationItems[0];

        // 监听语言切换，动态刷新导航标签
        LocalizationService.Instance.PropertyChanged += OnLanguageChanged;
    }

    // ═══════════════════════════════════════════════════════════════
    // 事件处理
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 语言切换事件处理。刷新所有导航项的显示标签和窗口标题
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">事件参数</param>
    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 语言切换时刷新所有导航标签和窗口标题
        foreach (var item in NavigationItems)
        {
            item.RefreshLabel();
        }
        Title = LocalizationService.Instance["Window.Title"];
    }

    // ═══════════════════════════════════════════════════════════════
    // 视图管理
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 根据当前选中的导航项更新显示视图。
    /// 优先从缓存获取已创建的视图实例，避免重复创建
    /// </summary>
    private void UpdateCurrentView()
    {
        var name = SelectedNavigationItem?.Name ?? "Home";
        CurrentView = PreloadView(name);
    }

    /// <summary>
    /// 预创建指定导航视图并加入缓存（不切换当前视图）。
    /// 供启动预热使用：提前创建并缓存实例，使其在首次导航时被直接复用，
    /// 配合离屏预热完成构图，避免首次切换页面时的卡顿。
    /// </summary>
    public UserControl? PreloadView(string name)
    {
        if (_viewCache.TryGetValue(name, out var cached))
            return cached;

        UserControl? view = name switch
        {
            "Home" => new HomeUserControl(),
            "Device" => new DeviceUserControl(),
            "Game" => new GameUserControl(),
            "Help" => new HelpUserControl(),
            "Settings" => new SettingsUserControl(),
            _ => null
        };

        if (view != null)
            _viewCache[name] = view;

        return view;
    }

    /// <summary>
    /// 从视图缓存中获取已预创建的视图实例（未创建则返回 null）。
    /// </summary>
    public UserControl? GetView(string name)
    {
        _viewCache.TryGetValue(name, out var view);
        return view;
    }
}
