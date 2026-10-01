using DroidTrace.Services;
using DroidTrace.UI;

namespace DroidTrace;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // A WinExe has no console, so startup crashes vanish silently under `dotnet run`.
        // Capture them to crash.log and show them in a dialog instead.
        var crashLog = Path.Combine(AppContext.BaseDirectory, "crash.log");

        void Report(string where, Exception? ex)
        {
            var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {where}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
            try { File.AppendAllText(crashLog, text); } catch { }
            MessageBox.Show(text, "DroidTrace — unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Report("UI thread exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report("Unhandled exception", e.ExceptionObject as Exception);

        try
        {
            ApplicationConfiguration.Initialize();
            var paths = new AppPaths();
            var settings = SettingsService.Load(paths.SettingsFile);
            Application.Run(new MainForm(new AdbService(settings.AdbPath), paths, settings));
        }
        catch (Exception ex)
        {
            Report("Startup failure", ex);
        }
    }
}
