using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Helpers;
using System.IO;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Integration tests for Phase 2 PDF compression (Phase 2A + Phase 2B pipeline).
/// All assertions are on observable outcomes: file sizes, success flags, output existence.
/// No mocking - the tests run the real service against real PDF files on disk.
/// </summary>
public sealed class PdfCompressionPhase2Tests
{
    private static readonly PdfCompressionService Svc = new();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string TmpPdf() =>
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".pdf");

    private static CompressionSettings Settings(int level = 2) => new()
    {
        CompressionLevel = level,
        Advanced = new AdvancedCompressionSettings { LinearizePdf = false },
    };

    private static void Cleanup(params string[] paths)
    {
        foreach (var p in paths)
            try { if (File.Exists(p)) File.Delete(p); } catch { }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A PDF with a large uncompressed content stream (~16 KB of repetitive ASCII)
    /// must be measurably smaller after QPdfNet structural compression.
    /// </summary>
    [Fact]
    public void Compress_UncompressedPdf_StructuralCompressionReducesSize()
    {
        var input = PdfFixtures.WriteUncompressedPdf(TmpPdf());
        var output = TmpPdf();
        try
        {
            var result = Svc.Compress(input, output, Settings(level: 2));

            Assert.True(result.Success);
            Assert.True(File.Exists(output));
            Assert.True(result.CompressedSizeBytes < result.OriginalSizeBytes,
                $"Expected output ({result.CompressedSizeBytes} B) to be smaller than input ({result.OriginalSizeBytes} B).");
        }
        finally { Cleanup(input, output); }
    }

    /// <summary>
    /// A PDF containing a high-quality (95) JPEG must shrink when compressed at level 0
    /// (quality 30). Phase 2B re-encodes the JPEG at lower quality before QPdfNet runs.
    /// </summary>
    [Fact]
    public void Compress_PdfWithHighQualityJpeg_Phase2BReducesFileSize()
    {
        var input = PdfFixtures.WritePdfWithJpeg(TmpPdf(), quality: 95, size: 200);
        var output = TmpPdf();
        try
        {
            var result = Svc.Compress(input, output, Settings(level: 0));

            Assert.True(result.Success);
            Assert.True(File.Exists(output));
            Assert.True(result.CompressedSizeBytes < result.OriginalSizeBytes,
                $"Expected output ({result.CompressedSizeBytes} B) to be smaller than input ({result.OriginalSizeBytes} B).");
        }
        finally { Cleanup(input, output); }
    }

    /// <summary>
    /// Compressing a PDF twice at level 0 must be idempotent.
    /// The JPEG is already at quality 30 after the first pass; re-encoding at quality 30
    /// yields no gain, so the no-gain guard skips it. The second output size must not
    /// significantly exceed the first output size (within 5% tolerance for structural noise).
    /// </summary>
    [Fact]
    public void Compress_MinimumQualityJpeg_SecondPassIsIdempotent()
    {
        var input = PdfFixtures.WritePdfWithJpeg(TmpPdf(), quality: 30, size: 200);
        var firstOut = TmpPdf();
        var secondOut = TmpPdf();
        try
        {
            var first = Svc.Compress(input, firstOut, Settings(level: 0));
            Assert.True(first.Success);

            var second = Svc.Compress(firstOut, secondOut, Settings(level: 0));
            Assert.True(second.Success);

            // Second pass must not inflate the file beyond a 5 % tolerance.
            long limit = (long)(first.CompressedSizeBytes * 1.05);
            Assert.True(second.CompressedSizeBytes <= limit,
                $"Second pass ({second.CompressedSizeBytes} B) inflated beyond 5 % tolerance of first pass ({first.CompressedSizeBytes} B).");
        }
        finally { Cleanup(input, firstOut, secondOut); }
    }

    /// <summary>
    /// An encrypted PDF (owner password only, empty user password) must produce
    /// a successful result. PDFsharp throws PdfReaderException → Phase 2B is skipped.
    /// QPdfNet processes the file with the empty user password and succeeds.
    /// </summary>
    [Fact]
    public void Compress_EncryptedPdfOwnerPasswordOnly_Succeeds()
    {
        var input = PdfFixtures.WriteEncryptedPdf(TmpPdf());
        var output = TmpPdf();
        try
        {
            var result = Svc.Compress(input, output, Settings(level: 2));

            Assert.True(result.Success, result.ErrorMessage ?? "(no error message)");
            Assert.True(File.Exists(output));
        }
        finally { Cleanup(input, output); }
    }

    /// <summary>
    /// A PDF with a DCTDecode-labelled stream containing garbage bytes must not abort
    /// the compression job. The per-stream catch in Phase 2B handles the decode failure;
    /// QPdfNet processes the PDF normally with the stream left unchanged.
    /// </summary>
    [Fact]
    public void Compress_PdfWithMalformedDctStream_DoesNotAbort()
    {
        var input = PdfFixtures.WritePdfWithMalformedDctStream(TmpPdf());
        var output = TmpPdf();
        try
        {
            var result = Svc.Compress(input, output, Settings(level: 1));

            Assert.True(result.Success, result.ErrorMessage ?? "(no error message)");
            Assert.True(File.Exists(output));
        }
        finally { Cleanup(input, output); }
    }

    /// <summary>
    /// The input file must never be modified in-place. The service must write only to
    /// the output path; the input bytes must remain identical after the call.
    /// </summary>
    [Fact]
    public void Compress_OriginalInputFile_IsNotModified()
    {
        var input = PdfFixtures.WriteUncompressedPdf(TmpPdf(), contentLines: 200);
        var output = TmpPdf();
        byte[] before = File.ReadAllBytes(input);
        try
        {
            Svc.Compress(input, output, Settings());

            Assert.Equal(before, File.ReadAllBytes(input));
        }
        finally { Cleanup(input, output); }
    }
}
