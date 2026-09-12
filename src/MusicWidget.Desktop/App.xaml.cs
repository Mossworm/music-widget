using System.Globalization;
using System.Windows;

namespace MusicWidget.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var index = Array.IndexOf(e.Args, "--render");
        if (index >= 0) CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var window = new MainWindow(e.Args.Contains("--sample") || index >= 0);
        if (index >= 0 && index + 1 < e.Args.Length) { window.RenderPreviews(e.Args[index + 1]); Shutdown(); }
        else window.Show();
    }
}
