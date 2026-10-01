using DroidTrace.Services;
using DroidTrace.UI;

namespace DroidTrace;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var paths = new AppPaths();
        var settings = SettingsService.Load(paths.SettingsFile);
        Application.Run(new MainForm(new AdbService(settings.AdbPath), paths, settings));
    }
}
