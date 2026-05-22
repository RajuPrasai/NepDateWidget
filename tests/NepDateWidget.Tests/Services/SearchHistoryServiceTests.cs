using System.IO;
using System.Text.Json;
using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

public sealed class SearchHistoryServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string TempPath()
    {
        var p = Path.Combine(Path.GetTempPath(), $"nhtest_{Guid.NewGuid():N}.json");
        _tempFiles.Add(p);
        _tempFiles.Add(p + ".tmp");
        _tempFiles.Add(p + ".bak");
        return p;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    // ── Load ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_FileAbsent_HistoryIsEmpty()
    {
        var svc = new SearchHistoryService(TempPath());
        svc.Load();
        Assert.Empty(svc.GetMatching("", int.MaxValue));
    }

    [Fact]
    public void Load_FilePresent_RestoresEntries()
    {
        var path = TempPath();
        File.WriteAllText(path, JsonSerializer.Serialize(new[] { "notepad", "calc" }));
        var svc = new SearchHistoryService(path);
        svc.Load();
        var all = svc.GetMatching("", int.MaxValue);
        Assert.Equal(2, all.Count);
        Assert.Equal("notepad", all[0]);
        Assert.Equal("calc", all[1]);
    }

    [Fact]
    public void Load_MalformedJson_HistoryIsEmpty()
    {
        var path = TempPath();
        File.WriteAllText(path, "not valid json {{{");
        var svc = new SearchHistoryService(path);
        svc.Load();
        Assert.Empty(svc.GetMatching("", int.MaxValue));
    }

    // ── Record ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Record_AddsToFront()
    {
        var svc = new SearchHistoryService(TempPath());
        svc.Record("a");
        svc.Record("b");
        var all = svc.GetMatching("", int.MaxValue);
        Assert.Equal("b", all[0]);
        Assert.Equal("a", all[1]);
    }

    [Fact]
    public void Record_DuplicateMoved_ToFront()
    {
        var svc = new SearchHistoryService(TempPath());
        svc.Record("a");
        svc.Record("b");
        svc.Record("a");
        var all = svc.GetMatching("", int.MaxValue);
        Assert.Equal("a", all[0]);
        Assert.Equal("b", all[1]);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Record_ExceedsMaxEntries_OldestDropped()
    {
        var svc = new SearchHistoryService(TempPath(), maxEntries: 3);
        svc.Record("a");
        svc.Record("b");
        svc.Record("c");
        svc.Record("d");
        var all = svc.GetMatching("", int.MaxValue);
        Assert.Equal(3, all.Count);
        Assert.DoesNotContain("a", all);
    }

    [Fact]
    public void Record_EmptyOrWhitespace_Ignored()
    {
        var svc = new SearchHistoryService(TempPath());
        svc.Record("");
        svc.Record("   ");
        Assert.Empty(svc.GetMatching("", int.MaxValue));
    }

    // ── Remove ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ExistingEntry_RemovesIt()
    {
        var svc = new SearchHistoryService(TempPath());
        svc.Record("a");
        svc.Record("b");
        svc.Remove("a");
        var all = svc.GetMatching("", int.MaxValue);
        Assert.Single(all);
        Assert.Equal("b", all[0]);
    }

    [Fact]
    public void Remove_NonExistentEntry_NoOp()
    {
        var svc = new SearchHistoryService(TempPath());
        svc.Record("a");
        svc.Remove("z");
        Assert.Single(svc.GetMatching("", int.MaxValue));
    }

    // ── GetMatching ────────────────────────────────────────────────────────────

    [Fact]
    public void GetMatching_EmptyPrefix_ReturnsAll()
    {
        var svc = new SearchHistoryService(TempPath());
        svc.Record("notepad");
        svc.Record("calc");
        Assert.Equal(2, svc.GetMatching("", int.MaxValue).Count);
    }

    [Fact]
    public void GetMatching_Prefix_FiltersCorrectly()
    {
        var svc = new SearchHistoryService(TempPath());
        svc.Record("notepad");
        svc.Record("note++");
        svc.Record("calc");
        var matches = svc.GetMatching("note", int.MaxValue);
        Assert.Equal(2, matches.Count);
        Assert.DoesNotContain("calc", matches);
    }

    [Fact]
    public void GetMatching_RespectsMaxCount()
    {
        var svc = new SearchHistoryService(TempPath());
        for (var i = 0; i < 10; i++)
        {
            svc.Record($"item{i}");
        }

        Assert.Equal(3, svc.GetMatching("", 3).Count);
    }

    // ── Default seeding ────────────────────────────────────────────────────────

    [Fact]
    public void LoadDefaultEntries_RunHistoryResource_ReturnsNonEmptyList()
    {
        var entries = SearchHistoryService.LoadDefaultEntries(TestPaths.DefaultRunHistoryPath);
        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e)));
    }

    [Fact]
    public void Load_FileAbsentWithDefaultResource_SeedsFromFile()
    {
        var svc = new SearchHistoryService(TempPath(), maxEntries: 500, defaultFilePath: TestPaths.DefaultRunHistoryPath);
        svc.Load();
        var all = svc.GetMatching("", int.MaxValue);
        Assert.NotEmpty(all);
        Assert.Contains("notepad", all, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_FileAbsentWithDefaultResource_HelpIsFirstEntry()
    {
        // "help" must be the first item in run-history.json defaults so new users
        // see it at the top of the run box suggestion list before any commands are typed.
        var svc = new SearchHistoryService(TempPath(), maxEntries: 500, defaultFilePath: TestPaths.DefaultRunHistoryPath);
        svc.Load();
        var all = svc.GetMatching("", int.MaxValue);
        Assert.NotEmpty(all);
        Assert.Equal("help", all[0], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_FilePresent_MergesHelp_ForExistingUsers()
    {
        // Existing users whose run-history.json pre-dates the "help" entry get it
        // appended via MergeNewDefaults on next launch.
        var path = TempPath();
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new[] { "notepad", "calc" }));
        var svc = new SearchHistoryService(path, maxEntries: 500, defaultFilePath: TestPaths.DefaultRunHistoryPath);
        svc.Load();
        var all = svc.GetMatching("", int.MaxValue);
        Assert.Contains("help", all, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_FileAbsentNoDefaultResource_HistoryStaysEmpty()
    {
        var svc = new SearchHistoryService(TempPath(), maxEntries: 500, defaultFilePath: null);
        svc.Load();
        Assert.Empty(svc.GetMatching("", int.MaxValue));
    }

    [Fact]
    public void Load_FileAbsentWithDefaultResource_PersistsToDisk()
    {
        var path = TempPath();
        var svc = new SearchHistoryService(path, maxEntries: 500, defaultFilePath: TestPaths.DefaultRunHistoryPath);
        svc.Load();
        // Seeding now persists immediately so the file exists after Load().
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LoadDefaultEntries_NonexistentFile_ReturnsEmptyList()
    {
        var entries = SearchHistoryService.LoadDefaultEntries(Path.Combine(Path.GetTempPath(), "nonexistent_nhtest_file.json"));
        Assert.Empty(entries);
    }

    [Fact]
    public void Load_FilePresent_WithDefaultFilePath_MergesNewDefaults()
    {
        // When the file already exists the service merges any default entries not already present.
        var path = TempPath();
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new[] { "mspaint" }));
        var svc = new SearchHistoryService(path, maxEntries: 500, defaultFilePath: TestPaths.DefaultRunHistoryPath);
        svc.Load();
        var all = svc.GetMatching("", int.MaxValue);
        // mspaint (user entry) is preserved at index 0.
        Assert.Equal("mspaint", all[0]);
        // notepad comes from defaults and should have been merged.
        Assert.Contains("notepad", all, StringComparer.OrdinalIgnoreCase);
        Assert.True(all.Count > 1);
    }

    [Fact]
    public void Load_FileAbsentWithDefaultResource_SeedRespects_MaxEntries()
    {
        // When maxEntries is smaller than the resource list, only up to maxEntries items
        // should be seeded in-memory.
        var svc = new SearchHistoryService(TempPath(), maxEntries: 5, defaultFilePath: TestPaths.DefaultRunHistoryPath);
        svc.Load();
        var all = svc.GetMatching("", int.MaxValue);
        Assert.Equal(5, all.Count);
    }

    [Fact]
    public void LoadDefaultEntries_RunHistoryResource_AllEntriesNonBlank()
    {
        var entries = SearchHistoryService.LoadDefaultEntries(TestPaths.DefaultRunHistoryPath);
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e)));
    }

    [Fact]
    public void LoadDefaultEntries_RunHistoryResource_ContainsExpectedSeeds()
    {
        var entries = SearchHistoryService.LoadDefaultEntries(TestPaths.DefaultRunHistoryPath);
        // Spot-check a representative sample from the default JSON
        Assert.Contains("notepad",    entries, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("calc",       entries, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("powershell", entries, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("taskmgr",    entries, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mstsc",      entries, StringComparer.OrdinalIgnoreCase);
    }

    // ── Case-insensitive matching ───────────────────────────────────────────────

    [Fact]
    public void GetMatching_CaseInsensitivePrefix_MatchesEntry()
    {
        var svc = new SearchHistoryService(TempPath());
        svc.Record("notepad");
        svc.Record("Notepad++");
        var matches = svc.GetMatching("NOTE", int.MaxValue);
        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Remove_CaseInsensitive_RemovesEntry()
    {
        var svc = new SearchHistoryService(TempPath());
        svc.Record("notepad");
        svc.Remove("NOTEPAD");
        Assert.Empty(svc.GetMatching("", int.MaxValue));
    }

    [Fact]
    public void Record_CaseInsensitive_Deduplicates()
    {
        // Recording "Notepad" after "notepad" must deduplicate (case-insensitive).
        var svc = new SearchHistoryService(TempPath());
        svc.Record("notepad");
        svc.Record("Notepad");
        var all = svc.GetMatching("", int.MaxValue);
        Assert.Single(all);
        Assert.Equal("Notepad", all[0]); // last-recorded casing wins
    }
}
