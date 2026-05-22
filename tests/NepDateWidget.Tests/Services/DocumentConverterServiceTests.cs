using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Tests for DocumentConverterService.
/// Assert values are derived from spec and domain knowledge, not from reading implementation.
/// </summary>
public sealed class DocumentConverterServiceTests : IDisposable
{
    private readonly string _tempDir;

    public DocumentConverterServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NepDateTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string Temp(string name) => Path.Combine(_tempDir, name);

    // ── IsLegacyNepaliFont ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Preeti",          true)]
    [InlineData("preeti",          true)]
    [InlineData("PREETI",          true)]
    [InlineData("Kantipur",        true)]
    [InlineData("Himalaya",        true)]
    [InlineData("Times Himalaya",  true)]  // substring match
    [InlineData("Sagarmatha",      true)]
    [InlineData("Sabdatara",       true)]
    [InlineData("Nagarjuna",       true)]
    [InlineData("Shangrila",       true)]
    [InlineData("Pcs",             true)]
    [InlineData("pcs",             true)]
    public void IsLegacyNepaliFont_KnownLegacyFonts_ReturnsTrue(string fontName, bool expected)
    {
        Assert.Equal(expected, DocumentConverterService.IsLegacyNepaliFont(fontName));
    }

    [Theory]
    [InlineData("Arial",           false)]
    [InlineData("Times New Roman", false)]
    [InlineData("Calibri",         false)]
    [InlineData("Mangal",          false)]   // Unicode Devanagari - not legacy
    [InlineData("Noto Sans",       false)]
    public void IsLegacyNepaliFont_NonLegacyFonts_ReturnsFalse(string fontName, bool expected)
    {
        Assert.Equal(expected, DocumentConverterService.IsLegacyNepaliFont(fontName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsLegacyNepaliFont_NullOrWhitespace_ReturnsFalse(string? fontName)
    {
        Assert.False(DocumentConverterService.IsLegacyNepaliFont(fontName));
    }

    // ── BuildOutputPath ───────────────────────────────────────────────────────

    [Fact]
    public void BuildOutputPath_DocxFile_InsertsConvertedBeforeExtension()
    {
        // Rule: same dir, same stem + "_converted" + same extension
        var input = Path.Combine(_tempDir, "letter.docx");
        var result = DocumentConverterService.BuildOutputPath(input);
        Assert.Equal(Path.Combine(_tempDir, "letter_converted.docx"), result);
    }

    [Fact]
    public void BuildOutputPath_TxtFile_AppendsSuffix()
    {
        var input = Path.Combine(_tempDir, "document.txt");
        var result = DocumentConverterService.BuildOutputPath(input);
        Assert.Equal(Path.Combine(_tempDir, "document_converted.txt"), result);
    }

    [Fact]
    public void BuildOutputPath_PreservesDirectory()
    {
        var input = Path.Combine(_tempDir, "sub", "file.docx");
        var result = DocumentConverterService.BuildOutputPath(input);
        Assert.StartsWith(Path.Combine(_tempDir, "sub"), result);
    }

    [Fact]
    public void BuildOutputPath_PreservesExtension()
    {
        var input = Path.Combine(_tempDir, "report.txt");
        var result = DocumentConverterService.BuildOutputPath(input);
        Assert.EndsWith(".txt", result);
    }

    [Fact]
    public void BuildOutputPath_DoesNotDuplicateStem()
    {
        var input = Path.Combine(_tempDir, "contract.docx");
        var result = DocumentConverterService.BuildOutputPath(input);
        Assert.Equal("contract_converted.docx", Path.GetFileName(result));
    }

    // ── Convert routing ───────────────────────────────────────────────────────

    [Fact]
    public void Convert_DocExtension_ThrowsNotSupported()
    {
        var input = Temp("old.doc");
        File.WriteAllText(input, "content");
        var ex = Assert.Throws<NotSupportedException>(() =>
            DocumentConverterService.Convert(input, Temp("out.doc"), (text, _) => text));
        Assert.Contains(".doc", ex.Message);
    }

    [Fact]
    public void Convert_UnknownExtension_ThrowsNotSupported()
    {
        var input = Temp("data.xyz");
        File.WriteAllText(input, "content");
        Assert.Throws<NotSupportedException>(() =>
            DocumentConverterService.Convert(input, Temp("out.xyz"), (text, _) => text));
    }

    // ── .txt conversion ───────────────────────────────────────────────────────

    [Fact]
    public void Convert_Txt_TransformsContent()
    {
        var input = Temp("in.txt");
        var output = Temp("out.txt");
        File.WriteAllText(input, "hello", System.Text.Encoding.UTF8);

        DocumentConverterService.Convert(input, output, (text, _) => text.ToUpperInvariant());

        Assert.Equal("HELLO", File.ReadAllText(output));
    }

    [Fact]
    public void Convert_Txt_OutputIsUtf8WithoutBom()
    {
        var input = Temp("in.txt");
        var output = Temp("out.txt");
        File.WriteAllText(input, "test");

        DocumentConverterService.Convert(input, output, (t, _) => t);

        var bytes = File.ReadAllBytes(output);
        // UTF-8 BOM is EF BB BF - output must NOT start with it
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "Output should not contain a UTF-8 BOM");
    }

    [Fact]
    public void Convert_Txt_PreservesUnicode()
    {
        var input = Temp("unicode.txt");
        var output = Temp("unicode_out.txt");
        const string text = "नेपाल is \u00e9l\u00e9gant"; // Devanagari + accented chars
        File.WriteAllText(input, text, System.Text.Encoding.UTF8);

        DocumentConverterService.Convert(input, output, (t, _) => t);

        Assert.Equal(text, File.ReadAllText(output, System.Text.Encoding.UTF8));
    }

    [Fact]
    public void Convert_Txt_EmptyFile_ProducesEmptyOutput()
    {
        var input = Temp("empty.txt");
        var output = Temp("empty_out.txt");
        File.WriteAllText(input, string.Empty);

        DocumentConverterService.Convert(input, output, (t, _) => t + "suffix");

        Assert.Equal("suffix", File.ReadAllText(output));
    }

    [Fact]
    public void Convert_Txt_TransformReceivesNullFontName()
    {
        // .txt conversion: font is always null - caller should convert unconditionally
        var input = Temp("in.txt");
        var output = Temp("out.txt");
        File.WriteAllText(input, "abc");

        string? capturedFont = "notset";
        DocumentConverterService.Convert(input, output, (t, font) => { capturedFont = font; return t; });

        Assert.Null(capturedFont);
    }

    [Fact]
    public void Convert_Txt_OriginalFileNotModified()
    {
        var input = Temp("original.txt");
        var output = Temp("result.txt");
        const string original = "original content";
        File.WriteAllText(input, original);

        DocumentConverterService.Convert(input, output, (t, _) => "changed");

        Assert.Equal(original, File.ReadAllText(input));
    }
}
