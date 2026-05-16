using HITAPEX.Models;
using HITAPEX.Services.Data.Api;
using HITAPEX.Services.Data.Cache;
using HITAPEX.Services.Data.Transformation;

namespace HITAPEX.Services.Data;

public class GameDataService : IDisposable
{
    private readonly GameApiService _gameApi;
    private readonly BannerApiService _bannerApi;
    private readonly ApiClient _apiClient;
    private readonly SteamInstallService _steamInstall;

    private const string BaseUrl = "http://192.168.1.214:1337/api";
    private const string MediaBaseUrl = "http://192.168.1.214:1337";
    private const string ApiToken = "b04e4b2ffa76e8ca6fc718886f85ba14bc4f06fc2dc706c34f3b3d2a1ffa7e41d178fbb7d232c2a27249f0de3b4f005558a00dba5a7ecda453fb280f7019578f3790b1c872b7160efb6fdf985524c74aa217a56f31f81ad18cec31ceee82c3fee19cf51300229104d3842e300ab899646e229b5a9c3b6852effc7e80e2d4421d";

    public event Action<GameDataState>? StateChanged
    {
        add => _gameApi.StateChanged += value;
        remove => _gameApi.StateChanged -= value;
    }

    public GameDataService()
    {
        _apiClient = new ApiClient(BaseUrl, ApiToken);
        var cache = new CacheService(TimeSpan.FromMinutes(5));
        var transformer = new DataTransformer(MediaBaseUrl);
        _gameApi = new GameApiService(_apiClient, cache, transformer);
        _bannerApi = new BannerApiService(_apiClient, MediaBaseUrl);
        _steamInstall = new SteamInstallService();
    }

    public Task<List<GameItem>> GetGamesAsync(bool forceRefresh = false, CancellationToken ct = default)
        => _gameApi.GetGamesAsync(forceRefresh, ct);

    public List<GameItem>? GetCachedGames()
        => _gameApi.GetCachedGames();

    public void InvalidateCache()
        => _gameApi.InvalidateCache();

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
                game.IsInstalled = info.IsInstalled;
                if (!game.LastLaunchTime.HasValue || info.LastPlayed > game.LastLaunchTime.Value)
                    game.LastLaunchTime = info.LastPlayed;
                if (info.IsInstalled && info.InstallDir != null)
                {
                    game.LaunchPath = info.InstallDir;
                }
            }
        }
    }

    public Task<List<BannerItem>> GetBannersAsync(CancellationToken ct = default)
        => _bannerApi.GetBannersAsync(ct);

    public void Dispose()
    {
        _apiClient.Dispose();
    }
}
