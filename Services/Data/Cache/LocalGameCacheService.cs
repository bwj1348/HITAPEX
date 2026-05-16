using System.IO;
using System.Text.Json;
using HITAPEX.Models;

namespace HITAPEX.Services.Data.Cache;

public static class LocalGameCacheService
{
    private static readonly string CacheFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static LocalGameCacheService()
    {
        var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HITAPEX");
        Directory.CreateDirectory(cacheDir);
        CacheFilePath = Path.Combine(cacheDir, "game_cache.json");
    }

    public static void Save(List<GameItem> games)
    {
        var json = JsonSerializer.Serialize(games, JsonOptions);
        File.WriteAllText(CacheFilePath, json);
    }

    public static List<GameItem>? Load()
    {
        if (!File.Exists(CacheFilePath))
            return null;

        try
        {
            var json = File.ReadAllText(CacheFilePath);
            return JsonSerializer.Deserialize<List<GameItem>>(json);
        }
        catch
        {
            return null;
        }
    }
}
