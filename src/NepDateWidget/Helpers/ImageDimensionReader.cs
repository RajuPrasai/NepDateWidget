using ImageMagick;

namespace NepDateWidget.Helpers;

/// <summary>
/// Reads pixel dimensions from an image file using a header-only Ping operation.
/// Ping reads only the image header — no pixel decoding — so it is fast even for large RAW files.
/// Returns null for PDFs (no pixel dimensions) and on any read failure.
/// </summary>
internal static class ImageDimensionReader
{
    private static readonly HashSet<string> _skipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
    };

    internal static (uint Width, uint Height)? TryRead(string path)
    {
        var ext = System.IO.Path.GetExtension(path);
        if (_skipExtensions.Contains(ext))
            return null;

        try
        {
            using var image = new MagickImage();
            image.Ping(path);
            return (image.Width, image.Height);
        }
        catch
        {
            return null;
        }
    }
}
