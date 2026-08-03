using System.ComponentModel;

namespace HITAPEX.Models;

/// <summary>
/// 单个游戏的数据模型。承载游戏的元数据（ID、名称、封面等）、安装状态、启动配置和用户操作数据。
/// 实现了 <see cref="INotifyPropertyChanged"/>，仅 <see cref="IsPinned"/> 属性支持变更通知
/// （因为置顶操作需要 UI 即时响应排序变化，其他属性由 GameDataService 批量更新后重新绑定）。
/// </summary>
/// <remarks>
/// 游戏元数据（Name、Description、SteamId、CoverImageUrl 等）由 <see cref="GameListConfig.GetGames"/>
/// 硬编码提供；用户操作数据（IsPinned、LaunchPath、LaunchMode、LastLaunchTime）由
/// <see cref="Services.Data.LocalGameCacheService"/> 持久化到本地 JSON 文件。
/// </remarks>
public class GameItem : INotifyPropertyChanged
{
    // ════════════════════════════════════════════════════════════════
    //  游戏身份标识
    // ════════════════════════════════════════════════════════════════

    /// <summary>游戏唯一标识。Steam 游戏使用 Steam App ID，非 Steam 游戏使用自定义 ID（如 RBR=22, LFS=25）</summary>
    public int Id { get; set; }

    /// <summary>游戏显示名称（中文）。如 "Assetto Corsa Competizione"</summary>
    public string Name { get; set; } = "";

    /// <summary>游戏缩写/简称，用于预设关联和筛选。如 "ACC"、"FH5"</summary>
    public string Abbreviation { get; set; } = "";

    /// <summary>Steam 商店 App ID 字符串。非 Steam 游戏可能为自定义值（如 "22"），用于 Steam 协议启动和安装检测</summary>
    public string SteamId { get; set; } = "";

    // ════════════════════════════════════════════════════════════════
    //  视觉资源路径
    // ════════════════════════════════════════════════════════════════

    /// <summary>游戏封面图片路径（列表卡片用）。相对路径如 "/Assets/77_cover.jpg"</summary>
    public string? CoverImageUrl { get; set; }

    /// <summary>游戏详情页背景图片路径（大图）。相对路径如 "/Assets/77_bg.jpg"</summary>
    public string? BgImageUrl { get; set; }

    // ════════════════════════════════════════════════════════════════
    //  安装与启动配置
    // ════════════════════════════════════════════════════════════════

    /// <summary>游戏是否已安装（由 <see cref="Services.SteamInstallService"/> 检测 Steam 库中的清单文件判断）</summary>
    public bool IsInstalled { get; set; }

    /// <summary>是否需要为该游戏部署遥测配置（DLL 注入、XML 修改、文件复制等）。true 时 GameUserControl 显示"配置"按钮</summary>
    public bool NeedsTelemetryConfig { get; set; }

    /// <summary>自定义启动路径（仅当 <see cref="LaunchMode"/> 为 CustomPath 时有效）。指向游戏可执行文件的完整路径</summary>
    public string LaunchPath { get; set; } = "";

    /// <summary>启动模式：Steam 协议启动（steam://run/{SteamId}）或本地自定义路径直接启动</summary>
    public LaunchModeUdf LaunchMode { get; set; } = LaunchModeUdf.Steam;

    // ════════════════════════════════════════════════════════════════
    //  描述信息
    // ════════════════════════════════════════════════════════════════

    /// <summary>游戏中文描述。显示在 GameUserControl 的游戏详情区域</summary>
    public string Description { get; set; } = "";

    /// <summary>游戏英文描述。用于英文界面下的游戏详情展示</summary>
    public string DescriptionEn { get; set; } = "";

    // ════════════════════════════════════════════════════════════════
    //  运行状态
    // ════════════════════════════════════════════════════════════════

    /// <summary>游戏版本号字符串</summary>
    public string Version { get; set; } = "";

    /// <summary>最后游玩时间（格式化字符串，用于 UI 显示）。如 "3 天前"、"2025-07-15"</summary>
    public string LastPlayed { get; set; } = "";

    /// <summary>最后启动时间戳（DateTime 类型）。<see cref="GameLauncher"/> 在启动成功后记录，用于排序</summary>
    public DateTime? LastLaunchTime { get; set; }

    // ════════════════════════════════════════════════════════════════
    //  用户操作属性（含变更通知）
    // ════════════════════════════════════════════════════════════════

    /// <summary>后备字段：是否由用户置顶</summary>
    private bool _isPinned;

    /// <summary>
    /// 是否由用户置顶（收藏/固定到列表顶部）。
    /// 该属性是 GameItem 中唯一需要主动通知 UI 的属性——
    /// 因为置顶操作会立即影响游戏列表的排序结果（置顶游戏排在最前），
    /// UI 必须实时刷新。其他属性在 GameDataService 批量更新后通过重新绑定数据源来反映。
    /// </summary>
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

    /// <summary>属性变更通知事件。由 <see cref="IsPinned"/> 使用，供 WPF 绑定系统订阅</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
}
