using NepDateWidget.Services;
using System.Text.Json;

namespace NepDateWidget.Tests.Services;

public sealed class NotesServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public NotesServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NepDateWidget_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "notes.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private NotesService Create()
    {
        var svc = new NotesService(_filePath);
        svc.Load();
        return svc;
    }

    // ── GetNote / SetNote ─────────────────────────────────────────────────────

    [Fact]
    public void GetNote_NoNotes_ReturnsNull()
    {
        var svc = Create();
        Assert.Null(svc.GetNote("2082/01/01"));
    }

    [Fact]
    public void SetNote_StoresAndRetrieves()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "Hello");
        Assert.Equal("Hello", svc.GetNote("2082/01/01"));
    }

    [Fact]
    public void SetNote_TrimsWhitespace()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "  trimmed  ");
        Assert.Equal("trimmed", svc.GetNote("2082/01/01"));
    }

    [Fact]
    public void SetNote_NullText_RemovesNote()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "Exists");
        svc.SetNote("2082/01/01", null);
        Assert.Null(svc.GetNote("2082/01/01"));
    }

    [Fact]
    public void SetNote_WhitespaceText_RemovesNote()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "Exists");
        svc.SetNote("2082/01/01", "   ");
        Assert.Null(svc.GetNote("2082/01/01"));
    }

    [Fact]
    public void SetNote_EmptyText_RemovesNote()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "Exists");
        svc.SetNote("2082/01/01", "");
        Assert.Null(svc.GetNote("2082/01/01"));
    }

    // ── DeleteNote ────────────────────────────────────────────────────────────

    [Fact]
    public void DeleteNote_ExistingKey_Removes()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "Note");
        svc.DeleteNote("2082/01/01");
        Assert.Null(svc.GetNote("2082/01/01"));
    }

    [Fact]
    public void DeleteNote_NonexistentKey_NoOp()
    {
        var svc = Create();
        svc.DeleteNote("nonexistent"); // should not throw
        Assert.Empty(svc.GetAll());
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsAllNotes()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "A");
        svc.SetNote("2082/01/02", "B");

        var all = svc.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal("A", all["2082/01/01"]);
        Assert.Equal("B", all["2082/01/02"]);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    [Fact]
    public void SetNote_PersistsToFile()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "Persisted");

        var svc2 = Create();
        Assert.Equal("Persisted", svc2.GetNote("2082/01/01"));
    }

    [Fact]
    public void Load_MissingFile_StartsEmpty()
    {
        var svc = Create();
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Load_CorruptJson_FallsBackToEmpty()
    {
        File.WriteAllText(_filePath, "NOT VALID {{{");
        var svc = Create();
        Assert.Empty(svc.GetAll());
    }

    // ── NotesChanged event ────────────────────────────────────────────────────

    [Fact]
    public void SetNote_RaisesNotesChanged()
    {
        var svc = Create();
        int called = 0;
        svc.NotesChanged += (_, _) => called++;

        svc.SetNote("2082/01/01", "X");
        Assert.Equal(1, called);
    }

    [Fact]
    public void SetNote_RemovingWithNull_RaisesNotesChanged()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "X");
        int called = 0;
        svc.NotesChanged += (_, _) => called++;
        svc.SetNote("2082/01/01", null);
        Assert.Equal(1, called);
    }

    [Fact]
    public void DeleteNote_ExistingKey_RaisesNotesChanged()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "X");
        int called = 0;
        svc.NotesChanged += (_, _) => called++;
        svc.DeleteNote("2082/01/01");
        Assert.Equal(1, called);
    }

    [Fact]
    public void DeleteNote_NonexistentKey_DoesNotRaiseNotesChanged()
    {
        var svc = Create();
        int called = 0;
        svc.NotesChanged += (_, _) => called++;
        svc.DeleteNote("nonexistent");
        Assert.Equal(0, called);
    }

    // ── MigrateFromSettings ───────────────────────────────────────────────────

    [Fact]
    public void MigrateFromSettings_ImportsNewKeys()
    {
        var svc = Create();
        var settings = new Dictionary<string, string>
        {
            ["2082/01/01"] = "Migrated",
            ["2082/01/02"] = "Also migrated",
        };

        svc.MigrateFromSettings(settings);

        Assert.Equal("Migrated", svc.GetNote("2082/01/01"));
        Assert.Equal("Also migrated", svc.GetNote("2082/01/02"));
    }

    [Fact]
    public void MigrateFromSettings_DoesNotOverwriteExisting()
    {
        var svc = Create();
        svc.SetNote("2082/01/01", "Existing");

        var settings = new Dictionary<string, string>
        {
            ["2082/01/01"] = "Should not overwrite",
        };

        svc.MigrateFromSettings(settings);
        Assert.Equal("Existing", svc.GetNote("2082/01/01"));
    }

    [Fact]
    public void MigrateFromSettings_SkipsWhitespaceValues()
    {
        var svc = Create();
        var settings = new Dictionary<string, string>
        {
            ["2082/01/01"] = "   ",
            ["2082/01/02"] = "",
        };

        svc.MigrateFromSettings(settings);
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void MigrateFromSettings_NullDictionary_NoOp()
    {
        var svc = Create();
        svc.MigrateFromSettings(null!);
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void MigrateFromSettings_EmptyDictionary_NoOp()
    {
        var svc = Create();
        svc.MigrateFromSettings(new Dictionary<string, string>());
        Assert.Empty(svc.GetAll());
    }
}
