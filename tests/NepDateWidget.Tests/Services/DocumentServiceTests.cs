using NepDateWidget.Models;
using NepDateWidget.Services;
using System.IO;

namespace NepDateWidget.Tests.Services;

public sealed class DocumentServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public DocumentServiceTests()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), $"NepDateWidget_DocTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "documents.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private DocumentService Create()
    {
        var svc = new DocumentService(_filePath);
        svc.Load();
        return svc;
    }

    // ── First-launch initialization ───────────────────────────────────────────

    [Fact]
    public void Load_FileAbsent_CreatesEmptyFile()
    {
        using var svc = Create();
        Assert.True(File.Exists(_filePath));
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Load_FileAbsent_FileContainsValidJson()
    {
        using var svc = Create();
        var content = File.ReadAllText(_filePath);
        Assert.Equal("[]", content.Trim());
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public void AddThenLoad_RoundTrips()
    {
        var entry = new DocumentEntry { Title = "Test Doc", FilePath = @"C:\test.pdf", SortOrder = 1 };
        string id;

        using (var svc = Create())
        {
            svc.Add(entry);
            id = entry.Id;
        }

        using var svc2 = new DocumentService(_filePath);
        svc2.Load();
        var all = svc2.GetAll();
        Assert.Single(all);
        Assert.Equal("Test Doc", all[0].Title);
        Assert.Equal(id, all[0].Id);
    }

    [Fact]
    public void Update_ChangesEntry_Persists()
    {
        var entry = new DocumentEntry { Title = "Old Title" };
        using var svc = Create();
        svc.Add(entry);

        entry.Title = "New Title";
        svc.Update(entry);

        using var svc2 = new DocumentService(_filePath);
        svc2.Load();
        Assert.Equal("New Title", svc2.GetAll()[0].Title);
    }

    [Fact]
    public void Delete_RemovesEntry_Persists()
    {
        var entry = new DocumentEntry { Title = "To Delete" };
        using var svc = Create();
        svc.Add(entry);
        svc.Delete(entry.Id);

        using var svc2 = new DocumentService(_filePath);
        svc2.Load();
        Assert.Empty(svc2.GetAll());
    }

    // ── Change notification ───────────────────────────────────────────────────

    [Fact]
    public void Add_RaisesDocumentsChanged()
    {
        using var svc = Create();
        int fired = 0;
        svc.DocumentsChanged += (_, _) => fired++;
        svc.Add(new DocumentEntry { Title = "x" });
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Delete_EntryNotFound_DoesNotRaiseChanged()
    {
        using var svc = Create();
        int fired = 0;
        svc.DocumentsChanged += (_, _) => fired++;
        svc.Delete("nonexistent-id");
        Assert.Equal(0, fired);
    }

    // ── Corrupted file ────────────────────────────────────────────────────────

    [Fact]
    public void Load_CorruptedJson_ReturnsEmpty()
    {
        File.WriteAllText(_filePath, "this is not json {{{{");
        using var svc = new DocumentService(_filePath);
        svc.Load();
        Assert.Empty(svc.GetAll());
    }

    // ── Atomic write ─────────────────────────────────────────────────────────

    [Fact]
    public void Save_WritesAtomically_NoBakOrTmpRemaining()
    {
        using var svc = Create();
        svc.Add(new DocumentEntry { Title = "Atomic Test" });

        Assert.False(File.Exists(_filePath + ".tmp"), ".tmp must be cleaned up");
        Assert.False(File.Exists(_filePath + ".bak"), ".bak must be cleaned up");
    }

    // ── Update edge cases ─────────────────────────────────────────────────────

    [Fact]
    public void Update_EntryNotFound_IsNoOp_DoesNotThrow()
    {
        using var svc = Create();
        var ghost = new DocumentEntry { Id = "nonexistent-id", Title = "Ghost" };
        var ex    = Record.Exception(() => svc.Update(ghost));
        Assert.Null(ex);
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Update_EntryNotFound_DoesNotRaiseChanged()
    {
        using var svc = Create();
        int fired = 0;
        svc.DocumentsChanged += (_, _) => fired++;
        svc.Update(new DocumentEntry { Id = "nonexistent", Title = "Ghost" });
        Assert.Equal(0, fired);
    }

    [Fact]
    public void Update_EntryFound_RaisesDocumentsChanged()
    {
        using var svc = Create();
        var entry = new DocumentEntry { Title = "Before" };
        svc.Add(entry);

        int fired = 0;
        svc.DocumentsChanged += (_, _) => fired++;

        entry.Title = "After";
        svc.Update(entry);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Delete_EntryFound_RaisesDocumentsChanged()
    {
        using var svc = Create();
        var entry = new DocumentEntry { Title = "To Delete" };
        svc.Add(entry);

        int fired = 0;
        svc.DocumentsChanged += (_, _) => fired++;

        svc.Delete(entry.Id);
        Assert.Equal(1, fired);
    }

    // ── Load edge cases ───────────────────────────────────────────────────────

    [Fact]
    public void Load_EmptyArray_ReturnsEmpty()
    {
        File.WriteAllText(_filePath, "[]");
        using var svc = new DocumentService(_filePath);
        svc.Load();
        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void GetAll_ReturnsReadOnlyView()
    {
        using var svc = Create();
        svc.Add(new DocumentEntry { Title = "A" });
        svc.Add(new DocumentEntry { Title = "B" });
        Assert.IsAssignableFrom<IReadOnlyList<DocumentEntry>>(svc.GetAll());
    }
}
