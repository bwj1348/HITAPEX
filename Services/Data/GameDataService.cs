using HITAPEX.Models;
using HITAPEX.Services.Data.Api;
using HITAPEX.Services.Data.Cache;

namespace HITAPEX.Services.Data;

public class GameDataService : IDisposable
{
    private readonly BannerApiService _bannerApi;
    private readonly ApiClient _apiClient;
    private readonly SteamInstallService _steamInstall;

    private const string BaseUrl = "http://192.168.1.214:1337/api";
    private const string MediaBaseUrl = "http://192.168.1.214:1337";
    private const string ApiToken = "b04e4b2ffa76e8ca6fc718886f85ba14bc4f06fc2dc706c34f3b3d2a1ffa7e41d178fbb7d232c2a27249f0de3b4f005558a00dba5a7ecda453fb280f7019578f3790b1c872b7160efb6fdf985524c74aa217a56f31f81ad18cec31ceee82c3fee19cf51300229104d3842e300ab899646e229b5a9c3b6852effc7e80e2d4421d";

    // 内存中的用户数据（与磁盘同步）
    private Dictionary<int, UserGameData> _userData = [];
    private List<GameItem>? _cachedGames;

    public event Action<GameDataState>? StateChanged;

    public GameDataService()
    {
        _apiClient = new ApiClient(BaseUrl, ApiToken);
        _bannerApi = new BannerApiService(_apiClient, MediaBaseUrl);
        _steamInstall = new SteamInstallService();
    }

    /// <summary>
    /// 获取游戏列表（从硬编码配置读取，合并本地用户数据）。
    /// </summary>
    public Task<List<GameItem>> GetGamesAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!forceRefresh && _cachedGames != null)
            return Task.FromResult(_cachedGames);

        NotifyState(GameDataState.Loading);

        try
        {
            var games = GameListConfig.GetGames();
            _userData = LocalGameCacheService.Load();
            ApplyUserData(games, _userData);
            _cachedGames = games;

            NotifyState(GameDataState.Loaded);
            return Task.FromResult(_cachedGames);
        }
        catch (Exception)
        {
            NotifyState(GameDataState.Error);
            return Task.FromResult(_cachedGames ?? GameListConfig.GetGames());
        }
    }

    /// <summary>
    /// 将用户数据应用到游戏列表（IsPinned / IsInstalled / LaunchPath / LastLaunchTime）。
    /// </summary>
    private static void ApplyUserData(List<GameItem> games, Dictionary<int, UserGameData> userData)
    {
        foreach (var game in games)
        {
            if (userData.TryGetValue(game.Id, out var data))
            {
                game.IsPinned = data.IsPinned;
                game.LaunchPath = data.LaunchPath;
                game.LaunchMode = data.LaunchMode;
                // 有自定义启动路径 → 用户手动配置过 → 视为已安装
                if (!string.IsNullOrWhiteSpace(data.LaunchPath))
                    game.IsInstalled = true;
                if (data.LastLaunchTime.HasValue &&
                    (!game.LastLaunchTime.HasValue || data.LastLaunchTime > game.LastLaunchTime))
                {
                    game.LastLaunchTime = data.LastLaunchTime;
                }
            }
        }
    }

    /// <summary>
    /// 持久化单个游戏的用户数据到磁盘。
    /// 在用户置顶/取消置顶、设置启动路径、启动游戏后调用。
    /// </summary>
    public void SaveUserData(GameItem game)
    {
        if (!_userData.TryGetValue(game.Id, out var existing))
            existing = new UserGameData();

        existing.IsPinned = game.IsPinned;
        existing.LaunchPath = game.LaunchPath;
        existing.LaunchMode = game.LaunchMode;
        existing.LastLaunchTime = game.LastLaunchTime;

        _userData[game.Id] = existing;
        LocalGameCacheService.Save(_userData);
    }

    public List<GameItem>? GetCachedGames()
    {
        if (_cachedGames != null)
            return _cachedGames;

        // 冷启动兜底：加载硬编码数据 + 磁盘用户数据
        _userData = LocalGameCacheService.Load();
        var games = GameListConfig.GetGames();
        ApplyUserData(games, _userData);
        _cachedGames = games;
        return games;
    }

    public void InvalidateCache()
    {
        _cachedGames = null;
    }

    public void EnrichWithInstallStatus(IList<GameItem> games)
    {
        var steamIds = games
            .Where(g => !string.IsNullOrWhiteSpace(g.SteamId))
            .Select(g => g.SteamId)
            .Distinct();

        var installInfo = _steamInstall.CheckInstalled(steamIds);

        foreach (var game in games)
        {
            if (!string.IsNullOrWhiteSpace(game.SteamId) && installInfo.TryGetValue(game.SteamId, out var info))
            {
                // Steam 检测到安装 → 设为 true
                // Steam 未检测到但有自定义启动路径 → 保留 true（用户手动配置）
                // Steam 未检测到且无自定义路径 → 设为 false
                game.IsInstalled = info.IsInstalled || !string.IsNullOrWhiteSpace(game.LaunchPath);
                if (!game.LastLaunchTime.HasValue || info.LastPlayed > game.LastLaunchTime.Value)
                    game.LastLaunchTime = info.LastPlayed;
            }
        }
    }

    public Task<List<BannerItem>> GetBannersAsync(CancellationToken ct = default)
        => _bannerApi.GetBannersAsync(ct);

    public void Dispose()
    {
        _apiClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private void NotifyState(GameDataState state)
    {
        StateChanged?.Invoke(state);
    }
}

public enum GameDataState
{
    Loading,
    Loaded,
    Error
}

public class GameServiceException : Exception
{
    public bool IsClientError { get; }

    public GameServiceException(string message, bool isClientError = false)
        : base(message)
    {
        IsClientError = isClientError;
    }
}
