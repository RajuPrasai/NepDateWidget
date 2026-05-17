using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests for MoreViewModel covering the Documents panel:
/// DocEditTitle invalid character stripping, ShowDocEmpty / ShowDocNoResults
/// visibility gates, FilteredDocuments search (title, notes, tag-prefix),
/// and SaveDocumentCommand validation (empty title, empty file path, duplicate title).
/// File-copy operations are intentionally not tested here (they require real disk).
/// </summary>
public class MoreViewModelDocumentTests
{
    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeDocumentService : IDocumentService
    {
        private readonly List<DocumentEntry> _items = new();
        public event EventHandler? DocumentsChanged;

        public IReadOnlyList<DocumentEntry> GetAll() => _items.AsReadOnly();

        public void Add(DocumentEntry entry)
        {
            _items.Add(entry);
            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Update(DocumentEntry entry)
        {
            var idx = _items.FindIndex(d => d.Id == entry.Id);
            if (idx >= 0) { _items[idx] = entry; }
            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Delete(string id)
        {
            _items.RemoveAll(d => d.Id == id);
            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Load() { }
        public void Save() { }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LocalizationService MakeLoc()
    {
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        return loc;
    }

    private static (MoreViewModel vm, FakeDocumentService svc) CreateDocs()
    {
        var svc = new FakeDocumentService();
        var loc = MakeLoc();
        var vm = new MoreViewModel(loc, documentService: svc);
        vm.SetModeDocumentsCommand.Execute(null);
        return (vm, svc);
    }

    private static DocumentEntry MakeEntry(string title, string notes = "", string[]? tags = null) =>
        new DocumentEntry
        {
            Title = title,
            FilePath = $"C:\\docs\\{title}.pdf",
            Notes = notes,
            Tags = tags?.ToList() ?? new(),
        };

    // ── DocEditTitle invalid character stripping ──────────────────────────────

    [Fact]
    public void DocEditTitle_RemovesColon_InvalidOnWindows()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditTitle = "Passport:2026";
        // ':' is in Path.GetInvalidFileNameChars()
        Assert.DoesNotContain(":", vm.DocEditTitle);
    }

    [Fact]
    public void DocEditTitle_RemovesForwardSlash()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditTitle = "My/Document";
        Assert.DoesNotContain("/", vm.DocEditTitle);
    }

    [Fact]
    public void DocEditTitle_RemovesBackslash()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditTitle = "My\\Document";
        Assert.DoesNotContain("\\", vm.DocEditTitle);
    }

    [Fact]
    public void DocEditTitle_RemovesPipe()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditTitle = "Salary|Slip";
        Assert.DoesNotContain("|", vm.DocEditTitle);
    }

