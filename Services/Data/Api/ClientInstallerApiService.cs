using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using HITAPEX.Models;
using HITAPEX.Services;

namespace HITAPEX.Services.Data.Api;

/// <summary>
/// 客户端安装包 API 服务 —— 获取最新版本信息并下载安装包。
/// </summary>
public class ClientInstallerApiService
{
    private readonly ApiClient _apiClient;

    private const string BaseUrl = "http://192.168.1.214:1337/api";
    private const string MediaBaseUrl = "http://192.168.1.214:1337";
    private const string ApiToken = "b04e4b2ffa76e8ca6fc718886f85ba14bc4f06fc2dc706c34f3b3d2a1ffa7e41d178fbb7d232c2a27249f0de3b4f005558a00dba5a7ecda453fb280f7019578f3790b1c872b7160efb6fdf985524c74aa217a56f31f81ad18cec31ceee82c3fee19cf51300229104d3842e300ab899646e229b5a9c3b6852effc7e80e2d4421d";

    public ClientInstallerApiService()
    {
        _apiClient = new ApiClient(BaseUrl, ApiToken);
    }

    /// <summary>
    /// 获取最新客户端安装包信息（按 publishedAt 倒序取第一条）。
    /// </summary>
    public async Task<ClientInstallerInfo?> GetLatestInstallerAsync(CancellationToken ct = default)
    {
        var locale = LocalizationService.Instance.CurrentLanguage == "en-US" ? "en" : "zh-Hans";
        Debug.WriteLine($"[ClientInstallerApi] 请求客户端安装包列表 (locale={locale})...");

        var result = await _apiClient.GetAsync<ClientInstallerApiResponse>(
            $"/api/client-installers?populate=*&sort=publishedAt:desc&pagination[pageSize]=1&locale={locale}", ct);

        if (result.IsSuccess && result.Data?.Data != null && result.Data.Data.Count > 0)
        {
            var latest = result.Data.Data[0];
            Debug.WriteLine($"[ClientInstallerApi] 最新版本: v{latest.Version}, 安装包: {latest.Installer?.Name}");
            return latest;
        }

        Debug.WriteLine($"[ClientInstallerApi] 获取客户端安装包失败: {result.ErrorMessage}");
        return null;
    }

    /// <summary>
    /// 下载安装包文件到本地临时目录，返回保存的完整路径。
    /// </summary>
    public async Task<string?> DownloadInstallerAsync(
        string fileUrl,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var fullUrl = MediaBaseUrl + fileUrl;
        Debug.WriteLine($"[ClientInstallerApi] 下载安装包: {fullUrl}");

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", ApiToken);

            var response = await httpClient.GetAsync(fullUrl, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[ClientInstallerApi] 下载失败: HTTP {(int)response.StatusCode}");
                return null;
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var fileName = Path.GetFileName(new Uri(fullUrl).AbsolutePath);
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var readBuffer = new byte[8192];
            int bytesRead;
            long totalRead = 0;

            while ((bytesRead = await stream.ReadAsync(readBuffer, ct)) > 0)
            {
                await fs.WriteAsync(readBuffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                if (totalBytes > 0 && progress != null)
                {
                    progress.Report((int)(totalRead * 100 / totalBytes));
                }
            }

            Debug.WriteLine($"[ClientInstallerApi] 安装包下载完成: {tempPath} ({totalRead} 字节)");
            return tempPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClientInstallerApi] 下载异常: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _apiClient.Dispose();
    }
}
