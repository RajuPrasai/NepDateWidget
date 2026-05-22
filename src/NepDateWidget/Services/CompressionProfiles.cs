namespace NepDateWidget.Services;

/// <summary>
/// Shared quality tables for the 5-point compression slider (index 0 = smallest, 4 = best quality).
/// Both ImageCompressionService and PdfCompressionService read from here - no private duplicates.
/// </summary>
internal static class CompressionProfiles
{
    /// <summary>
    /// Proportional scale applied to the source's longest edge per compression level.
    /// null = no auto-resize at that level (levels 3-4 preserve original resolution).
    /// The target is min(source × ResizeScalePercent, ResizeMaxPx), then clamped up to
    /// ResizeFloorPx. If the result would be >= the original (i.e., upscale), no resize is applied.
    /// This ensures small images are still proportionally reduced while large images respect
    /// the absolute cap. Explicit user dimensions always override these profiles.
    /// </summary>
    internal static readonly double?[] ResizeScalePercent = { 0.30, 0.50, 0.70, null, null };

    /// <summary>Absolute longest-edge pixel ceiling per level - prevents excessive output from huge sources.</summary>
    internal static readonly uint?[] ResizeMaxPx = { 1280, 1600, 2048, null, null };

    /// <summary>Minimum output longest edge. Prevents producing unusable tiny images from small sources.</summary>
    internal const uint ResizeFloorPx = 320;

    /// <summary>
    /// Percentage (0.0–1.0) of original pixel dimensions to pre-populate the resize fields
    /// in ImageToolsViewModel when files are first loaded. Index 0 = smallest (70%), 4 = best (95%).
    /// These are display/UX defaults only - the actual compression auto-resize uses ResizeScalePercent.
    /// </summary>
    internal static readonly double[] ResizeDefaultPercent = { 0.70, 0.80, 0.88, 0.94, 0.98 };

    // Quality values are intentionally aggressive at the low end. At level 0 the combination of
    // quality 10 + proportional 30% resize produces a very small file (typically 95%+ reduction
    // vs the original) while remaining recognizable. Extreme quality values (< 5) add almost no
    // further size benefit and produce severe artifacts.
    internal static readonly int[] JpegQuality         = { 10, 30, 65, 85, 92 };
    internal static readonly int[] WebpQuality         = { 10, 30, 65, 85, 92 };
    internal static readonly int[] AvifQuality         = { 10, 30, 65, 85, 92 };
    internal static readonly int[] PngFlate            = {  9,  9,  8,  4,  2 };
    internal static readonly int[] PdfCompressionLevel = {  9,  8,  7,  5,  3 };
    internal static readonly int[] PdfOiMinArea        = {  0, 4096, 16384, 40000, 65536 };

    /// <summary>
    /// GIF color palette size per compression level. GIF supports max 256 colors.
    /// Reducing colors allows LZW to compress more aggressively.
    /// Level 4 (best) keeps the full palette - no quantization applied.
    /// </summary>
    internal static readonly int[] GifColors = { 64, 128, 192, 224, 256 };
}
