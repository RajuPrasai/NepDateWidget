using System.Windows;

namespace NepDateWidget.Helpers;

/// <summary>
/// Utility for checking whether a window position is visible on any screen and
/// recovering off-screen windows to a safe position.
///
/// Core logic is parameter-based so it can be unit-tested without a running WPF app.
/// Public entry points read from SystemParameters at call time.
/// </summary>
public static class ScreenBoundsHelper
{
    /// <summary>Minimum visible width in px - enough to grab and drag the widget.</summary>
    private const double MinVisibleWidth = 64.0;

    /// <summary>Minimum visible height in px - a 48px collapsed bar must pass.</summary>
    private const double MinVisibleHeight = 20.0;

    // ── Testable core ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if at least <see cref="MinVisiblePixels"/> of the window rectangle
    /// overlaps the virtual screen rectangle defined by the supplied coordinates.
    /// </summary>
    public static bool IsPositionOnScreen(
        double winLeft, double winTop, double winWidth, double winHeight,
        double screenLeft, double screenTop, double screenWidth, double screenHeight)
    {
        double screenRight = screenLeft + screenWidth;
        double screenBottom = screenTop + screenHeight;

        // Intersection left/top/right/bottom
        double ix = Math.Max(winLeft, screenLeft);
        double iy = Math.Max(winTop, screenTop);
        double iw = Math.Min(winLeft + winWidth, screenRight) - ix;
        double ih = Math.Min(winTop + winHeight, screenBottom) - iy;

        return iw >= MinVisibleWidth && ih >= MinVisibleHeight;
    }

    /// <summary>
    /// Returns a safe recovery position (clamped to primary work area) when the window
    /// falls outside the visible virtual screen bounds.
    /// </summary>
    public static (double Left, double Top) RecoverToSafePosition(
        double winWidth, double winHeight,
        double workAreaLeft, double workAreaTop,
        double workAreaWidth, double workAreaHeight)
    {
        double safeLeft = workAreaLeft + workAreaWidth - winWidth - 20;
        double safeTop = workAreaTop + 20;

        // Clamp so window is not placed off the left or top edge
        safeLeft = Math.Max(workAreaLeft, safeLeft);
        safeTop = Math.Max(workAreaTop, safeTop);

        return (safeLeft, safeTop);
    }

    // ── Wrappers that read live screen info ──────────────────────────────────

    /// <summary>
    /// Checks whether the window rectangle is sufficiently visible on the current virtual desktop.
    /// Reads screen bounds from WPF SystemParameters at call time.
    /// </summary>
    public static bool IsOnScreen(double left, double top, double width, double height)
    {
        return IsPositionOnScreen(
            left, top, width, height,
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
    }

    /// <summary>
    /// Returns the position to use on startup.
    /// If the saved position is visible, returns it unchanged; otherwise recovers to a safe spot.
    /// </summary>
    public static (double Left, double Top) GetStartupPosition(
        double savedLeft, double savedTop, double width, double height)
    {
        if (IsOnScreen(savedLeft, savedTop, width, height))
            return (savedLeft, savedTop);

        var wa = SystemParameters.WorkArea;
        return RecoverToSafePosition(width, height, wa.Left, wa.Top, wa.Width, wa.Height);
    }

    /// <summary>
    /// Returns a first-run default position: bottom-left of the primary screen,
    /// vertically centered on the taskbar. Auto-detects taskbar edge and size
    /// by comparing WorkArea to PrimaryScreen dimensions.
    /// </summary>
    public static (double Left, double Top) GetFirstRunPosition(double widgetWidth, double widgetHeight)
    {
        var wa = SystemParameters.WorkArea;
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        const double margin = 8;

        // Bottom taskbar (most common)
        double bottomGap = screenH - wa.Bottom;
        if (bottomGap > 10)
        {
            double taskbarCenter = wa.Bottom + bottomGap / 2;
            return (wa.Left + margin, taskbarCenter - widgetHeight / 2);
        }

        // Top taskbar
        if (wa.Top > 10)
        {
            double taskbarCenter = wa.Top / 2;
            return (wa.Left + margin, taskbarCenter - widgetHeight / 2);
        }

        // Left taskbar
        if (wa.Left > 10)
        {
            return (wa.Left + margin, wa.Bottom - widgetHeight - margin);
        }

        // Right taskbar or no detectable taskbar: bottom-left of work area
        return (wa.Left + margin, wa.Bottom - widgetHeight - margin);
    }
}
