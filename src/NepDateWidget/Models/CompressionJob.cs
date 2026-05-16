namespace NepDateWidget.Models;

/// <summary>
/// A single file to be processed by the job orchestrator.
/// </summary>
public sealed class CompressionJob
{
    public required string InputPath    { get; init; }
    public required string OutputPath   { get; set; }   // may be updated by orchestrator for collision handling
    public required CompressionSettings Settings   { get; init; }
    public required FileCategory Category   { get; init; }
    public required string MimeType     { get; init; }
}
