using System.Runtime.InteropServices;
using System.Text;
using MusicWidget;
using Windows.Media.Control;

internal static class MinimizedLikeChecks
{
    delegate bool EnumWindow(nint window, nint data);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern int GetWindowLong(nint window, int index);

    // Explicit opt-in integration check: temporarily changes the current song's
    // rating and restores it in finally. Run with the PWA already minimized.
    internal static async Task RunAsync()
    {
        void Check(bool value, string label) {
            if (!value) throw new Exception(label);
            Console.WriteLine($"PASS {label}");
        }
        var controller = new MediaController();
        var before = await controller.RefreshAsync();
        Check(before.Connected && before.CanLike, "Minimized player exposes its current rating");
        var windows = new List<nint>();
        EnumWindows((w, _) => {
            var caption = new StringBuilder(2048); GetWindowText(w, caption, caption.Capacity);
            if (YouTubeApp.IsMusicWindow(caption.ToString(), "chrome", null)) windows.Add(w);
            return true;
        }, 0);
        Check(windows.Count == 1 && IsIconic(windows[0]), "Exactly one minimized PWA is available for the live check");
        var window = windows[0];
        var foreground = GetForegroundWindow();
        var style = GetWindowLong(window, -20);
        using var monitoring = new CancellationTokenSource();
        var activated = false;
        var monitor = Task.Run(async () => {
            while (!monitoring.IsCancellationRequested) {
                if (GetForegroundWindow() == window) activated = true;
                await Task.Delay(5);
            }
        });
        void CheckWindow() {
            Console.WriteLine($"Window: minimized={IsIconic(window)}, foreground={GetForegroundWindow()} (expected {foreground}), style={GetWindowLong(window, -20):X} (expected {style:X})");
            Check(IsIconic(window) && !activated && GetWindowLong(window, -20) == style,
                "Minimization and styles preserved without activating the PWA");
        }
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = manager.GetSessions().First(s => YouTubeIdentity.Matches(s.SourceAppUserModelId, before.Title));
        var media = await session.TryGetMediaPropertiesAsync();
        try {
            var changed = await controller.ExecuteAsync(before.Liked ? "unlike" : "like", before.SessionId);
            Check(changed.SessionId == before.SessionId && changed.CanLike && changed.Liked != before.Liked && changed.Message is null,
                "Widget action changes the real rating while minimized");
            CheckWindow();
            var same = await controller.ExecuteAsync(before.Liked ? "unlike" : "like", before.SessionId);
            Check(same.Liked == changed.Liked, "Repeated desired-state request does not toggle back");
            Check(!await YouTubeApp.SetLikedAsync(session.SourceAppUserModelId, media.Artist, "not-the-current-track", before.Liked),
                "Mismatched track cannot change the rating");
        }
        finally {
            Check(await YouTubeApp.SetLikedAsync(session.SourceAppUserModelId, media.Artist, before.Title, before.Liked), "Original rating restored");
        }
        var restored = await controller.RefreshAsync();
        Check(restored.CanLike && restored.Liked == before.Liked, "Icon state reflects the restored real rating");
        CheckWindow();
        // Cancel during the hidden refresh, verifying cleanup independently of UIA.
        await Task.Run(() => {
            using var cancel = new CancellationTokenSource(100);
            try { BackgroundWindow.Refresh(window, cancel.Token); }
            catch (OperationCanceledException) { }
        });
        CheckWindow();
        monitoring.Cancel();
        await monitor;
    }
}
