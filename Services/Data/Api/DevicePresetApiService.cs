using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using HITAPEX.Models;
using HITAPEX.Models.Usb;
using HITAPEX.Views.DeviceParameters;

namespace HITAPEX.Services.Data.Api;

/// <summary>
/// 设备预设 API 服务 —— 从 Strapi 后端获取官方预设列表并缓存到本地。
/// 每条预设携带各自的 publishedAt，支持逐条按时间戳增量更新。
/// </summary>
public class DevicePresetApiService
{
    private readonly ApiClient _apiClient;

    private const string BaseUrl = "http://192.168.1.214:1337/api";
    private const string ApiToken = "b04e4b2ffa76e8ca6fc718886f85ba14bc4f06fc2dc706c34f3b3d2a1ffa7e41d178fbb7d232c2a27249f0de3b4f005558a00dba5a7ecda453fb280f7019578f3790b1c872b7160efb6fdf985524c74aa217a56f31f81ad18cec31ceee82c3fee19cf51300229104d3842e300ab899646e229b5a9c3b6852effc7e80e2d4421d";
    private const string Endpoint = "/api/device-presets?populate=*";

    public DevicePresetApiService()
    {
        _apiClient = new ApiClient(BaseUrl, ApiToken);
    }

    /// <summary>
    /// 从 API 获取所有设备预设，每条携带其 publishedAt。
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>成功时返回预设列表（含各自的 publishedAt），失败返回 null</returns>
    public async Task<List<DevicePresetEntry>?> FetchPresetsAsync(CancellationToken ct = default)
    {
        Debug.WriteLine("[DevicePresetApi] 请求设备预设列表...");

        var result = await _apiClient.GetAsync<DevicePresetApiResponse>(Endpoint, ct);

        if (!result.IsSuccess || result.Data?.Data == null)
        {
            Debug.WriteLine($"[DevicePresetApi] 获取设备预设失败: {result.ErrorMessage}");
            return null;
        }

        var entries = new List<DevicePresetEntry>();

        foreach (var entry in result.Data.Data)
        {
            if (entry.ConfigData == null) continue;

            var config = entry.ConfigData;

            if (!Enum.IsDefined(typeof(DeviceType), config.DeviceType))
                continue;

            entries.Add(new DevicePresetEntry
            {
                PublishedAt = entry.PublishedAt,
                Preset = new PresetItem
                {
                    Name = config.Name,
                    Games = config.Games ?? [],
                    IsPersonal = false,
                    DeviceType = (DeviceType)config.DeviceType,
                    PedalParameters = config.PedalParameters,
                    WheelParameters = config.WheelParameters,
                    BaseParameters = config.BaseParameters,
                }
            });
        }

        Debug.WriteLine($"[DevicePresetApi] 获取到 {entries.Count} 条预设");
        return entries;
    }
}

// ════════════════════════════════════════════════════════════════
//  API 响应 / 缓存模型
// ════════════════════════════════════════════════════════════════

/// <summary>API 顶层响应包装</summary>
public class DevicePresetApiResponse
{
    public List<DevicePresetApiItem> Data { get; set; } = [];
}

/// <summary>API 单条预设条目</summary>
public class DevicePresetApiItem
{
    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }

    [JsonPropertyName("config_data")]
    public DevicePresetConfigData? ConfigData { get; set; }
}

/// <summary>config_data 中的预设数据，结构与 PresetItem 兼容</summary>
public class DevicePresetConfigData
{
    public string Name { get; set; } = string.Empty;
    public List<string> Games { get; set; } = [];
    public int DeviceType { get; set; }
    public PedalPresetSnapshot? PedalParameters { get; set; }
    public WheelPresetSnapshot? WheelParameters { get; set; }
    public BasePresetSnapshot? BaseParameters { get; set; }
}

/// <summary>带 publishedAt 的预设条目，用于 API 返回和本地缓存读写</summary>
public class DevicePresetEntry
{
    public string? PublishedAt { get; set; }
    public PresetItem Preset { get; set; } = new();
}

/// <summary>本地缓存文件顶层结构</summary>
public class OfficialCacheFile
{
    public List<DevicePresetEntry> Presets { get; set; } = [];
}
