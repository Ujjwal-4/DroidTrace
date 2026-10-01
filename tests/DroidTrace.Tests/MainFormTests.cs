using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using DroidTrace.Models;
using DroidTrace.Services;
using DroidTrace.UI;
using Xunit;

namespace DroidTrace.Tests;

public class MainFormTests
{
    [Fact]
    public void MainForm_Initializes_And_SwitchesAllViews_Without_Exception()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var paths = new AppPaths();
                var settings = new AppSettings();
                var adb = new AdbService("adb");
                using var form = new MainForm(adb, paths, settings);
                Assert.NotNull(form);

                // Use reflection to call SwitchView for all views
                var switchViewMethod = typeof(MainForm).GetMethod("SwitchView", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(switchViewMethod);

                string[] views = ["ACQUIRE", "TIMELINE", "DEVICE", "LOGS", "SETTINGS"];
                foreach (var view in views)
                {
                    switchViewMethod.Invoke(form, [view]);
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            throw new Exception("MainForm view switching threw an exception", threadEx);
        }
    }
}
