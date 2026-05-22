namespace NepDateWidget.Models;

/// <summary>
/// Compression / resize parameters passed from ViewModel to services.
/// Plain data model - not a service.
/// </summary>
public sealed class CompressionSettings
{
    /// <summary>0..4 compression profile from smallest output to best quality.</summary>
    public int CompressionLevel { get; set; } = 1;

    // Resize
    public uint? ResizeWidth { get; set; }
    public uint? ResizeHeight { get; set; }

    /// <summary>
    /// When true, suppresses the per-level auto-resize built into the compression pipeline.
    /// Set when the user explicitly disabled the Resize toggle in the UI.
    /// </summary>
    public bool NoAutoResize { get; set; }

    // Populated from the advanced panel when open
    public AdvancedCompressionSettings Advanced { get; set; } = new();
}
