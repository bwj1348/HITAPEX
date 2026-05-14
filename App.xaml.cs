using System.Configuration;
using System.Data;
using System.Windows;

namespace HITAPEX;

public partial class App : Application
{
    public static bool IsSessionEnding { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow();

        SessionEnding += (_, _) => { IsSessionEnding = true; };

        if (HITAPEX.Properties.Settings.Default.StartMinimizedToTray)
        {
            mainWindow.MinimizeToTray();
        }
        else
        {
            mainWindow.Show();
        }
    }
}
