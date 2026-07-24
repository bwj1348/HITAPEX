using System.Text.Json.Serialization;

namespace HITAPEX.Models;

/// <summary>
/// 固件版本信息，对应 Strapi /api/FirmwareVersions 返回的单条记录。
/// 包含设备标识（VID/PID）、固件版本号、更新日志及对应的固件文件信息。
/// </summary>
public class FirmwareVersionInfo
{
    /// <summary>Strapi 记录 ID</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Strapi 文档 ID</summary>
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    /// <summary>产品 ID（十六进制字符串）</summary>
    [JsonPropertyName("pid")]
    public string Pid { get; set; } = "";

    /// <summary>供应商 ID（十六进制字符串）</summary>
    [JsonPropertyName("vid")]
    public string Vid { get; set; } = "";

    /// <summary>固件版本号字符串，如 "1.0.2"</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    /// <summary>设备名称</summary>
    [JsonPropertyName("device_name")]
    public string DeviceName { get; set; } = "";

    /// <summary>版本更新日志</summary>
    [JsonPropertyName("update_log")]
    public string UpdateLog { get; set; } = "";

    /// <summary>固件文件信息</summary>
    [JsonPropertyName("update_file")]
    public FirmwareFileInfo? UpdateFile { get; set; }

    /// <summary>记录创建时间</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    /// <summary>记录更新时间</summary>
    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";

    /// <summary>记录发布时间</summary>
    [JsonPropertyName("publishedAt")]
    public string PublishedAt { get; set; } = "";

    /// <summary>地区语言代码</summary>
    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    /// <summary>将 VID 十六进制字符串解析为整数，解析失败返回 0</summary>
    public int ParsedVid => int.TryParse(Vid, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;

    /// <summary>将 PID 十六进制字符串解析为整数，解析失败返回 0</summary>
    public int ParsedPid => int.TryParse(Pid, System.Globalization.NumberStyles.HexNumber, null, out var p) ? p : 0;

    /// <summary>返回固件版本信息摘要字符串</summary>
    public override string ToString() => $"[{DeviceName}] VID={Vid} PID={Pid} v{Version}";
}

/// <summary>
/// 固件文件信息，对应 Strapi upload 媒体字段。
/// </summary>
public class FirmwareFileInfo
{
    /// <summary>文件记录 ID</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>文件文档 ID</summary>
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    /// <summary>文件名</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>文件下载 URL</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    /// <summary>文件扩展名，如 ".bin"</summary>
    [JsonPropertyName("ext")]
    public string Ext { get; set; } = "";

    /// <summary>文件 MIME 类型</summary>
    [JsonPropertyName("mime")]
    public string Mime { get; set; } = "";

    /// <summary>文件大小（字节）</summary>
    [JsonPropertyName("size")]
    public double Size { get; set; }

    /// <summary>文件哈希值（用于校验完整性）</summary>
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";
}

/// <summary>
/// 固件版本 API 响应信封，对应 /api/FirmwareVersions 的顶层 JSON。
/// </summary>
public class FirmwareApiResponse
{
    /// <summary>固件版本信息列表</summary>
    [JsonPropertyName("data")]
    public List<FirmwareVersionInfo> Data { get; set; } = new();

    /// <summary>分页元数据</summary>
    [JsonPropertyName("meta")]
    public FirmwareApiMeta? Meta { get; set; }
}

/// <summary>
/// 固件 API 响应元数据。
/// </summary>
public class FirmwareApiMeta
{
    /// <summary>分页信息</summary>
    [JsonPropertyName("pagination")]
    public FirmwarePagination? Pagination { get; set; }
}

/// <summary>
/// 固件 API 分页信息。
/// </summary>
public class FirmwarePagination
{
    /// <summary>当前页码</summary>
    [JsonPropertyName("page")]
    public int Page { get; set; }

    /// <summary>每页记录数</summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    /// <summary>总页数</summary>
    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    /// <summary>总记录数</summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}
