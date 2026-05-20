namespace NepDateWidget.Services;

public sealed record ImageConversionResult(bool Success, string? ErrorMessage = null);

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

