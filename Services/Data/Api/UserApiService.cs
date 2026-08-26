using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HITAPEX.Models.Usb;

namespace HITAPEX.Services.Data.Api;

/// <summary>
/// 用户系统 API 服务 —— 封装所有用户认证、信息管理、云预设接口。
/// 使用 ApiClient 发送 HTTP 请求，通过 JWT token 实现认证状态管理，
/// Token 使用 Windows DPAPI 加密存储到本地配置。
/// </summary>
public class UserApiService
{
    private readonly ApiClient _apiClient;

    /// <summary>
    /// API 服务器基础地址（主机根，不含 /api——接口路径已带 /api 前缀）。
    /// 从用户设置读取，可配置（上线后 IP / 域名会变），默认开发机内网地址；
    /// 同时用于拼接头像等媒体资源的完整地址。
    /// </summary>
    public static string BaseUrl
    {
        get
        {
            var url = Properties.Settings.Default.ApiBaseUrl;
            return string.IsNullOrWhiteSpace(url) ? "http://192.168.1.214:1337" : url.TrimEnd('/');
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 登录状态事件
    // ════════════════════════════════════════════════════════════════

    /// <summary>登录/退出时触发，用于通知 UI 刷新登录状态</summary>
    public event Action? LoginStateChanged;

    // ════════════════════════════════════════════════════════════════
    // 当前登录用户信息（由登录/获取用户信息后填充）
    // ════════════════════════════════════════════════════════════════

    /// <summary>当前登录用户信息（成功登录或恢复会话后填充）</summary>
    public UserInfo? CurrentUser { get; set; }

    // ════════════════════════════════════════════════════════════════
    // Token 加密存储（DPAPI — Windows 用户级别加密）
    // ════════════════════════════════════════════════════════════════

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HITAPEX.JWT.Salt.2026");

    private static string EncryptToken(string plain)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plain);
        var cipherBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipherBytes);
    }

    private static string? DecryptToken(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return null;
        try
        {
            var cipherBytes = Convert.FromBase64String(cipher);
            var plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // 兼容旧版本未加密 token（以 JWT 特征头 "eyJ" 开头）
            if (cipher.StartsWith("eyJ"))
            {
                Debug.WriteLine("[UserApi] 检测到旧版明文 token，本次自动升级为加密存储");
                return cipher;
            }
            return null;
        }
    }

    public UserApiService()
    {
        _apiClient = new ApiClient(BaseUrl, "");
        // 401 未授权全局处理：token 无效/过期时清空登录态（触发 LoginStateChanged 通知 UI 刷新）
        _apiClient.UnauthorizedHandler = ClearToken;
    }

    public bool IsLoggedIn => !string.IsNullOrEmpty(Properties.Settings.Default.UserAccessToken);

    // ════════════════════════════════════════════════════════════════
    // Token 管理
    // ════════════════════════════════════════════════════════════════

    /// <summary>加密并持久化 JWT token，同时设置到 HTTP 客户端并触发登录状态变更通知</summary>
    private void SaveToken(string jwt)
    {
        Debug.WriteLine($"[UserApi] SaveToken: jwt 长度={jwt.Length}");
        Properties.Settings.Default.UserAccessToken = EncryptToken(jwt);
        Properties.Settings.Default.Save();
        Debug.WriteLine($"[UserApi] Token 已加密保存, 密文长度={Properties.Settings.Default.UserAccessToken.Length}");
        _apiClient.SetUserToken(jwt);
        NotifyLoginStateChanged();
    }

    /// <summary>清除本地 token 和当前用户信息，通知 UI 状态变更</summary>
    private void ClearToken()
    {
        Properties.Settings.Default.UserAccessToken = "";
        Properties.Settings.Default.Save();
        _apiClient.SetUserToken(null);
        CurrentUser = null;
        NotifyLoginStateChanged();
    }

    /// <summary>应用启动时加载已保存的 token 并调用 refresh-token 验证/续期</summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        var savedToken = DecryptToken(Properties.Settings.Default.UserAccessToken);
        if (string.IsNullOrEmpty(savedToken)) return false;

        _apiClient.SetUserToken(savedToken);

        var refreshResult = await _apiClient.PostWrappedAsync<RefreshTokenResponse>("/api/auth/refresh-token", new { });
        if (refreshResult.IsSuccess && refreshResult.Data != null)
        {
            CurrentUser = refreshResult.Data.User;
            SaveToken(refreshResult.Data.Jwt);
            return true;
        }

        ClearToken();
        return false;
    }

    /// <summary>退出登录，清除 token 和用户信息</summary>
    public void Logout()
    {
        ClearToken();
    }

    private void NotifyLoginStateChanged() => LoginStateChanged?.Invoke();

    // ════════════════════════════════════════════════════════════════
    // 一、验证码注册
    // ════════════════════════════════════════════════════════════════

    /// <summary>注册（发送验证码）—— POST /api/auth/local/register-otp</summary>
    public async Task<ApiResult<RegisterOtpResponse?>> RegisterOtpAsync(string email, string username, string password)
        => await _apiClient.PostWrappedAsync<RegisterOtpResponse>("/api/auth/local/register-otp",
            new { email, username, password = PasswordHasher.Hash(password) });

    /// <summary>验证邮箱（激活 / 验证码登录）并保存 JWT —— POST /api/auth/verify-otp</summary>
    public async Task<ApiResult<VerifyOtpResponse?>> VerifyOtpAsync(string email, string code)
    {
        var result = await _apiClient.PostWrappedAsync<VerifyOtpResponse>("/api/auth/verify-otp", new { email, code });
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Data?.Jwt))
        {
            CurrentUser = result.Data.User;
            SaveToken(result.Data!.Jwt);
        }
        return result;
    }

    /// <summary>重发注册验证码 —— POST /api/auth/resend-otp</summary>
    public async Task<ApiResult<ResendOtpResponse?>> ResendOtpAsync(string email)
        => await _apiClient.PostWrappedAsync<ResendOtpResponse>("/api/auth/resend-otp", new { email });

    // ════════════════════════════════════════════════════════════════
    // 二、验证码登录
    // ════════════════════════════════════════════════════════════════

    /// <summary>登录（发送验证码）—— POST /api/auth/login-otp</summary>
    public async Task<ApiResult<LoginOtpResponse?>> LoginOtpAsync(string email)
        => await _apiClient.PostWrappedAsync<LoginOtpResponse>("/api/auth/login-otp", new { email });

    // ════════════════════════════════════════════════════════════════
    // 三、密码登录
    // ════════════════════════════════════════════════════════════════

    /// <summary>密码登录并保存 JWT —— POST /api/auth/local</summary>
    public async Task<ApiResult<AuthLocalResponse?>> LoginPasswordAsync(string email, string password)
    {
        var result = await _apiClient.PostWrappedAsync<AuthLocalResponse>("/api/auth/local",
            new { identifier = email, password = PasswordHasher.Hash(password) });
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Data?.Jwt))
        {
            CurrentUser = result.Data.User;
            SaveToken(result.Data!.Jwt);
        }
        return result;
    }

    // ════════════════════════════════════════════════════════════════
    // 五、获取当前用户信息
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取当前用户完整信息（需认证）—— GET /api/users/me。
    /// image 是 Strapi 关联字段，默认不返回，需显式 populate 才能拿到头像。
    /// </summary>
    public async Task<ApiResult<UserMeResponse?>> GetCurrentUserAsync()
    {
        var result = await _apiClient.GetWrappedAsync<UserMeResponse>("/api/users/me?populate[0]=image");
        if (result.IsSuccess && result.Data != null) CurrentUser = result.Data;
        return result;
    }

    /// <summary>
    /// 拉取当前用户完整资料（users/me 返回头像等 top-level 字段），成功后通知 UI 刷新。
    /// 登录 / 会话恢复 / 修改密码等接口返回的 user 不含 image，需用此方法补齐头像。
    /// </summary>
    public async Task<bool> RefreshCurrentUserAsync()
    {
        if (!IsLoggedIn) return false;
        var result = await GetCurrentUserAsync();
        if (result.IsSuccess && result.Data != null)
        {
            CurrentUser = result.Data;
            NotifyLoginStateChanged();
            return true;
        }
        return false;
    }

    /// <summary>当前用户信息（用户名 / 头像）被修改后，通知所有订阅者刷新 UI</summary>
    public void NotifyUserInfoChanged() => NotifyLoginStateChanged();

    // ════════════════════════════════════════════════════════════════
    // 六、用户信息管理
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 上传头像并关联到当前用户（可同时修改用户名）。
    /// 流程：PUT /api/auth/update-me（multipart/form-data 一次请求，头像文件和可选 username 同表单上传）。
    /// </summary>
    public async Task<ApiResult<UpdateUserResponse?>> UploadAvatarAsync(string filePath, string? username = null)
    {
        var formFields = username != null
            ? new Dictionary<string, string> { ["username"] = username }
            : null;
        var filePaths = new Dictionary<string, string> { ["image"] = filePath };
        return await _apiClient.PutMultipartAsync<UpdateUserResponse>("/api/auth/update-me", formFields, filePaths);
    }

    /// <summary>修改用户名（需认证）—— PUT /api/auth/update-me（JSON 模式，仅支持 username 字段）</summary>
    public async Task<ApiResult<UpdateUserResponse?>> UpdateUserAsync(string username)
        => await _apiClient.PutWrappedAsync<UpdateUserResponse>("/api/auth/update-me", new { username });

    // ════════════════════════════════════════════════════════════════
    // 七、修改密码 / 忘记密码
    // ════════════════════════════════════════════════════════════════

    /// <summary>修改密码（需认证）—— POST /api/auth/change-password</summary>
    public async Task<ApiResult<ChangePasswordResponse?>> ChangePasswordAsync(string currentPassword, string newPassword, string passwordConfirmation)
    {
        var result = await _apiClient.PostWrappedAsync<ChangePasswordResponse>("/api/auth/change-password",
            new
            {
                currentPassword = PasswordHasher.Hash(currentPassword),
                password = PasswordHasher.Hash(newPassword),
                passwordConfirmation = PasswordHasher.Hash(passwordConfirmation)
            });
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Data?.Jwt))
        {
            // change-password 响应不含 image 字段，保留当前头像，避免被覆盖丢失
            if (result.Data!.User.Image == null && CurrentUser?.Image != null)
                result.Data.User.Image = CurrentUser.Image;
            CurrentUser = result.Data.User;
            SaveToken(result.Data!.Jwt);
        }
        return result;
    }

    /// <summary>忘记密码（发送重置邮件）—— POST /api/auth/forgot-password</summary>
    public async Task<ApiResult<ForgotPasswordResponse?>> ForgotPasswordAsync(string email)
        => await _apiClient.PostWrappedAsync<ForgotPasswordResponse>("/api/auth/forgot-password", new { email });

    /// <summary>
    /// 身份确认（校验忘记密码验证码，签发 step-up JWT）—— POST /api/auth/verify-stepup。
    /// 返回的 stepup_jwt 是 5 分钟有效的短期凭证，用于证明身份已完成确认，仅供下一步 reset-password 使用，不是登录凭证。
    /// </summary>
    public async Task<ApiResult<VerifyStepupResponse?>> VerifyStepupAsync(string email, string code)
        => await _apiClient.PostWrappedAsync<VerifyStepupResponse>("/api/auth/verify-stepup",
            new { email, code, purpose = "reset_password" });

    /// <summary>
    /// 重置密码（凭 step-up JWT 设置新密码）—— POST /api/auth/reset-password。
    /// 本接口不签发登录 JWT，成功后需用新密码重新登录。
    /// </summary>
    public async Task<ApiResult<ResetPasswordResponse?>> ResetPasswordAsync(string stepupJwt, string newPassword)
        => await _apiClient.PostWrappedAsync<ResetPasswordResponse>("/api/auth/reset-password",
            new { password = PasswordHasher.Hash(newPassword) }, authToken: stepupJwt);

    // ════════════════════════════════════════════════════════════════
    // 八、云预设管理
    // ════════════════════════════════════════════════════════════════

    /// <summary>获取当前用户的预设列表 —— GET /api/user-presets（默认全量返回；page>0 时启用分页）</summary>
    public async Task<ApiResult<List<UserPresetEntry>?>> GetPresetsAsync(int page = 0, int pageSize = 0)
    {
        var endpoint = "/api/user-presets";
        if (page > 0 && pageSize > 0)
            endpoint += $"?pagination[page]={page}&pagination[pageSize]={pageSize}";
        return await _apiClient.GetWrappedAsync<List<UserPresetEntry>>(endpoint);
    }

    /// <summary>获取单个预设（非自己的预设返回 404）—— GET /api/user-presets/:documentId</summary>
    public async Task<ApiResult<UserPresetEntry?>> GetPresetAsync(string documentId)
        => await _apiClient.GetWrappedAsync<UserPresetEntry>($"/api/user-presets/{documentId}");

    /// <summary>创建预设（user 由服务端自动绑定，禁止手动传）—— POST /api/user-presets</summary>
    public async Task<ApiResult<UserPresetEntry?>> CreatePresetAsync(object configData)
        => await _apiClient.PostWrappedAsync<UserPresetEntry>("/api/user-presets", new { config_data = configData });

    /// <summary>更新预设（非自己的预设返回 404）—— PUT /api/user-presets/:documentId</summary>
    public async Task<ApiResult<UserPresetEntry?>> UpdatePresetAsync(string documentId, object configData)
        => await _apiClient.PutWrappedAsync<UserPresetEntry>($"/api/user-presets/{documentId}", new { config_data = configData });

    /// <summary>删除预设（非自己的预设返回 404）—— DELETE /api/user-presets/:documentId</summary>
    public async Task<ApiResult<object>> DeletePresetAsync(string documentId)
        => await _apiClient.DeleteAsync<object>($"/api/user-presets/{documentId}");
}

