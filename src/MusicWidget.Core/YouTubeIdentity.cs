using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;

namespace MusicWidget;

internal static class YouTubeIdentity
{
    delegate bool EnumWindow(nint window, nint data);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    static readonly ConcurrentDictionary<string, bool> identities = new(StringComparer.OrdinalIgnoreCase);
    static readonly object installedGate = new();
    static HashSet<string> installed = new(StringComparer.OrdinalIgnoreCase);
    static DateTimeOffset nextScan;

    internal static string? InstalledAppId(string? preferred)
    {
        IsInstalledYouTubeMusic(preferred ?? "");
        lock (installedGate)
            return preferred is not null && installed.Contains(preferred) ? preferred : installed.Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    static bool IsInstalledYouTubeMusic(string id)
    {
        lock (installedGate) {
            if (DateTimeOffset.UtcNow >= nextScan) {
                nextScan = DateTimeOffset.UtcNow.AddMinutes(1);
                object? shell = null, folder = null, items = null;
                try {
                    shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
                    folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
                    items = ((dynamic)folder!).Items();
                    var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < (int)((dynamic)items).Count; i++) {
                        object? item = null;
                        try {
                            item = ((dynamic)items).Item(i);
                            if (string.Equals((string)((dynamic)item).Name, "YouTube Music", StringComparison.OrdinalIgnoreCase))
                                result.Add((string)((dynamic)item).Path);
                        } finally { if (item is not null && Marshal.IsComObject(item)) Marshal.ReleaseComObject(item); }
                    }
                    installed = result;
                } catch (Exception e) when (e is COMException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
                finally {
                    foreach (var value in new[] { items, folder, shell }) if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
                }
            }
            return installed.Contains(id);
        }
    }

    public static bool Matches(string id, string title)
    {
        if (IsInstalledYouTubeMusic(id)) return true;
        if (!identities.TryGetValue(id, out var known)) {
            try { known = AppInfo.GetFromAppUserModelId(id)?.DisplayInfo.DisplayName.Equals("YouTube Music", StringComparison.OrdinalIgnoreCase) == true; }
            catch (Exception e) when (e is ArgumentException or COMException) { }
            identities[id] = known;
        }
        if (known) return true;
        // Chromium can publish a browser-wide identity. Require a matching YTM window
        // AND song title before accepting that session; a generic Chrome session is insufficient.
        var browser = id.StartsWith("Chrome", StringComparison.OrdinalIgnoreCase) ? "chrome"
            : id.StartsWith("Microsoft.MicrosoftEdge", StringComparison.OrdinalIgnoreCase) || id.StartsWith("msedge", StringComparison.OrdinalIgnoreCase) ? "msedge" : null;
        if (browser is null || string.IsNullOrWhiteSpace(title)) return false;
        var found = false;
        EnumWindows((window, _) => {
            var text = new StringBuilder(1024);
            GetWindowText(window, text, text.Capacity);
            var caption = text.ToString();
            if (!caption.Contains("YouTube Music", StringComparison.OrdinalIgnoreCase) || !caption.Contains(title, StringComparison.OrdinalIgnoreCase)) return true;
            GetWindowThreadProcessId(window, out var pid);
            try { using var process = Process.GetProcessById((int)pid); found = process.ProcessName.Equals(browser, StringComparison.OrdinalIgnoreCase); }
            catch (ArgumentException) { }
            return !found;
        }, 0);
        return found;
    }
}
