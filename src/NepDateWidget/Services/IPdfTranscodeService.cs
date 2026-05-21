using NepDateWidget.Models;

namespace NepDateWidget.Services;

/// <summary>
/// Handles PDF-to-image and image-to-PDF transcoding.
/// All methods are synchronous and safe to call inside Task.Run.
/// WinRT async operations are synchronously awaited via AsTask().GetAwaiter().GetResult()
/// which is safe on thread-pool threads.
/// </summary>
public interface IPdfTranscodeService
{
    /// <summary>
    /// Renders PDF page(s) to raster image files.
    /// <paramref name="outputBasePath"/> is used as:
    ///   - The exact output path for FirstPageOnly.
    ///   - The base path pattern for AllPagesPerFile / AllPagesCombined
    ///     (extension preserved; _p01, _p02, ... suffixes added for per-file mode).
    /// Returns OutputPaths containing the paths of all written files.
    /// </summary>
    ImageConversionResult PdfToImage(
        string inputPath,
        string outputBasePath,
        string targetExt,
        int qualityLevel,
        PdfConvertPageMode pageMode);

    /// <summary>
    /// Embeds a single image as a one-page PDF.
    /// Any Magick.NET-readable format is accepted as input.
    /// </summary>
    ImageConversionResult ImageToPdf(string inputPath, string outputPath);

    /// <summary>
    /// Combines multiple images into a single multi-page PDF.
    /// Each image becomes one page in document order.
    /// </summary>
    ImageConversionResult ImagesToPdf(IReadOnlyList<string> inputPaths, string outputPath);
}
