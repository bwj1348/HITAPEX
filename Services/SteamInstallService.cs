using System.IO;
using Microsoft.Win32;

namespace HITAPEX.Services;

public class SteamInstallInfo
{
    public bool IsInstalled { get; set; }
    public string? InstallDir { get; set; }
    public string? LibraryPath { get; set; }
    public DateTime? LastPlayed { get; set; }
}

public class SteamInstallService
{
    public Dictionary<string, SteamInstallInfo> CheckInstalled(IEnumerable<string> steamIds)
    {
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

    private List<string> GetSteamLibraryPaths()
    {
        var libraries = new List<string>();

        var steamPath = GetSteamPath();
        if (steamPath == null)
            return libraries;

        libraries.Add(steamPath);

        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            libraries.AddRange(ParseLibraryFoldersVdf(vdfPath));
        }

        return libraries;
    }

    private static string? GetSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch
        {
            return null;
        }
    }

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

                var path = trimmed.Substring(valueStart + 1, valueEnd - valueStart - 1)
                                  .Replace("\\\\", "\\");
                if (Directory.Exists(path))
                    paths.Add(path);
            }
        }
        catch
        {
            // VDF parse failure — return empty
        }
        return paths;
    }

    private static string? ParseManifestInstallDir(string manifestPath)
    {
        try
        {
            foreach (var line in File.ReadLines(manifestPath))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"installdir\"")) continue;

                var valueStart = trimmed.IndexOf('"', 13); // 跳过 "installdir"
                if (valueStart < 0) continue;
                var valueEnd = trimmed.IndexOf('"', valueStart + 1);
                if (valueEnd < 0) continue;
                return trimmed.Substring(valueStart + 1, valueEnd - valueStart - 1);
            }
        }
        catch
        {
            // Manifest parse failure
        }
        return null;
    }

    private static DateTime? ParseManifestTimestamp(string manifestPath)
    {
        try
        {
            foreach (var line in File.ReadLines(manifestPath))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("\"LastPlayed\"")) continue;

                var valueStart = trimmed.IndexOf('"', 13); // 跳过 "LastPlayed"
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
            // Manifest parse failure
        }
        return null;
    }
}
