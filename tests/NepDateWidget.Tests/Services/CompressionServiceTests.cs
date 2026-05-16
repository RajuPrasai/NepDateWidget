using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.Services;

public sealed class FileTypeServiceTests
{
    private readonly FileTypeService _svc = new();

    // ── GetMimeType ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(".jpg",  "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png",  "image/png")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".gif",  "image/gif")]
    [InlineData(".tif",  "image/tiff")]
    [InlineData(".tiff", "image/tiff")]
    [InlineData(".bmp",  "image/bmp")]
    [InlineData(".heic", "image/heif")]
    [InlineData(".heif", "image/heif")]
    [InlineData(".avif", "image/avif")]
    [InlineData(".pdf",  "application/pdf")]
    public void GetMimeType_KnownExtension_ReturnsExpected(string ext, string expected)
    {
        Assert.Equal(expected, _svc.GetMimeType(ext));
    }

    [Theory]
    [InlineData(".JPG")]
    [InlineData(".PNG")]
    [InlineData(".PDF")]
    public void GetMimeType_UppercaseExtension_Works(string ext)
    {
        Assert.NotNull(_svc.GetMimeType(ext));
    }

    [Theory]
    [InlineData(".xyz")]
    [InlineData(".doc")]
    [InlineData("")]
    public void GetMimeType_UnknownOrEmpty_ReturnsNull(string ext)
    {
        Assert.Null(_svc.GetMimeType(ext));
    }

    // HEIC and HEIF from the same iPhone batch share the same MIME type
    [Fact]
    public void GetMimeType_HeicAndHeif_NormalizeToSameType()
    {
        var heic = _svc.GetMimeType(".heic");
        var heif = _svc.GetMimeType(".heif");
        Assert.Equal(heic, heif);
        Assert.NotNull(heic);
    }

    // ── GetCategory ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    [InlineData("image/tiff")]
    [InlineData("image/bmp")]
    [InlineData("image/heif")]
    [InlineData("image/avif")]
    public void GetCategory_ImageMime_ReturnsImage(string mime)
    {
        Assert.Equal(FileCategory.Image, _svc.GetCategory(mime));
    }

    [Fact]
    public void GetCategory_PdfMime_ReturnsPdf()
    {
        Assert.Equal(FileCategory.Pdf, _svc.GetCategory("application/pdf"));
    }

    [Fact]
    public void GetCategory_Unknown_ReturnsUnsupported()
    {
        Assert.Equal(FileCategory.Unsupported, _svc.GetCategory("application/octet-stream"));
    }

    // ── ValidateSameType ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateSameType_EmptyList_ReturnsNull()
    {
        Assert.Null(_svc.ValidateSameType([]));
    }

    [Fact]
    public void ValidateSameType_HomogeneousJpegs_ReturnsNull()
    {
        string[] files = ["photo1.jpg", "photo2.jpeg", "photo3.jpg"];
        Assert.Null(_svc.ValidateSameType(files));
    }

    [Fact]
    public void ValidateSameType_HeicAndHeifMixed_AcceptedAsSameType()
    {
        // Both map to image/heif - should be accepted as homogeneous
        string[] files = ["a.heic", "b.heif", "c.heic"];
        Assert.Null(_svc.ValidateSameType(files));
    }

    [Fact]
    public void ValidateSameType_JpegAndPngMixed_ReturnsError()
    {
        string[] files = ["photo.jpg", "image.png"];
        var result = _svc.ValidateSameType(files);
        Assert.NotNull(result);
        Assert.Contains("Mixed", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSameType_ImageAndPdfMixed_ReturnsError()
    {
        string[] files = ["photo.jpg", "document.pdf"];
        var result = _svc.ValidateSameType(files);
        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateSameType_UnsupportedExtension_ReturnsError()
    {
        string[] files = ["file.xyz"];
        var result = _svc.ValidateSameType(files);
        Assert.NotNull(result);
        Assert.Contains("Unsupported", result, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class GetOutputFileNameTests
{
    // ── Suffix ───────────────────────────────────────────────────────────────

    [Fact]
    public void Compression_AddsCompressedSuffix()
    {
        var result = CompressionViewModel.GetOutputFileName("photo.jpg", "image/jpeg", isResize: false);
        Assert.Contains("_Compressed", result);
    }

    [Fact]
    public void Resize_AddsResizedSuffix()
    {
        var result = CompressionViewModel.GetOutputFileName("photo.jpg", "image/jpeg", isResize: true);
        Assert.Contains("_Resized", result);
    }

    // ── Extension remapping ──────────────────────────────────────────────────

    [Fact]
    public void Bmp_RemapsToJpg()
    {
        var result = CompressionViewModel.GetOutputFileName("photo.bmp", "image/bmp", isResize: false);
        Assert.EndsWith(".jpg", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Heif_RemapsToJpg()
    {
        var result = CompressionViewModel.GetOutputFileName("photo.heic", "image/heif", isResize: false);
        Assert.EndsWith(".jpg", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Jpeg_PreservesJpegExtension()
    {
        var result = CompressionViewModel.GetOutputFileName("photo.jpeg", "image/jpeg", isResize: false);
        Assert.EndsWith(".jpeg", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Png_PreservesPngExtension()
    {
        var result = CompressionViewModel.GetOutputFileName("photo.png", "image/png", isResize: false);
        Assert.EndsWith(".png", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pdf_PreservesPdfExtension()
    {
        var result = CompressionViewModel.GetOutputFileName("doc.pdf", "application/pdf", isResize: false);
        Assert.EndsWith(".pdf", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── Directory preservation ───────────────────────────────────────────────

    [Fact]
    public void OutputFile_DirectoryMatches_InputDirectory()
    {
        const string input  = @"C:\Users\test\Pictures\photo.jpg";
        var          result = CompressionViewModel.GetOutputFileName(input, "image/jpeg", isResize: false);
        Assert.StartsWith(@"C:\Users\test\Pictures\", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── ConvertToWebP extension remapping ────────────────────────────────────

    [Fact]
    public void Jpeg_ConvertToWebP_RemapsToWebp()
    {
        var result = CompressionViewModel.GetOutputFileName("photo.jpg", "image/jpeg", isResize: false, convertToWebP: true);
        Assert.EndsWith(".webp", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Png_ConvertToWebP_RemapsToWebp()
    {
        var result = CompressionViewModel.GetOutputFileName("photo.png", "image/png", isResize: false, convertToWebP: true);
        Assert.EndsWith(".webp", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Jpeg_ConvertToWebP_False_PreservesJpegExtension()
    {
        var result = CompressionViewModel.GetOutputFileName("photo.jpg", "image/jpeg", isResize: false, convertToWebP: false);
        Assert.EndsWith(".jpg", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Png_ConvertToWebP_False_PreservesPngExtension()
    {
        var result = CompressionViewModel.GetOutputFileName("photo.png", "image/png", isResize: false, convertToWebP: false);
        Assert.EndsWith(".png", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bmp_ConvertToWebP_True_StillRemapsToJpg()
    {
        // BMP→JPG remapping takes priority in the switch; WebP flag only applies to JPEG and PNG.
        var result = CompressionViewModel.GetOutputFileName("photo.bmp", "image/bmp", isResize: false, convertToWebP: true);
        Assert.EndsWith(".jpg", result, StringComparison.OrdinalIgnoreCase);
    }
}
