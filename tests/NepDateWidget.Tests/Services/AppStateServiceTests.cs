using NepDateWidget.Models;
using NepDateWidget.Services;
using System.IO;
using System.Text.Json;

namespace NepDateWidget.Tests.Services;

public sealed class AppStateServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string TempPath()
    {
        var p = Path.Combine(Path.GetTempPath(), $"NepDateWidget_AppState_{Guid.NewGuid():N}.json");
        _tempFiles.Add(p);
        _tempFiles.Add(p + ".tmp");
        _tempFiles.Add(p + ".bak");
        return p;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            try { File.Delete(f); } catch { }
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullPath_Throws()
        => Assert.Throws<ArgumentException>(() => new AppStateService(null!));

    [Fact]
    public void Constructor_EmptyPath_Throws()
        => Assert.Throws<ArgumentException>(() => new AppStateService(""));

    [Fact]
    public void Constructor_WhitespacePath_Throws()
        => Assert.Throws<ArgumentException>(() => new AppStateService("   "));

    // ── Load: file absent ─────────────────────────────────────────────────────

    [Fact]
    public void Load_FileAbsent_CreatesFile()
    {
        var path = TempPath();
        var svc  = new AppStateService(path);
        svc.Load();
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Load_FileAbsent_CreatesValidJson()
    {
        var path = TempPath();
        var svc  = new AppStateService(path);
        svc.Load();
        var json  = File.ReadAllText(path);
        var state = JsonSerializer.Deserialize<AppState>(json);
        Assert.NotNull(state);
    }

    [Fact]
    public void Load_FileAbsent_CurrentHasDefaultValues()
    {
        var svc = new AppStateService(TempPath());
        svc.Load();
        Assert.Null(svc.Current.LastUpdateCheckUtc);
        Assert.Equal(string.Empty, svc.Current.LastDailyEventsNotificationDate);
    }

    // ── Load: file present ────────────────────────────────────────────────────

    [Fact]
    public void Load_FilePresent_RestoresLastUpdateCheck()
    {
        var path     = TempPath();
        var expected = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        File.WriteAllText(path, JsonSerializer.Serialize(new AppState { LastUpdateCheckUtc = expected }));

        var svc = new AppStateService(path);
        svc.Load();
        Assert.Equal(expected, svc.Current.LastUpdateCheckUtc);
    }

    [Fact]
    public void Load_FilePresent_RestoresLastDailyEventsDate()
    {
        var path = TempPath();
        File.WriteAllText(path, """{"LastDailyEventsNotificationDate":"2026-05-11"}""");

        var svc = new AppStateService(path);
        svc.Load();
        Assert.Equal("2026-05-11", svc.Current.LastDailyEventsNotificationDate);
    }

    // ── Load: corrupted / edge-case JSON ─────────────────────────────────────

    [Fact]
    public void Load_CorruptedJson_DoesNotThrow_ReturnsDefaults()
    {
        var path = TempPath();
        File.WriteAllText(path, "not valid json {{{");

        var svc = new AppStateService(path);
        var ex  = Record.Exception(() => svc.Load());
        Assert.Null(ex);
        Assert.Null(svc.Current.LastUpdateCheckUtc);
    }

    [Fact]
    public void Load_EmptyFile_DoesNotThrow_ReturnsDefaults()
    {
        var path = TempPath();
        File.WriteAllText(path, "");

        var svc = new AppStateService(path);
        var ex  = Record.Exception(() => svc.Load());
        Assert.Null(ex);
        Assert.NotNull(svc.Current);
    }

    [Fact]
    public void Load_NullJson_DoesNotThrow_ReturnsDefaults()
    {
        var path = TempPath();
        File.WriteAllText(path, "null");

        var svc = new AppStateService(path);
        var ex  = Record.Exception(() => svc.Load());
        Assert.Null(ex);
        Assert.NotNull(svc.Current);
    }

    [Fact]
    public void Load_UnknownFields_AreIgnored_DoesNotThrow()
    {
        var path = TempPath();
        File.WriteAllText(path, """{"LastUpdateCheckUtc":null,"FutureField":42,"AnotherNew":"x"}""");

        var svc = new AppStateService(path);
        var ex  = Record.Exception(() => svc.Load());
        Assert.Null(ex);
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void SaveThenLoad_RoundTrips_LastUpdateCheckUtc()
    {
        var path = TempPath();
        var ts   = new DateTime(2026, 5, 11, 0, 0, 0, DateTimeKind.Utc);

        var svc = new AppStateService(path);
        svc.Load();
        svc.Current.LastUpdateCheckUtc = ts;
        svc.Save();

        var svc2 = new AppStateService(path);
        svc2.Load();
        Assert.Equal(ts, svc2.Current.LastUpdateCheckUtc);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_LastDailyEventsDate()
    {
        var path = TempPath();

        var svc = new AppStateService(path);
        svc.Load();
        svc.Current.LastDailyEventsNotificationDate = "2026-05-11";
        svc.Save();

        var svc2 = new AppStateService(path);
        svc2.Load();
        Assert.Equal("2026-05-11", svc2.Current.LastDailyEventsNotificationDate);
    }

    // ── Atomic write ──────────────────────────────────────────────────────────

    [Fact]
    public void Save_LeavesNoTmpFile()
    {
        var path = TempPath();
        var svc  = new AppStateService(path);
        svc.Load();
        svc.Save(); // second write triggers File.Replace path
        Assert.False(File.Exists(path + ".tmp"), ".tmp must be cleaned up");
    }

    [Fact]
    public void Save_LeavesNoBakFile()
    {
        var path = TempPath();
        var svc  = new AppStateService(path);
        svc.Load();
        svc.Save(); // creates, then overwrites atomically
        Assert.False(File.Exists(path + ".bak"), ".bak must be cleaned up");
    }
}
