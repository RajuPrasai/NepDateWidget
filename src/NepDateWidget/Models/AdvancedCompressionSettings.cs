namespace NepDateWidget.Models;

/// <summary>
/// Advanced per-format overrides collected from the advanced settings panel.
/// All overrides are optional (null = use slider default).
/// </summary>
public sealed class AdvancedCompressionSettings
{
    // Image - quality overrides
    public int? QualityOverride { get; set; }   // 1-95; null = use slider mapping
    public bool StripMetadata { get; set; } = true;

    // PNG
    public bool ConvertToWebP { get; set; }
    public bool LosslessWebP { get; set; }

    // GIF
    public bool OptimizeGifFrames { get; set; } = true;

    // TIFF compression method: "LZW" | "JPEG" | "ZIP" | "None"
    public string TiffCompression { get; set; } = "LZW";

    // PDF
    public bool LinearizePdf { get; set; } = true;
}
