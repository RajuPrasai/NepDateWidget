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
    internal const int WM_DISPLAYCHANGE = 0x007E;

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
    internal const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    internal const int OBJID_WINDOW = 0;

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(
        EnumWindowsProc lpEnumFunc,
        IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // ── DWM / Mica / Dark mode ────────────────────────────────────────────────
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    // DWM_WINDOW_CORNER_PREFERENCE values
    internal const int DWMWCP_DEFAULT = 0;
    internal const int DWMWCP_DONOTROUND = 1;
    internal const int DWMWCP_ROUND = 2;
    internal const int DWMWCP_ROUNDSMALL = 3;

    // DWM_SYSTEMBACKDROP_TYPE values
    internal const int DWMSBT_AUTO = 0;
    internal const int DWMSBT_NONE = 1;
    internal const int DWMSBT_MAINWINDOW = 2; // Mica
    internal const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
    internal const int DWMSBT_TABBEDWINDOW = 4; // Mica Alt

    [DllImport("dwmapi.dll", PreserveSig = true)]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── DWM cloaking ──────────────────────────────────────────────────────────
    internal const int DWMWA_CLOAKED = 14;

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);

    // ── Fullscreen detection logic ────────────────────────────────────────────

    /// <summary>
    /// Returns true when a fullscreen application is occupying the monitor
    /// containing the supplied window.
    ///
    /// Detection combines Windows' fullscreen notification state with a
    /// per-monitor top-level window scan. This keeps fullscreen suppression
    /// tied to the widget's monitor rather than whichever monitor currently
    /// has the foreground window.
    /// </summary>
    internal static bool IsMonitorFullscreen(IntPtr widgetHwnd)
    {
        if (widgetHwnd == IntPtr.Zero)
        {
            return false;
        }

        IntPtr widgetMonitor = MonitorFromWindow(widgetHwnd, MONITOR_DEFAULTTONEAREST);
        if (widgetMonitor == IntPtr.Zero)
        {
            return false;
        }

        // Preserve the existing D3D/presentation-mode detection, but only
        // suppress the widget when the foreground fullscreen state belongs
        // to the widget's monitor.
        if (SHQueryUserNotificationState(out int state) == 0 &&
            state is QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE)
        {
            IntPtr foregroundWindow = GetForegroundWindow();

            if (foregroundWindow != IntPtr.Zero &&
                MonitorFromWindow(foregroundWindow, MONITOR_DEFAULTTONEAREST) == widgetMonitor)
            {
                return true;
            }
        }

        bool fullscreenFound = false;

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd) ||
                hWnd == widgetHwnd ||
                hWnd == GetDesktopWindow() ||
                hWnd == GetShellWindow())
            {
                return true;
            }

            var className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);

            if (className.ToString() is "WorkerW" or "Progman")
            {
                return true;
            }

            // Skip minimized windows and DWM-cloaked windows (Search host, Start menu,
            // Widgets board, ApplicationFrameHost shells, etc.) — these report
            // IsWindowVisible == true and are often sized to the full monitor even
            // though the user can't actually see them.
            if (IsIconic(hWnd))
            {
                return true;
            }

            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            {
                return true;
            }

            IntPtr windowMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            if (windowMonitor != widgetMonitor)
            {
                return true;
            }

            if (!GetWindowRect(hWnd, out RECT windowRect))
            {
                return true;
            }

            var monitorInfo = new MONITORINFO
            {
                cbSize = Marshal.SizeOf<MONITORINFO>()
            };

            if (!GetMonitorInfo(widgetMonitor, ref monitorInfo))
            {
                return true;
            }

            if (IsFullscreenRect(windowRect, monitorInfo.rcMonitor))
            {
                fullscreenFound = true;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return fullscreenFound;
    }

    /// <summary>
    /// Determines whether a window rectangle covers the monitor rectangle,
    /// allowing a one-pixel tolerance for window-manager rounding.
    /// </summary>
    internal static bool IsFullscreenRect(RECT windowRect, RECT monitorRect)
    {
        const int tolerance = 1;

        return windowRect.Left >= monitorRect.Left - tolerance &&
              windowRect.Top >= monitorRect.Top - tolerance &&
              windowRect.Right <= monitorRect.Right + tolerance &&
              windowRect.Bottom <= monitorRect.Bottom + tolerance &&
              windowRect.Right - windowRect.Left >=
                  monitorRect.Right - monitorRect.Left - tolerance &&
              windowRect.Bottom - windowRect.Top >=
                  monitorRect.Bottom - monitorRect.Top - tolerance;
    }
}
