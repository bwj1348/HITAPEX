using System.Text.Json.Serialization;

namespace HITAPEX.Models;

/// <summary>
/// 客户端安装包信息（对应 Strapi /api/ClientInstallers 返回的单条记录）。
/// </summary>
public class ClientInstallerInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("log")]
    public string Log { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";

    [JsonPropertyName("publishedAt")]
    public string PublishedAt { get; set; } = "";

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    [JsonPropertyName("installer")]
    public InstallerFileInfo? Installer { get; set; }

    /// <summary>
    /// 尝试将 Version 字符串解析为 System.Version 对象，解析失败返回 0.0.0.0。
    /// </summary>
    public System.Version ParsedVersion =>
        System.Version.TryParse(Version, out var v) ? v : new System.Version(0, 0, 0, 0);

    public override string ToString() => $"ClientInstaller v{Version} [{Installer?.Name}]";
}

/// <summary>
/// 安装包文件信息（Strapi upload 媒体字段）。
/// </summary>
public class InstallerFileInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("ext")]
    public string Ext { get; set; } = "";

    [JsonPropertyName("mime")]
    public string Mime { get; set; } = "";

    [JsonPropertyName("size")]
    public double Size { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";
}

/// <summary>
/// Strapi 风格 API 响应信封：/api/ClientInstallers 的顶层 JSON。
/// </summary>
public class ClientInstallerApiResponse
{
    [JsonPropertyName("data")]
    public List<ClientInstallerInfo> Data { get; set; } = new();

    [JsonPropertyName("meta")]
    public ClientInstallerApiMeta? Meta { get; set; }
}

public class ClientInstallerApiMeta
{
    [JsonPropertyName("pagination")]
    public ClientInstallerPagination? Pagination { get; set; }
}

public class ClientInstallerPagination
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
