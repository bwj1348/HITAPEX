using System.Configuration;
using System.Data;
using System.Windows;

namespace HITAPEX;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow();

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
