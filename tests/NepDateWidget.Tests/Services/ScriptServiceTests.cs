using NepDateWidget.Models;
using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

public sealed class ScriptServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public ScriptServiceTests()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), $"NepDateWidget_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "scripts.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private ScriptService Create()
    {
        var svc = new ScriptService(_filePath);
        svc.LoadFromFile();
        return svc;
    }

    private void WriteJson(string json)
        => File.WriteAllText(_filePath, json);

    // ── Load ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_FileNotFound_ReturnsEmptyList()
    {
        var svc = Create();
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Load_ValidJson_ParsesEntries()
    {
        WriteJson("""
            [
              { "Name": "hello", "Description": "say hi", "Path": "C:\\scripts\\hello.ps1", "Interpreter": "powershell" }
            ]
            """);
        var svc = Create();
        var all = svc.GetAll();
        Assert.Single(all);
        var e = all[0];
        Assert.Equal("hello",              e.Name);
        Assert.Equal("say hi",             e.Description);
        Assert.Equal("C:\\scripts\\hello.ps1", e.Path);
        Assert.Equal("powershell",         e.Interpreter);
    }

    [Fact]
    public void Load_MalformedJson_ReturnsEmptyList()
    {
        WriteJson("this is not json {{{{");
        var svc = Create();
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Load_EmptyArray_ReturnsEmptyList()
    {
        WriteJson("[]");
        var svc = Create();
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Load_FiltersEntriesWithEmptyName()
    {
        WriteJson("""
            [
              { "Name": "", "Description": "no name", "Path": "C:\\x.ps1", "Interpreter": "powershell" },
              { "Name": "valid", "Description": "", "Path": "C:\\y.ps1", "Interpreter": "cmd" }
            ]
            """);
        var svc = Create();
        var all = svc.GetAll();
        Assert.Single(all);
        Assert.Equal("valid", all[0].Name);
    }

    [Fact]
    public void Load_FiltersEntriesWithEmptyPath()
    {
        WriteJson("""
            [
              { "Name": "nopath", "Description": "", "Path": "", "Interpreter": "powershell" },
              { "Name": "goodpath", "Description": "", "Path": "C:\\good.ps1", "Interpreter": "cmd" }
            ]
            """);
        var svc = Create();
        var all = svc.GetAll();
        Assert.Single(all);
        Assert.Equal("goodpath", all[0].Name);
    }

    [Fact]
    public void Load_UnknownJsonField_DoesNotThrow()
    {
        WriteJson("""
            [
              { "Name": "ok", "Path": "C:\\x.ps1", "Interpreter": "cmd", "UnknownFutureField": true }
            ]
            """);
        var svc = Create();
        Assert.Single(svc.GetAll());
    }

    [Fact]
    public void Load_DefaultInterpreter_IsNotEmpty()
    {
        WriteJson("""
            [
              { "Name": "test", "Path": "C:\\t.ps1" }
            ]
            """);
        var svc = Create();
        var e = Assert.Single(svc.GetAll());
        Assert.False(string.IsNullOrEmpty(e.Interpreter));
    }

    // ── Find ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Find_ExistingName_ReturnsEntry()
    {
        WriteJson("""
            [
              { "Name": "greet", "Path": "C:\\greet.ps1", "Interpreter": "powershell" }
            ]
            """);
        var svc = Create();
        var entry = svc.Find("greet");
        Assert.NotNull(entry);
        Assert.Equal("greet", entry.Name);
    }

    [Fact]
    public void Find_CaseInsensitive()
    {
        WriteJson("""
            [
              { "Name": "MyScript", "Path": "C:\\my.ps1", "Interpreter": "powershell" }
            ]
            """);
        var svc = Create();
        Assert.NotNull(svc.Find("myscript"));
        Assert.NotNull(svc.Find("MYSCRIPT"));
        Assert.NotNull(svc.Find("MyScript"));
    }

    [Fact]
    public void Find_UnknownName_ReturnsNull()
    {
        WriteJson("""
            [
              { "Name": "known", "Path": "C:\\k.ps1", "Interpreter": "cmd" }
            ]
            """);
        var svc = Create();
        Assert.Null(svc.Find("unknown"));
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsReadOnlyView()
    {
        WriteJson("""
            [
              { "Name": "a", "Path": "C:\\a.ps1" },
              { "Name": "b", "Path": "C:\\b.ps1" }
            ]
            """);
        var svc = Create();
        var all = svc.GetAll();
        Assert.Equal(2, all.Count);
        Assert.IsAssignableFrom<IReadOnlyList<ScriptEntry>>(all);
    }

    // ── Load() - file absent: seed + watcher setup ────────────────────────────

    [Fact]
    public void Load_FileAbsent_SeedsFile()
    {
        // Create() calls LoadFromFile(). Load() also seeds. Use Load() directly here.
        var svc = new ScriptService(_filePath);
        svc.Load();
        Assert.True(File.Exists(_filePath), "scripts.json must be created on first Load()");
    }

    [Fact]
    public void Load_FileAbsent_GetAllIsEmpty()
    {
        // The seeded file contains the comment header and an empty array `[]`.
        var svc = new ScriptService(_filePath);
        svc.Load();
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Load_FileWithComments_ParsesCorrectly()
    {
        // scripts.json supports // line comments (JsonCommentHandling.Skip).
        WriteJson("""
            // line comment
            [
              // another comment
              { "Name": "commented", "Path": "C:\\c.ps1", "Interpreter": "cmd" }
            ]
            """);
        var svc = new ScriptService(_filePath);
        svc.Load();
        var all = svc.GetAll();
        Assert.Single(all);
        Assert.Equal("commented", all[0].Name);
    }
}
