using NepDateWidget.Views;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Regression tests for IdPhotoView.ComputeZoomedCropSize.
///
/// The bug: when the crop aspect ratio is extreme (very wide or very tall), the
/// minShort enforcement scale-up can push one dimension above the image boundary
/// even after the first-pass caps. The old code then called
/// Math.Clamp(pos, offset, offset + imgRendered - newDim) with max &lt; min,
/// throwing ArgumentException and crashing the app on scroll-wheel zoom.
///
/// Every test below verifies that the returned dimensions satisfy:
///   newW &lt;= imgRenderedW  and  newH &lt;= imgRenderedH
/// which is the invariant that makes the subsequent Clamp calls safe.
/// </summary>
public class IdPhotoCropZoomTests
{
    // Helper: assert both dimensions are within image bounds and positive.
    private static void AssertWithinBounds(double newW, double newH, double imgW, double imgH, string label = "")
    {
        Assert.True(newW > 0,    $"{label}: newW must be > 0");
        Assert.True(newH > 0,    $"{label}: newH must be > 0");
        Assert.True(newW <= imgW, $"{label}: newW ({newW:F2}) must be <= imgW ({imgW:F2})");
        Assert.True(newH <= imgH, $"{label}: newH ({newH:F2}) must be <= imgH ({imgH:F2})");
    }

    // ── Crash regression: extreme wide aspect (10:1) on zoom-in ─────────────

    [Fact]
    public void ZoomIn_ExtremeWideAspect_DoesNotExceedImageBounds()
    {
        // Crop 200×20 (aspect=10), image 200×200.
        // First-pass cap: newW capped to 200, newH = 200/10 = 20.
        // minShort = Max(20, 200*0.2) = 40. shortSide=20 < 40.
        // scale = 40/20 = 2 → newW = 400 > imgW=200 - old code crashes here.
        var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(200, 20, 200, 200, delta: +120);
        AssertWithinBounds(newW, newH, 200, 200, "wide aspect zoom-in");
    }

    // ── Crash regression: extreme tall aspect (1:10) on zoom-out ─────────────

    [Fact]
    public void ZoomOut_ExtremeTallAspect_DoesNotExceedImageBounds()
    {
        // Crop 20×200 (aspect=0.1), image 200×200.
        // After zooming out to ~18×180, minShort=18 < 40, scale=40/18≈2.2.
        // newH ≈ 400 > imgH=200 - old code crashes here.
        // Start at a size where the next zoom-out triggers the min-short path.
        var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(22, 220, 200, 200, delta: -120);
        AssertWithinBounds(newW, newH, 200, 200, "tall aspect zoom-out");
    }

    // ── Crash regression: wide crop + tall image ─────────────────────────────

    [Fact]
    public void ZoomIn_WideCropOnTallImage_DoesNotExceedImageBounds()
    {
        // Image 100×800 (portrait), crop aspect ≈ 1.3 (landscape).
        // After first-pass caps: newW capped to 100, newH = 100/1.3 ≈ 77 - fine.
        // But if minShort triggers: scale-up could push newW > 100.
        // Crop near minimum size to force min-short path.
        var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(18, 14, 100, 800, delta: -120);
        AssertWithinBounds(newW, newH, 100, 800, "wide crop on tall image");
    }

    // ── Crash regression: tall crop + wide image ─────────────────────────────

    [Fact]
    public void ZoomOut_TallCropOnWideImage_DoesNotExceedImageBounds()
    {
        // Image 800×100 (landscape), crop aspect ≈ 0.5 (portrait).
        // minShort = Max(20, 100*0.2) = 20. If newH near zero, scale pushes newH > 100.
        var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(14, 22, 800, 100, delta: -120);
        AssertWithinBounds(newW, newH, 800, 100, "tall crop on wide image");
    }

    // ── Normal zoom-in: size increases, stays within bounds ──────────────────

    [Fact]
    public void ZoomIn_NormalAspect_SizeIncreasesWithinBounds()
    {
        // Passport aspect 35/45 ≈ 0.778, image 600×800.
        double cropW = 255, cropH = 328;
        double imgW = 600, imgH = 800;
        var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(cropW, cropH, imgW, imgH, delta: +120);
        Assert.True(newW > cropW, "zoom-in should increase width");
        Assert.True(newH > cropH, "zoom-in should increase height");
        AssertWithinBounds(newW, newH, imgW, imgH, "normal zoom-in");
    }

    // ── Normal zoom-out: size decreases, stays at or above minimum ───────────

