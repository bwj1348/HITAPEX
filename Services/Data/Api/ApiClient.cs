using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HITAPEX.Services.Data.Api;

/// <summary>
/// HTTP API 客户端 —— 封装带重试、超时和 Bearer Token 认证的 HTTP 请求。
/// 所有 API 服务（Banner、固件、客户端安装包等）共用此客户端实例。
/// </summary>
/// <remarks>
/// 重试策略：4xx 客户端错误不重试，5xx 和网络异常以指数退避重试（500ms → 1000ms → 2000ms）。
/// 默认超时：15 秒。
/// </remarks>
public class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelayBase;

    /// <summary>JSON 反序列化选项（属性名大小写不敏感）</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 初始化 API 客户端。
    /// </summary>
    /// <param name="baseUrl">API 服务器基础 URL（如 http://192.168.1.214:1337/api）</param>
    /// <param name="apiToken">Strapi API Token（Bearer 认证）</param>
    /// <param name="maxRetries">最大重试次数（默认 3）</param>
    /// <param name="retryDelayMs">初始重试延迟毫秒数（默认 500，指数退避）</param>
    public ApiClient(
        string baseUrl,
        string apiToken,
        int maxRetries = 3,
        int retryDelayMs = 500)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _maxRetries = maxRetries;
        _retryDelayBase = TimeSpan.FromMilliseconds(retryDelayMs);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// 发送 GET 请求并解析 JSON 响应。
    /// </summary>
    /// <typeparam name="T">响应数据类型</typeparam>
    /// <param name="endpoint">API 端点路径（如 "/api/banners?populate=*"）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含数据或错误信息的 ApiResult</returns>
    public async Task<ApiResult<T>> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(endpoint, ct);

                // 成功 → 直接解析 JSON
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
                    return ApiResult<T>.Success(data!);
                }

                // 4xx 客户端错误 → 不重试，直接返回错误
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    return ApiResult<T>.Failure(
                        $"请求失败 ({(int)response.StatusCode}): {errorBody}",
                        isClientError: true);
                }
            }
            catch (TaskCanceledException)
            {
                return ApiResult<T>.Failure("请求已取消", isClientError: false);
            }
            catch (HttpRequestException ex)
            {
                // 网络异常 → 记录并准备重试
                lastException = ex;
            }
            catch (OperationCanceledException)
            {
                return ApiResult<T>.Failure("请求已取消", isClientError: false);
            }

            // 指数退避：500ms → 1000ms → 2000ms
            if (attempt < _maxRetries)
            {
                var delayMs = (int)(_retryDelayBase.TotalMilliseconds * Math.Pow(2, attempt));
                await Task.Delay(delayMs, ct);
            }
        }

        return ApiResult<T>.Failure(
            $"请求失败，已重试 {_maxRetries} 次: {lastException?.Message}",
            isClientError: false);
    }

    /// <summary>释放 HttpClient 资源</summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// API 请求结果封装，使用 Discriminated Union 模式，避免异常驱动的错误处理。
/// 调用方通过检查 IsSuccess 判断成功/失败，不使用 try/catch。
/// </summary>
/// <typeparam name="T">成功时携带的数据类型</typeparam>
public class ApiResult<T>
{
    /// <summary>请求是否成功</summary>
    public bool IsSuccess { get; }
    /// <summary>成功时返回的数据（失败时为 default）</summary>
    public T? Data { get; }
    /// <summary>失败时的错误描述</summary>
    public string? ErrorMessage { get; }
    /// <summary>是否由客户端错误（4xx）导致失败</summary>
    public bool IsClientError { get; }

    private ApiResult(bool isSuccess, T? data, string? errorMessage, bool isClientError)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
        IsClientError = isClientError;
    }

    /// <summary>创建成功结果</summary>
    public static ApiResult<T> Success(T data) =>
        new(true, data, null, false);

    /// <summary>创建失败结果</summary>
    public static ApiResult<T> Failure(string error, bool isClientError) =>
        new(false, default, error, isClientError);
}
