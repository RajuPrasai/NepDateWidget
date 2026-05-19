namespace NepDateWidget.Services;

/// <summary>
/// Shared quality tables for the 5-point compression slider (index 0 = smallest, 4 = best quality).
/// Both ImageCompressionService and PdfCompressionService read from here — no private duplicates.
/// </summary>
internal static class CompressionProfiles
{
    internal static readonly int[] JpegQuality         = { 30, 50, 70, 85, 95 };
    internal static readonly int[] WebpQuality         = { 26, 46, 68, 82, 93 };
    internal static readonly int[] AvifQuality         = { 26, 44, 63, 80, 92 };
    internal static readonly int[] PngFlate            = {  9,  9,  8,  4,  2 };
    internal static readonly int[] PdfCompressionLevel = {  9,  8,  7,  5,  3 };
    internal static readonly int[] PdfOiMinArea        = {  0, 4096, 16384, 40000, 65536 };
}
