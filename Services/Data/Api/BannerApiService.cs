using HITAPEX.Models;
using HITAPEX.Services.Data.Models;

namespace HITAPEX.Services.Data.Api;

public class BannerApiService
{
    private readonly ApiClient _apiClient;
    private readonly string _mediaBaseUrl;

    private const string BannersEndpoint = "/api/banners?populate=*";

    public BannerApiService(ApiClient apiClient, string mediaBaseUrl)
    {
        _apiClient = apiClient;
        _mediaBaseUrl = mediaBaseUrl;
    }

    public async Task<List<BannerItem>> GetBannersAsync(CancellationToken ct = default)
    {
        var result = await _apiClient.GetAsync<ApiResponse<List<BannerApiDto>>>(BannersEndpoint, ct);

        if (!result.IsSuccess || result.Data?.Data == null)
            return new List<BannerItem>();

        return result.Data.Data.Take(3).Select(dto => new BannerItem
        {
            ImageUrl = _mediaBaseUrl + (dto.Image?.Url ?? ""),
            LinkUrl = dto.Url
        }).ToList();
    }
}
