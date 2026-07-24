namespace HITAPEX.Models;

/// <summary>
/// 用户对单个游戏的操作数据（需持久化到本地）。
/// 只有用户操作产生的字段才需要缓存，游戏元数据来自硬编码配置。
/// </summary>
public class UserGameData
{
    public bool IsPinned { get; set; }
    public string LaunchPath { get; set; } = "";
    public DateTime? LastLaunchTime { get; set; }
    public LaunchModeUdf LaunchMode { get; set; } = LaunchModeUdf.Steam;
}

/// <summary>用户数据中的启动模式（用于 JSON 序列化）</summary>
public enum LaunchModeUdf
{
    /// <summary>通过 Steam 启动</summary>
    Steam,
    /// <summary>通过自定义路径启动</summary>
    CustomPath
}
