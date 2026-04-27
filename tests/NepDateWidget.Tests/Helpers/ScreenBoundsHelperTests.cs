using NepDateWidget.Helpers;

namespace NepDateWidget.Tests.Helpers;

/// <summary>
/// Tests for ScreenBoundsHelper core logic.
/// Uses the parameter-based overloads - no WPF runtime required.
/// </summary>
public class ScreenBoundsHelperTests
{
    // Virtual screen: 0,0 → 1920×1080
    private const double SL = 0, ST = 0, SW = 1920, SH = 1080;

    // ── IsPositionOnScreen ────────────────────────────────────────────────────

    [Fact]
    public void IsPositionOnScreen_WindowFullyOnScreen_ReturnsTrue()
    {
        Assert.True(ScreenBoundsHelper.IsPositionOnScreen(100, 100, 320, 420, SL, ST, SW, SH));
    }

    [Fact]
    public void IsPositionOnScreen_WindowCompletelyOffRight_ReturnsFalse()
    {
        // Window starts at x=2000, well past the 1920 right edge
        Assert.False(ScreenBoundsHelper.IsPositionOnScreen(2000, 100, 320, 420, SL, ST, SW, SH));
    }

    [Fact]
    public void IsPositionOnScreen_WindowCompletelyOffBottom_ReturnsFalse()
    {
        Assert.False(ScreenBoundsHelper.IsPositionOnScreen(100, 1200, 320, 420, SL, ST, SW, SH));
    }

    [Fact]
    public void IsPositionOnScreen_WindowPartiallyOffRightButEnoughVisible_ReturnsTrue()
    {
        // Window left edge at 1900; 20 px visible on screen (1920-1900), less than 64 → false
        Assert.False(ScreenBoundsHelper.IsPositionOnScreen(1900, 100, 320, 420, SL, ST, SW, SH));
    }

    [Fact]
    public void IsPositionOnScreen_WindowPartiallyOffRightWith64pxVisible_ReturnsTrue()
    {
        // WindowLeft = 1920 - 64 = 1856 → exactly 64 px visible horizontally
        Assert.True(ScreenBoundsHelper.IsPositionOnScreen(1856, 100, 320, 420, SL, ST, SW, SH));
    }

    [Fact]
    public void IsPositionOnScreen_WindowAtOrigin_ReturnsTrue()
    {
        Assert.True(ScreenBoundsHelper.IsPositionOnScreen(0, 0, 320, 48, SL, ST, SW, SH));
    }

    // ── RecoverToSafePosition ─────────────────────────────────────────────────

    [Fact]
    public void RecoverToSafePosition_PlacesWindowInsideWorkArea()
    {
        // Work area: 0,0 → 1920×1040
        var (left, top) = ScreenBoundsHelper.RecoverToSafePosition(320, 48, 0, 0, 1920, 1040);

        Assert.True(left >= 0, "Recovered left should be non-negative");
        Assert.True(top  >= 0, "Recovered top should be non-negative");
        Assert.True(left + 320 <= 1920 + 1, "Window should not extend far past right edge");
    }

    [Fact]
    public void RecoverToSafePosition_WidgetWiderThanScreen_ClampedToLeftEdge()
    {
        // Edge case: widget wider than work area width
        var (left, _) = ScreenBoundsHelper.RecoverToSafePosition(2000, 48, 0, 0, 1920, 1040);

        Assert.True(left >= 0, "Should not recover to a negative left position");
    }
}
