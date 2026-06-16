using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using HITAPEX.Models;

namespace HITAPEX.Services;

public static class GameLauncher
{
    public static bool Launch(GameItem? game)
    {
        if (game == null)
            return false;

        // 优先使用自定义启动路径
        if (!string.IsNullOrWhiteSpace(game.LaunchPath))
        {
            try
            {
                if (!File.Exists(game.LaunchPath))
                {
                    Debug.WriteLine($"[GameLauncher] 自定义启动路径不存在: {game.LaunchPath}");
                    return false;
                }

                var workingDir = Path.GetDirectoryName(game.LaunchPath) ?? "";
                Process.Start(new ProcessStartInfo
                {
                    FileName = game.LaunchPath,
                    WorkingDirectory = workingDir,
                    UseShellExecute = true
                });
                game.LastLaunchTime = DateTime.Now;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameLauncher] 自定义路径启动失败: {ex.Message}");
                return false;
            }
        }

        if (game.IsInstalled && !string.IsNullOrWhiteSpace(game.SteamId))
        {
            // SteamId 必须是纯数字，防止命令注入
            if (!Regex.IsMatch(game.SteamId, @"^\d+$"))
            {
                Debug.WriteLine($"[GameLauncher] 无效的 SteamId: {game.SteamId}");
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"steam://run/{game.SteamId}",
                    UseShellExecute = true
                });
                game.LastLaunchTime = DateTime.Now;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameLauncher] Steam启动失败: {ex.Message}");
                return false;
            }
        }
        return false;
    }
}
