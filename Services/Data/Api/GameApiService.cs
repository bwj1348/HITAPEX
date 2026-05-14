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
        if (!forceRefresh && _cache.TryGet<List<GameItem>>(GamesCacheKey, out var cached))
            return cached!;

        NotifyState(GameDataState.Loading);

        var result = await _apiClient.GetAsync<ApiResponse<List<GameApiDto>>>(GamesEndpoint, ct);

        if (!result.IsSuccess)
        {
            NotifyState(GameDataState.Error);

            if (_cache.TryGet<List<GameItem>>(GamesCacheKey, out var stale))
            {
                NotifyState(GameDataState.Loaded);
                return stale!;
            }

            throw new GameServiceException(result.ErrorMessage ?? "获取游戏数据失败", result.IsClientError);
        }

        var games = _transformer.TransformGames(result.Data!.Data);
        _cache.Set(GamesCacheKey, games, GamesCacheTtl);
        NotifyState(GameDataState.Loaded);
        return games;
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
