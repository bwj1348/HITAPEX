using System.IO;
using System.Net.Http;
using HITAPEX.Models;

namespace HITAPEX.Services.Data.Cache;

public static class ImageCacheService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly string CacheDir;

    static ImageCacheService()
    {
        CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HITAPEX", "images");
        Directory.CreateDirectory(CacheDir);
    }

    public static async Task CacheAllAsync(List<GameItem> games)
    {
        var tasks = new List<Task>();
        foreach (var game in games)
        {
            if (!string.IsNullOrEmpty(game.CoverImageUrl) &&
                game.CoverImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                tasks.Add(CacheImageAsync(game.Id, "cover", game.CoverImageUrl, localPath => game.CoverImageUrl = localPath));
            if (!string.IsNullOrEmpty(game.BgImageUrl))
                tasks.Add(CacheImageAsync(game.Id, "bg", game.BgImageUrl, localPath => game.BgImageUrl = localPath));
        }
        await Task.WhenAll(tasks);
    }

    private static async Task CacheImageAsync(int gameId, string type, string url, Action<string> setPath)
    {
        var localPath = LocalPath(gameId, type);

        if (File.Exists(localPath))
        {
            setPath(localPath);
            return;
        }

        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(localPath, bytes);
            setPath(localPath);
        }
        catch
        {
            // Keep remote URL on failure
        }
    }

    private static string LocalPath(int gameId, string type)
    {
        return Path.Combine(CacheDir, $"{gameId}_{type}.jpg");
    }
}
