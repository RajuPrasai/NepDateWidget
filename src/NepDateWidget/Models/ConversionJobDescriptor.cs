namespace NepDateWidget.Models;

/// <summary>
/// Describes a single file to be processed by the conversion pipeline
/// (format change ± quality ± resize). Used by StartConversionJobAsync.
/// </summary>
public sealed class ConversionJobDescriptor
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; set; }   // may be updated by orchestrator for collision handling
    public required string TargetExtension { get; init; }
    public required int QualityLevel { get; init; }
    public required bool StripMetadata { get; init; }
    public uint? TargetWidth { get; init; }
    public uint? TargetHeight { get; init; }
}
