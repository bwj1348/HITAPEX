using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
    private string? _jwtToken;

    /// <summary>
    /// 401 未授权时触发的全局回调（token 无效或过期）。调用方（如用户系统）注册此回调
    /// 用于统一清空本地 token / 通知 UI 跳转登录，避免每个接口单独处理。
    /// </summary>
    public Action? UnauthorizedHandler { get; set; }

    /// <summary>JSON 反序列化选项（属性名大小写不敏感）</summary>
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 初始化 API 客户端。
    /// </summary>
    /// <param name="baseUrl">API 服务器基础 URL（如 http://192.168.1.214:1337）</param>
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

        if (!string.IsNullOrEmpty(apiToken))
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// 设置用户 JWT token，用于认证 API 请求。设置后所有请求将使用用户 token 替代默认 token。
    /// </summary>
    public void SetUserToken(string? jwt)
    {
        _jwtToken = jwt;
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(jwt) ? null :
            new AuthenticationHeaderValue("Bearer", jwt);
    }

    /// <summary>获取当前用户 token</summary>
    public string? GetUserToken() => _jwtToken;

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
        string? lastErrorMessage = null;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var url = $"GET {_baseUrl}{endpoint}";
                Debug.WriteLine($"[API] >>> {url}");

                var response = await _httpClient.GetAsync(endpoint, ct);

                var responseBody = await response.Content.ReadAsStringAsync(ct);
                Debug.WriteLine($"[API] <<< GET {(int)response.StatusCode} {response.ReasonPhrase}");
                Debug.WriteLine($"[API] <<< Body: {responseBody}");

                // 成功 → 直接解析 JSON
                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
                    return ApiResult<T>.Success(data!);
                }

                // 非 2xx：解析统一错误响应 { error: { status, code, message, details } }
                var (code, message, retryAfter) = ParseApiError(responseBody, response);
                lastErrorMessage = message ?? $"请求失败 ({(int)response.StatusCode})";

                // 401 未授权 → 触发全局处理（token 失效 → 清空登录态）
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    UnauthorizedHandler?.Invoke();

                // 4xx 客户端错误（含 401 / 429）→ 不重试，直接返回错误
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    return ApiResult<T>.Failure(lastErrorMessage, isClientError: true,
                        errorCode: code, retryAfterSeconds: retryAfter);
                }
            }
            catch (TaskCanceledException)
            {
                return ApiResult<T>.Failure("请求已取消", isClientError: false);
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                Debug.WriteLine($"[API] [网络异常] GET {endpoint}: {ex.Message}");
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
            $"请求失败，已重试 {_maxRetries} 次: {lastException?.Message ?? lastErrorMessage}",
            isClientError: false);
    }

    /// <summary>释放 HttpClient 资源</summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }

    // ════════════════════════════════════════════════════════════════
    // POST / PUT / DELETE
    // ════════════════════════════════════════════════════════════════

    /// <summary>发送 POST 请求（JSON body）</summary>
    public async Task<ApiResult<TResponse>> PostAsync<TResponse>(string endpoint, object body, CancellationToken ct = default)
        => await SendJsonAsync<TResponse>(HttpMethod.Post, endpoint, body, ct);

    /// <summary>发送 PUT 请求（JSON body）</summary>
    public async Task<ApiResult<TResponse>> PutAsync<TResponse>(string endpoint, object body, CancellationToken ct = default)
        => await SendJsonAsync<TResponse>(HttpMethod.Put, endpoint, body, ct);

    /// <summary>
    /// 上传文件（multipart/form-data）—— 用于头像等媒体上传。
    /// 返回上传后的文件 ID，后续可通过 PUT /api/users/me { "image": fileId } 关联到用户。
    /// </summary>
    public async Task<ApiResult<int>> UploadFileAsync(string filePath, CancellationToken ct = default)
    {
        var url = $"POST(Multipart) {_baseUrl}/api/upload";
        Debug.WriteLine($"[API] >>> {url}");
        Debug.WriteLine($"[API] >>> File: {filePath}");

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                using var formData = new MultipartFormDataContent();
                var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    filePath.EndsWith(".png") ? "image/png" :
                    filePath.EndsWith(".jpg") || filePath.EndsWith(".jpeg") ? "image/jpeg" :
                    filePath.EndsWith(".bmp") ? "image/bmp" : "application/octet-stream");
                formData.Add(fileContent, "files", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync("/api/upload", formData, ct);
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                Debug.WriteLine($"[API] <<< {(int)response.StatusCode}");
                Debug.WriteLine($"[API] <<< Body: {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    // Strapi upload response: [{ "id": 5, "url": "/uploads/..." }]
                    var uploaded = JsonSerializer.Deserialize<List<UploadResult>>(responseBody, JsonOptions);
                    if (uploaded?.Count > 0)
                        return ApiResult<int>.Success(uploaded[0].Id);
                    return ApiResult<int>.Failure("上传成功但未获取到文件 ID", isClientError: false);
                }

                // 非 2xx：解析统一错误响应
                var (code, message, retryAfter) = ParseApiError(responseBody, response);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    UnauthorizedHandler?.Invoke();

                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                    return ApiResult<int>.Failure(message ?? $"上传失败 ({(int)response.StatusCode})",
                        isClientError: true, errorCode: code, retryAfterSeconds: retryAfter);
            }
            catch (TaskCanceledException) { return ApiResult<int>.Failure("上传已取消", isClientError: false); }
            catch (HttpRequestException ex) { Debug.WriteLine($"[API] [网络异常] 上传: {ex.Message}"); }
            catch (OperationCanceledException) { return ApiResult<int>.Failure("上传已取消", isClientError: false); }

            if (attempt < _maxRetries)
                await Task.Delay((int)(_retryDelayBase.TotalMilliseconds * Math.Pow(2, attempt)), ct);
        }

        return ApiResult<int>.Failure("上传失败，已重试", isClientError: false);
    }

    /// <summary>
    /// 发送 POST 请求并自动解包 {success, data} 响应（用于用户 API：jwt/user 嵌套在 data 中）。
    /// </summary>
    /// <summary>
    /// 发送 POST 请求并自动解包 {success, data} 响应（用于用户 API：jwt/user 嵌套在 data 中）。
    /// </summary>
    /// <param name="endpoint">API 端点路径</param>
    /// <param name="body">请求体</param>
    /// <param name="authToken">可选：本请求专用的 Bearer token（如 step-up JWT），覆盖默认 token</param>
    public async Task<ApiResult<TData?>> PostWrappedAsync<TData>(string endpoint, object body, string? authToken = null, CancellationToken ct = default)
    {
        var result = await SendJsonAsync<ApiWrappedResponse<TData>>(HttpMethod.Post, endpoint, body, ct, authToken);
        if (!result.IsSuccess)
            return ApiResult<TData?>.Failure(result.ErrorMessage ?? "请求失败", result.IsClientError,
                errorCode: result.ErrorCode, retryAfterSeconds: result.RetryAfterSeconds);
        return ApiResult<TData?>.Success(result.Data!.Data);
    }

    /// <summary>
    /// 发送 PUT 请求并自动解包 {success, data} 响应。
    /// </summary>
    public async Task<ApiResult<TData?>> PutWrappedAsync<TData>(string endpoint, object body, CancellationToken ct = default)
    {
        var result = await SendJsonAsync<ApiWrappedResponse<TData>>(HttpMethod.Put, endpoint, body, ct);
        if (!result.IsSuccess)
            return ApiResult<TData?>.Failure(result.ErrorMessage ?? "请求失败", result.IsClientError,
                errorCode: result.ErrorCode, retryAfterSeconds: result.RetryAfterSeconds);
        return ApiResult<TData?>.Success(result.Data!.Data);
    }

    /// <summary>
    /// 发送 GET 请求并自动解包 {success, data} 响应（用于 users/me、user-presets 等统一包装格式的接口）。
    /// </summary>
    public async Task<ApiResult<TData?>> GetWrappedAsync<TData>(string endpoint, CancellationToken ct = default)
    {
        var result = await GetAsync<ApiWrappedResponse<TData>>(endpoint, ct);
        if (!result.IsSuccess)
            return ApiResult<TData?>.Failure(result.ErrorMessage ?? "请求失败", result.IsClientError,
                errorCode: result.ErrorCode, retryAfterSeconds: result.RetryAfterSeconds);
        return ApiResult<TData?>.Success(result.Data!.Data);
    }

    private async Task<ApiResult<TResponse>> SendJsonAsync<TResponse>(HttpMethod method, string endpoint, object? body,
        CancellationToken ct = default, string? authToken = null)
    {
        Exception? lastException = null;
        string? lastErrorMessage = null;

        var json = body == null ? null : JsonSerializer.Serialize(body, JsonOptions);

        var url = $"{method} {_baseUrl}{endpoint}";
        Debug.WriteLine($"[API] >>> {url}");
        if (json != null) Debug.WriteLine($"[API] >>> Body: {json}");

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(method, endpoint);
                // 每次请求都新建 content——HttpContent 只能被发送一次，重试复用会抛异常
                if (json != null)
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                // 本请求专用 Bearer token（如 step-up JWT），覆盖 HttpClient 默认 token
                if (!string.IsNullOrEmpty(authToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

                var response = await _httpClient.SendAsync(request, ct);

                var responseBody = await response.Content.ReadAsStringAsync(ct);
                Debug.WriteLine($"[API] <<< {(int)response.StatusCode} {response.ReasonPhrase}");
                Debug.WriteLine($"[API] <<< Body: {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions);
                    return ApiResult<TResponse>.Success(data!);
                }

                // 非 2xx：解析统一错误响应 { error: { status, code, message, details } }
                var (code, message, retryAfter) = ParseApiError(responseBody, response);
                lastErrorMessage = message ?? $"请求失败 ({(int)response.StatusCode})";

                // 401 未授权 → 触发全局处理（token 失效 → 清空登录态）
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    UnauthorizedHandler?.Invoke();

                // 4xx 客户端错误（含 401 / 429）→ 不重试，直接返回错误
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    return ApiResult<TResponse>.Failure(lastErrorMessage, isClientError: true,
                        errorCode: code, retryAfterSeconds: retryAfter);
                }
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine($"[API] [取消] {url}");
                return ApiResult<TResponse>.Failure("请求已取消", isClientError: false);
            }
            catch (HttpRequestException ex) { lastException = ex; Debug.WriteLine($"[API] [网络异常] {url}: {ex.Message}"); }
            catch (OperationCanceledException) { Debug.WriteLine($"[API] [取消] {url}"); return ApiResult<TResponse>.Failure("请求已取消", isClientError: false); }

            if (attempt < _maxRetries)
                await Task.Delay((int)(_retryDelayBase.TotalMilliseconds * Math.Pow(2, attempt)), ct);
        }

        return ApiResult<TResponse>.Failure($"请求失败，已重试 {_maxRetries} 次: {lastException?.Message ?? lastErrorMessage}", isClientError: false);
    }

    /// <summary>发送 DELETE 请求（无请求体）</summary>
    public async Task<ApiResult<T>> DeleteAsync<T>(string endpoint, CancellationToken ct = default)
        => await SendJsonAsync<T>(HttpMethod.Delete, endpoint, null, ct);

    /// <summary>
    /// 发送 multipart/form-data 的 PUT 请求（用于 update-me 一次性上传头像 + 可选改字段），
    /// 自动解包 {success, data} 响应。
    /// </summary>
    /// <param name="endpoint">API 端点路径</param>
    /// <param name="formFields">文本字段（如 username），键值对</param>
    /// <param name="filePaths">文件字段，键为字段名，值为本地文件路径（如 image → 头像文件）</param>
    /// <returns>成功时返回 {success, data} 中的 data</returns>
    public async Task<ApiResult<TData?>> PutMultipartAsync<TData>(
        string endpoint,
        Dictionary<string, string>? formFields = null,
        Dictionary<string, string>? filePaths = null,
        CancellationToken ct = default)
    {
        var result = await SendMultipartAsync<ApiWrappedResponse<TData>>(HttpMethod.Put, endpoint, formFields, filePaths, ct);
        if (!result.IsSuccess)
            return ApiResult<TData?>.Failure(result.ErrorMessage ?? "请求失败", result.IsClientError,
                errorCode: result.ErrorCode, retryAfterSeconds: result.RetryAfterSeconds);
        return ApiResult<TData?>.Success(result.Data!.Data);
    }

    /// <summary>
    /// 通用 multipart 请求：把文本字段和文件字段组装成 form-data 发送。
    /// 响应按 {success, data} 包装解析。
    /// </summary>
    private async Task<ApiResult<TResponse>> SendMultipartAsync<TResponse>(
        HttpMethod method, string endpoint,
        Dictionary<string, string>? formFields, Dictionary<string, string>? filePaths,
        CancellationToken ct)
    {
        Exception? lastException = null;
        string? lastErrorMessage = null;

        var url = $"{method}(Multipart) {_baseUrl}{endpoint}";
        Debug.WriteLine($"[API] >>> {url}");
        if (formFields != null)
            foreach (var (k, v) in formFields) Debug.WriteLine($"[API] >>> Field: {k}={v}");
        if (filePaths != null)
            foreach (var (k, path) in filePaths) Debug.WriteLine($"[API] >>> File: {k}={path}");

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                using var formData = new MultipartFormDataContent();

                // 文本字段
                if (formFields != null)
                    foreach (var (k, v) in formFields)
                        formData.Add(new StringContent(v ?? string.Empty), k);

                // 文件字段
                if (filePaths != null)
                    foreach (var (k, path) in filePaths)
                    {
                        var fileBytes = await File.ReadAllBytesAsync(path, ct);
                        var fileContent = new ByteArrayContent(fileBytes);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(path));
                        formData.Add(fileContent, k, Path.GetFileName(path));
                    }

                var response = await _httpClient.SendAsync(new HttpRequestMessage(method, endpoint) { Content = formData }, ct);
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                Debug.WriteLine($"[API] <<< {(int)response.StatusCode} {response.ReasonPhrase}");
                Debug.WriteLine($"[API] <<< Body: {responseBody}");

                if (response.IsSuccessStatusCode)
                {
                    var data = JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions);
                    return ApiResult<TResponse>.Success(data!);
                }

                var (code, message, retryAfter) = ParseApiError(responseBody, response);
                lastErrorMessage = message ?? $"请求失败 ({(int)response.StatusCode})";

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    UnauthorizedHandler?.Invoke();

                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    return ApiResult<TResponse>.Failure(lastErrorMessage, isClientError: true,
                        errorCode: code, retryAfterSeconds: retryAfter);
                }
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine($"[API] [取消] {url}");
                return ApiResult<TResponse>.Failure("请求已取消", isClientError: false);
            }
            catch (HttpRequestException ex) { lastException = ex; Debug.WriteLine($"[API] [网络异常] {url}: {ex.Message}"); }
            catch (OperationCanceledException) { Debug.WriteLine($"[API] [取消] {url}"); return ApiResult<TResponse>.Failure("请求已取消", isClientError: false); }

            if (attempt < _maxRetries)
                await Task.Delay((int)(_retryDelayBase.TotalMilliseconds * Math.Pow(2, attempt)), ct);
        }

        return ApiResult<TResponse>.Failure($"请求失败，已重试 {_maxRetries} 次: {lastException?.Message ?? lastErrorMessage}", isClientError: false);
    }

    /// <summary>根据文件扩展名返回 MIME 类型（头像上传等用途）</summary>
    private static string GetMimeType(string filePath) =>
        filePath.EndsWith(".png") ? "image/png" :
        filePath.EndsWith(".jpg") || filePath.EndsWith(".jpeg") ? "image/jpeg" :
        filePath.EndsWith(".webp") ? "image/webp" :
        filePath.EndsWith(".gif") ? "image/gif" :
        filePath.EndsWith(".bmp") ? "image/bmp" : "application/octet-stream";

    /// <summary>
    /// 解析统一错误响应体：{ success: false, error: { status, code, message, details, timestamp } }。
    /// 限流 429 时从 details.retry_after 或 Retry-After 响应头读取倒计时秒数。
    /// </summary>
    /// <returns>(业务错误码, 用户可读错误信息, 限流倒计时秒数)</returns>
    private static (string? Code, string? Message, int? RetryAfterSeconds) ParseApiError(
        string responseBody, HttpResponseMessage response)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object)
            {
                // 直接用 DTO 反序列化整个 error 对象，规避 JsonElement 逐字段解析的兼容性问题
                var body = error.Deserialize<ApiErrorBody>(JsonOptions);
                if (body?.Message != null)
                {
                    // 429 限流倒计时：优先 details.retry_after，其次 Retry-After 响应头
                    int? retryAfter = null;
                    if (body.Details is JsonElement details &&
                        details.ValueKind == JsonValueKind.Object &&
                        details.TryGetProperty("retry_after", out var raEl) &&
                        raEl.ValueKind == JsonValueKind.Number)
                    {
                        retryAfter = raEl.GetInt32();
                    }
                    if (retryAfter == null && response.Headers.TryGetValues("Retry-After", out var retryValues) &&
                        int.TryParse(retryValues.FirstOrDefault(), out var headerValue))
                    {
                        retryAfter = headerValue;
                    }

                    Debug.WriteLine($"[API] 错误解析: code={body.Code ?? "<null>"}, message={body.Message}");
                    return (body.Code, body.Message, retryAfter);
                }
            }
        }
        catch (JsonException)
        {
            // 响应体不是合法 JSON → 走兜底
        }

        return (null, responseBody.Trim(), null);
    }
}

