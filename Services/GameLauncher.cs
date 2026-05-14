using System.Diagnostics;
using HITAPEX.Models;

namespace HITAPEX.Services;

public static class GameLauncher
{
    public static bool Launch(GameItem game)
    {
        if (game.IsInstalled && !string.IsNullOrWhiteSpace(game.SteamId))
        {
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
            catch
            {
                return false;
            }
        }
        return false;
    }
}
