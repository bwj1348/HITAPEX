using System.Security.Cryptography;
using System.Text;

namespace HITAPEX.Services;

/// <summary>
/// 密码哈希工具：客户端将明文密码哈希后再发送给服务器，避免明文密码出网。
/// 使用 SHA-256，输出 64 位十六进制小写字符串。
/// </summary>
public static class PasswordHasher
{
    /// <summary>计算密码的 SHA-256 哈希值（十六进制小写）。</summary>
    public static string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
            return string.Empty;
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
