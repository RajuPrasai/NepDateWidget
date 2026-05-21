using ImageMagick;
using NepDateWidget.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.IO;
using System.Windows.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinPdf = Windows.Data.Pdf;

namespace NepDateWidget.Services;

public sealed class PdfTranscodeService : IPdfTranscodeService
{
    // Render width in pixels per quality level (0=smallest … 4=best).
    // Maps to roughly 72/100/144/216/288 DPI for a standard A4 page.
    private static readonly uint[] RenderWidths = [600u, 900u, 1200u, 1800u, 2400u];

    // Output format quality per level — mirrors ImageConversionService tables.
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
        try
        {
            int level = Math.Clamp(qualityLevel, 0, 4);
            uint renderWidth = RenderWidths[level];
            string ext = targetExt.TrimStart('.').ToLowerInvariant();

            // Load PDF via Windows.Data.Pdf (built into Windows 10, no Ghostscript needed).
            var file = StorageFile.GetFileFromPathAsync(inputPath).AsTask().GetAwaiter().GetResult();
            var pdfDoc = WinPdf.PdfDocument.LoadFromFileAsync(file).AsTask().GetAwaiter().GetResult();

            if (pdfDoc.IsPasswordProtected)
                return new ImageConversionResult(false, "PDF is password-protected and cannot be converted.");

            uint pageCount = pdfDoc.PageCount;
            if (pageCount == 0)
                return new ImageConversionResult(false, "PDF contains no pages.");

            var outputPaths = new List<string>();

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
                    // Render every page into a MagickImage, then stack vertically.
                    using var collection = new MagickImageCollection();
                    for (uint p = 0; p < pageCount; p++)
                    {
                        var pageBytes = RenderPageToBytes(pdfDoc, p, renderWidth);
                        collection.Add(new MagickImage(pageBytes));
                    }
                    using var combined = collection.AppendVertically();
                    ApplyFormatSettings((IMagickImage<ushort>)combined, ext, level);
                    combined.Write(outputBasePath);
                    outputPaths.Add(outputBasePath);
                    break;
                }
            }

            return new ImageConversionResult(true, null, outputPaths);
        }
        catch (Exception ex)
        {
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

    /// <summary>
    /// Adds one page to an open PdfDocument by decoding the source image via MagickImage
    /// (supports all formats including HEIC, WebP, AVIF, RAW, etc.) and embedding it
    /// as a lossless PNG stream so PDFsharp's XImage can consume it.
    /// Page dimensions are derived from the image's native pixel size at 96 DPI.
    /// </summary>
    private static void AddImagePageToPdf(PdfDocument doc, string imagePath)
    {
        // MagickImage handles any format Magick.NET supports — far broader than PDFsharp.
        using var magick = new MagickImage(imagePath);

        // Animated formats: use first frame only.
        if (magick.BaseWidth == 0)
            throw new InvalidOperationException($"Could not read image dimensions from '{Path.GetFileName(imagePath)}'.");

        var pngBytes = magick.ToByteArray(MagickFormat.Png);

        // Decode via WPF to get DPI-aware BitmapFrame.
        BitmapFrame frame;
        using (var ms = new MemoryStream(pngBytes))
            frame = BitmapFrame.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        double dpiX = frame.DpiX > 0 ? frame.DpiX : 96.0;
        double dpiY = frame.DpiY > 0 ? frame.DpiY : 96.0;

        var page = doc.AddPage();
        // XUnit.FromPoint: 1 point = 1/72 inch.  pixels / dpi * 72 = points.
        page.Width  = XUnit.FromPoint(frame.PixelWidth  * 72.0 / dpiX);
        page.Height = XUnit.FromPoint(frame.PixelHeight * 72.0 / dpiY);

        using var gfx = XGraphics.FromPdfPage(page);

        // PDFsharp 6 XImage.FromStream takes Stream directly.
        var captured = pngBytes;
        using var imgStream = new MemoryStream(captured);
        var xImage = XImage.FromStream(imgStream);
        gfx.DrawImage(xImage, 0, 0, page.Width.Point, page.Height.Point);
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
                image.Quality = (uint)WebpQuality[level]; // reuse webp table — same perceptual range
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
