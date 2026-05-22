using ImageMagick;
using NepDateWidget.Models;
using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Tests for the per-level proportional auto-resize logic.
/// Formula: target = min(source × ResizeScalePercent, ResizeMaxPx), clamped to ResizeFloorPx.
/// Never upscales; levels 3-4 never resize.
/// </summary>
public sealed class CompressionAutoResizeTests
{
    // ── CompressionProfiles table structure ──────────────────────────────────

    [Fact]
    public void ResizeScalePercent_HasFiveEntries()
    {
        Assert.Equal(5, CompressionProfiles.ResizeScalePercent.Length);
    }

    [Theory]
    [InlineData(0, 0.30)]
    [InlineData(1, 0.50)]
    [InlineData(2, 0.70)]
    public void ResizeScalePercent_LowerLevels_HaveExpectedScale(int level, double expected)
    {
        Assert.Equal(expected, CompressionProfiles.ResizeScalePercent[level]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void ResizeScalePercent_UpperLevels_AreNull(int level)
    {
        Assert.Null(CompressionProfiles.ResizeScalePercent[level]);
    }

    // ── Quality tables are monotonically non-decreasing ──────────────────────

    [Fact]
    public void JpegQuality_IsMonotonicallyNonDecreasing()
    {
        var q = CompressionProfiles.JpegQuality;
        for (int i = 1; i < q.Length; i++)
            Assert.True(q[i] >= q[i - 1], $"JpegQuality[{i}]={q[i]} < [{i - 1}]={q[i - 1]}");
    }

    [Fact]
    public void WebpQuality_IsMonotonicallyNonDecreasing()
    {
        var q = CompressionProfiles.WebpQuality;
        for (int i = 1; i < q.Length; i++)
            Assert.True(q[i] >= q[i - 1], $"WebpQuality[{i}]={q[i]} < [{i - 1}]={q[i - 1]}");
    }

    [Fact]
    public void AvifQuality_IsMonotonicallyNonDecreasing()
    {
        var q = CompressionProfiles.AvifQuality;
        for (int i = 1; i < q.Length; i++)
            Assert.True(q[i] >= q[i - 1], $"AvifQuality[{i}]={q[i]} < [{i - 1}]={q[i - 1]}");
    }

    // ── ApplyAutoResize: proportional scale with absolute cap ────────────────

    [Theory]
    // Level 0: scale 30%, cap 1280. 4000 * 0.30 = 1200, min(1200, 1280) = 1200.
    [InlineData(0, 4000, 3000, 1200u)]
    // Level 1: scale 50%, cap 1600. 4000 * 0.50 = 2000, min(2000, 1600) = 1600.
    [InlineData(1, 4000, 3000, 1600u)]
    // Level 2: scale 70%, cap 2048. 4000 * 0.70 = 2800, min(2800, 2048) = 2048.
    [InlineData(2, 4000, 3000, 2048u)]
    public void ApplyAutoResize_LargeImage_DownscaledToExpectedLongestEdge(int level, uint w, uint h, uint expectedLongest)
    {
        using var image = new MagickImage(MagickColors.Blue, w, h);
        var settings = new CompressionSettings { CompressionLevel = level };

        ImageCompressionService.ApplyAutoResize(image, level, settings);

        Assert.Equal(expectedLongest, Math.Max(image.Width, image.Height));
    }

    // ── ApplyAutoResize: scale-percentage wins over cap for smaller sources ──

    [Theory]
    // 800px source at level 0: scale 30% = 240, floor = 320 → output 320.
    [InlineData(0, 800, 600, 320u)]
    // 400px source at level 0: scale 30% = 120, floor = 320 → 320 < 400, so resize.
    [InlineData(0, 400, 300, 320u)]
    // 1000px source at level 1: scale 50% = 500, min(500, 1600) = 500, floor ok → output 500.
    [InlineData(1, 1000, 800, 500u)]
    public void ApplyAutoResize_MediumSmallImage_ProportionallyResized(int level, uint w, uint h, uint expectedLongest)
    {
        using var image = new MagickImage(MagickColors.Green, w, h);
        var settings = new CompressionSettings { CompressionLevel = level };

        ImageCompressionService.ApplyAutoResize(image, level, settings);

        Assert.Equal(expectedLongest, Math.Max(image.Width, image.Height));
    }

    // ── ApplyAutoResize: floor prevents upscaling very small images ──────────

    [Theory]
    // 280px: 280 * 0.30 = 84, floor = 320 → 320 >= 280 → no resize.
    [InlineData(0, 280, 210)]
    // 300px: 300 * 0.30 = 90, floor = 320 → 320 >= 300 → no resize.
    [InlineData(0, 300, 225)]
    public void ApplyAutoResize_VerySmallImage_NotUpscaled(int level, uint origW, uint origH)
    {
        using var image = new MagickImage(MagickColors.Red, origW, origH);
        var settings = new CompressionSettings { CompressionLevel = level };

        ImageCompressionService.ApplyAutoResize(image, level, settings);

        Assert.Equal(origW, image.Width);
        Assert.Equal(origH, image.Height);
    }

    // ── ApplyAutoResize: levels 3 and 4 never resize ────────────────────────

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void ApplyAutoResize_HighQualityLevel_NeverResizes(int level)
    {
        using var image = new MagickImage(MagickColors.White, 8000, 6000);
        var settings = new CompressionSettings { CompressionLevel = level };

        ImageCompressionService.ApplyAutoResize(image, level, settings);

        Assert.Equal(8000u, image.Width);
        Assert.Equal(6000u, image.Height);
    }

    // ── ApplyAutoResize: explicit user resize takes priority ─────────────────

    [Fact]
    public void ApplyAutoResize_ExplicitWidthSet_NoAutoResize()
    {
        using var image = new MagickImage(MagickColors.Blue, 4000, 3000);
        var settings = new CompressionSettings { CompressionLevel = 0, ResizeWidth = 2000 };

        ImageCompressionService.ApplyAutoResize(image, 0, settings);

        Assert.Equal(4000u, image.Width);
        Assert.Equal(3000u, image.Height);
    }

    [Fact]
    public void ApplyAutoResize_ExplicitHeightSet_NoAutoResize()
    {
        using var image = new MagickImage(MagickColors.Blue, 4000, 3000);
        var settings = new CompressionSettings { CompressionLevel = 0, ResizeHeight = 500 };

        ImageCompressionService.ApplyAutoResize(image, 0, settings);

        Assert.Equal(4000u, image.Width);
        Assert.Equal(3000u, image.Height);
    }

    // ── ApplyAutoResize: aspect ratio is preserved ───────────────────────────

    [Fact]
    public void ApplyAutoResize_WideImage_AspectRatioPreserved()
    {
        // 3840×2160 (16:9) at level 0: 3840*0.30=1152, min(1152,1280)=1152 → 1152×648
        using var image = new MagickImage(MagickColors.Blue, 3840, 2160);
        var settings = new CompressionSettings { CompressionLevel = 0 };

        ImageCompressionService.ApplyAutoResize(image, 0, settings);

        Assert.Equal(1152u, image.Width);
        Assert.Equal(648u, image.Height);
    }

    [Fact]
    public void ApplyAutoResize_PortraitLargeImage_LongestEdgeCapped()
    {
        // 1500×4000 portrait at level 0: longestEdge=4000, 4000*0.30=1200, min(1200,1280)=1200.
        using var image = new MagickImage(MagickColors.Red, 1500, 4000);
        var settings = new CompressionSettings { CompressionLevel = 0 };

        ImageCompressionService.ApplyAutoResize(image, 0, settings);

        Assert.Equal(1200u, Math.Max(image.Width, image.Height));
    }

    [Fact]
    public void ApplyAutoResize_SquareImage_RemainsSquare()
    {
        // 3000×3000 at level 0: 3000*0.30=900, min(900,1280)=900.
        using var image = new MagickImage(MagickColors.Blue, 3000, 3000);
        var settings = new CompressionSettings { CompressionLevel = 0 };

        ImageCompressionService.ApplyAutoResize(image, 0, settings);

        Assert.Equal(image.Width, image.Height);
        Assert.Equal(900u, image.Width);
    }
}
