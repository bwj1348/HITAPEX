using HITAPEX.Models;
using HITAPEX.Services.Data.Models;

namespace HITAPEX.Services.Data.Api;

/// <summary>
/// Banner（横幅广告）API 服务 —— 从 Strapi 后端获取首页轮播横幅数据。
/// 最多取前 3 条，图片 URL 拼接 mediaBaseUrl 后返回 ViewModel。
/// </summary>
public class BannerApiService
{
    private readonly ApiClient _apiClient;
    private readonly string _mediaBaseUrl;

    /// <summary>Strapi banners 端点，populate=* 获取关联图片</summary>
    private const string BannersEndpoint = "/api/banners?populate=*";

    /// <summary>
    /// 初始化 Banner API 服务。
    /// </summary>
    /// <param name="apiClient">共享的 API 客户端实例</param>
    /// <param name="mediaBaseUrl">媒体资源基础 URL（用于拼接图片完整 URL）</param>
    public BannerApiService(ApiClient apiClient, string mediaBaseUrl)
    {
        _apiClient = apiClient;
        _mediaBaseUrl = mediaBaseUrl;
    }

    /// <summary>
    /// 获取首页 Banner 列表（最多 3 条）。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>BannerItem 列表（失败时返回空列表）</returns>
    public async Task<List<BannerItem>> GetBannersAsync(CancellationToken ct = default)
    {
        var result = await _apiClient.GetAsync<ApiResponse<List<BannerApiDto>>>(BannersEndpoint, ct);

        if (!result.IsSuccess || result.Data?.Data == null)
            return new List<BannerItem>();

        // 取前 3 条，拼接完整图片 URL
        return result.Data.Data.Take(3).Select(dto => new BannerItem
        {
            ImageUrl = _mediaBaseUrl + (dto.Image?.Url ?? ""),
            LinkUrl = dto.Url
        }).ToList();
    }
}
