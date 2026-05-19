namespace NepDateWidget.Services;

public sealed record ImageConversionResult(bool Success, string? ErrorMessage = null);

public interface IImageConversionService
{
    ImageConversionResult Convert(
        string inputPath,
        string outputPath,
        string targetExtension,
        int qualityLevel,
        bool stripMetadata);
}
