using System.IO;
using Microsoft.Win32;

namespace HITAPEX.Services;

public class SteamInstallInfo
{
    public bool IsInstalled { get; set; }
    public string? InstallDir { get; set; }
    public string? LibraryPath { get; set; }
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
                return new SteamInstallInfo
                {
                    IsInstalled = true,
                    InstallDir = installDir != null ? Path.Combine(library, "steamapps", "common", installDir) : null,
                    LibraryPath = library
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
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("\"path\""))
                {
                    var parts = trimmed.Split('\t');
                    if (parts.Length >= 2)
                    {
                        var path = parts[1].Trim('"').Replace("\\\\", "\\");
                        if (Directory.Exists(path))
                            paths.Add(path);
                    }
                }
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
            var lines = File.ReadAllLines(manifestPath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("\"installdir\""))
                {
                    var parts = trimmed.Split('\t');
                    if (parts.Length >= 2)
                        return parts[1].Trim('"');
                }
            }
        }
        catch
        {
            // Manifest parse failure
        }
        return null;
    }
}
