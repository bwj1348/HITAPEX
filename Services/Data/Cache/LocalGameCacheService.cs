using System.IO;
using System.Text.Json;
using HITAPEX.Models;

namespace HITAPEX.Services.Data.Cache;

/// <summary>
/// 本地用户游戏数据缓存 —— 持久化用户操作产生的数据到磁盘 JSON 文件。
/// 仅保存用户数据（置顶状态、自定义启动路径、启动模式、最后启动时间），
/// 游戏元数据由 GameListConfig 硬编码，不参与缓存。
/// </summary>
/// <remarks>
/// 缓存文件路径：%LocalAppData%\HITAPEX\user_game_data.json
/// 线程安全性：静态方法，调用方（GameDataService）负责线程安全。
/// </remarks>
public static class LocalGameCacheService
{
    private static readonly string CacheFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static LocalGameCacheService()
    {
        var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HITAPEX");
        Directory.CreateDirectory(cacheDir);
        CacheFilePath = Path.Combine(cacheDir, "user_game_data.json");
    }

    /// <summary>保存用户游戏数据</summary>
    public static void Save(Dictionary<int, UserGameData> data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(CacheFilePath, json);
    }

    /// <summary>加载用户游戏数据。文件不存在返回空字典。</summary>
    public static Dictionary<int, UserGameData> Load()
    {
        if (!File.Exists(CacheFilePath))
            return [];

        try
        {
            var json = File.ReadAllText(CacheFilePath);
            return JsonSerializer.Deserialize<Dictionary<int, UserGameData>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
