using ImageMagick;
using System.IO;

namespace NepDateWidget.Services;

public sealed class ImageConversionService : IImageConversionService
{
    private static readonly int[] JpegQuality = [30, 50, 70, 85, 95];
    private static readonly int[] WebpQuality = [25, 45, 65, 80, 90];
    private static readonly int[] AvifQuality = [30, 45, 60, 75, 88];
    private static readonly uint[] PngCompression = [9, 7, 6, 4, 1];

    public ImageConversionResult Convert(
        string inputPath,
        string outputPath,
        string targetExtension,
        int qualityLevel,
        bool stripMetadata,
        uint? targetWidth = null,
        uint? targetHeight = null)
    {
        try
        {
            var level = Math.Clamp(qualityLevel, 0, 4);
            var ext = targetExtension.TrimStart('.').ToLowerInvariant();

            if (ext == "gif")
            {
                using var collection = new MagickImageCollection(inputPath);

                bool needsResize = targetWidth is not null || targetHeight is not null;

                if (needsResize)
                    collection.Coalesce();

                foreach (var frame in collection)
                {
                    if (stripMetadata)
                        frame.Strip();

                    if (needsResize)
                        ApplyResizeFrame(frame, targetWidth, targetHeight);
                }

                if (needsResize)
                {
                    collection.OptimizePlus();
                    collection.OptimizeTransparency();
                }

                collection.Write(outputPath, MagickFormat.Gif);
                return new ImageConversionResult(true);
            }

            using var image = new MagickImage(inputPath);

            if (stripMetadata)
                image.Strip();

            // Apply resize before encoding if either dimension is specified.
            ApplyResize(image, targetWidth, targetHeight);

            var format = GetFormat(ext);
            image.Format = format;

            switch (ext)
            {
                case "jpg":
                case "jpeg":
                    image.Quality = (uint)JpegQuality[level];
                    break;
                case "webp":
                    image.Quality = (uint)WebpQuality[level];
                    break;
                case "avif":
                    image.Quality = (uint)AvifQuality[level];
                    break;
                case "png":
                    image.Quality = PngCompression[level];
                    break;
            }

            image.Write(outputPath);
            return new ImageConversionResult(true);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            return new ImageConversionResult(false, ex.Message);
        }
    }

    private static MagickFormat GetFormat(string ext) => ext switch
    {
        "jpg" or "jpeg" => MagickFormat.Jpeg,
        "png"           => MagickFormat.Png,
        "webp"          => MagickFormat.WebP,
        "avif"          => MagickFormat.Avif,
        "gif"           => MagickFormat.Gif,
        "bmp"           => MagickFormat.Bmp,
        "tif" or "tiff" => MagickFormat.Tiff,
        "ico"           => MagickFormat.Ico,
        "tga"           => MagickFormat.Tga,
        _               => MagickFormat.Jpeg,
    };

    /// <summary>
    /// Resizes the image proportionally if one or both target dimensions are provided.
    /// One dimension: scales to that dimension preserving aspect ratio.
    /// Both dimensions: resizes to exact size (may change aspect ratio).
    /// Neither: no-op.
    /// </summary>
    private static void ApplyResize(MagickImage image, uint? targetWidth, uint? targetHeight)
    {
        if (targetWidth is null && targetHeight is null)
            return;

        var geometry = new MagickGeometry(
            targetWidth ?? 0,
            targetHeight ?? 0);

        // If only one dimension is given, preserve aspect ratio by setting IgnoreAspectRatio=false.
        if (targetWidth is null || targetHeight is null)
        {
            geometry.IgnoreAspectRatio = false;
        }
        else
        {
            geometry.IgnoreAspectRatio = true;
        }

        image.Resize(geometry);
    }

    private static void ApplyResizeFrame(IMagickImage frame, uint? targetWidth, uint? targetHeight)
    {
        var geometry = new MagickGeometry(
            targetWidth ?? 0,
            targetHeight ?? 0);

        if (targetWidth is null || targetHeight is null)
            geometry.IgnoreAspectRatio = false;
        else
            geometry.IgnoreAspectRatio = true;

        frame.Resize(geometry);
    }
}
