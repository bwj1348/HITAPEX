using System.Text.Json.Serialization;

namespace HITAPEX.Services.Data.Models;

/// <summary>
/// Strapi v4 API 标准响应包装 —— data 字段直接泛型化。
/// 用于 Banner 等返回单个数据对象的端点。
/// </summary>
/// <typeparam name="T">data 字段的实际数据类型</typeparam>
public class ApiResponse<T>
{
    /// <summary>API 响应的 data 字段（Strapi v4 格式）</summary>
    [JsonPropertyName("data")]
    public T Data { get; set; } = default!;
}

/// <summary>
/// Banner API DTO —— 对应 Strapi Banner 集合类型的数据模型。
/// </summary>
public class BannerApiDto
{
    /// <summary>Banner 记录 ID</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Banner 点击跳转链接</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    /// <summary>Banner 关联的图片媒体资产</summary>
    [JsonPropertyName("image")]
    public MediaAssetDto? Image { get; set; }
}

/// <summary>
/// Strapi 媒体资产 DTO —— 对应 Strapi upload 插件中的文件记录。
/// </summary>
public class MediaAssetDto
{
    /// <summary>媒体资产 ID</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>媒体资产文件名</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>媒体资产相对路径（需拼接 MediaBaseUrl 构成完整 URL）</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}
