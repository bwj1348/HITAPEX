using HITAPEX.Services;

namespace HITAPEX.ViewModels;

public class NavigationItem : ViewModelBase
{
    private bool _isSelected;
    private string _label;

    public string Name { get; }
    public string IconPath { get; }
    public string LocKey { get; }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public NavigationItem(string name, string iconPath, string locKey)
    {
        Name = name;
        IconPath = iconPath;
        LocKey = locKey;
        _label = LocalizationService.Instance[locKey];
    }

    /// <summary>
    /// 语言切换时刷新标签文本
    /// </summary>
    public void RefreshLabel()
    {
        Label = LocalizationService.Instance[LocKey];
    }
}
