using HITAPEX.Models;
using HITAPEX.Services.Data.Api;
using HITAPEX.Services.Data.Cache;

namespace HITAPEX.Services.Data;

/// <summary>
/// 游戏数据服务 —— 整合 API 数据、本地缓存和 Steam 安装检测，
/// 提供统一的游戏列表获取、用户数据持久化和安装状态检测入口。
/// 是 GameViewModel 的数据层核心。
/// </summary>
/// <remarks>
/// 数据流：GameListConfig（硬编码基础数据）→ API（元数据）→ LocalGameCacheService（用户操作数据）→ SteamInstallService（安装状态）。
/// 有内存缓存 _cachedGames，调用 forceRefresh: true 或 InvalidateCache() 可强制刷新。
/// </remarks>
public class GameDataService : IDisposable
{
    private readonly BannerApiService _bannerApi;
    private readonly ApiClient _apiClient;
    private readonly SteamInstallService _steamInstall;

    /// <summary>Strapi API 基础 URL</summary>
    private const string BaseUrl = "http://192.168.1.214:1337/api";
    /// <summary>Strapi 媒体资源基础 URL</summary>
    private const string MediaBaseUrl = "http://192.168.1.214:1337";
    /// <summary>Strapi API Token（Bearer 认证）</summary>
    private const string ApiToken = "b04e4b2ffa76e8ca6fc718886f85ba14bc4f06fc2dc706c34f3b3d2a1ffa7e41d178fbb7d232c2a27249f0de3b4f005558a00dba5a7ecda453fb280f7019578f3790b1c872b7160efb6fdf985524c74aa217a56f31f81ad18cec31ceee82c3fee19cf51300229104d3842e300ab899646e229b5a9c3b6852effc7e80e2d4421d";

    // 内存中的用户数据字典（与磁盘持久化同步）
    private Dictionary<int, UserGameData> _userData = [];
    private List<GameItem>? _cachedGames;

    /// <summary>游戏数据加载状态变更时触发（Loading / Loaded / Error）</summary>
    public event Action<GameDataState>? StateChanged;

    /// <summary>初始化游戏数据服务</summary>
    public GameDataService()
    {
        _apiClient = new ApiClient(BaseUrl, ApiToken);
        _bannerApi = new BannerApiService(_apiClient, MediaBaseUrl);
        _steamInstall = new SteamInstallService();
    }

    /// <summary>
    /// 获取完整游戏列表（合并硬编码配置 + 用户本地数据），有内存缓存。
    /// </summary>
    /// <param name="forceRefresh">是否强制重新加载（忽略缓存）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>GameItem 列表</returns>
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
    /// 持久化单个游戏的用户数据到磁盘，保留现有集合中的其他游戏数据。
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

    /// <summary>
    /// 获取缓存的游戏列表。若缓存不存在，则冷启动加载（合并硬编码 + 磁盘用户数据）。
    /// </summary>
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

    /// <summary>使内存缓存失效，下次调用 GetGamesAsync 将重新加载。</summary>
    public void InvalidateCache()
    {
        _cachedGames = null;
    }

    /// <summary>
    /// 通过 SteamInstallService 检测游戏的 Steam 安装状态，填充 IsInstalled 和 LastLaunchTime。
    /// 有自定义启动路径的游戏即使 Steam 未检测到也保留 IsInstalled = true。
    /// </summary>
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

    /// <summary>获取首页 Banner 列表（代理到 BannerApiService）</summary>
    public Task<List<BannerItem>> GetBannersAsync(CancellationToken ct = default)
        => _bannerApi.GetBannersAsync(ct);

    /// <summary>释放 API 客户端资源</summary>
    public void Dispose()
    {
        _apiClient.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>通知所有订阅者游戏数据加载状态已变更</summary>
    private void NotifyState(GameDataState state)
    {
        StateChanged?.Invoke(state);
    }
}

/// <summary>
/// 游戏数据加载状态枚举。
/// </summary>
public enum GameDataState
{
    /// <summary>加载中</summary>
    Loading,
    /// <summary>加载完成</summary>
    Loaded,
    /// <summary>加载出错</summary>
    Error
}

/// <summary>
/// 游戏数据服务异常。
/// </summary>
public class GameServiceException : Exception
{
    /// <summary>是否由客户端错误（如网络超时、4xx 响应）导致</summary>
    public bool IsClientError { get; }

    /// <summary>
    /// 初始化游戏服务异常。
    /// </summary>
    /// <param name="message">错误描述</param>
    /// <param name="isClientError">是否客户端错误</param>
    public GameServiceException(string message, bool isClientError = false)
        : base(message)
    {
        IsClientError = isClientError;
    }
}
