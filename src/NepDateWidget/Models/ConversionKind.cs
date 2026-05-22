namespace NepDateWidget.Models;

public enum ConversionKind
{
    /// <summary>Standard image-to-image format conversion (existing path).</summary>
    ImageToImage,
    /// <summary>PDF page(s) rendered to raster image format.</summary>
    PdfToImage,
    /// <summary>Single image embedded as one-page PDF.</summary>
    ImageToPdf,
    /// <summary>Multiple images combined into a single multi-page PDF.</summary>
    ImagesToPdf,
}
