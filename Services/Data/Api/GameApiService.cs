using HITAPEX.Models;
using HITAPEX.Services.Data.Cache;
using HITAPEX.Services.Data.Models;
using HITAPEX.Services.Data.Transformation;

namespace HITAPEX.Services.Data.Api;

public class GameApiService
{
    private readonly ApiClient _apiClient;
    private readonly CacheService _cache;
    private readonly DataTransformer _transformer;

    private const string GamesEndpoint = "/api/games?populate=*";
    private const string GamesCacheKey = "all_games";
    private static readonly TimeSpan GamesCacheTtl = TimeSpan.FromMinutes(10);

    public event Action<GameDataState>? StateChanged;

    public GameApiService(ApiClient apiClient, CacheService cache, DataTransformer transformer)
    {
        _apiClient = apiClient;
        _cache = cache;
        _transformer = transformer;
    }

    public async Task<List<GameItem>> GetGamesAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (!forceRefresh)
        {
            if (_cache.TryGet<List<GameItem>>(GamesCacheKey, out var cached))
                return cached!;

            var localGames = LocalGameCacheService.Load();
            if (localGames != null)
            {
                await ImageCacheService.CacheAllAsync(localGames);
                _cache.Set(GamesCacheKey, localGames, GamesCacheTtl);

                _ = BackgroundRefreshAsync();
                return localGames;
            }
        }

        return await FetchAndMergeAsync(skipIfUnchanged: false, ct);
    }

    private async Task BackgroundRefreshAsync()
    {
        try
        {
            await FetchAndMergeAsync(skipIfUnchanged: true, CancellationToken.None);
        }
        catch
        {
            // Silently fail — background refresh shouldn't disrupt UI
        }
    }

    private async Task<List<GameItem>> FetchAndMergeAsync(bool skipIfUnchanged, CancellationToken ct)
    {
        NotifyState(GameDataState.Loading);

        var result = await _apiClient.GetAsync<ApiResponse<List<GameApiDto>>>(GamesEndpoint, ct);

        if (!result.IsSuccess)
        {
            NotifyState(GameDataState.Error);

            var fallback = _cache.TryGet<List<GameItem>>(GamesCacheKey, out var stale) ? stale
                : LocalGameCacheService.Load();

            if (fallback != null)
            {
                await ImageCacheService.CacheAllAsync(fallback);
                NotifyState(GameDataState.Loaded);
                return fallback!;
            }

            throw new GameServiceException(result.ErrorMessage ?? "获取游戏数据失败", result.IsClientError);
        }

        var freshGames = _transformer.TransformGames(result.Data!.Data);
        var localExisting = LocalGameCacheService.Load();

        if (skipIfUnchanged && localExisting != null && !HasChanges(freshGames, localExisting))
        {
            NotifyState(GameDataState.Loaded);
            return localExisting;
        }

        var merged = MergeWithLocal(freshGames, localExisting);

        LocalGameCacheService.Save(merged);
        await ImageCacheService.CacheAllAsync(merged);
        _cache.Set(GamesCacheKey, merged, GamesCacheTtl);
        NotifyState(GameDataState.Loaded);
        return merged;
    }

    private static bool HasChanges(List<GameItem> fresh, List<GameItem> local)
    {
        if (fresh.Count != local.Count)
            return true;

        var localMap = local.ToDictionary(g => g.Id);

        foreach (var f in fresh)
        {
            if (!localMap.TryGetValue(f.Id, out var l))
                return true;

            if (f.Name != l.Name ||
                f.Description != l.Description ||
                f.SteamId != l.SteamId ||
                f.BgImageUrl != l.BgImageUrl ||
                f.CoverImageUrl != l.CoverImageUrl)
                return true;
        }

        return false;
    }

    private static List<GameItem> MergeWithLocal(List<GameItem> freshGames, List<GameItem>? localGames)
    {
        if (localGames == null || localGames.Count == 0)
            return freshGames;

        var localMap = localGames.ToDictionary(g => g.Id);

        foreach (var fresh in freshGames)
        {
            if (localMap.TryGetValue(fresh.Id, out var local))
            {
                fresh.IsPinned = local.IsPinned;
                fresh.LaunchPath = local.LaunchPath;
                if (local.LastLaunchTime > fresh.LastLaunchTime)
                    fresh.LastLaunchTime = local.LastLaunchTime;
                fresh.IsInstalled = local.IsInstalled;
            }
        }

        return freshGames;
    }

    public List<GameItem>? GetCachedGames()
    {
        _cache.TryGet<List<GameItem>>(GamesCacheKey, out var cached);
        return cached;
    }

    public void InvalidateCache()
    {
        _cache.Remove(GamesCacheKey);
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
