using System.Windows;

namespace HITAPEX.ViewModels;

public class NavigationItem : ViewModelBase
{
    private bool _isSelected;

    public string Name { get; }
    public string IconPath { get; }
    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public NavigationItem(string name, string iconPath, string label)
    {
        Name = name;
        IconPath = iconPath;
        Label = label;
    }
}
