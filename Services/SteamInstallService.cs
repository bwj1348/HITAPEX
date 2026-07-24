using System.IO;
using Microsoft.Win32;

namespace HITAPEX.Services;

/// <summary>
/// Steam 游戏安装信息，由本机 Steam 客户端状态检测得出。
/// </summary>
public class SteamInstallInfo
{
    /// <summary>是否已安装（appmanifest 文件存在）</summary>
    public bool IsInstalled { get; set; }
    /// <summary>游戏安装目录完整路径（{library}\steamapps\common\{installdir}）</summary>
    public string? InstallDir { get; set; }
    /// <summary>Steam 库文件夹根路径</summary>
    public string? LibraryPath { get; set; }
    /// <summary>最后一次游玩时间（从 appmanifest LastPlayed 时间戳转换）</summary>
    public DateTime? LastPlayed { get; set; }
}

/// <summary>
/// Steam 安装检测服务 —— 读取本机 Steam 配置以判断指定游戏是否已安装。
/// 通过解析 registry 获取 Steam 安装路径，再遍历所有库文件夹中的 appmanifest 文件
/// 来确定安装状态、安装目录和最后游玩时间。
/// </summary>
/// <remarks>
/// 线程安全性：纯静态方法，无状态，可在任意线程调用。
/// </remarks>
public class SteamInstallService
{
    /// <summary>
    /// 批量检测指定 Steam 游戏的安装状态。
    /// </summary>
    /// <param name="steamIds">Steam App ID 集合</param>
    /// <returns>SteamId → SteamInstallInfo 的映射字典</returns>
    public Dictionary<string, SteamInstallInfo> CheckInstalled(IEnumerable<string> steamIds)
    {
        // 获取本机所有 Steam 库文件夹路径
        var libraries = GetSteamLibraryPaths();
        if (libraries.Count == 0)
            return steamIds.ToDictionary(id => id, _ => new SteamInstallInfo());

        var result = new Dictionary<string, SteamInstallInfo>();
        foreach (var steamId in steamIds.Distinct())
        {
            result[steamId] = FindGameManifest(steamId, libraries);
        }
        return result;
    }

    /// <summary>
    /// 在所有 Steam 库文件夹中搜索指定游戏的 appmanifest 文件。
    /// </summary>
    /// <param name="steamId">Steam App ID</param>
    /// <param name="libraries">Steam 库文件夹路径列表</param>
    /// <returns>安装信息（未找到则 IsInstalled 为 false）</returns>
    private SteamInstallInfo FindGameManifest(string steamId, List<string> libraries)
    {
        foreach (var library in libraries)
        {
            var manifestPath = Path.Combine(library, "steamapps", $"appmanifest_{steamId}.acf");
            if (File.Exists(manifestPath))
            {
                var installDir = ParseManifestInstallDir(manifestPath);
                var lastPlayed = ParseManifestTimestamp(manifestPath);
                return new SteamInstallInfo
                {
                    IsInstalled = true,
                    InstallDir = installDir != null ? Path.Combine(library, "steamapps", "common", installDir) : null,
                    LibraryPath = library,
                    LastPlayed = lastPlayed
                };
            }
        }
        return new SteamInstallInfo();
    }

    /// <summary>
    /// 获取本机所有 Steam 库文件夹路径（主目录 + libraryfolders.vdf 中的额外库）。
    /// </summary>
    private List<string> GetSteamLibraryPaths()
    {
        var libraries = new List<string>();

        var steamPath = GetSteamPath();
        if (steamPath == null)
            return libraries;

        // 主 Steam 安装目录本身就是一个库文件夹
        libraries.Add(steamPath);

        // 解析 libraryfolders.vdf 获取额外的库文件夹
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            libraries.AddRange(ParseLibraryFoldersVdf(vdfPath));
        }

        return libraries;
    }

    /// <summary>
    /// 从 Windows 注册表读取 Steam 安装路径。
    /// 位置：HKCU\Software\Valve\Steam → SteamPath 值。
    /// </summary>
    private static string? GetSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch
        {
            // 注册表读取失败（可能未安装 Steam）
            return null;
        }
    }

    /// <summary>
    /// 解析 libraryfolders.vdf 文件，提取所有额外的 Steam 库文件夹路径。
    /// VDF（Valve Data Format）中路径格式为 "path"\t\t"{escaped_path}"。
    /// </summary>
    private static List<string> ParseLibraryFoldersVdf(string filePath)
    {
        var paths = new List<string>();
        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"path\"")) continue;

                // 提取引号内的路径值：\"path\" 后紧跟的制表符/空格后第一个带引号的值
                // 格式示例："path"\t\t"D:\\SteamLibrary"
                var valueStart = trimmed.IndexOf('"', 6); // 跳过 "path" (6 个字符)
                if (valueStart < 0) continue;

                var valueEnd = trimmed.IndexOf('"', valueStart + 1);
                if (valueEnd < 0) continue;

                // 将双反斜杠还原为单反斜杠
                var path = trimmed.Substring(valueStart + 1, valueEnd - valueStart - 1)
                                  .Replace("\\\\", "\\");
                if (Directory.Exists(path))
                    paths.Add(path);
            }
        }
        catch
        {
            // VDF 解析失败 — 返回空列表
        }
        return paths;
    }

    /// <summary>
    /// 从 appmanifest 文件中提取 "installdir" 键值。
    /// 格式示例："installdir"\t\t"Assetto Corsa Competizione"。
    /// </summary>
    private static string? ParseManifestInstallDir(string manifestPath)
    {
        try
        {
            foreach (var line in File.ReadLines(manifestPath))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"installdir\"")) continue;

                var valueStart = trimmed.IndexOf('"', 13); // 跳过 "installdir"（12 个字符 + 开引号）
                if (valueStart < 0) continue;
                var valueEnd = trimmed.IndexOf('"', valueStart + 1);
                if (valueEnd < 0) continue;
                return trimmed.Substring(valueStart + 1, valueEnd - valueStart - 1);
            }
        }
        catch
        {
            // Manifest 解析失败
        }
        return null;
    }

    /// <summary>
    /// 从 appmanifest 文件中提取 "LastPlayed" 时间戳。
    /// 值为 Unix 时间戳（秒），转换为本地 DateTime。
    /// </summary>
    private static DateTime? ParseManifestTimestamp(string manifestPath)
    {
        try
        {
            foreach (var line in File.ReadLines(manifestPath))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"LastPlayed\"")) continue;

                var valueStart = trimmed.IndexOf('"', 13); // 跳过 "LastPlayed"（12 个字符 + 开引号）
                if (valueStart < 0) continue;
                var valueEnd = trimmed.IndexOf('"', valueStart + 1);
                if (valueEnd < 0) continue;
                var valueStr = trimmed.Substring(valueStart + 1, valueEnd - valueStart - 1);
                if (long.TryParse(valueStr, out var unixTime))
                    return DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;
            }
        }
        catch
        {
            // Manifest 解析失败
        }
        return null;
    }
}
