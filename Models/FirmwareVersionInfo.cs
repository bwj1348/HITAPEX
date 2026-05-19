using System.Text.Json.Serialization;

namespace HITAPEX.Models;

public class FirmwareVersionInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("pid")]
    public string Pid { get; set; } = "";

    [JsonPropertyName("vid")]
    public string Vid { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("device_name")]
    public string DeviceName { get; set; } = "";

    [JsonPropertyName("update_log")]
    public string UpdateLog { get; set; } = "";

    [JsonPropertyName("update_file")]
    public FirmwareFileInfo? UpdateFile { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";

    [JsonPropertyName("publishedAt")]
    public string PublishedAt { get; set; } = "";

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    public int ParsedVid => int.TryParse(Vid, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;
    public int ParsedPid => int.TryParse(Pid, System.Globalization.NumberStyles.HexNumber, null, out var p) ? p : 0;

    public override string ToString() => $"[{DeviceName}] VID={Vid} PID={Pid} v{Version}";
}

public class FirmwareFileInfo
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

public class FirmwareApiResponse
{
    [JsonPropertyName("data")]
    public List<FirmwareVersionInfo> Data { get; set; } = new();

    [JsonPropertyName("meta")]
    public FirmwareApiMeta? Meta { get; set; }
}

public class FirmwareApiMeta
{
    [JsonPropertyName("pagination")]
    public FirmwarePagination? Pagination { get; set; }
}

public class FirmwarePagination
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
