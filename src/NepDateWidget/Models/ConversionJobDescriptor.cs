namespace NepDateWidget.Models;

/// <summary>
/// Describes a single unit of work for the conversion pipeline.
/// Kind determines which code path the orchestrator uses.
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

    // ── PDF-specific ──────────────────────────────────────────────────────────

    /// <summary>Routing discriminator. Defaults to ImageToImage (existing behaviour).</summary>
    public ConversionKind Kind { get; init; } = ConversionKind.ImageToImage;

    /// <summary>For PdfToImage jobs: which pages to render.</summary>
    public PdfConvertPageMode PdfPageMode { get; init; } = PdfConvertPageMode.FirstPageOnly;

    /// <summary>
    /// For ImagesToPdf (combined) jobs: all input image paths.
    /// InputPath is set to the first entry for display/progress purposes.
    /// </summary>
    public IReadOnlyList<string>? CombinedInputPaths { get; init; }
}
