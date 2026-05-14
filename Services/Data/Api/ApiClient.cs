using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HITAPEX.Services.Data.Api;

public class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelayBase;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

    public async Task<ApiResult<T>> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var response = await _httpClient.GetAsync(endpoint, ct);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
                    return ApiResult<T>.Success(data!);
                }

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
                lastException = ex;
            }
            catch (OperationCanceledException)
            {
                return ApiResult<T>.Failure("请求已取消", isClientError: false);
            }

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

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public class ApiResult<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? ErrorMessage { get; }
    public bool IsClientError { get; }

    private ApiResult(bool isSuccess, T? data, string? errorMessage, bool isClientError)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorMessage = errorMessage;
        IsClientError = isClientError;
    }

    public static ApiResult<T> Success(T data) =>
        new(true, data, null, false);

    public static ApiResult<T> Failure(string error, bool isClientError) =>
        new(false, default, error, isClientError);
}
