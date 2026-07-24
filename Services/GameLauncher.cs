using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using HITAPEX.Models;

namespace HITAPEX.Services;

/// <summary>
/// 游戏启动模式枚举。
/// </summary>
public enum LaunchMode
{
    /// <summary>通过 Steam 协议启动（steam://run/{SteamId}）</summary>
    Steam,
    /// <summary>通过用户指定的自定义可执行文件路径启动</summary>
    CustomPath
}

/// <summary>
/// 游戏启动器 —— 负责以 Steam 协议或自定义路径方式启动游戏，
/// 并在游戏启动后延迟 5 秒自动开启遥测数据采集。
/// </summary>
public static class GameLauncher
{
    /// <summary>
    /// 启动游戏，并在 5 秒延迟后启动对应遥测采集。
    /// </summary>
    public static bool Launch(GameItem? game, LaunchMode mode = LaunchMode.Steam)
    {
        if (game == null)
            return false;

        // 自定义路径模式
        if (mode == LaunchMode.CustomPath)
        {
            if (string.IsNullOrWhiteSpace(game.LaunchPath))
            {
                Debug.WriteLine("[GameLauncher] 自定义路径模式下 LaunchPath 为空");
                return false;
            }

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
                _ = StartTelemetryAsync(game);
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
                _ = StartTelemetryAsync(game);
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

    /// <summary>
    /// 延迟 5 秒后尝试启动遥测数据采集（给游戏加载时间）。
    /// </summary>
    private static async Task StartTelemetryAsync(GameItem game)
    {
        var telemetryService = App.TelemetryService;
        if (telemetryService == null) return;

        if (!int.TryParse(game.SteamId, out var steamAppId))
        {
            Debug.WriteLine($"[GameLauncher] 无法解析 SteamId: {game.SteamId}");
            return;
        }

        if (!Enum.IsDefined(typeof(TelemetryAPI.GameId), steamAppId) && steamAppId != 0)
        {
            Debug.WriteLine($"[GameLauncher] GameId={steamAppId} 不在 TelemetrySDK 支持列表中");
            return;
        }

        await Task.Delay(5000);

        try
        {
            if (telemetryService.Start(steamAppId))
            {
                Debug.WriteLine($"[GameLauncher] 遥测启动成功: {game.Name} (GameId={steamAppId})");
            }
            else
            {
                Debug.WriteLine($"[GameLauncher] 遥测启动失败: {game.Name} (GameId={steamAppId})");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameLauncher] 遥测启动异常: {ex.Message}");
        }
    }
}
