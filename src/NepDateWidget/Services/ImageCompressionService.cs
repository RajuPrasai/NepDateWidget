using ImageMagick;
using NepDateWidget.Models;
using System.IO;

namespace NepDateWidget.Services;

public sealed class ImageCompressionService : IImageCompressionService
{
    // 5-point profile from smallest output (0) to best quality (4).
    private static readonly int[] JpegQuality = { 45, 60, 72, 85, 95 };
    private static readonly int[] WebpQuality = { 40, 58, 70, 82, 93 };
    private static readonly int[] AvifQuality = { 40, 55, 65, 80, 92 };
    private static readonly int[] PngFlate    = { 9, 8, 6, 4, 2 };

    public CompressionResult Compress(string inputPath, string outputPath, string mimeType, CompressionSettings settings)
    {
        long originalSize = 0;
        try
        {
            originalSize = new FileInfo(inputPath).Length;

            if (string.Equals(mimeType, "image/gif", StringComparison.OrdinalIgnoreCase))
                CompressGif(inputPath, outputPath, settings);
            else
                CompressSingleFrame(inputPath, outputPath, mimeType, settings);

            long compressedSize = new FileInfo(outputPath).Length;
            return new CompressionResult
            {
                InputPath          = inputPath,
                OutputPath         = outputPath,
                OriginalSizeBytes  = originalSize,
                CompressedSizeBytes = compressedSize,
                Success            = true,
            };
        }
        catch (Exception ex)
        {
            // Clean up partial output if it exists
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }

            return new CompressionResult
            {
                InputPath          = inputPath,
                OutputPath         = outputPath,
                OriginalSizeBytes  = originalSize,
                CompressedSizeBytes = 0,
                Success            = false,
                ErrorMessage       = ex.Message,
            };
        }
    }

    // ── Single-frame (all non-GIF types) ────────────────────────────────────

    private static void CompressSingleFrame(string inputPath, string outputPath, string mimeType, CompressionSettings settings)
    {
        var adv   = settings.Advanced;
        int level = Math.Clamp(settings.CompressionLevel, 0, 4);

        using var image = new MagickImage(inputPath);

        // 1. Bake EXIF rotation into pixels BEFORE anything else.
        image.AutoOrient();

        // 2. Resize if requested.
        ApplyResize(image, settings);

        // 3. Strip metadata.
        if (adv.StripMetadata)
            image.Strip();

        // 4. Apply format-specific quality and format.
        var format = ApplyFormatSettings(image, mimeType, level, adv, out string effectiveOutputPath);

        // Use the derived output path (may have changed extension for BMP→JPEG, HEIC→JPEG, PNG→WebP).
        var realOutputPath = string.IsNullOrEmpty(effectiveOutputPath) ? outputPath : effectiveOutputPath;

        // 5. Write atomically.
        WriteAtomic(image, format, realOutputPath);

        // If the effective path differs from the caller's outputPath, rename to expected location.
        // (This happens when the orchestrator already named the file correctly before calling us.)
        // The orchestrator always sets outputPath to the format-adjusted name, so this is a no-op
        // in practice. But if they differ, correct it.
        if (!string.Equals(realOutputPath, outputPath, StringComparison.OrdinalIgnoreCase)
            && !File.Exists(outputPath))
        {
            File.Move(realOutputPath, outputPath);
        }
    }

    private static MagickFormat ApplyFormatSettings(
        MagickImage image,
        string mimeType,
        int level,
        AdvancedCompressionSettings adv,
        out string effectiveOutputPath)
    {
        effectiveOutputPath = string.Empty;

        switch (mimeType.ToLowerInvariant())
        {
            case "image/jpeg":
            {
                int q = adv.QualityOverride ?? JpegQuality[level];
                image.Quality = (uint)Math.Clamp(q, 1, 95);
                image.Format = MagickFormat.Jpeg;
                return MagickFormat.Jpeg;
            }

            case "image/bmp":
            {
                // BMP → JPEG: output extension changes to .jpg
                int q = adv.QualityOverride ?? JpegQuality[level];
                image.Quality = (uint)Math.Clamp(q, 1, 95);
                image.Format = MagickFormat.Jpeg;
                return MagickFormat.Jpeg;
            }

            case "image/heif":
            {
                // HEIC/HEIF → JPEG: output extension changes to .jpg
                int q = adv.QualityOverride ?? JpegQuality[level];
                image.Quality = (uint)Math.Clamp(q, 1, 95);
                image.Format = MagickFormat.Jpeg;
                return MagickFormat.Jpeg;
            }

            case "image/png":
            {
                if (adv.ConvertToWebP)
                {
                    int q = adv.QualityOverride ?? WebpQuality[level];
                    image.Quality = (uint)Math.Clamp(q, 1, 95);
                    if (adv.LosslessWebP) image.Quality = 100;
                    image.Format = MagickFormat.WebP;
                    return MagickFormat.WebP;
                }
                // PNG deflate is set via Compression + CompressionLevel.
                // Magick.NET maps image.Quality to deflate level for PNG (0-9 mapped to 0-100 range).
                int flate = PngFlate[level];
                image.Quality = (uint)(flate * 11); // scale 0-9 → 0-99
                image.Format = MagickFormat.Png;
                return MagickFormat.Png;
            }

            case "image/webp":
            {
                if (adv.LosslessWebP)
                {
                    image.Quality = 100;
                }
                else
                {
                    int q = adv.QualityOverride ?? WebpQuality[level];
                    image.Quality = (uint)Math.Clamp(q, 1, 95);
                }
                image.Format = MagickFormat.WebP;
                return MagickFormat.WebP;
            }

            case "image/tiff":
            {
                ApplyTiffCompression(image, adv, level);
                image.Format = MagickFormat.Tiff;
                return MagickFormat.Tiff;
            }

            case "image/avif":
            {
                int q = adv.QualityOverride ?? AvifQuality[level];
                image.Quality = (uint)Math.Clamp(q, 1, 95);
                image.Format = MagickFormat.Avif;
                return MagickFormat.Avif;
            }

            default:
            {
                // Unknown image - write as-is with minimal quality reduction.
                image.Quality = (uint)JpegQuality[level];
                return image.Format;
            }
        }
    }

    private static void ApplyTiffCompression(MagickImage image, AdvancedCompressionSettings adv, int level)
    {
        switch (adv.TiffCompression.ToUpperInvariant())
        {
            case "JPEG":
                image.Settings.SetDefine(MagickFormat.Tiff, "compression", "JPEG");
                int q = adv.QualityOverride ?? JpegQuality[level];
                image.Quality = (uint)Math.Clamp(q, 1, 95);
                break;
            case "ZIP":
                image.Settings.SetDefine(MagickFormat.Tiff, "compression", "Zip");
                break;
            case "NONE":
                image.Settings.SetDefine(MagickFormat.Tiff, "compression", "None");
                break;
            default: // LZW
                image.Settings.SetDefine(MagickFormat.Tiff, "compression", "LZW");
                break;
        }
    }

    // ── GIF (MagickImageCollection - must not use MagickImage for GIF) ───────

    private static void CompressGif(string inputPath, string outputPath, CompressionSettings settings)
    {
        var adv   = settings.Advanced;
        bool hasResize = settings.ResizeWidth.HasValue || settings.ResizeHeight.HasValue;

        using var collection = new MagickImageCollection(inputPath);

        if (hasResize)
        {
            // Coalesce must happen before resize: delta frames need full-frame context.
            collection.Coalesce();
            foreach (var frame in collection)
            {
                if (adv.StripMetadata) frame.Strip();
                var geo = BuildGeometry(settings.ResizeWidth, settings.ResizeHeight);
                frame.Resize(geo);
            }
        }
        else
        {
            foreach (var frame in collection)
            {
                if (adv.StripMetadata) frame.Strip();
            }
        }

        if (adv.OptimizeGifFrames)
        {
            collection.OptimizePlus();
            collection.OptimizeTransparency();
        }

        // Write atomically.
        WriteAtomicCollection(collection, MagickFormat.Gif, outputPath);
    }

    // ── Resize ───────────────────────────────────────────────────────────────

    private static void ApplyResize(MagickImage image, CompressionSettings settings)
    {
        bool hasW = settings.ResizeWidth.HasValue && settings.ResizeWidth.Value > 0;
        bool hasH = settings.ResizeHeight.HasValue && settings.ResizeHeight.Value > 0;
        if (!hasW && !hasH) return;

        var geo = BuildGeometry(settings.ResizeWidth, settings.ResizeHeight);
        image.Resize(geo);
    }

    private static MagickGeometry BuildGeometry(uint? width, uint? height)
    {
        bool hasW = width.HasValue && width.Value > 0;
        bool hasH = height.HasValue && height.Value > 0;

        if (hasW && hasH)
        {
            // Exact stretch - both provided.
            return new MagickGeometry(width!.Value, height!.Value) { IgnoreAspectRatio = true };
        }
        if (hasW)
        {
            // Proportional by width - use geometry string form; zero uint is unreliable.
            return new MagickGeometry($"{width!.Value}x");
        }
        // Proportional by height.
        return new MagickGeometry($"x{height!.Value}");
    }

    // ── Atomic output ────────────────────────────────────────────────────────

    private static void WriteAtomic(MagickImage image, MagickFormat format, string outputPath)
    {
        var tmpPath = outputPath + ".tmp";
        image.Write(tmpPath, format);

        if (File.Exists(outputPath))
            File.Replace(tmpPath, outputPath, null);
        else
            File.Move(tmpPath, outputPath);
    }

    private static void WriteAtomicCollection(MagickImageCollection collection, MagickFormat format, string outputPath)
    {
        var tmpPath = outputPath + ".tmp";
        collection.Write(tmpPath, format);

        if (File.Exists(outputPath))
            File.Replace(tmpPath, outputPath, null);
        else
            File.Move(tmpPath, outputPath);
    }
}
