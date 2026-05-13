using System.Collections.ObjectModel;
using System.Windows.Controls;
using HITAPEX.Views;

namespace HITAPEX.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private NavigationItem? _selectedNavigationItem;
    private UserControl? _currentView;
    private string _title = "HITAPEX Racing Simulator";

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
            new("Home", "/Assets/HomeIcon.svg", "首\u200A页"),
            new("Device", "/Assets/DeviceIcon.svg", "设\u200A备"),
            new("Game", "/Assets/GameIcon.svg", "游\u200A戏"),
            new("Help", "/Assets/HelpIcon.svg", "帮\u200A助"),
            new("Settings", "/Assets/SettingsIcon.svg", "设\u200A置")
        };

        SelectedNavigationItem = NavigationItems[0];
    }

    private void UpdateCurrentView()
    {
        CurrentView = SelectedNavigationItem?.Name switch
        {
            "Home" => new HomeUserControl(),
            "Device" => new DeviceUserControl(),
            "Game" => new GameUserControl(),
            "Help" => new HelpUserControl(),
            "Settings" => new SettingsUserControl(),
            _ => new HomeUserControl()
        };
    }
}
