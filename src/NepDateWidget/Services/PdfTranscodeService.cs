using ImageMagick;
using NepDateWidget.Models;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.IO;
using Windows.Storage;
using Windows.Storage.Streams;
using WinPdf = Windows.Data.Pdf;

namespace NepDateWidget.Services;

public sealed class PdfTranscodeService : IPdfTranscodeService
{
    // Render width in pixels per quality level (0=smallest … 4=best).
    // Maps to roughly 72/100/144/216/288 DPI for a standard A4 page.
    private static readonly uint[] RenderWidths = [600u, 900u, 1200u, 1800u, 2400u];

    // Output format quality per level - mirrors ImageConversionService tables.
    private static readonly int[] JpegQuality = [30, 50, 70, 85, 95];
    private static readonly int[] WebpQuality = [25, 45, 65, 80, 90];
    private static readonly uint[] PngCompression = [9u, 7u, 6u, 4u, 1u];

    // ── PdfToImage ────────────────────────────────────────────────────────────

    public ImageConversionResult PdfToImage(
        string inputPath,
        string outputBasePath,
        string targetExt,
        int qualityLevel,
        PdfConvertPageMode pageMode)
    {
        var outputPaths = new List<string>();
        try
        {
            int level = Math.Clamp(qualityLevel, 0, 4);
            uint renderWidth = RenderWidths[level];
            string ext = targetExt.TrimStart('.').ToLowerInvariant();

            var file = StorageFile.GetFileFromPathAsync(inputPath).AsTask().GetAwaiter().GetResult();
            var pdfDoc = WinPdf.PdfDocument.LoadFromFileAsync(file).AsTask().GetAwaiter().GetResult();

            if (pdfDoc.IsPasswordProtected)
                return new ImageConversionResult(false, "PDF is password-protected and cannot be converted.");

            uint pageCount = pdfDoc.PageCount;
            if (pageCount == 0)
                return new ImageConversionResult(false, "PDF contains no pages.");

            switch (pageMode)
            {
                case PdfConvertPageMode.FirstPageOnly:
                {
                    RenderPageToFile(pdfDoc, 0, renderWidth, outputBasePath, ext, level);
                    outputPaths.Add(outputBasePath);
                    break;
                }

                case PdfConvertPageMode.AllPagesPerFile:
                {
                    string dir = Path.GetDirectoryName(outputBasePath) ?? string.Empty;
                    string baseName = Path.GetFileNameWithoutExtension(outputBasePath);
                    int digits = pageCount.ToString().Length;

                    for (uint p = 0; p < pageCount; p++)
                    {
                        string pagePath = Path.Combine(
                            dir,
                            $"{baseName}_p{(p + 1).ToString().PadLeft(digits, '0')}.{ext}");
                        RenderPageToFile(pdfDoc, p, renderWidth, pagePath, ext, level);
                        outputPaths.Add(pagePath);
                    }
                    break;
                }

                case PdfConvertPageMode.AllPagesCombined:
                {
                    using var collection = new MagickImageCollection();
                    for (uint p = 0; p < pageCount; p++)
                    {
                        var pageBytes = RenderPageToBytes(pdfDoc, p, renderWidth);
                        collection.Add(new MagickImage(pageBytes));
                    }
                    // Q8 AppendVertically() returns IMagickImage<byte>; the concrete type is MagickImage.
                    using var combined = (MagickImage)collection.AppendVertically();
                    ApplyFormatSettings(combined, ext, level);
                    combined.Write(outputBasePath);
                    outputPaths.Add(outputBasePath);
                    break;
                }
            }

            return new ImageConversionResult(true, null, outputPaths);
        }
        catch (Exception ex)
        {
            // Clean up any partially written files so stale outputs are not left on disk.
            foreach (var path in outputPaths)
                TryDelete(path);
            return new ImageConversionResult(false, ex.Message);
        }
    }

    // ── ImageToPdf ────────────────────────────────────────────────────────────

    public ImageConversionResult ImageToPdf(string inputPath, string outputPath)
    {
        try
        {
            using var doc = new PdfDocument();
            AddImagePageToPdf(doc, inputPath);
            doc.Save(outputPath);
            return new ImageConversionResult(true, null, new[] { outputPath });
        }
        catch (Exception ex)
        {
            TryDelete(outputPath);
            return new ImageConversionResult(false, ex.Message);
        }
    }

