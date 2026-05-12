using NepDateWidget.Services;
using System.IO;

namespace NepDateWidget.Tests.Services;

public sealed class ShortcutsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public ShortcutsServiceTests()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), $"NepDateWidget_ShortcutsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "shortcuts.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private ShortcutsService CreateAndLoad()
    {
        var svc = new ShortcutsService(_filePath, TestPaths.DefaultShortcutsPath);
        svc.Load();
        return svc;
    }

    private void WriteJson(string json)
        => File.WriteAllText(_filePath, json);

    // ── CreateBuiltInOnly ─────────────────────────────────────────────────────

    [Fact]
    public void CreateBuiltInOnly_PrefixesNotEmpty()
    {
        var svc = ShortcutsService.CreateBuiltInOnly(TestPaths.DefaultShortcutsPath);
        Assert.NotEmpty(svc.Prefixes);
    }

    [Fact]
    public void CreateBuiltInOnly_PrefixSiteNamesNotEmpty()
    {
        var svc = ShortcutsService.CreateBuiltInOnly(TestPaths.DefaultShortcutsPath);
        Assert.NotEmpty(svc.PrefixSiteNames);
    }

    [Fact]
    public void CreateBuiltInOnly_ContainsKnownBuiltIn_Google()
    {
        var svc = ShortcutsService.CreateBuiltInOnly(TestPaths.DefaultShortcutsPath);
        Assert.True(svc.Prefixes.ContainsKey("g"), "Built-in 'g' (Google) shortcut must exist");
    }

    [Fact]
    public void CreateBuiltInOnly_AllUrlsUseFormatPlaceholder_NotQueryToken()
    {
        // {query} must be normalized to {0} so callers can use string.Format directly.
        var svc = ShortcutsService.CreateBuiltInOnly(TestPaths.DefaultShortcutsPath);
        foreach (var (key, url) in svc.Prefixes)
        {
            Assert.False(url.Contains("{query}", StringComparison.OrdinalIgnoreCase),
                $"Built-in '{key}' URL still contains {{query}} instead of {{0}}");
            Assert.Contains("{0}", url);
        }
    }

    [Fact]
    public void CreateBuiltInOnly_PrefixesAndSiteNames_SameKeySet()
    {
        var svc = ShortcutsService.CreateBuiltInOnly(TestPaths.DefaultShortcutsPath);
        Assert.Equal(svc.Prefixes.Keys.OrderBy(k => k), svc.PrefixSiteNames.Keys.OrderBy(k => k));
    }

    // ── File absent → seed from embedded ─────────────────────────────────────

    [Fact]
    public void Load_FileAbsent_SeedsFile()
    {
        using var svc = CreateAndLoad();
        Assert.True(File.Exists(_filePath));
    }

    [Fact]
    public void Load_FileAbsent_PopulatesBuiltInShortcuts()
    {
        using var svc = CreateAndLoad();
        Assert.NotEmpty(svc.Prefixes);
    }

    [Fact]
    public void Load_FileAbsent_SeedFileIsValidJson()
    {
        using var svc = CreateAndLoad();
        var content = File.ReadAllText(_filePath);
        var ex = Record.Exception(() => System.Text.Json.JsonDocument.Parse(content));
        Assert.Null(ex);
    }

    // ── Validation: invalid entries are skipped ───────────────────────────────

    [Fact]
    public void Load_EntryWithEmptyKey_IsSkipped()
    {
        WriteJson("""[{"key":"","url":"https://x.com/s?q={query}","name":"X"}]""");
        using var svc = CreateAndLoad();
        Assert.False(svc.Prefixes.ContainsKey(""));
    }

    [Fact]
    public void Load_EntryWithInvalidKeyChars_IsSkipped()
    {
        WriteJson("""[{"key":"bad key!","url":"https://x.com/s?q={query}","name":"X"}]""");
        using var svc = CreateAndLoad();
        Assert.False(svc.Prefixes.ContainsKey("bad key!"));
    }

    [Fact]
    public void Load_EntryWithNoUrl_IsSkipped()
    {
        WriteJson("""[{"key":"mykey","name":"X"}]""");
        using var svc = CreateAndLoad();
        Assert.False(svc.Prefixes.ContainsKey("mykey"));
    }

    [Fact]
    public void Load_EntryWithNoQueryPlaceholder_IsSkipped()
    {
        WriteJson("""[{"key":"ex","url":"https://x.com/","name":"X"}]""");
        using var svc = CreateAndLoad();
        Assert.False(svc.Prefixes.ContainsKey("ex"));
    }

    [Fact]
    public void Load_EntryWithTwoQueryPlaceholders_IsSkipped()
    {
        WriteJson("""[{"key":"ex","url":"https://x.com/{query}?also={query}","name":"X"}]""");
        using var svc = CreateAndLoad();
        Assert.False(svc.Prefixes.ContainsKey("ex"));
    }

    // ── Valid user entry ──────────────────────────────────────────────────────

    [Fact]
    public void Load_ValidEntry_AddedToPrefixes()
    {
        WriteJson("""[{"key":"shop","url":"https://shop.com/s?q={query}","name":"My Shop"}]""");
        using var svc = CreateAndLoad();
        Assert.True(svc.Prefixes.ContainsKey("shop"));
    }

    [Fact]
    public void Load_ValidEntry_UrlNormalized_QueryToFormatPlaceholder()
    {
        WriteJson("""[{"key":"shop","url":"https://shop.com/s?q={query}","name":"My Shop"}]""");
        using var svc = CreateAndLoad();
        Assert.Equal("https://shop.com/s?q={0}", svc.Prefixes["shop"]);
    }

    [Fact]
    public void Load_ValidEntry_NameStoredCorrectly()
    {
        WriteJson("""[{"key":"shop","url":"https://shop.com/s?q={query}","name":"My Shop"}]""");
        using var svc = CreateAndLoad();
        Assert.Equal("My Shop", svc.PrefixSiteNames["shop"]);
    }

    [Fact]
    public void Load_ValidEntry_NoName_FallsBackToKey()
    {
        WriteJson("""[{"key":"shop","url":"https://shop.com/s?q={query}"}]""");
        using var svc = CreateAndLoad();
        Assert.Equal("shop", svc.PrefixSiteNames["shop"]);
    }

    // ── Disabled entries ──────────────────────────────────────────────────────

    [Fact]
    public void Load_DisabledEntry_NotAddedToPrefixes()
    {
        WriteJson("""[{"key":"shop","disabled":true}]""");
        using var svc = CreateAndLoad();
        Assert.False(svc.Prefixes.ContainsKey("shop"));
    }

    [Fact]
    public void Load_DisabledEntry_NoPrefixSiteName()
    {
        WriteJson("""[{"key":"shop","disabled":true}]""");
        using var svc = CreateAndLoad();
        Assert.False(svc.PrefixSiteNames.ContainsKey("shop"));
    }

    // ── Corrupted JSON ────────────────────────────────────────────────────────

    [Fact]
    public void Load_CorruptedJson_DoesNotThrow()
    {
        WriteJson("this is not json {{{{");
        var svc = new ShortcutsService(_filePath);
        var ex  = Record.Exception(() => svc.Load());
        Assert.Null(ex);
    }

    [Fact]
    public void Load_CorruptedJson_PrefixesEmpty()
    {
        WriteJson("this is not json {{{{");
        using var svc = new ShortcutsService(_filePath);
        svc.Load();
        Assert.Empty(svc.Prefixes);
    }

    // ── Case-insensitive key lookup ───────────────────────────────────────────

    [Fact]
    public void Prefixes_LookupIsCaseInsensitive()
    {
        WriteJson("""[{"key":"Shop","url":"https://shop.com/s?q={query}","name":"My Shop"}]""");
        using var svc = CreateAndLoad();
        Assert.True(svc.Prefixes.ContainsKey("shop"));
        Assert.True(svc.Prefixes.ContainsKey("SHOP"));
        Assert.True(svc.Prefixes.ContainsKey("Shop"));
    }
}
