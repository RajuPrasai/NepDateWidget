namespace NepDateWidget.Models;

public enum CompressionFileStatus
{
    Pending,
    Running,
    Done,
    Error,
}

/// <summary>
/// Observable item representing one file in the compression/resize file list.
/// </summary>
public sealed class CompressionFileItem
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public long FileSizeBytes { get; init; }
    public CompressionFileStatus Status { get; set; } = CompressionFileStatus.Pending;
    public string? ErrorMessage { get; set; }
    public long OutputSizeBytes { get; set; }
}
