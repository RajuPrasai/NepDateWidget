using System.Runtime.InteropServices;
using System.Text;

namespace NepDateWidget.Helpers;

/// <summary>
/// Centralised Win32 P/Invoke declarations and helper methods used by MainWindow.
/// Keeps interop concerns out of the code behind.
/// </summary>
internal static class Win32Interop
{
    // ── Window style constants ────────────────────────────────────────────────
    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_MAXIMIZEBOX = 0x00010000;
    internal const int WS_MINIMIZEBOX = 0x00020000;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;

    internal static readonly IntPtr HWND_TOPMOST = new(-1);
    internal static readonly IntPtr HWND_NOTOPMOST = new(-2);
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_HIDEWINDOW = 0x0080;
    internal const uint SWP_SHOWWINDOW = 0x0040;
    internal const uint SWP_ASYNCWINDOWPOS = 0x4000;
    internal const uint GW_HWNDPREV = 3;

    // ── WM constants ──────────────────────────────────────────────────────────
    internal const int WM_GETMINMAXINFO = 0x0024;
    internal const int WM_HOTKEY = 0x0312;
    internal const int WM_NCHITTEST = 0x0084;
    internal const int WM_WINDOWPOSCHANGING = 0x0046;

    // ── Hit-test result codes ──────────────────────────────────────────────
    internal const int HTCLIENT = 1;
    internal const int HTLEFT = 10;
    internal const int HTRIGHT = 11;
    internal const int HTTOP = 12;
    internal const int HTTOPLEFT = 13;
    internal const int HTTOPRIGHT = 14;
    internal const int HTBOTTOM = 15;
    internal const int HTBOTTOMLEFT = 16;
    internal const int HTBOTTOMRIGHT = 17;

    // ── Fullscreen detection ──────────────────────────────────────────────────
    internal const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    internal const int QUNS_PRESENTATION_MODE = 4;
    internal const uint MONITOR_DEFAULTTONEAREST = 2;
    internal const uint EVENT_SYSTEM_FOREGROUND = 3;
    internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    internal const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    // ── Structs ───────────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // ── Delegate ──────────────────────────────────────────────────────────────
    internal delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    // ── P/Invoke ──────────────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    internal static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    internal static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("shell32.dll")]
    internal static extern int SHQueryUserNotificationState(out int pquns);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder className, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    internal static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFO lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ── DWM / Mica / Dark mode ────────────────────────────────────────────────
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    // DWM_WINDOW_CORNER_PREFERENCE values
    internal const int DWMWCP_DEFAULT     = 0;
    internal const int DWMWCP_DONOTROUND  = 1;
    internal const int DWMWCP_ROUND       = 2;
    internal const int DWMWCP_ROUNDSMALL  = 3;

    // DWM_SYSTEMBACKDROP_TYPE values
    internal const int DWMSBT_AUTO        = 0;
    internal const int DWMSBT_NONE        = 1;
    internal const int DWMSBT_MAINWINDOW  = 2; // Mica
    internal const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
    internal const int DWMSBT_TABBEDWINDOW    = 4; // Mica Alt

    [DllImport("dwmapi.dll", PreserveSig = true)]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── Fullscreen detection logic ────────────────────────────────────────────

    /// <summary>
    /// Returns true when a true fullscreen application is in the foreground.
    /// Two detection strategies:
    ///  1) SHQueryUserNotificationState for D3D games and presentation mode.
    ///  2) Per-monitor rect comparison for browsers (F11 / YouTube fullscreen).
    /// </summary>
    internal static bool IsForegroundFullscreen()
    {
        if (SHQueryUserNotificationState(out int state) == 0)
        {
            if (state is QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE)
                return true;
        }

        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
            return false;

        if (hWnd == GetDesktopWindow() || hWnd == GetShellWindow())
            return false;

        var className = new StringBuilder(256);
        GetClassName(hWnd, className, className.Capacity);
        if (className.ToString() == "WorkerW")
            return false;

        GetWindowRect(hWnd, out RECT rcApp);

        IntPtr hMon = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMon, ref mi))
            return false;

        RECT rcMon = mi.rcMonitor;
        const int T = 1;
        return rcApp.Left >= rcMon.Left - T &&
               rcApp.Top >= rcMon.Top - T &&
               rcApp.Right <= rcMon.Right + T &&
               rcApp.Bottom <= rcMon.Bottom + T &&
               rcApp.Right - rcApp.Left >= rcMon.Right - rcMon.Left - T &&
               rcApp.Bottom - rcApp.Top >= rcMon.Bottom - rcMon.Top - T;
    }
}
