using System.IO;
using System.Text.Json;
using NepDateWidget.Models;
using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Unit tests for SettingsService.
/// Each test gets its own temp file path so tests are fully isolated.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string TempPath()
    {
        var p = Path.Combine(Path.GetTempPath(), $"nwtest_{Guid.NewGuid():N}.json");
        _tempFiles.Add(p);
        _tempFiles.Add(p + ".tmp");
        _tempFiles.Add(p + ".bak");
        return p;
    }

    private static SettingsService ServiceAt(string path) => new(path);

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    // ── Missing file ──────────────────────────────────────────────────────────

    [Fact]
    public void Load_FileDoesNotExist_ReturnsDefaults()
    {
        var svc = ServiceAt(TempPath());
        svc.Load();

        Assert.NotNull(svc.Current);
        Assert.Equal("en",    svc.Current.Language);
        Assert.Equal("Light", svc.Current.Theme);
        Assert.False(svc.Current.IsExpanded);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void SaveThenLoad_RoundTrips_AllValues()
    {
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load();

        svc.Current.Language    = "ne";
        svc.Current.Theme       = "Light";
        svc.Current.AlwaysOnTop = true;
        svc.Current.WindowLeft  = 250;
        svc.Current.WindowTop   = 180;
        svc.Current.IsExpanded  = true;
        svc.Current.HighlightedDays.Add("2082-01-15");

        svc.Save();

        var svc2 = ServiceAt(path);
        svc2.Load();

        Assert.Equal("ne",         svc2.Current.Language);
        Assert.Equal("Light",      svc2.Current.Theme);
        Assert.True(svc2.Current.AlwaysOnTop);
        Assert.Equal(250,          svc2.Current.WindowLeft);
        Assert.Equal(180,          svc2.Current.WindowTop);
        Assert.True(svc2.Current.IsExpanded);
        Assert.Contains("2082-01-15", svc2.Current.HighlightedDays);
    }

    // ── Corrupted JSON ────────────────────────────────────────────────────────

    [Fact]
    public void Load_CorruptedJson_DoesNotThrow_ReturnsDefaults()
    {
        var path = TempPath();
        File.WriteAllText(path, "{ this is not valid json {{ }}");

        var svc = ServiceAt(path);
        var ex  = Record.Exception(() => svc.Load());

        Assert.Null(ex);
        Assert.Equal("en", svc.Current.Language);
    }

    [Fact]
    public void Load_EmptyFile_DoesNotThrow_ReturnsDefaults()
    {
        var path = TempPath();
        File.WriteAllText(path, "");

        var svc = ServiceAt(path);
        var ex  = Record.Exception(() => svc.Load());

        Assert.Null(ex);
        Assert.NotNull(svc.Current);
    }

    [Fact]
    public void Load_NullJson_DoesNotThrow_ReturnsDefaults()
    {
        var path = TempPath();
        File.WriteAllText(path, "null");

        var svc = ServiceAt(path);
        var ex  = Record.Exception(() => svc.Load());

        Assert.Null(ex);
        Assert.NotNull(svc.Current);
    }

    // ── Unknown fields (forward-compatibility) ────────────────────────────────

    [Fact]
    public void Load_JsonWithUnknownFields_DoesNotThrow_PreservesKnownFields()
    {
        var path = TempPath();
        var json = """
            {
                "SchemaVersion": 1,
                "Language": "ne",
                "UnknownFutureField": "some value",
                "Theme": "Light",
                "AnotherUnknown": 42
            }
            """;
        File.WriteAllText(path, json);

        var svc = ServiceAt(path);
        var ex  = Record.Exception(() => svc.Load());

        Assert.Null(ex);
        Assert.Equal("ne",    svc.Current.Language);
        Assert.Equal("Light", svc.Current.Theme);
    }

    // ── Validation / clamping on load ─────────────────────────────────────────

    [Fact]
    public void Load_InvalidLanguage_FallsBackToDefault()
    {
        var path = TempPath();
        File.WriteAllText(path, """{"Language":"klingon"}""");

        var svc = ServiceAt(path);
        svc.Load();

        Assert.Equal("en", svc.Current.Language);
    }

    [Fact]
    public void Load_InvalidTheme_FallsBackToDefault()
    {
        var path = TempPath();
        File.WriteAllText(path, """{"Theme":"Neon"}""");

        var svc = ServiceAt(path);
        svc.Load();

        Assert.Equal("Light", svc.Current.Theme);
    }

    [Fact]
    public void Load_NullHighlightedDays_ReplacedWithEmptyList()
    {
        var path = TempPath();
        File.WriteAllText(path, """{"HighlightedDays":null}""");

        var svc = ServiceAt(path);
        svc.Load();

        Assert.NotNull(svc.Current.HighlightedDays);
    }

    // ── Atomic write ──────────────────────────────────────────────────────────

    [Fact]
    public void Save_WritesFile_WhenItDidNotExist()
    {
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load(); // no file → defaults

        svc.Save();

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_LeavesNoTmpFile_AfterSuccessfulWrite()
    {
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load();
        svc.Save();

        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Save_LeavesNoBakFile_AfterSuccessfulOverwrite()
    {
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load();
        svc.Save();    // creates the file
        svc.Save();    // overwrites atomically

        Assert.False(File.Exists(path + ".bak"));
    }

    // ── ResetToDefaults ───────────────────────────────────────────────────────

    [Fact]
    public void ResetToDefaults_ResetsInMemoryAndSaves()
    {
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load();

        svc.Current.Language = "ne";
        svc.Current.Theme    = "Dark";

        svc.ResetToDefaults();

        Assert.Equal("en",    svc.Current.Language);
        Assert.Equal("Light", svc.Current.Theme);
        Assert.True(File.Exists(path));
    }
}
