using System.Runtime.InteropServices;

namespace MusicWidget;

// Chromium can leave its accessibility tree frozen while minimized. Briefly let
// it render with an invisible, non-activating window, then restore minimization
// BEFORE calling UI Automation (whose provider may hang).
internal static class BackgroundWindow
{
    const int ExStyle = -20, Layered = 0x80000, Transparent = 0x20;
    // WS_EX_NOACTIVATE also removes an ordinary PWA from taskbar management.
    // Minimizing with it can leave a legacy minimized caption on the desktop.
    // The SW_SHOWNOACTIVATE / SW_SHOWMINNOACTIVE commands already avoid focus.
    const int TemporaryStyles = Layered | Transparent;
    [StructLayout(LayoutKind.Sequential)]
    struct Placement
    {
        public int Length, Flags, ShowCommand;
        public int MinX, MinY, MaxX, MaxY, Left, Top, Right, Bottom;
    }
    [DllImport("user32.dll")] static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", SetLastError = true)] static extern int SetWindowLong(nint window, int index, int value);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(nint window, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll")] static extern bool SetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(nint window, int command);

    // Desktop preview and widget provider are separate processes. Opening the app
    // shares only this short native-window lock, never the UI Automation lock.
    internal static Mutex Gate(nint window) => new(false, $"Local\\Mossworm.MusicWidget.Window.{window}");

    internal static bool Enter(Mutex gate, int milliseconds)
    {
        try { return gate.WaitOne(milliseconds); }
        catch (AbandonedMutexException) { return true; }
    }

    internal static bool Refresh(nint window, CancellationToken token)
    {
        using var gate = Gate(window);
        if (!Enter(gate, 500)) return false;
        try {
            token.ThrowIfCancellationRequested();
            if (!IsIconic(window)) return true;
            var placement = new Placement { Length = Marshal.SizeOf<Placement>() };
            var style = GetWindowLong(window, ExStyle);
            // Do not overwrite transparency owned by the browser or another app.
            if ((style & Layered) != 0 || !GetWindowPlacement(window, ref placement)) return false;
            Marshal.SetLastPInvokeError(0);
            if (SetWindowLong(window, ExStyle, style | TemporaryStyles) == 0 && Marshal.GetLastPInvokeError() != 0) return false;
            var minimized = false;
            try {
                if (!SetLayeredWindowAttributes(window, 0, 0, 2)) return false;
                token.ThrowIfCancellationRequested();
                if (!ShowWindowAsync(window, 4)) return false; // SW_SHOWNOACTIVATE
                var deadline = Environment.TickCount64 + 500;
                while (IsIconic(window) && Environment.TickCount64 < deadline) Pause(20, token);
                if (IsIconic(window) || !IsWindow(window)) return false;
                Pause(250, token); // Allow Chromium to publish pending accessibility updates.
            }
            finally {
                // Cleanup must run even on cancellation, and must not depend on UIA.
                placement.Flags |= 4; // WPF_ASYNCWINDOWPLACEMENT avoids a hung browser thread.
                placement.ShowCommand = 7; // SW_SHOWMINNOACTIVE; retain restore-to-maximized and bounds.
                SetWindowPlacement(window, ref placement);
                ShowWindowAsync(window, 7);
                var deadline = Environment.TickCount64 + 500;
                while (IsWindow(window) && !IsIconic(window) && Environment.TickCount64 < deadline) Thread.Sleep(20);
                minimized = IsIconic(window);
                if (IsWindow(window)) {
                    var current = GetWindowLong(window, ExStyle);
                    SetWindowLong(window, ExStyle, (current & ~TemporaryStyles) | (style & TemporaryStyles));
                }
            }
            return minimized;
        }
        finally { gate.ReleaseMutex(); }
    }

    static void Pause(int milliseconds, CancellationToken token)
    {
        if (token.WaitHandle.WaitOne(milliseconds)) token.ThrowIfCancellationRequested();
    }
}
