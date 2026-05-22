namespace NepDateWidget.Models;

/// <summary>
/// Result returned by image/PDF compression services.
/// No bytes are included - services write directly to the output path.
/// </summary>
public sealed class CompressionResult
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public long OriginalSizeBytes { get; init; }
    public long CompressedSizeBytes { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public long SavedBytes => OriginalSizeBytes - CompressedSizeBytes;
}
