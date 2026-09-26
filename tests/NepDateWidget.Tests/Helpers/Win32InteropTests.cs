using NepDateWidget.Helpers;

namespace NepDateWidget.Tests.Helpers;

public class Win32InteropTests
{
    // A typical 1920x1080 monitor at the origin.
    private static readonly Win32Interop.RECT StandardMonitor =
        new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

    private static Win32Interop.RECT Rect(int left, int top, int right, int bottom) =>
        new() { Left = left, Top = top, Right = right, Bottom = bottom };

    [Fact]
    public void ExactMatch_IsFullscreen()
    {
        var window = Rect(0, 0, 1920, 1080);

        Assert.True(Win32Interop.IsFullscreenRect(window, StandardMonitor));
    }

    [Theory]
    [InlineData(-1, -1, 1921, 1081)] // window 1px larger on every edge
    [InlineData(1, 0, 1920, 1080)]   // left inset by 1px alone
    [InlineData(0, 1, 1920, 1080)]   // top inset by 1px alone
    [InlineData(0, 0, 1919, 1080)]   // right inset by 1px alone
    [InlineData(0, 0, 1920, 1079)]   // bottom inset by 1px alone
    [InlineData(0, 0, 1920, 1080)]   // exact, sanity check within Theory
    public void WithinOnePixelTolerance_IsFullscreen(int left, int top, int right, int bottom)
    {
        var window = Rect(left, top, right, bottom);

        Assert.True(Win32Interop.IsFullscreenRect(window, StandardMonitor));
    }

    [Fact]
    public void InsetOnBothSides_ExceedsTolerance_IsNotFullscreen()
    {
        // Each edge individually is within 1px, but width shrinks by 2px total
        // (1px off Left AND 1px off Right) — the size check treats width/height
        // tolerance cumulatively, not per-edge, so this correctly fails.
        var window = Rect(1, 1, 1919, 1079);

        Assert.False(Win32Interop.IsFullscreenRect(window, StandardMonitor));
    }

    [Fact]
    public void TwoPixelsSmaller_IsNotFullscreen()
    {
        // Just outside tolerance — a maximized (not fullscreen) window with
        // visible borders should not be mistaken for fullscreen.
        var window = Rect(2, 2, 1918, 1078);

        Assert.False(Win32Interop.IsFullscreenRect(window, StandardMonitor));
    }

    [Fact]
    public void HalfScreenWindow_IsNotFullscreen()
    {
        var window = Rect(0, 0, 960, 1080);

        Assert.False(Win32Interop.IsFullscreenRect(window, StandardMonitor));
    }

    [Fact]
    public void WindowOnDifferentOrigin_MatchingSize_IsFullscreen()
    {
        // Secondary monitor positioned to the right of the primary at x=1920.
        var secondaryMonitor = Rect(1920, 0, 3840, 1080);
        var window = Rect(1920, 0, 3840, 1080);

        Assert.True(Win32Interop.IsFullscreenRect(window, secondaryMonitor));
    }

    [Fact]
    public void WindowSmallerThanMonitor_TaskbarVisibleCase_IsNotFullscreen()
    {
        // Maximized window that stops above a 40px taskbar — must not be
        // treated as fullscreen even though it's close in size.
        var window = Rect(0, 0, 1920, 1040);

        Assert.False(Win32Interop.IsFullscreenRect(window, StandardMonitor));
    }

    [Fact]
    public void ZeroSizeWindow_IsNotFullscreen()
    {
        var window = Rect(0, 0, 0, 0);

        Assert.False(Win32Interop.IsFullscreenRect(window, StandardMonitor));
    }
}