using ImageMagick;
using NepDateWidget.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using QPdfNet;
using QPdfNet.Enums;
using System.IO;

namespace NepDateWidget.Services;

public sealed class PdfCompressionService : IPdfCompressionService
{
    public CompressionResult Compress(string inputPath, string outputPath, CompressionSettings settings)
    {
        long originalSize = 0;
        string p2bTmpPath = outputPath + ".p2b.tmp";
        string qpdfTmpPath = outputPath + ".tmp";
        try
        {
            originalSize = new FileInfo(inputPath).Length;

            int level = Math.Clamp(settings.CompressionLevel, 0, 4);
            var adv = settings.Advanced;

            // Phase 2B: DCTDecode JPEG re-encoding via PDFsharp.
            // Produces a pre-processed input for QPdfNet.
            // Falls back to original input if the PDF is encrypted or Phase 2B fails.
            string qpdfInputPath = inputPath;
            bool p2bProduced = TryRunPhase2B(inputPath, p2bTmpPath, level);
            if (p2bProduced)
            {
                qpdfInputPath = p2bTmpPath;
            }

            // Phase 2A: structural compression via QPdfNet.
            bool phase2aOk = TryRunPhase2A(qpdfInputPath, qpdfTmpPath, level, adv, out string? phase2aError);

            if (!phase2aOk)
            {
                // Fallback: minimal pipeline - copy + optional linearize only.
                RunPhase1(qpdfInputPath, qpdfTmpPath, adv);
            }

            WriteAtomicFromTmp(qpdfTmpPath, outputPath);

            long compressedSize = new FileInfo(outputPath).Length;
            return new CompressionResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                OriginalSizeBytes = originalSize,
                CompressedSizeBytes = compressedSize,
                Success = true,
                ErrorMessage = phase2aOk ? null : $"Full compression unavailable; applied basic optimization. ({phase2aError})",
            };
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            TryClean(p2bTmpPath, qpdfTmpPath);
            return new CompressionResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                OriginalSizeBytes = originalSize,
                CompressedSizeBytes = 0,
                Success = false,
                ErrorMessage = "PDF compression requires the Microsoft Visual C++ 2022 Runtime. " +
                               "Please install it from microsoft.com and restart the app.",
            };
        }
        catch (Exception ex)
        {
            TryClean(p2bTmpPath, qpdfTmpPath);
            return new CompressionResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                OriginalSizeBytes = originalSize,
                CompressedSizeBytes = 0,
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
        finally
        {
            TryClean(p2bTmpPath);
        }
    }

    // ── Phase 2B: DCTDecode JPEG re-encoding (PDFsharp) ─────────────────────

    /// <summary>
    /// Traverses the PDF object graph, finds embedded JPEG (DCTDecode) images, and
    /// re-encodes them at a lower quality if doing so yields a smaller stream.
    /// Returns true if the output file was written and should be used as QPdfNet input.
    /// Returns false (and does not write p2bTmpPath) on encrypted PDFs or any fatal error.
    /// </summary>
    private static bool TryRunPhase2B(string inputPath, string p2bTmpPath, int level)
    {
        try
        {
            using PdfDocument doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify);
            int quality = CompressionProfiles.JpegQuality[level];
            int minArea = CompressionProfiles.PdfOiMinArea[level];

            foreach (PdfPage page in doc.Pages)
            {
                var xObjectsDict = page.Resources?.Elements.GetDictionary("/XObject");
                if (xObjectsDict == null)
                {
                    continue;
                }

                foreach (var key in xObjectsDict.Elements.Keys)
                {
                    try
                    {
                        var item = xObjectsDict.Elements[key];
                        var xObj = item is null ? null : ResolveDict(item);
                        if (xObj == null)
                        {
                            continue;
                        }

                        // Must be an image XObject with a single DCTDecode filter.
                        var subtype = xObj.Elements.GetName("/Subtype");
                        if (subtype != "/Image")
                        {
                            continue;
                        }

                        // Only process single-name /Filter /DCTDecode.
                        // If /Filter is an array (multiple filters), skip.
                        var filterElement = xObj.Elements["/Filter"];
                        if (filterElement is not PdfName filterName || filterName.Value != "/DCTDecode")
                        {
                            continue;
                        }

                        // Area threshold: skip small images.
                        int w = xObj.Elements.GetInteger("/Width");
                        int h = xObj.Elements.GetInteger("/Height");
                        if (minArea > 0 && w * h < minArea)
                        {
                            continue;
                        }

                        if (xObj.Stream == null)
                        {
                            continue;
                        }

                        byte[] original = xObj.Stream.Value;
                        if (original.Length == 0)
                        {
                            continue;
                        }

                        byte[] reencoded = ReencodeJpeg(original, quality);
                        if (reencoded.Length >= original.Length)
                        {
                            continue; // no gain
                        }

                        xObj.Stream.Value = reencoded;
                        xObj.Elements.SetInteger("/Length", reencoded.Length);
                    }
                    catch
                    {
                        // One bad stream must not abort the document.
                    }
                }
            }

            doc.Save(p2bTmpPath);
            return true;
        }
        catch (PdfReaderException)
        {
            // Encrypted PDF - skip Phase 2B, run Phase 2A from original input.
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static PdfDictionary? ResolveDict(PdfItem item)
    {
        if (item is PdfDictionary d)
        {
            return d;
        }

        if (item is PdfReference r && r.Value is PdfDictionary rd)
        {
            return rd;
        }

        return null;
    }

    private static byte[] ReencodeJpeg(byte[] jpegBytes, int quality)
    {
        using var image = new MagickImage(jpegBytes);
        image.Format = MagickFormat.Jpeg;
        image.Quality = (uint)Math.Clamp(quality, 1, 95);
        return image.ToByteArray();
    }

    // ── Phase 2A: structural compression (QPdfNet) ───────────────────────────

    private static bool TryRunPhase2A(
        string inputPath,
        string tmpPath,
        int level,
        AdvancedCompressionSettings adv,
        out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            var job = new Job();
            job.InputFile(inputPath).OutputFile(tmpPath);

            // Unconditional structural methods.
            job.ObjectStreams(ObjectStreams.Generate);
            job.CompressStreams(true);
            job.RecompressFlate();

            // Slider-driven compression level (1-9 range; CompressionProfiles maps 0-4 to 9-3).
            job.CompressionLevel(CompressionProfiles.PdfCompressionLevel[level]);

            // Image optimization: convert non-JPEG inline images to DCT JPEG.
            job.OptimizeImages();
            int minArea = CompressionProfiles.PdfOiMinArea[level];
            if (minArea > 0)
            {
                job.OiMinArea(minArea);
            }

            if (adv.LinearizePdf)
            {
                job.Linearize();
            }

            var exitCode = job.Run(out _);
            if (exitCode != ExitCode.Success && exitCode != ExitCode.WarningsWereFoundFileProcessed)
            {
                errorMessage = $"QPDF exited with code {(int)exitCode}.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    // ── Phase 1 fallback: copy + optional linearize only ────────────────────

    private static void RunPhase1(string inputPath, string tmpPath, AdvancedCompressionSettings adv)
    {
        var job = new Job();
        job.InputFile(inputPath).OutputFile(tmpPath);

        if (adv.LinearizePdf)
        {
            job.Linearize();
        }

        var exitCode = job.Run(out _);
        if (exitCode != ExitCode.Success && exitCode != ExitCode.WarningsWereFoundFileProcessed)
        {
            throw new InvalidOperationException($"QPDF fallback exited with code {(int)exitCode}.");
        }
    }

    // ── Output helpers ───────────────────────────────────────────────────────

    private static void WriteAtomicFromTmp(string tmpPath, string outputPath)
    {
        if (File.Exists(outputPath))
        {
            File.Replace(tmpPath, outputPath, null);
        }
        else
        {
            File.Move(tmpPath, outputPath);
        }
    }

    private static void TryClean(params string[] paths)
    {
        foreach (var p in paths)
        {
            try { if (File.Exists(p)) { File.Delete(p); } } catch { }
        }
    }
}

