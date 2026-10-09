using System.Security.Cryptography;

namespace HITAPEX.Services;

/// <summary>
/// 预设文件加密器 —— 基于 AES-256-GCM 的对称加解密，密钥内嵌并混淆派生。
/// 目的：防止直接用文本编辑器查看 / 篡改预设 JSON（防逆向查看数据结构）。
/// </summary>
/// <remarks>
/// 密文格式：[4B 魔数 "HAPF"] [12B nonce] [16B GCM tag] [ciphertext]
/// GCM 自带认证，密文被篡改或损坏后解密失败，<see cref="Unprotect"/> 会原样返回输入。
/// <see cref="Unprotect"/> 对非加密输入（明文旧文件、安装目录兜底文件）直接原样返回，
/// 因此新旧版本文件可双向兼容读取。
/// </remarks>
internal static class PresetEncryptor
{
#if DEBUG
    // 调试构建：改为 false 即切回明文写入，方便直接用编辑器查看/修改文件。
    // 注意：改为 false 后无法解密旧的密文文件（认证失败），需先删除旧文件再测试。
    internal static readonly bool Enabled = false;
#else
    // Release 构建强制加密，不留明文后门。
    internal static readonly bool Enabled = true;
#endif

    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] Header = { 0x48, 0x41, 0x50, 0x46 }; // "HAPF"

    // 运行时由分散碎片组合并派生 256 位密钥，避免在二进制中留下连续可搜的密钥字符串。
    private static readonly byte[] Key = DeriveKey();

    /// <summary>加密：返回密文；明文模式（Enabled=false）下原样返回</summary>
    internal static byte[] Protect(byte[] plain)
    {
        if (!Enabled) return plain;

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(Key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var result = new byte[Header.Length + NonceSize + TagSize + plain.Length];
        Header.CopyTo(result, 0);
        nonce.CopyTo(result, Header.Length);
        tag.CopyTo(result, Header.Length + NonceSize);
        cipher.CopyTo(result, Header.Length + NonceSize + TagSize);
        return result;
    }

    /// <summary>
    /// 解密：返回明文；对非加密输入（明文旧文件）或认证失败（损坏/被篡改）的密文原样返回输入，
    /// 由上层解析并兜底。
    /// </summary>
    internal static byte[] Unprotect(byte[] data)
    {
        if (!Enabled || data.Length < Header.Length + NonceSize + TagSize) return data;
        if (!data.AsSpan(0, Header.Length).SequenceEqual(Header)) return data; // 明文旧文件

        try
        {
            var plain = new byte[data.Length - Header.Length - NonceSize - TagSize];
            using var aes = new AesGcm(Key, TagSize);
            aes.Decrypt(
                data.AsSpan(Header.Length, NonceSize),
                data.AsSpan(Header.Length + NonceSize + TagSize),
                data.AsSpan(Header.Length + NonceSize, TagSize),
                plain);
            return plain;
        }
        catch (CryptographicException)
        {
            // 认证失败 / 文件损坏：原样返回，由上层解析失败兜底为空列表
            return data;
        }
    }

    /// <summary>将分散的字节碎片拼接后经 SHA-256 派生 256 位密钥</summary>
    private static byte[] DeriveKey()
    {
        Span<byte> seed = stackalloc byte[48]
        {
            // 碎片 1
            0x9F, 0x3C, 0x7A, 0x11, 0x5D, 0xE8, 0x02, 0xB4,
            // 碎片 2
            0xC6, 0x29, 0x70, 0xDD, 0x4A, 0x8F, 0x15, 0x63,
            // 碎片 3
            0x3B, 0xE1, 0x58, 0xA7, 0x94, 0x0C, 0x6F, 0xD2,
            // 碎片 4
            0x71, 0xB8, 0x2E, 0x4C, 0x96, 0x0A, 0xF5, 0x3D,
            // 碎片 5
            0x28, 0xC3, 0x6A, 0x9E, 0x5B, 0x17, 0xD4, 0x80,
            // 碎片 6
            0x4F, 0x12, 0xB6, 0x73, 0x8C, 0xE7, 0x2D, 0x9A
        };
        return SHA256.HashData(seed);
    }
}