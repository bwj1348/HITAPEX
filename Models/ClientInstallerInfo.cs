using System.Text.Json.Serialization;

namespace HITAPEX.Models;

/// <summary>
/// 客户端安装包信息（对应 Strapi /api/ClientInstallers 返回的单条记录）。
/// </summary>
public class ClientInstallerInfo
{
    /// <summary>Strapi 记录 ID</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Strapi 文档 ID</summary>
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    /// <summary>版本更新日志</summary>
    [JsonPropertyName("log")]
    public string Log { get; set; } = "";

    /// <summary>安装包版本号，如 "1.0.0"</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

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

    /// <summary>安装包文件信息</summary>
    [JsonPropertyName("installer")]
    public InstallerFileInfo? Installer { get; set; }

    /// <summary>
    /// 尝试将 Version 字符串解析为 System.Version 对象，解析失败返回 0.0.0.0。
    /// </summary>
    public System.Version ParsedVersion =>
        System.Version.TryParse(Version, out var v) ? v : new System.Version(0, 0, 0, 0);

    /// <summary>返回客户端安装包信息摘要字符串</summary>
    public override string ToString() => $"ClientInstaller v{Version} [{Installer?.Name}]";
}

/// <summary>
/// 安装包文件信息（Strapi upload 媒体字段）。
/// </summary>
public class InstallerFileInfo
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

    /// <summary>文件扩展名，如 ".exe"</summary>
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
/// Strapi 风格 API 响应信封：/api/ClientInstallers 的顶层 JSON。
/// </summary>
public class ClientInstallerApiResponse
{
    /// <summary>客户端安装包信息列表</summary>
    [JsonPropertyName("data")]
    public List<ClientInstallerInfo> Data { get; set; } = new();

    /// <summary>分页元数据</summary>
    [JsonPropertyName("meta")]
    public ClientInstallerApiMeta? Meta { get; set; }
}

/// <summary>
/// 客户端安装包 API 响应元数据。
/// </summary>
public class ClientInstallerApiMeta
{
    /// <summary>分页信息</summary>
    [JsonPropertyName("pagination")]
    public ClientInstallerPagination? Pagination { get; set; }
}

/// <summary>
/// 客户端安装包 API 分页信息。
/// </summary>
public class ClientInstallerPagination
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
