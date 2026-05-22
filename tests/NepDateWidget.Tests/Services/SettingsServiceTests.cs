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

    private static SettingsService ServiceAt(string path) => new(path, TestPaths.DefaultSettingsPath);

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            if (File.Exists(f))
            {
                File.Delete(f);
            }
        }
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
        svc.Current.WindowLeft  = 250;
        svc.Current.WindowTop   = 180;
        svc.Current.IsExpanded  = true;

        svc.Save();

        var svc2 = ServiceAt(path);
        svc2.Load();

        Assert.Equal("ne",         svc2.Current.Language);
        Assert.Equal("Light",      svc2.Current.Theme);
        Assert.Equal(250,          svc2.Current.WindowLeft);
        Assert.Equal(180,          svc2.Current.WindowTop);
        Assert.True(svc2.Current.IsExpanded);
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
    public void ResetToDefaults_PreservesAllLayoutFields()
    {
        // All 8 positional / window-state fields must be preserved verbatim.
        // Only user-visible preferences should revert to defaults.
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load();

        svc.Current.WindowLeft         = 450;
        svc.Current.WindowTop          = 310;
        svc.Current.ExpandedWindowLeft = 120.5;
        svc.Current.ExpandedWindowTop  = 230.0;
        svc.Current.ExpandedWidth      = 960;
        svc.Current.ExpandedHeight     = 820;
        svc.Current.IsExpanded         = true;
        svc.Current.LastExpandedTab    = 3;

        svc.ResetToDefaults();

        Assert.Equal(450,   svc.Current.WindowLeft);
        Assert.Equal(310,   svc.Current.WindowTop);
        Assert.Equal(120.5, svc.Current.ExpandedWindowLeft);
        Assert.Equal(230.0, svc.Current.ExpandedWindowTop);
        Assert.Equal(960,   svc.Current.ExpandedWidth);
        Assert.Equal(820,   svc.Current.ExpandedHeight);
        Assert.True(svc.Current.IsExpanded);
        Assert.Equal(3,     svc.Current.LastExpandedTab);
    }

    [Fact]
    public void ResetToDefaults_ExpandedWindowPosition_NullPreservedWhenNeverSet()
    {
        // ExpandedWindowLeft/Top start null (window never moved). Reset must keep null,
        // not replace with 0 or a default double value.
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load();

        Assert.Null(svc.Current.ExpandedWindowLeft);
        Assert.Null(svc.Current.ExpandedWindowTop);

        svc.Current.Language = "ne";  // mutate something else
        svc.ResetToDefaults();

        Assert.Null(svc.Current.ExpandedWindowLeft);
        Assert.Null(svc.Current.ExpandedWindowTop);
    }

    [Fact]
    public void ResetToDefaults_ResetsAllUserPreferences()
    {
        // Mutate every user-visible preference away from its default,
        // then reset and assert each field returns to the factory default.
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load();

        svc.Current.Language                    = "ne";
        svc.Current.Theme                       = "Dark";
        svc.Current.BackgroundPreset            = "Ocean";
        svc.Current.CornerStyle                 = "Sharp";
        svc.Current.FontFamily                  = "Cascadia Code";
        svc.Current.AutoStart                   = false;
        svc.Current.AnimationEnabled            = false;
        svc.Current.TransparentWhenCollapsed    = false;
        svc.Current.ShowEnglishDayNumbers       = false;
        svc.Current.HighlightSaturdays          = false;
        svc.Current.HighlightSundays            = false;
        svc.Current.HighlightColor              = "#E53935";
        svc.Current.ShowTithi                   = false;
        svc.Current.ShowEvents                  = false;
        svc.Current.HighlightPublicHolidays     = false;
        svc.Current.ShowTimezone                = false;
        svc.Current.ClockFormat                 = "24h";
        svc.Current.ShowOffset                  = true;
        svc.Current.ShowDayOfWeek               = false;
        svc.Current.ShowEnglishDate             = false;
        svc.Current.ShowHolidayCountdown        = false;
        svc.Current.ShowDailyEventsNotification = false;
        svc.Current.LogMaxSizeMb                = 5;
        svc.Current.NotificationDurationSeconds = 60;
        svc.Current.NotificationSound           = false;
        svc.Current.ShowSecondsInClock          = true;
        svc.Current.ShowFiscalYear              = false;
        svc.Current.ShowHelpBadges              = false;

        svc.ResetToDefaults();

        Assert.Equal("en",        svc.Current.Language);
        Assert.Equal("Light",     svc.Current.Theme);
        Assert.Equal("Default",   svc.Current.BackgroundPreset);
        Assert.Equal("Rounded",   svc.Current.CornerStyle);
        Assert.Equal("Open Sans", svc.Current.FontFamily);
        Assert.True(svc.Current.AutoStart);
        Assert.True(svc.Current.AnimationEnabled);
        Assert.True(svc.Current.TransparentWhenCollapsed);
        Assert.True(svc.Current.ShowEnglishDayNumbers);
        Assert.True(svc.Current.HighlightSaturdays);
        Assert.True(svc.Current.HighlightSundays);
        Assert.Equal("#F4511E",   svc.Current.HighlightColor);
        Assert.True(svc.Current.ShowTithi);
        Assert.True(svc.Current.ShowEvents);
        Assert.True(svc.Current.HighlightPublicHolidays);
        Assert.True(svc.Current.ShowTimezone);
        Assert.Equal("12h",       svc.Current.ClockFormat);
        Assert.False(svc.Current.ShowOffset);
        Assert.True(svc.Current.ShowDayOfWeek);
        Assert.True(svc.Current.ShowEnglishDate);
        Assert.True(svc.Current.ShowHolidayCountdown);
        Assert.True(svc.Current.ShowDailyEventsNotification);
        Assert.Equal(10,          svc.Current.LogMaxSizeMb);
        Assert.Equal(10,          svc.Current.NotificationDurationSeconds);
        Assert.True(svc.Current.NotificationSound);
        Assert.False(svc.Current.ShowSecondsInClock);
        Assert.True(svc.Current.ShowFiscalYear);
        Assert.True(svc.Current.ShowHelpBadges);
    }

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

    [Fact]
    public void ResetToDefaults_PreservesWindowPosition()
    {
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load();

        svc.Current.WindowLeft = 400;
        svc.Current.WindowTop  = 300;

        svc.ResetToDefaults();

        Assert.Equal(400, svc.Current.WindowLeft);
        Assert.Equal(300, svc.Current.WindowTop);
    }

    [Fact]
    public void ResetToDefaults_SecondReset_StillRestoresDefaults()
    {
        // Regression: the first reset must not alias _cachedDefaults. If it does,
        // subsequent Apply()-style mutations to _current also corrupt _cachedDefaults,
        // and the second reset restores those corrupted values instead of the real defaults.
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load();

        svc.ResetToDefaults();

        // Simulate what Apply() does after a reset: writes VM state back to _current.
        svc.Current.Theme    = "Dark";
        svc.Current.Language = "ne";

        svc.ResetToDefaults();

        Assert.Equal("en",    svc.Current.Language);
        Assert.Equal("Light", svc.Current.Theme);
    }

    // ── SchemaVersion default (#25) ───────────────────────────────────────────

    [Fact]
    public void WidgetSettings_DefaultSchemaVersion_MatchesCurrentSchemaVersion()
    {
        // Ensures the model default is always in sync with the validator constant.
        var s = new WidgetSettings();
        Assert.Equal(SettingsValidator.CurrentSchemaVersion, s.SchemaVersion);
    }

    // ── V1 → V2 migration: stale fields silently dropped (#25) ───────────────

    [Fact]
    public void Load_V1JsonWithDayNotes_DoesNotThrow_AndPreservesSchemaVersion()
    {
        // DayNotes was removed in V2 (moved to notes.json). UnmappedMemberHandling.Skip
        // must silently discard it. SchemaVersion is preserved exactly as stored (1 < 2
        // does not trigger re-clamping because the clamp only fires when < 1).
        var path = TempPath();
        var json = """
            {
                "SchemaVersion": 1,
                "Language": "ne",
                "Theme": "Light",
                "DayNotes": { "2082-01-01": "test note" }
            }
            """;
        File.WriteAllText(path, json);

        var svc = ServiceAt(path);
        var ex  = Record.Exception(() => svc.Load());

        Assert.Null(ex);
        Assert.Equal("ne",    svc.Current.Language);
        Assert.Equal("Light", svc.Current.Theme);
        Assert.Equal(1,       svc.Current.SchemaVersion);
    }

    [Fact]
    public void Load_UnknownFields_DoNotThrow_AndAreIgnored()
    {
        // UnmappedMemberHandling.Skip must silently discard unknown fields.
        var path = TempPath();
        var json = """
            {
                "SchemaVersion": 1,
                "Language": "en",
                "SomeRemovedField": "2026-01-01T00:00:00Z"
            }
            """;
        File.WriteAllText(path, json);

        var svc = ServiceAt(path);
        var ex  = Record.Exception(() => svc.Load());

        Assert.Null(ex);
        Assert.Equal("en", svc.Current.Language);
    }

    [Fact]
    public void Load_V1Json_StaleFields_DoNotThrow()
    {
        var path = TempPath();
        var json = """
            {
                "SchemaVersion": 1,
                "Language": "ne",
                "DayNotes": {}
            }
            """;
        File.WriteAllText(path, json);

        var svc = ServiceAt(path);
        var ex  = Record.Exception(() => svc.Load());

        Assert.Null(ex);
        Assert.Equal("ne", svc.Current.Language);
        Assert.Equal(1,    svc.Current.SchemaVersion);
    }

    [Fact]
    public void Load_SchemaBelowOne_ClampsToCurrentVersion()
    {
        // SchemaVersion < 1 means the file is from an ancient unknown build.
        // Migrate() must clamp it up to CurrentSchemaVersion.
        var path = TempPath();
        File.WriteAllText(path, """{"SchemaVersion":0,"Language":"en"}""");

        var svc = ServiceAt(path);
        svc.Load();

        Assert.Equal(SettingsValidator.CurrentSchemaVersion, svc.Current.SchemaVersion);
    }

    // ── First-launch behavior ─────────────────────────────────────────────────

    [Fact]
    public void Load_FileAbsent_SetsIsFirstLaunch()
    {
        var svc = ServiceAt(TempPath());
        svc.Load();
        Assert.True(svc.IsFirstLaunch);
    }

    [Fact]
    public void Load_FileAbsent_CreatesFileOnDisk()
    {
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load();
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Load_FilePresent_IsFirstLaunchFalse()
    {
        var path = TempPath();
        var svc  = ServiceAt(path);
        svc.Load(); // creates the file (first launch)

        var svc2 = ServiceAt(path);
        svc2.Load(); // file exists now
        Assert.False(svc2.IsFirstLaunch);
    }
}