    // ── ImagesToPdf ───────────────────────────────────────────────────────────

    public ImageConversionResult ImagesToPdf(IReadOnlyList<string> inputPaths, string outputPath)
    {
        try
        {
            using var doc = new PdfDocument();
            foreach (var imagePath in inputPaths)
                AddImagePageToPdf(doc, imagePath);
            doc.Save(outputPath);
            return new ImageConversionResult(true, null, new[] { outputPath });
        }
        catch (Exception ex)
        {
            TryDelete(outputPath);
            return new ImageConversionResult(false, ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Adds one page to an open PdfDocument. Page is always A4 portrait; the image
    // is scaled to fit (preserving aspect ratio) and centered, leaving white margins.
    private static void AddImagePageToPdf(PdfDocument doc, string imagePath)
    {
        using var magick = new MagickImage(imagePath);

        if (magick.BaseWidth == 0)
            throw new InvalidOperationException($"Could not read image dimensions from '{Path.GetFileName(imagePath)}'.");

        var pngBytes = magick.ToByteArray(MagickFormat.Png);

        var page = doc.AddPage();
        page.Size        = PageSize.A4;
        page.Orientation = PageOrientation.Portrait;

        double pageW = page.Width.Point;
        double pageH = page.Height.Point;
        double imgW  = (double)magick.Width;
        double imgH  = (double)magick.Height;

        double scale = Math.Min(pageW / imgW, pageH / imgH);
        double drawW = imgW * scale;
        double drawH = imgH * scale;
        double drawX = (pageW - drawW) / 2.0;
        double drawY = (pageH - drawH) / 2.0;

        using var imgStream = new MemoryStream(pngBytes);
        using var xImage = XImage.FromStream(imgStream);
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(xImage, drawX, drawY, drawW, drawH);
    }

    private static void RenderPageToFile(
        WinPdf.PdfDocument pdfDoc, uint pageIndex, uint renderWidth,
        string outputPath, string ext, int level)
    {
        var bytes = RenderPageToBytes(pdfDoc, pageIndex, renderWidth);
        using var magick = new MagickImage(bytes);
        ApplyFormatSettings(magick, ext, level);
        magick.Write(outputPath);
    }

    private static byte[] RenderPageToBytes(WinPdf.PdfDocument pdfDoc, uint pageIndex, uint renderWidth)
    {
        using var page = pdfDoc.GetPage(pageIndex);
        using var stream = new InMemoryRandomAccessStream();
        var options = new WinPdf.PdfPageRenderOptions { DestinationWidth = renderWidth };
        page.RenderToStreamAsync(stream, options).AsTask().GetAwaiter().GetResult();
        stream.Seek(0);
        using var dotNetStream = stream.AsStreamForRead();
        using var ms = new MemoryStream();
        dotNetStream.CopyTo(ms);
        return ms.ToArray();
    }

    private static void ApplyFormatSettings(IMagickImage<ushort> image, string ext, int level)
    {
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
                image.Quality = (uint)WebpQuality[level]; // reuse webp table - same perceptual range
                break;
            case "png":
                image.Quality = PngCompression[level];
                break;
            // bmp, tif, tga: no quality setting needed
        }
    }

    // Overload for Q8 MagickImage (byte channel) which does not share the Q16 interface.
    private static void ApplyFormatSettings(MagickImage image, string ext, int level)
    {
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
                image.Quality = (uint)WebpQuality[level];
                break;
            case "png":
                image.Quality = PngCompression[level];
                break;
        }
    }

    private static MagickFormat GetFormat(string ext) => ext switch
    {
        "jpg" or "jpeg" => MagickFormat.Jpeg,
        "png"           => MagickFormat.Png,
        "webp"          => MagickFormat.WebP,
        "avif"          => MagickFormat.Avif,
        "bmp"           => MagickFormat.Bmp,
        "tif" or "tiff" => MagickFormat.Tiff,
        "tga"           => MagickFormat.Tga,
        _               => MagickFormat.Png,
    };

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
