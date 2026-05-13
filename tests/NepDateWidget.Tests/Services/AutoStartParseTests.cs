using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Tests for <see cref="AutoStartService.ParseStoredExePath"/>.
///
/// The original implementation used <c>stored.Split(' ')[0].Trim('"')</c>,
/// which silently broke for any path containing a space (e.g. anything under
/// <c>C:\Program Files</c>). The replacement parses the first quoted segment
/// when present, otherwise falls back to the first space-delimited token.
/// </summary>
public class AutoStartParseTests
{
    [Fact]
    public void ParseStoredExePath_Empty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, AutoStartService.ParseStoredExePath(string.Empty));
    }

    [Fact]
    public void ParseStoredExePath_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, AutoStartService.ParseStoredExePath("   "));
    }

    [Fact]
    public void ParseStoredExePath_UnquotedSimplePath_ReturnsItVerbatim()
    {
        Assert.Equal(
            @"C:\Tools\NepDateWidget\NepDateWidget.exe",
            AutoStartService.ParseStoredExePath(@"C:\Tools\NepDateWidget\NepDateWidget.exe"));
    }

    [Fact]
    public void ParseStoredExePath_QuotedPathWithSpaces_ReturnsPathWithoutQuotes()
    {
        Assert.Equal(
            @"C:\Program Files\NepDate Widget\NepDateWidget.exe",
            AutoStartService.ParseStoredExePath(
                "\"C:\\Program Files\\NepDate Widget\\NepDateWidget.exe\""));
    }

    [Fact]
    public void ParseStoredExePath_QuotedPathWithSpaces_AndArgs_ReturnsOnlyPath()
    {
        Assert.Equal(
            @"C:\Program Files\NepDateWidget\NepDateWidget.exe",
            AutoStartService.ParseStoredExePath(
                "\"C:\\Program Files\\NepDateWidget\\NepDateWidget.exe\" --silent --updated"));
    }

    [Fact]
    public void ParseStoredExePath_UnquotedPath_WithTrailingArgs_ReturnsPathOnly()
    {
        // Legal only when the path itself has no spaces.
        Assert.Equal(
            @"C:\Tools\app.exe",
            AutoStartService.ParseStoredExePath(@"C:\Tools\app.exe --updated"));
    }

    [Fact]
    public void ParseStoredExePath_UnclosedQuote_ReturnsRestOfString()
    {
        // Defensive: malformed registry value should not crash.
        Assert.Equal(
            @"C:\foo bar\app.exe",
            AutoStartService.ParseStoredExePath("\"C:\\foo bar\\app.exe"));
    }

    [Fact]
    public void ParseStoredExePath_LeadingWhitespace_IsTrimmed()
    {
        Assert.Equal(
            @"C:\Program Files\app.exe",
            AutoStartService.ParseStoredExePath("   \"C:\\Program Files\\app.exe\""));
    }

    [Fact]
    public void ParseStoredExePath_MatchesActualLocalAppDataInstallLocation()
    {
        // Unpackaged (dev/portable) builds write the full EXE path (quoted) to the
        // HKCU Run registry key. Verify that a quoted path round-trips correctly.
        var input = "\"C:\\Users\\example\\AppData\\Local\\NepDateWidget\\AppData\\NepDateWidget.exe\"";
        var expected = "C:\\Users\\example\\AppData\\Local\\NepDateWidget\\AppData\\NepDateWidget.exe";
        Assert.Equal(expected, AutoStartService.ParseStoredExePath(input));
    }
}
