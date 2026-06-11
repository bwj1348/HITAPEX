using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using HITAPEX.Views;

namespace HITAPEX.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private NavigationItem? _selectedNavigationItem;
    private UserControl? _currentView;
    private string _title = "HITAPEX Racing Simulator";

    // 视图缓存，避免每次导航都重新创建（保持设备连接事件订阅有效）
    private readonly Dictionary<string, UserControl> _viewCache = new();

    public ObservableCollection<NavigationItem> NavigationItems { get; }

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

    public UserControl? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public MainWindowViewModel()
    {
        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Home", "/Assets/HomeIcon.svg", "首 页"),
            new("Device", "/Assets/DeviceIcon.svg", "设 备"),
            new("Game", "/Assets/GameIcon.svg", "游 戏"),
            new("Help", "/Assets/HelpIcon.svg", "帮 助"),
            new("Settings", "/Assets/SettingsIcon.svg", "设 置")
        };

        SelectedNavigationItem = NavigationItems[0];
    }

    private void UpdateCurrentView()
    {
        var name = SelectedNavigationItem?.Name ?? "Home";
        if (_viewCache.TryGetValue(name, out var cached))
        {
            CurrentView = cached;
            return;
        }

        UserControl view = name switch
        {
            "Home" => new HomeUserControl(),
            "Device" => new DeviceUserControl(),
            "Game" => new GameUserControl(),
            "Help" => new HelpUserControl(),
            "Settings" => new SettingsUserControl(),
            _ => new HomeUserControl()
        };

        _viewCache[name] = view;
        CurrentView = view;
    }
}
