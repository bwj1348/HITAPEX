using System.ComponentModel;

namespace HITAPEX.Models;

public class GameItem : INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string? CoverImageUrl { get; set; }
    public string? BgImageUrl { get; set; }
    public string SteamId { get; set; } = "";
    public bool IsInstalled { get; set; }
    public string LaunchPath { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string LastPlayed { get; set; } = "";
    
    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned != value)
            {
                _isPinned = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPinned)));
            }
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
}
