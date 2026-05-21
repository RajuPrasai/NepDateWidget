namespace NepDateWidget.Models;

public enum ImageToPdfMode
{
    /// <summary>Each input image produces its own PDF file.</summary>
    OnePerFile,
    /// <summary>All input images are combined into a single multi-page PDF.</summary>
    CombinedPdf,
}
