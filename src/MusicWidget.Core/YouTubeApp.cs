using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace MusicWidget;

internal readonly record struct LikeStatus(bool Available, bool Liked);

internal static class YouTubeApp
{
    // UI Automation runs off the WPF thread. A timed-out provider must finish before
    // another request starts, and may never perform a delayed click after timeout.
    static readonly SemaphoreSlim automationGate = new(1, 1);
    delegate bool EnumWindow(nint window, nint data);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback, nint data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("user32.dll", SetLastError = true)] static extern nint SendMessageTimeout(nint window, uint message, nuint wParam, nint lParam, uint flags, uint timeout, out nuint result);
    [DllImport("user32.dll")] static extern bool PeekMessage(out NativeMessage message, nint window, uint min, uint max, uint remove);
    [StructLayout(LayoutKind.Sequential)]
    struct NativeMessage { public nint Window; public uint Message; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(nint window);
    [DllImport("user32.dll")] static extern nint SetActiveWindow(nint window);
    [DllImport("user32.dll")] static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(nint window, StringBuilder text, int count);

    internal static bool IsMusicWindow(string caption, string process, string? source)
    {
        var browser = source?.StartsWith("Chrome", StringComparison.OrdinalIgnoreCase) == true ? "chrome"
            : source?.StartsWith("Microsoft.MicrosoftEdge", StringComparison.OrdinalIgnoreCase) == true || source?.StartsWith("msedge", StringComparison.OrdinalIgnoreCase) == true ? "msedge" : null;
        return (process is "chrome" or "msedge") && (browser is null || process == browser)
            && (caption.Equals("YouTube Music", StringComparison.OrdinalIgnoreCase)
                || caption.EndsWith(" - YouTube Music", StringComparison.OrdinalIgnoreCase)
                // The PWA also uses "YouTube Music - <song> | YouTube Music" while playing.
                || caption.EndsWith(" | YouTube Music", StringComparison.OrdinalIgnoreCase));
    }

    static List<nint> Windows(string? source)
    {
        var result = new List<nint>();
        EnumWindows((window, _) => {
            if (!IsWindowVisible(window)) return true;
            var caption = new StringBuilder(2048);
            GetWindowText(window, caption, caption.Capacity);
            GetWindowThreadProcessId(window, out var pid);
            try {
                using var process = Process.GetProcessById((int)pid);
                if (IsMusicWindow(caption.ToString(), process.ProcessName, source)) result.Add(window);
            } catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception) { }
            return true;
        }, 0);
        return result;
    }

    internal static bool Focus(nint window, CancellationToken token = default)
    {
        using var gate = BackgroundWindow.Gate(window);
        if (!BackgroundWindow.Enter(gate, 1500)) return false;
        try { return FocusCore(window, token); }
        finally { gate.ReleaseMutex(); }
    }

    static bool FocusCore(nint window, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsWindow(window)) return false;
        // ShowWindowAsync only queues restoration: focusing immediately can race
        // Chrome's minimized state. Wait for SC_RESTORE to be processed first.
        if (IsIconic(window)) {
            if (SendMessageTimeout(window, 0x0112, 0xF120, 0, 0x0003, 1000, out _) == 0) return false;
            if (IsIconic(window)) return false;
        }
        PeekMessage(out _, 0, 0, 0, 0); // AttachThreadInput requires this worker to have a message queue.
        SetForegroundWindow(window);
        if (IsFocused(window)) return true;
        var current = GetCurrentThreadId();
        var target = GetWindowThreadProcessId(window, out _);
        // The widget callback runs on a worker, separate from both the foreground
        // host and Chromium. Join both queues while activating and raising the PWA.
        // Re-read the foreground thread if the widget board is closing meanwhile.
        for (var attempt = 0; attempt < 2; attempt++) {
            token.ThrowIfCancellationRequested();
            var foreground = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            var attachedForeground = foreground != 0 && foreground != current && AttachThreadInput(current, foreground, true);
            var attachedTarget = target != 0 && target != current && target != foreground && AttachThreadInput(current, target, true);
            try {
                token.ThrowIfCancellationRequested();
                BringWindowToTop(window);
                SetForegroundWindow(window);
                SetActiveWindow(window);
            }
            finally {
                if (attachedTarget) AttachThreadInput(current, target, false);
                if (attachedForeground) AttachThreadInput(current, foreground, false);
            }
            if (IsFocused(window)) return true;
        }
        return false;
    }

    static bool IsFocused(nint window)
    {
        // Foreground activation also crosses a thread boundary; check the result,
        // rather than treating the API's accepted request as completed activation.
        SendMessageTimeout(window, 0, 0, 0, 0x0003, 500, out _);
        return !IsIconic(window) && GetForegroundWindow() == window;
    }

    public static Task<bool> OpenAsync(string? source, string? title) => RunAsync(token => {
        var windows = Windows(source);
        if (windows.Count == 0 && source is not null) windows = Windows(null);
        var window = windows.FirstOrDefault(w => {
            var caption = new StringBuilder(2048); GetWindowText(w, caption, caption.Capacity);
            return !string.IsNullOrWhiteSpace(title) && caption.ToString().Contains(title, StringComparison.OrdinalIgnoreCase);
        });
        if (window == 0) window = windows.FirstOrDefault();
        token.ThrowIfCancellationRequested();
        if (window != 0) return Focus(window, token);
        var appId = YouTubeIdentity.InstalledAppId(source);
        token.ThrowIfCancellationRequested();
        using var launched = Process.Start(appId is null
            ? new ProcessStartInfo("https://music.youtube.com/") { UseShellExecute = true }
            : new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = $"shell:AppsFolder\\{appId}" });
        return true;
    }, false, serializeAutomation: false);

    internal static bool IsLikeLabel(string name) => name.Trim().ToLowerInvariant() is
        "like" or "unlike" or "remove like" or "like this song" or "좋아요" or "좋아요 취소";

    static bool HasClass(AutomationElement element, string value) => element.Current.ClassName.Split(' ').Contains(value);

    internal static string NormalizeTrackText(string value) => string.Concat(value
        .Normalize(NormalizationForm.FormKC)
        .ToLowerInvariant()
        .Where(char.IsLetterOrDigit));

    internal static bool MatchesTrackTitle(string actual, string expected)
    {
        var normalizedActual = NormalizeTrackText(actual);
        var normalizedExpected = NormalizeTrackText(expected);
        if (normalizedActual == normalizedExpected) return true;
        var actualBase = FeatureBase(actual);
        var expectedBase = FeatureBase(expected);
        return actualBase.Length > 0 && actualBase == expectedBase
            && (actualBase != normalizedActual) != (expectedBase != normalizedExpected);
    }

    static string FeatureBase(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC);
        foreach (var marker in new[] { "feat", "ft", "with" }) {
            var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index > 0 && !char.IsLetterOrDigit(normalized[index - 1]))
                return NormalizeTrackText(normalized[..index]);
        }
        return NormalizeTrackText(value);
    }

    static bool MatchesTrack(AutomationElement bar, string title, string artist)
    {
        var normalizedArtist = NormalizeTrackText(artist);
        var children = bar.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>();
        return children.Any(e => HasClass(e, "ytmusic-player-bar") && HasClass(e, "title")
                && MatchesTrackTitle(e.Current.Name, title))
            && children.Any(e => HasClass(e, "ytmusic-player-bar") && HasClass(e, "byline")
                && NormalizeTrackText(e.Current.Name).Contains(normalizedArtist, StringComparison.Ordinal));
    }

    static (AutomationElement Bar, AutomationElement Button)? FindLike(string source, string title, string artist, CancellationToken token)
    {
        (AutomationElement, AutomationElement)? match = null;
        foreach (var window in Windows(source)) {
            token.ThrowIfCancellationRequested();
            if (!BackgroundWindow.Refresh(window, token)) return null;
            token.ThrowIfCancellationRequested();
            var root = AutomationElement.FromHandle(window);
            var bars = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar));
            foreach (AutomationElement bar in bars) {
                if (!MatchesTrack(bar, title, artist)) continue;
                var buttons = bar.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                foreach (AutomationElement button in buttons) {
                    if (!IsLikeLabel(button.Current.Name) || !button.Current.IsEnabled
                        || !button.TryGetCurrentPattern(TogglePattern.Pattern, out _)) continue;
                    if (match is not null) return null; // Multiple matching players are ambiguous.
                    match = (bar, button);
                }
            }
        }
        return match;
    }

    public static Task<LikeStatus> ReadLikeAsync(string source, string title, string artist) => RunAsync(token => {
        var match = FindLike(source, title, artist, token);
        if (match is null) return default;
        var toggle = (TogglePattern)match.Value.Button.GetCurrentPattern(TogglePattern.Pattern);
        return new LikeStatus(true, toggle.Current.ToggleState == ToggleState.On);
    }, default(LikeStatus));

    public static Task<bool> SetLikedAsync(string source, string title, string artist, bool liked) => RunAsync(token => {
        var match = FindLike(source, title, artist, token);
        if (match is null) return false;
        var (bar, button) = match.Value;
        var toggle = (TogglePattern)button.GetCurrentPattern(TogglePattern.Pattern);
        if ((toggle.Current.ToggleState == ToggleState.On) == liked) return true;
        if (!MatchesTrack(bar, title, artist)) return false;
        token.ThrowIfCancellationRequested();
        toggle.Toggle();
        return true;
    }, false);

    static async Task<T> RunAsync<T>(Func<CancellationToken, T> action, T fallback, bool serializeAutomation = true)
    {
        // Opening the app must keep working even if its accessibility tree hangs.
        if (serializeAutomation && !automationGate.Wait(0)) return fallback;
        var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var task = Task.Run(() => {
            try { return action(timeout.Token); }
            catch (Exception e) when (e is COMException or InvalidOperationException or ArgumentException
                or Win32Exception or UnauthorizedAccessException or ElementNotAvailableException
                or ElementNotEnabledException or OperationCanceledException) { return fallback; }
            finally { timeout.Dispose(); if (serializeAutomation) automationGate.Release(); }
        });
        try { return await task.WaitAsync(TimeSpan.FromSeconds(3)); }
        catch (TimeoutException) { return fallback; }
    }
}
