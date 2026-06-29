using System.Text.Json.Serialization;

namespace HITAPEX.Services.Data.Models;

public class ApiResponse<T>
{
    [JsonPropertyName("data")]
    public T Data { get; set; } = default!;
}

public class BannerApiDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("image")]
    public MediaAssetDto? Image { get; set; }
}

public class MediaAssetDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}
