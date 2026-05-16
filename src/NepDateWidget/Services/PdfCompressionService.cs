using NepDateWidget.Models;
using QPdfNet;
using System.IO;

namespace NepDateWidget.Services;

public sealed class PdfCompressionService : IPdfCompressionService
{
    public CompressionResult Compress(string inputPath, string outputPath, CompressionSettings settings)
    {
        long originalSize = 0;
        try
        {
            originalSize = new FileInfo(inputPath).Length;

            var adv     = settings.Advanced;
            var tmpPath = outputPath + ".tmp";

            // Build QPDF job - use only the stable documented API surface.
            // ObjectStreams / RecompressFlate / OptimizeImages are CLI flags that
            // map to QPdfNet methods we cannot guarantee exist in this version,
            // so we keep the job minimal: copy + optional linearize.
            var job = new Job();
            job.InputFile(inputPath).OutputFile(tmpPath);

            if (adv.LinearizePdf)
                job.Linearize();

            var exitCode = job.Run(out _);

            // Non-zero exit means QPDF signalled an error.
            if (exitCode != 0)
                throw new InvalidOperationException($"QPDF exited with code {(int)exitCode}.");

            if (File.Exists(outputPath))
                File.Replace(tmpPath, outputPath, null);
            else
                File.Move(tmpPath, outputPath);

            long compressedSize = new FileInfo(outputPath).Length;
            return new CompressionResult
            {
                InputPath           = inputPath,
                OutputPath          = outputPath,
                OriginalSizeBytes   = originalSize,
                CompressedSizeBytes = compressedSize,
                Success             = true,
            };
        }
        catch (DllNotFoundException)
        {
            TryCleanTmp(outputPath);
            return new CompressionResult
            {
                InputPath           = inputPath,
                OutputPath          = outputPath,
                OriginalSizeBytes   = originalSize,
                CompressedSizeBytes = 0,
                Success             = false,
                ErrorMessage        = "PDF compression requires the Microsoft Visual C++ 2022 Runtime. " +
                                      "Please install it from microsoft.com and restart the app.",
            };
        }
        catch (Exception ex)
        {
            TryCleanTmp(outputPath);
            return new CompressionResult
            {
                InputPath           = inputPath,
                OutputPath          = outputPath,
                OriginalSizeBytes   = originalSize,
                CompressedSizeBytes = 0,
                Success             = false,
                ErrorMessage        = ex.Message,
            };
        }
    }

    private static void TryCleanTmp(string outputPath)
    {
        var tmp = outputPath + ".tmp";
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
    }
}

