namespace NepDateWidget.Services;

/// <param name="OutputPaths">
/// Output file paths produced by this operation.
/// Empty/null for failures. For multi-output operations (PDF all-pages mode)
/// contains all generated paths; for single-output contains one entry.
/// </param>
public sealed record ImageConversionResult(
    bool Success,
    string? ErrorMessage = null,
    IReadOnlyList<string>? OutputPaths = null);

public interface IImageConversionService
{
    /// <summary>
    /// Converts an image to the target format, optionally resizing it.
    /// </summary>
    /// <param name="targetWidth">Target width in pixels; null means no resize on this axis.</param>
    /// <param name="targetHeight">Target height in pixels; null means no resize on this axis.
    /// Providing only one dimension preserves aspect ratio.</param>
    ImageConversionResult Convert(
        string inputPath,
        string outputPath,
        string targetExtension,
        int qualityLevel,
        bool stripMetadata,
        uint? targetWidth = null,
        uint? targetHeight = null);
}

