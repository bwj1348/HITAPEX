using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using HITAPEX.Models;

namespace HITAPEX.Services.Data.Api;

public class FirmwareApiService
{
    private readonly ApiClient _apiClient;

    private const string BaseUrl = "http://192.168.1.214:1337/api";
    private const string MediaBaseUrl = "http://192.168.1.214:1337";
    private const string ApiToken = "b04e4b2ffa76e8ca6fc718886f85ba14bc4f06fc2dc706c34f3b3d2a1ffa7e41d178fbb7d232c2a27249f0de3b4f005558a00dba5a7ecda453fb280f7019578f3790b1c872b7160efb6fdf985524c74aa217a56f31f81ad18cec31ceee82c3fee19cf51300229104d3842e300ab899646e229b5a9c3b6852effc7e80e2d4421d";

    public FirmwareApiService()
    {
        _apiClient = new ApiClient(BaseUrl, ApiToken);
    }

    /// <summary>
    /// Get all firmware versions from the API.
    /// </summary>
    public async Task<List<FirmwareVersionInfo>> GetFirmwareVersionsAsync(CancellationToken ct = default)
    {
        Debug.WriteLine("[FirmwareApi] 请求固件版本列表...");

        var result = await _apiClient.GetAsync<FirmwareApiResponse>("/api/firmware-versions?populate=*", ct);

        if (result.IsSuccess && result.Data?.Data != null)
        {
            Debug.WriteLine($"[FirmwareApi] 获取到 {result.Data.Data.Count} 条固件版本记录");
            foreach (var fw in result.Data.Data)
            {
                Debug.WriteLine($"[FirmwareApi]   {fw}");
            }
            return result.Data.Data;
        }

        Debug.WriteLine($"[FirmwareApi] 获取固件版本失败: {result.ErrorMessage}");
        return new List<FirmwareVersionInfo>();
    }

    /// <summary>
    /// Download firmware binary file from the API.
    /// </summary>
    public async Task<byte[]?> DownloadFirmwareAsync(string fileUrl, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var fullUrl = MediaBaseUrl + fileUrl;
        Debug.WriteLine($"[FirmwareApi] 下载固件文件: {fullUrl}");

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiToken);

            var response = await httpClient.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[FirmwareApi] 下载失败: HTTP {(int)response.StatusCode}");
                return null;
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            using var stream = await response.Content.ReadAsStreamAsync(ct);

            var buffer = new byte[totalBytes > 0 ? totalBytes : 1024 * 1024];
            using var ms = new MemoryStream();
            var readBuffer = new byte[8192];
            int bytesRead;
            long totalRead = 0;

            while ((bytesRead = await stream.ReadAsync(readBuffer, ct)) > 0)
            {
                await ms.WriteAsync(readBuffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    progress.Report((int)(totalRead * 100 / totalBytes));
                }
            }

            var firmwareData = ms.ToArray();
            Debug.WriteLine($"[FirmwareApi] 固件下载完成: {firmwareData.Length} 字节");
            return firmwareData;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FirmwareApi] 下载异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Find firmware version info matching a specific VID/PID pair.
    /// </summary>
    public FirmwareVersionInfo? FindFirmwareForDevice(List<FirmwareVersionInfo> firmwares, int vid, int pid)
    {
        var vidHex = vid.ToString("X4");
        var pidHex = pid.ToString("X4");

        return firmwares.FirstOrDefault(f =>
            string.Equals(f.Vid, vidHex, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.Pid, pidHex, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _apiClient.Dispose();
    }
}
