using ImageMagick;
using PdfSharp.Pdf;
using System.IO;
using System.Linq;
using System.Text;

namespace NepDateWidget.Tests.Helpers;

/// <summary>
/// Programmatically builds minimal PDF files for compression tests.
/// All PDFs are syntactically valid and processable by QPdfNet (QPDF).
/// </summary>
internal static class PdfFixtures
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

    /// <summary>
    /// A PDF whose content stream is large (≈16 KB), uncompressed, and highly compressible.
    /// QPdfNet CompressStreams + ObjectStreams will reduce it dramatically.
    /// </summary>
    public static string WriteUncompressedPdf(string path, int contentLines = 4000)
    {
        // "q Q\n" × N — valid, balanced graphics-state operators, highly repetitive ASCII.
        var contentBytes = Latin1.GetBytes(string.Concat(Enumerable.Repeat("q Q\n", contentLines)));
        File.WriteAllBytes(path, BuildRawPdf(contentBytes));
        return path;
    }

    /// <summary>
    /// A PDF with a JPEG image XObject encoded at the given quality.
    /// A 200×200 solid-colour JPEG is generated via ImageMagick.
    /// </summary>
    public static string WritePdfWithJpeg(string path, int quality, int size = 400)
    {
        byte[] jpeg = MakeJpeg(quality, size);
        var contentBytes = Latin1.GetBytes("/Im1 Do\n");
        File.WriteAllBytes(path, BuildRawPdf(contentBytes, jpegBytes: jpeg, jpegWidth: size, jpegHeight: size));
        return path;
    }

    /// <summary>
    /// A PDF with an image XObject labelled /DCTDecode but containing garbage bytes.
    /// Used to verify that a malformed stream is skipped without aborting the document.
    /// </summary>
    public static string WritePdfWithMalformedDctStream(string path, int declaredSize = 50)
    {
        byte[] garbage = Latin1.GetBytes("NOT VALID JPEG GARBAGE DATA THIS WILL FAIL DECODE");
        var contentBytes = Latin1.GetBytes("/Im1 Do\n");
        File.WriteAllBytes(path, BuildRawPdf(contentBytes, jpegBytes: garbage,
            jpegWidth: declaredSize, jpegHeight: declaredSize));
        return path;
    }

    /// <summary>
    /// A PDF encrypted with an owner password and an empty user password.
    /// QPDF treats an empty user password as open access and can process the file (Success=true).
    /// PDFsharp may or may not open the file depending on its encrypted-PDF handling; either way
    /// the overall result is Success=true — the test asserts the service handles it gracefully.
    /// </summary>
    public static string WriteEncryptedPdf(string path)
    {
        using var doc = new PdfDocument();
        doc.AddPage();
        // Setting OwnerPassword automatically enables 128-bit RC4 encryption.
        // UserPassword defaults to "" — QPDF opens empty-user-password PDFs without a key.
        doc.SecuritySettings.OwnerPassword = "test-owner";
        doc.Save(path);
        return path;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static byte[] MakeJpeg(int quality, int size)
    {
        // plasma: generates a colorful fractal pseudo-image with high spatial-frequency content.
        // This ensures quality 95 produces a meaningfully larger JPEG than quality 30,
        // making the Phase 2B size-reduction assertion reliable.
        var settings = new MagickReadSettings { Width = (uint)size, Height = (uint)size };
        using var img = new MagickImage("plasma:", settings);
        img.Format = MagickFormat.Jpeg;
        img.Quality = (uint)Math.Clamp(quality, 1, 95);
        return img.ToByteArray();
    }

    /// <summary>
    /// Writes a minimal but syntactically valid PDF-1.4 document.
    /// When jpegBytes is provided, object 5 is a DCTDecode image XObject
    /// and the page resources reference it as /Im1.
    /// </summary>
    private static byte[] BuildRawPdf(
        byte[] contentBytes,
        byte[]? jpegBytes = null,
        int jpegWidth = 0,
        int jpegHeight = 0)
    {
        var ms = new MemoryStream();
        var offsets = new List<long>(); // byte offset of each numbered object (1-based)

        void WriteStr(string s) => ms.Write(Latin1.GetBytes(s));
        void WriteRaw(byte[] b) => ms.Write(b);

        // Binary-content marker — signals to tools that this is not pure ASCII.
        WriteStr("%PDF-1.4\n");
        WriteStr("%\x80\x81\x82\x83\n");

        // Object 1: Catalog
        offsets.Add(ms.Position);
        WriteStr("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        // Object 2: Pages
        offsets.Add(ms.Position);
        WriteStr("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        // Object 3: Page
        offsets.Add(ms.Position);
        bool hasImage = jpegBytes != null;
        string resources = hasImage ? "<< /XObject << /Im1 5 0 R >> >>" : "<< >>";
        WriteStr($"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]"
               + $" /Contents 4 0 R /Resources {resources} >>\nendobj\n");

        // Object 4: Content stream (uncompressed)
        offsets.Add(ms.Position);
        WriteStr($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        WriteRaw(contentBytes);
        WriteStr("\nendstream\nendobj\n");

        // Object 5: Image XObject (optional)
        if (hasImage)
        {
            offsets.Add(ms.Position);
            WriteStr($"5 0 obj\n<< /Type /XObject /Subtype /Image"
                   + $" /Width {jpegWidth} /Height {jpegHeight}"
                   + $" /ColorSpace /DeviceRGB /BitsPerComponent 8"
                   + $" /Filter /DCTDecode /Length {jpegBytes!.Length} >>\nstream\n");
            WriteRaw(jpegBytes!);
            WriteStr("\nendstream\nendobj\n");
        }

        // Cross-reference table.
        // Each entry is exactly 20 bytes: "nnnnnnnnnn ggggg X \n" (space before LF).
        int objCount = offsets.Count + 1; // +1 for free object 0
        long xrefPos = ms.Position;
        WriteStr($"xref\n0 {objCount}\n");
        WriteStr("0000000000 65535 f \n"); // free entry for object 0
        foreach (long off in offsets)
            WriteStr($"{off:D10} 00000 n \n");

        WriteStr($"trailer\n<< /Size {objCount} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");

        return ms.ToArray();
    }
}
