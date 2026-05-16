using NepDateWidget.Models;

namespace NepDateWidget.Services;

public interface IImageCompressionService
{
    /// <summary>
    /// Compress (and optionally resize) the image at inputPath, writing the result to outputPath.
    /// Fully synchronous - intended to be called from Task.Run by the orchestrator.
    /// Does not throw; errors are returned in CompressionResult.ErrorMessage.
    /// </summary>
    CompressionResult Compress(string inputPath, string outputPath, string mimeType, CompressionSettings settings);
}