/// <summary>统一错误响应中 error 对象的结构（用于 DTO 反序列化）</summary>
internal class ApiErrorBody
{
    public int Status { get; set; }
    public string? Code { get; set; }
    public string? Message { get; set; }
    public JsonElement? Details { get; set; }
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
    /// <summary>失败时的错误描述（error.message，可直接 toast 给用户）</summary>
    public string? ErrorMessage { get; }
    /// <summary>是否由客户端错误（4xx）导致失败</summary>
    public bool IsClientError { get; }
    /// <summary>业务错误码（error.code，稳定契约，UI 按此分支处理）</summary>
    public string? ErrorCode { get; }
    /// <summary>限流倒计时秒数（status=429 时从 error.details.retry_after 或 Retry-After 头解析）</summary>
    public int? RetryAfterSeconds { get; }

    private ApiResult(bool isSuccess, T? data, string? errorMessage, bool isClientError,
        string? errorCode = null, int? retryAfterSeconds = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
        IsClientError = isClientError;
        ErrorCode = errorCode;
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>创建成功结果</summary>
    public static ApiResult<T> Success(T data) =>
        new(true, data, null, false);

    /// <summary>创建失败结果</summary>
    public static ApiResult<T> Failure(string error, bool isClientError,
        string? errorCode = null, int? retryAfterSeconds = null) =>
        new(false, default, error, isClientError, errorCode, retryAfterSeconds);
}

/// <summary>带 { success, data } 包装的 API 响应（用户系统 API 统一格式）</summary>
public class ApiWrappedResponse<T>
{
    [System.Text.Json.Serialization.JsonPropertyName("success")]
    public bool Success { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("data")]
    public T? Data { get; set; }
}

/// <summary>Strapi 文件上传响应格式（POST /api/upload 返回 [{id, url, ...}]）</summary>
public class UploadResult
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public int Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("url")]
    public string? Url { get; set; }
}