    [Fact]
    public void DocEditTitle_ValidChars_StoredExactly()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditTitle = "Citizenship Front 2082";
        Assert.Equal("Citizenship Front 2082", vm.DocEditTitle);
    }

    [Fact]
    public void DocEditTitle_MultipleInvalidChars_AllStripped()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditTitle = "A:B/C\\D";
        // All invalid chars removed; valid chars remain
        Assert.Equal("ABCD", vm.DocEditTitle);
    }

    // ── ShowDocEmpty ──────────────────────────────────────────────────────────

    [Fact]
    public void ShowDocEmpty_True_WhenNoDocuments_AndFormClosed()
    {
        var (vm, _) = CreateDocs();
        Assert.False(vm.HasDocuments);
        Assert.False(vm.IsDocFormOpen);
        Assert.True(vm.ShowDocEmpty);
    }

    [Fact]
    public void ShowDocEmpty_False_WhenFormOpen()
    {
        var (vm, _) = CreateDocs();
        vm.ShowAddDocumentCommand.Execute(null);
        Assert.True(vm.IsDocFormOpen);
        Assert.False(vm.ShowDocEmpty);
    }

    [Fact]
    public void ShowDocEmpty_False_WhenHasDocuments()
    {
        var (vm, svc) = CreateDocs();
        svc.Add(MakeEntry("Passport"));
        // DocumentsChanged event triggers RefreshDocuments which updates HasDocuments
        Assert.True(vm.HasDocuments);
        Assert.False(vm.ShowDocEmpty);
    }

    // ── ShowDocNoResults ──────────────────────────────────────────────────────

    [Fact]
    public void ShowDocNoResults_False_WhenNoDocuments()
    {
        var (vm, _) = CreateDocs();
        vm.DocSearchText = "anything";
        // HasDocuments is false → condition not met
        Assert.False(vm.ShowDocNoResults);
    }

    [Fact]
    public void ShowDocNoResults_False_WhenSearchEmpty()
    {
        var (vm, svc) = CreateDocs();
        svc.Add(MakeEntry("Passport"));

        Assert.True(vm.HasDocuments);
        Assert.Equal(string.Empty, vm.DocSearchText);
        Assert.False(vm.ShowDocNoResults);
    }

    [Fact]
    public void ShowDocNoResults_True_WhenHasDocuments_SearchActive_NoMatch()
    {
        var (vm, svc) = CreateDocs();
        svc.Add(MakeEntry("Passport"));

        vm.DocSearchText = "xxxxxxxxxnotpresent";

        Assert.True(vm.ShowDocNoResults);
    }

    // ── FilteredDocuments: title search ───────────────────────────────────────

    [Fact]
    public void DocSearchText_MatchesTitle_CaseInsensitive()
    {
        var (vm, svc) = CreateDocs();
        svc.Add(MakeEntry("citizenship front"));

        vm.DocSearchText = "CITIZENSHIP";

        Assert.Single(vm.FilteredDocuments);
        Assert.Equal("citizenship front", vm.FilteredDocuments[0].Title);
    }

    [Fact]
    public void DocSearchText_Empty_ShowsAllDocuments()
    {
        var (vm, svc) = CreateDocs();
        svc.Add(MakeEntry("Passport"));
        svc.Add(MakeEntry("PAN Card"));

        vm.DocSearchText = string.Empty;

        Assert.Equal(2, vm.FilteredDocuments.Count);
    }

    [Fact]
    public void DocSearchText_NoMatch_FilteredDocuments_IsEmpty()
    {
        var (vm, svc) = CreateDocs();
        svc.Add(MakeEntry("Passport"));

        vm.DocSearchText = "zzz_no_match";

        Assert.Empty(vm.FilteredDocuments);
    }

    // ── FilteredDocuments: notes search ──────────────────────────────────────

    [Fact]
    public void DocSearchText_MatchesNotes_WhenTitleDoesNotMatch()
    {
        var (vm, svc) = CreateDocs();
        svc.Add(MakeEntry("Passport", notes: "Expires April 2030"));

        vm.DocSearchText = "expires";

        Assert.Single(vm.FilteredDocuments);
    }

    // ── FilteredDocuments: tag prefix search (#tag) ───────────────────────────

    [Fact]
    public void DocSearchText_HashPrefix_FiltersOnTag()
    {
        var (vm, svc) = CreateDocs();
        svc.Add(MakeEntry("Passport", tags: ["Government", "Personal"]));
        svc.Add(MakeEntry("Salary Slip", tags: ["Financial"]));

        vm.DocSearchText = "#government";

        Assert.Single(vm.FilteredDocuments);
        Assert.Equal("Passport", vm.FilteredDocuments[0].Title);
    }

    [Fact]
    public void DocSearchText_HashPrefix_CaseInsensitive()
    {
        var (vm, svc) = CreateDocs();
        svc.Add(MakeEntry("Visa", tags: ["government"]));

        vm.DocSearchText = "#GOVERNMENT";

        Assert.Single(vm.FilteredDocuments);
    }

    // ── SaveDocumentCommand validation ────────────────────────────────────────

    [Fact]
    public void SaveDocumentCommand_EmptyTitle_SetsDocEditError()
    {
        var (vm, _) = CreateDocs();
        vm.ShowAddDocumentCommand.Execute(null);
        vm.DocEditTitle = string.Empty;
        vm.DocEditFilePath = "C:\\test\\doc.pdf";

        vm.SaveDocumentCommand.Execute(null);

        Assert.True(vm.HasDocEditError);
        Assert.NotEmpty(vm.DocEditError);
    }

    [Fact]
    public void SaveDocumentCommand_EmptyFilePath_SetsDocEditError()
    {
        var (vm, _) = CreateDocs();
        vm.ShowAddDocumentCommand.Execute(null);
        vm.DocEditTitle = "My Document";
        vm.DocEditFilePath = string.Empty;

        vm.SaveDocumentCommand.Execute(null);

        Assert.True(vm.HasDocEditError);
        Assert.NotEmpty(vm.DocEditError);
    }

    [Fact]
    public void SaveDocumentCommand_EmptyTitle_DoesNotAddEntry()
    {
        var (vm, svc) = CreateDocs();
        vm.ShowAddDocumentCommand.Execute(null);
        vm.DocEditTitle = string.Empty;
        vm.DocEditFilePath = "C:\\test\\doc.pdf";

        vm.SaveDocumentCommand.Execute(null);

        Assert.False(vm.HasDocuments);
        Assert.Empty(svc.GetAll());
    }

    // ── Tag management ────────────────────────────────────────────────────────

    [Fact]
    public void AddDocTagPresetCommand_AddsTag()
    {
        var (vm, _) = CreateDocs();
        vm.AddDocTagPresetCommand.Execute("Personal");

        Assert.Single(vm.DocEditTags);
        Assert.Equal("Personal", vm.DocEditTags[0]);
    }

    [Fact]
    public void AddDocTagPresetCommand_Duplicate_NotAdded()
    {
        var (vm, _) = CreateDocs();
        vm.AddDocTagPresetCommand.Execute("Personal");
        vm.AddDocTagPresetCommand.Execute("Personal");

        Assert.Single(vm.DocEditTags);
    }

    [Fact]
    public void AddDocTagPresetCommand_CaseInsensitiveDuplicate_NotAdded()
    {
        var (vm, _) = CreateDocs();
        vm.AddDocTagPresetCommand.Execute("Personal");
        vm.AddDocTagPresetCommand.Execute("personal");

        Assert.Single(vm.DocEditTags);
    }

    [Fact]
    public void RemoveDocTagCommand_RemovesExistingTag()
    {
        var (vm, _) = CreateDocs();
        vm.AddDocTagPresetCommand.Execute("Government");

        vm.RemoveDocTagCommand.Execute("Government");

        Assert.Empty(vm.DocEditTags);
    }

    [Fact]
    public void AddDocTagFromInput_CommaSeparated_AddsMultipleTags()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditTagInput = "Personal, Government, Education";
        vm.AddDocTagFromInputCommand.Execute(null);

        Assert.Equal(3, vm.DocEditTags.Count);
        Assert.Contains("Personal", vm.DocEditTags);
        Assert.Contains("Government", vm.DocEditTags);
        Assert.Contains("Education", vm.DocEditTags);
    }

    [Fact]
    public void AddDocTagFromInput_ClearsInputAfterAdd()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditTagInput = "Personal";
        vm.AddDocTagFromInputCommand.Execute(null);

        Assert.Equal(string.Empty, vm.DocEditTagInput);
    }

    // ── DocEditFilePath properties ────────────────────────────────────────────

    [Fact]
    public void DocEditHasFile_True_WhenPathIsSet()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditFilePath = "C:\\test\\doc.pdf";
        Assert.True(vm.DocEditHasFile);
    }

    [Fact]
    public void DocEditHasFile_False_WhenPathIsEmpty()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditFilePath = string.Empty;
        Assert.False(vm.DocEditHasFile);
    }

    [Fact]
    public void DocEditFileName_ReturnsFilenameOnly()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditFilePath = "C:\\docs\\reports\\passport_scan.pdf";
        Assert.Equal("passport_scan.pdf", vm.DocEditFileName);
    }

    [Fact]
    public void DocEditFileExtension_ReturnsUpperCaseExtensionWithoutDot()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditFilePath = "C:\\docs\\citizenship.pdf";
        Assert.Equal("PDF", vm.DocEditFileExtension);
    }

    [Fact]
    public void DocEditFileExtension_EmptyWhenNoFile()
    {
        var (vm, _) = CreateDocs();
        vm.DocEditFilePath = string.Empty;
        Assert.Equal(string.Empty, vm.DocEditFileExtension);
    }

    // ── CancelDocumentEditCommand ─────────────────────────────────────────────

    [Fact]
    public void CancelDocumentEditCommand_ClosesForm()
    {
        var (vm, _) = CreateDocs();
        vm.ShowAddDocumentCommand.Execute(null);
        Assert.True(vm.IsDocFormOpen);

        vm.CancelDocumentEditCommand.Execute(null);

        Assert.False(vm.IsDocFormOpen);
    }

    [Fact]
    public void CancelDocumentEditCommand_ClearsFields()
    {
        var (vm, _) = CreateDocs();
        vm.ShowAddDocumentCommand.Execute(null);
        vm.DocEditTitle = "Test Title";
        vm.DocEditFilePath = "C:\\test.pdf";

        vm.CancelDocumentEditCommand.Execute(null);

        Assert.Equal(string.Empty, vm.DocEditTitle);
        Assert.Equal(string.Empty, vm.DocEditFilePath);
    }

    // ── SetDocTitlePresetCommand ──────────────────────────────────────────────

    [Fact]
    public void SetDocTitlePresetCommand_SetsTitle()
    {
        var (vm, _) = CreateDocs();
        vm.SetDocTitlePresetCommand.Execute("Passport");

        Assert.Equal("Passport", vm.DocEditTitle);
    }

    [Fact]
    public void DocTitlePresets_ContainsExpectedDefaults()
    {
        Assert.Contains("Passport", MoreViewModel.DocTitlePresets);
        Assert.Contains("PAN Card", MoreViewModel.DocTitlePresets);
    }
}
