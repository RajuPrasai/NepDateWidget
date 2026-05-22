namespace NepDateWidget.Models;

public enum PdfConvertPageMode
{
    /// <summary>Render page 1 only. One output file.</summary>
    FirstPageOnly,
    /// <summary>Render every page to a separate image file.</summary>
    AllPagesPerFile,
    /// <summary>Render every page and stack them into one combined image.</summary>
    AllPagesCombined,
}
