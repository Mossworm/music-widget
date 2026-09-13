using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MusicWidget;

// Read-only live regression check; does not change playback or the song's rating.
internal static class MinimizedRefreshChecks
{
    delegate bool EnumWindow(nint window, nint data);
    [StructLayout(LayoutKind.Sequential)]
    struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll")] static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] static extern nint MonitorFromRect(ref Rect rect, uint flags);

    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        Console.WriteLine($"PASS {label}");
    }

    internal static async Task RunAsync()
    {
        var windows = new List<nint>();
        EnumWindows((window, _) => {
            if (!IsWindowVisible(window)) return true;
            var caption = new StringBuilder(2048);
            GetWindowText(window, caption, caption.Capacity);
            GetWindowThreadProcessId(window, out var pid);
            try {
                using var process = Process.GetProcessById((int)pid);
                if (YouTubeApp.IsMusicWindow(caption.ToString(), process.ProcessName, null)) windows.Add(window);
            } catch (ArgumentException) { }
            return true;
        }, 0);
        Check(windows.Count == 1 && IsIconic(windows[0]), "Exactly one minimized PWA is available");
        var window = windows[0];
        var style = GetWindowLong(window, -20);
        var activated = 0;
        using var monitoring = new CancellationTokenSource();
        var monitor = Task.Run(async () => {
            while (!monitoring.IsCancellationRequested) {
                if (GetForegroundWindow() == window) Interlocked.Exchange(ref activated, 1);
                await Task.Delay(5);
            }
        });
        void CheckWindow()
        {
            Check(IsIconic(window) && IsWindowVisible(window) && GetWindowLong(window, -20) == style,
                "PWA stays minimized with its original visibility and styles");
            Check(GetWindowRect(window, out var rect), "Minimized window bounds are readable");
            Console.WriteLine($"Minimized bounds: {rect.Left},{rect.Top},{rect.Right},{rect.Bottom}");
            Check(MonitorFromRect(ref rect, 0) == 0, "No minimized caption remains on any monitor");
            Check(Volatile.Read(ref activated) == 0, "Background refresh never activates the PWA");
        }
        try {
            var controller = new MediaController();
            for (var attempt = 0; attempt < 5; attempt++) {
                var state = await controller.RefreshAsync();
                Check(state.Connected && state.CanLike, "Panel refresh still reads the current rating");
                CheckWindow();
                await Task.Delay(100);
            }
            await Task.Run(() => {
                using var cancel = new CancellationTokenSource(100);
                try { BackgroundWindow.Refresh(window, cancel.Token); }
                catch (OperationCanceledException) { }
            });
            CheckWindow();
        }
        finally {
            monitoring.Cancel();
            await monitor;
        }
    }
}