// ════════════════════════════════════════════════════════════════

public class UserInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("confirmed")] public bool Confirmed { get; set; }
    [JsonPropertyName("blocked")] public bool Blocked { get; set; }
    [JsonPropertyName("image")] public UserImageInfo? Image { get; set; }
}

public class UserImageInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
}

public class RegisterOtpResponse { [JsonPropertyName("user")] public UserInfo User { get; set; } = new(); [JsonPropertyName("message")] public string? Message { get; set; } }
public class VerifyOtpResponse { [JsonPropertyName("jwt")] public string Jwt { get; set; } = string.Empty; [JsonPropertyName("user")] public UserInfo User { get; set; } = new(); [JsonPropertyName("message")] public string? Message { get; set; } }
public class ResendOtpResponse { [JsonPropertyName("message")] public string? Message { get; set; } }
public class LoginOtpResponse { [JsonPropertyName("message")] public string? Message { get; set; } }
public class RefreshTokenResponse { [JsonPropertyName("jwt")] public string Jwt { get; set; } = string.Empty; [JsonPropertyName("user")] public UserInfo User { get; set; } = new(); }
public class UserMeResponse : UserInfo { }
public class UpdateUserResponse : UserInfo { }
public class ChangePasswordResponse { [JsonPropertyName("jwt")] public string Jwt { get; set; } = string.Empty; [JsonPropertyName("user")] public UserInfo User { get; set; } = new(); }
public class ForgotPasswordResponse { [JsonPropertyName("message")] public string? Message { get; set; } }
public class ResetPasswordResponse { [JsonPropertyName("message")] public string? Message { get; set; } }
public class AuthLocalResponse { [JsonPropertyName("jwt")] public string Jwt { get; set; } = string.Empty; [JsonPropertyName("user")] public UserInfo User { get; set; } = new(); }
public class VerifyStepupResponse { [JsonPropertyName("stepup_jwt")] public string StepupJwt { get; set; } = string.Empty; }

/// <summary>
/// 用户云预设条目（UserPreset）：config_data 结构由客户端约定（服务端不校验内部字段），
/// user 字段由服务端自动绑定为当前用户 ID；单条查/改/删需用 documentId 作为路径参数。
/// </summary>
public class UserPresetEntry
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("documentId")] public string DocumentId { get; set; } = string.Empty;
    [JsonPropertyName("config_data")] public JsonElement? ConfigData { get; set; }
    [JsonPropertyName("user")] public int UserId { get; set; }
}

/// <summary>
/// 云预设的 config_data 结构（客户端约定，服务端原样存储不校验内部字段）。
/// 与 PresetItem 的本地数据一一对应，用于上传 / 下载云端预设。
/// </summary>
public class UserPresetConfigData
{
    public string Name { get; set; } = string.Empty;
    public List<string> Games { get; set; } = new();
    public int DeviceType { get; set; }
    public PedalPresetSnapshot? PedalParameters { get; set; }
    public WheelPresetSnapshot? WheelParameters { get; set; }
    public BasePresetSnapshot? BaseParameters { get; set; }
}
