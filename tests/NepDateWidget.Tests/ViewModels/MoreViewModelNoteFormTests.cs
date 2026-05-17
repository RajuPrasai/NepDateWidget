using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests for MoreViewModel covering the Notes form (add/validate/save),
/// Notes visibility gates (ShowNoteEmpty, ShowNoteNoResults), Notes inline
/// edit buffer truncation, and note append-on-add behaviour.
/// </summary>
public class MoreViewModelNoteFormTests
{
    // ── Minimal fakes ─────────────────────────────────────────────────────────

    private sealed class FakeNotesService : INotesService
    {
        private readonly Dictionary<string, string> _notes = new();
        public event EventHandler? NotesChanged;

        public string? GetNote(string key) => _notes.GetValueOrDefault(key);
        public IReadOnlyDictionary<string, string> GetAll() => _notes;
        public HashSet<int> GetHasNotesForMonth(int y, int m) => new();

        public void SetNote(string key, string? text)
        {
            if (string.IsNullOrEmpty(text)) { _notes.Remove(key); }
            else { _notes[key] = text; }
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteNote(string key)
        {
            _notes.Remove(key);
            NotesChanged?.Invoke(this, EventArgs.Empty);
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

    private static (MoreViewModel vm, FakeNotesService svc) CreateNotes()
    {
        var adapter = new FakeNepaliDateAdapter();
        var svc = new FakeNotesService();
        var loc = MakeLoc();
        var vm = new MoreViewModel(loc, notesService: svc, adapter: adapter);
        vm.SetModeNotesCommand.Execute(null);
        return (vm, svc);
    }

    // ── ShowNoteEmpty ─────────────────────────────────────────────────────────

    [Fact]
    public void ShowNoteEmpty_True_WhenNoNotes_AndFormClosed()
    {
        var (vm, _) = CreateNotes();
        Assert.False(vm.IsNoteFormOpen);
        Assert.False(vm.HasNotes);
        Assert.True(vm.ShowNoteEmpty);
    }

    [Fact]
    public void ShowNoteEmpty_False_WhenFormOpen()
    {
        var (vm, _) = CreateNotes();
        vm.AddNoteCommand.Execute(null); // opens the form
        Assert.True(vm.IsNoteFormOpen);
        Assert.False(vm.ShowNoteEmpty);
    }

    [Fact]
    public void ShowNoteEmpty_False_WhenHasNotes()
    {
        var (vm, svc) = CreateNotes();
        svc.SetNote("2082-01-01", "Test note");
        // Refresh is triggered by NotesChanged event raised inside SetNote
        Assert.True(vm.HasNotes);
        Assert.False(vm.ShowNoteEmpty);
    }

    // ── ShowNoteNoResults ─────────────────────────────────────────────────────

    [Fact]
    public void ShowNoteNoResults_False_WhenNoNotes()
    {
        var (vm, _) = CreateNotes();
        vm.NoteSearchText = "anything";
        // No notes exist — the condition requires HasNotes = true
        Assert.False(vm.ShowNoteNoResults);
    }

    [Fact]
    public void ShowNoteNoResults_False_WhenSearchIsEmpty()
    {
        var (vm, svc) = CreateNotes();
        svc.SetNote("2082-01-01", "Hello");

        Assert.True(vm.HasNotes);
        Assert.Equal(string.Empty, vm.NoteSearchText); // empty search
        Assert.False(vm.ShowNoteNoResults);
    }

    [Fact]
    public void ShowNoteNoResults_True_WhenHasNotes_SearchActive_NoMatch()
    {
        var (vm, svc) = CreateNotes();
        svc.SetNote("2082-01-01", "Hello World");

        vm.NoteSearchText = "xxxxxxxxxnotpresent";

        Assert.True(vm.ShowNoteNoResults);
    }

    [Fact]
    public void ShowNoteNoResults_False_WhenHasNotes_SearchActive_HasMatch()
    {
        var (vm, svc) = CreateNotes();
        svc.SetNote("2082-01-01", "Hello World");

        vm.NoteSearchText = "Hello";

        Assert.False(vm.ShowNoteNoResults);
        Assert.Single(vm.FilteredNotes);
    }

    // ── Note search filtering ─────────────────────────────────────────────────

    [Fact]
    public void NoteSearchText_CaseInsensitive_Matches()
    {
        var (vm, svc) = CreateNotes();
        svc.SetNote("2082-01-01", "Passport Scan");

        vm.NoteSearchText = "passport";

        Assert.Single(vm.FilteredNotes);
    }

    [Fact]
    public void NoteSearchText_Empty_ShowsAllNotes()
    {
        var (vm, svc) = CreateNotes();
        svc.SetNote("2082-01-01", "Note one");
        svc.SetNote("2082-02-01", "Note two");

        vm.NoteSearchText = string.Empty;

        Assert.Equal(2, vm.FilteredNotes.Count);
    }

    [Fact]
    public void NoteSearchText_Set_Then_Cleared_ShowsAllNotes()
    {
        var (vm, svc) = CreateNotes();
        svc.SetNote("2082-01-01", "Only note");

        vm.NoteSearchText = "zz_no_match";
        Assert.Empty(vm.FilteredNotes); // precondition

        vm.NoteSearchText = string.Empty;

        Assert.Single(vm.FilteredNotes);
    }

    // ── AddNoteCommand opens form ─────────────────────────────────────────────

    [Fact]
    public void AddNoteCommand_OpensForm()
    {
        var (vm, _) = CreateNotes();
        Assert.False(vm.IsNoteFormOpen); // precondition

        vm.AddNoteCommand.Execute(null);

        Assert.True(vm.IsNoteFormOpen);
    }

    [Fact]
    public void AddNoteCommand_PreFillsDateFromAdapter()
    {
        // FakeNepaliDateAdapter.GetTodayBs = (2082, 12, 20)
        var (vm, _) = CreateNotes();
        vm.AddNoteCommand.Execute(null);

        // Prefill: "{y:D4}/{m:D2}/{d:D2}" = "2082/12/20"
        Assert.Equal("2082/12/20", vm.NoteFormDateInput);
    }

    // ── CancelNoteFormCommand ─────────────────────────────────────────────────

    [Fact]
    public void CancelNoteFormCommand_ClosesForm()
    {
        var (vm, _) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        Assert.True(vm.IsNoteFormOpen); // precondition

        vm.CancelNoteFormCommand.Execute(null);

        Assert.False(vm.IsNoteFormOpen);
    }

    [Fact]
    public void CancelNoteFormCommand_ClearsFormFields()
    {
        var (vm, _) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        vm.NoteFormText = "some text I typed";
        vm.NoteFormDateInput = "2082/06/06";

        vm.CancelNoteFormCommand.Execute(null);

        Assert.Equal(string.Empty, vm.NoteFormText);
        Assert.Equal(string.Empty, vm.NoteFormDateInput);
    }

    // ── SaveNoteFormCommand validation ────────────────────────────────────────

    [Fact]
    public void SaveNoteFormCommand_EmptyDate_SetsError_FormStaysOpen()
    {
        var (vm, svc) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = string.Empty;
        vm.NoteFormText = "Some text";

        vm.SaveNoteFormCommand.Execute(null);

        Assert.True(vm.HasNoteFormError);
        Assert.NotEmpty(vm.NoteFormError);
        Assert.True(vm.IsNoteFormOpen);
        Assert.False(svc.GetAll().Any());
    }

    [Fact]
    public void SaveNoteFormCommand_InvalidDateFormat_SetsError()
    {
        var (vm, _) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = "today"; // not YYYY-MM-DD or YYYY/MM/DD
        vm.NoteFormText = "Some text";

        vm.SaveNoteFormCommand.Execute(null);

        Assert.True(vm.HasNoteFormError);
    }

    [Fact]
    public void SaveNoteFormCommand_EmptyText_SetsError()
    {
        var (vm, _) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = "2082/01/01";
        vm.NoteFormText = string.Empty;

        vm.SaveNoteFormCommand.Execute(null);

        Assert.True(vm.HasNoteFormError);
    }

    [Fact]
    public void SaveNoteFormCommand_WhitespaceTextOnly_SetsError()
    {
        var (vm, _) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = "2082/01/01";
        vm.NoteFormText = "   ";

        vm.SaveNoteFormCommand.Execute(null);

        Assert.True(vm.HasNoteFormError);
    }

    // ── SaveNoteFormCommand success ───────────────────────────────────────────

    [Fact]
    public void SaveNoteFormCommand_Valid_CallsSetNote_OnService()
    {
        var (vm, svc) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = "2082/03/15";
        vm.NoteFormText = "Doctor appointment";

        vm.SaveNoteFormCommand.Execute(null);

        // Key is normalised to YYYY-MM-DD
        Assert.Equal("Doctor appointment", svc.GetNote("2082-03-15"));
    }

    [Fact]
    public void SaveNoteFormCommand_Valid_WithSlashDate_NormalisesKeyToHyphen()
    {
        // Input "2082/06/01" must be stored as "2082-06-01"
        var (vm, svc) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = "2082/06/01";
        vm.NoteFormText = "Meeting";

        vm.SaveNoteFormCommand.Execute(null);

        Assert.Null(svc.GetNote("2082/06/01")); // slash form not stored
        Assert.Equal("Meeting", svc.GetNote("2082-06-01")); // hyphen form stored
    }

    [Fact]
    public void SaveNoteFormCommand_Valid_ClosesForm()
    {
        var (vm, _) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = "2082/01/10";
        vm.NoteFormText = "Reminder text";

        vm.SaveNoteFormCommand.Execute(null);

        Assert.False(vm.IsNoteFormOpen);
    }

    [Fact]
    public void SaveNoteFormCommand_Valid_ClearsFormError()
    {
        var (vm, _) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = string.Empty;
        vm.SaveNoteFormCommand.Execute(null);
        Assert.True(vm.HasNoteFormError); // precondition

        vm.NoteFormDateInput = "2082/01/10";
        vm.NoteFormText = "Good text";
        vm.SaveNoteFormCommand.Execute(null);

        Assert.False(vm.HasNoteFormError);
    }

    [Fact]
    public void SaveNoteFormCommand_AddMode_ExistingNoteForSameDate_AppendsText()
    {
        // In add mode (not edit), if a note already exists the new text is appended
        var (vm, svc) = CreateNotes();
        svc.SetNote("2082-02-01", "First entry");

        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = "2082/02/01";
        vm.NoteFormText = "Second entry";

        vm.SaveNoteFormCommand.Execute(null);

        var stored = svc.GetNote("2082-02-01");
        Assert.Contains("First entry", stored);
        Assert.Contains("Second entry", stored);
    }

    [Fact]
    public void SaveNoteFormCommand_Valid_RefreshesNotesCollection()
    {
        var (vm, svc) = CreateNotes();
        Assert.False(vm.HasNotes); // precondition

        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = "2082/05/20";
        vm.NoteFormText = "New note";
        vm.SaveNoteFormCommand.Execute(null);

        Assert.True(vm.HasNotes);
    }

    // ── NoteEditBuffer truncation ─────────────────────────────────────────────

    [Fact]
    public void NoteEditBuffer_Under500_StoredExactly()
    {
        var (vm, _) = CreateNotes();
        var text = new string('A', 499);
        vm.NoteEditBuffer = text;
        Assert.Equal(499, vm.NoteEditBuffer.Length);
    }

    [Fact]
    public void NoteEditBuffer_Exactly500_StoredExactly()
    {
        var (vm, _) = CreateNotes();
        vm.NoteEditBuffer = new string('B', 500);
        Assert.Equal(500, vm.NoteEditBuffer.Length);
    }

    [Fact]
    public void NoteEditBuffer_Over500_TruncatedAt500()
    {
        var (vm, _) = CreateNotes();
        vm.NoteEditBuffer = new string('C', 600);
        Assert.Equal(500, vm.NoteEditBuffer.Length);
    }

    [Fact]
    public void NoteEditBuffer_Null_StoredAsEmpty()
    {
        var (vm, _) = CreateNotes();
        vm.NoteEditBuffer = null!;
        Assert.Equal(string.Empty, vm.NoteEditBuffer);
    }

    // ── NoteFormError cleared on date input change ────────────────────────────

    [Fact]
    public void NoteFormDateInput_Change_ClearsFormError()
    {
        var (vm, _) = CreateNotes();
        vm.AddNoteCommand.Execute(null);
        vm.NoteFormDateInput = string.Empty;
        vm.SaveNoteFormCommand.Execute(null);
        Assert.True(vm.HasNoteFormError); // precondition

        vm.NoteFormDateInput = "2082/01/01"; // change triggers error clear

        Assert.Equal(string.Empty, vm.NoteFormError);
    }
}