    [Fact]
    public void ZoomOut_NormalAspect_SizeDecreasesAboveMinimum()
    {
        // Start large, zoom out. Size should decrease.
        double cropW = 400, cropH = 514;
        double imgW = 600, imgH = 800;
        var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(cropW, cropH, imgW, imgH, delta: -120);
        Assert.True(newW < cropW, "zoom-out should decrease width");
        Assert.True(newW > 0);
        Assert.True(newH > 0);
        AssertWithinBounds(newW, newH, imgW, imgH, "normal zoom-out");
    }

    // ── Zoom-in at max size: result is clamped to image size ─────────────────

    [Fact]
    public void ZoomIn_AtMaxSize_ClampedToImageBounds()
    {
        // Crop already fills the image. Zoom-in should keep it at or below image size.
        double imgW = 400, imgH = 300;
        var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(imgW, imgH, imgW, imgH, delta: +120);
        AssertWithinBounds(newW, newH, imgW, imgH, "zoom-in at max");
    }

    // ── Zoom-out at minimum: result stays at or above minimum ────────────────

    [Fact]
    public void ZoomOut_AtMinimumSize_StaysAtMinimum()
    {
        // Crop at exactly minShort. Zoom-out should not go below min.
        // Image 400×300, minShort = Max(20, 300*0.2) = 60.
        // Aspect 1.0 (square): cropW=cropH=60.
        double imgW = 400, imgH = 300;
        double minShort = Math.Max(20.0, Math.Min(imgW, imgH) * 0.20); // 60
        var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(minShort, minShort, imgW, imgH, delta: -120);
        Assert.True(newW >= minShort - 0.01, $"newW {newW:F2} should not go below minShort {minShort}");
        Assert.True(newH >= minShort - 0.01, $"newH {newH:F2} should not go below minShort {minShort}");
        AssertWithinBounds(newW, newH, imgW, imgH, "zoom-out at minimum");
    }

    // ── Aspect ratio is preserved on normal zoom ──────────────────────────────

    [Fact]
    public void Zoom_AspectRatioPreserved_UnlessConstraintForces()
    {
        // Passport 35/45 ≈ 0.7778. Mid-size crop, no constraints should fire.
        double aspect = 35.0 / 45.0;
        double cropW = 200, cropH = 200 / aspect; // ≈ 257
        double imgW = 600, imgH = 800;
        var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(cropW, cropH, imgW, imgH, delta: +120);
        double newAspect = newW / newH;
        Assert.True(Math.Abs(newAspect - aspect) < 1e-6, $"aspect changed from {aspect:F6} to {newAspect:F6}");
    }

    // ── Square crop (circle mode) zoom-in on non-square image ────────────────

    [Fact]
    public void ZoomIn_SquareCropOnLandscapeImage_DoesNotExceedImageBounds()
    {
        // Circle mode uses square crop. Image 800×400.
        // Max possible: newW=newH=400 (limited by height).
        double imgW = 800, imgH = 400;
        var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(380, 380, imgW, imgH, delta: +120);
        AssertWithinBounds(newW, newH, imgW, imgH, "square crop on landscape");
    }

    // ── Multiple successive zoom steps never crash ────────────────────────────

    [Theory]
    [InlineData(+120, 30)]  // 30 zoom-in steps
    [InlineData(-120, 30)]  // 30 zoom-out steps
    public void RepeatedZoom_NeverThrowsAndStaysInBounds(int delta, int steps)
    {
        double cropW = 255, cropH = 328;
        double imgW = 600, imgH = 800;

        for (int i = 0; i < steps; i++)
        {
            var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(cropW, cropH, imgW, imgH, delta);
            AssertWithinBounds(newW, newH, imgW, imgH, $"step {i}");
            cropW = newW;
            cropH = newH;
        }
    }

    // ── Repeated zoom-in with extreme aspect (the exact crash scenario) ───────

    [Theory]
    [InlineData(200, 20, 200, 200)]   // 10:1 wide
    [InlineData(20, 200, 200, 200)]   // 1:10 tall
    [InlineData(100, 5, 300, 200)]    // 20:1 extreme wide
    [InlineData(5, 100, 200, 300)]    // 1:20 extreme tall
    public void RepeatedZoomIn_ExtremeAspect_NeverExceedsImageBounds(
        double cropW, double cropH, double imgW, double imgH)
    {
        for (int i = 0; i < 20; i++)
        {
            var (newW, newH) = IdPhotoView.ComputeZoomedCropSize(cropW, cropH, imgW, imgH, delta: +120);
            AssertWithinBounds(newW, newH, imgW, imgH, $"extreme aspect step {i}");
            cropW = newW;
            cropH = newH;
        }
    }
}
