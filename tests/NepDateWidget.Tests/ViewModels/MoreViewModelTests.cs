using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests for MoreViewModel: Notes/Reminders mode toggling, NavigateToReminder highlight
/// logic, and data-refresh behaviour driven by service events.
/// </summary>
public sealed class MoreViewModelTests
{
    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeReminderService : IReminderService
    {
        private readonly List<ReminderEntry> _items = new();

        public event EventHandler? RemindersChanged;

        public IReadOnlyList<ReminderEntry> GetAll() => _items.AsReadOnly();
        public IReadOnlyList<ReminderEntry> GetForDate(int y, int m, int d) => Array.Empty<ReminderEntry>();
        public bool HasRemindersForDate(int y, int m, int d) => false;
        public bool HasRemindersForDateExpanded(int y, int m, int d) => false;
        public IReadOnlyList<ReminderEntry> GetRecurringForDate(int y, int m, int d) => Array.Empty<ReminderEntry>();

        public void Add(ReminderEntry entry)
        {
            _items.Add(entry);
            RemindersChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Update(ReminderEntry entry)
        {
            var idx = _items.FindIndex(r => r.Id == entry.Id);
            if (idx >= 0)
            {
                _items[idx] = entry;
            }

            RemindersChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Delete(string id)
        {
            _items.RemoveAll(r => r.Id == id);
            RemindersChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Load() { }
        public void Save() { }

        public IReadOnlyList<ReminderEntry> CheckAndFireDueReminders(DateTime nowUtc) => Array.Empty<ReminderEntry>();
        public IReadOnlyList<ReminderEntry> GetMissedReminders() => Array.Empty<ReminderEntry>();
        public HashSet<int> GetHasRemindersForMonth(int bsYear, int bsMonth) => new();

        public void RaiseRemindersChanged() => RemindersChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeNotesService : INotesService
    {
        private readonly Dictionary<string, string> _notes = new();

        public event EventHandler? NotesChanged;

        public string? GetNote(string dateKey) => _notes.GetValueOrDefault(dateKey);
        public IReadOnlyDictionary<string, string> GetAll() => _notes;
        public HashSet<int> GetHasNotesForMonth(int bsYear, int bsMonth) => new();
        public void SetNote(string dateKey, string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _notes.Remove(dateKey);
            }
            else
            {
                _notes[dateKey] = text;
            }

            NotesChanged?.Invoke(this, EventArgs.Empty);
        }
        public void DeleteNote(string dateKey)
        {
            _notes.Remove(dateKey);
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Load() { }
        public void Save() { }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static LocalizationService MakeLoc(string lang = "en")
    {
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage(lang);
        return loc;
    }

    private static ReminderEntry MakeReminder(string id, string title = "Test", string date = "2082/12/20")
        => new() { Id = id, Title = title, BsDate = date, Time = "09:00" };

    private static MoreViewModel Create(
        FakeReminderService? rs = null,
        FakeNotesService? ns = null)
        => new(MakeLoc(), ns, rs);

    // ── Mode toggle ───────────────────────────────────────────────────────────

    [Fact]
    public void DefaultMode_IsNotes()
    {
        var vm = Create();
        Assert.True(vm.IsModeNotes);
        Assert.False(vm.IsModeReminders);
    }

    [Fact]
    public void SetModeRemindersCommand_SwitchesToReminders()
    {
        var vm = Create();
        vm.SetModeRemindersCommand.Execute(null);
        Assert.False(vm.IsModeNotes);
        Assert.True(vm.IsModeReminders);
    }

    [Fact]
    public void SetModeNotesCommand_SwitchesBack()
    {
        var vm = Create();
        vm.SetModeRemindersCommand.Execute(null);
        vm.SetModeNotesCommand.Execute(null);
        Assert.True(vm.IsModeNotes);
        Assert.False(vm.IsModeReminders);
    }

    // ── NavigateToReminder ────────────────────────────────────────────────────

    [Fact]
    public void NavigateToReminder_SwitchesToRemindersMode()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1"));
        var vm = Create(rs);

        Assert.True(vm.IsModeNotes); // starts in Notes

        vm.NavigateToReminder("r1");

        Assert.False(vm.IsModeNotes);
        Assert.True(vm.IsModeReminders);
    }

    [Fact]
    public void NavigateToReminder_HighlightsMatchingItem()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "First"));
        rs.Add(MakeReminder("r2", "Second"));
        var vm = Create(rs);

        vm.NavigateToReminder("r2");

        var item1 = vm.Reminders.First(r => r.Id == "r1");
        var item2 = vm.Reminders.First(r => r.Id == "r2");
        Assert.False(item1.IsHighlighted);
        Assert.True(item2.IsHighlighted);
    }

    [Fact]
    public void NavigateToReminder_ClearsHighlightOnOtherItems()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1"));
        rs.Add(MakeReminder("r2"));
        rs.Add(MakeReminder("r3"));
        var vm = Create(rs);

        vm.NavigateToReminder("r1");

        var highlighted = vm.Reminders.Where(r => r.IsHighlighted).ToList();
        Assert.Single(highlighted);
        Assert.Equal("r1", highlighted[0].Id);
    }

    [Fact]
    public void NavigateToReminder_UnknownId_DoesNotHighlightAnything()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1"));
        var vm = Create(rs);

        var ex = Record.Exception(() => vm.NavigateToReminder("nonexistent"));

        Assert.Null(ex);
        Assert.All(vm.Reminders, r => Assert.False(r.IsHighlighted));
    }

    [Fact]
    public void NavigateToReminder_EmptyCollection_DoesNotThrow()
    {
        var rs = new FakeReminderService();
        var vm = Create(rs);

        var ex = Record.Exception(() => vm.NavigateToReminder("r1"));

        Assert.Null(ex);
    }

    [Fact]
    public void NavigateToReminder_AlreadyInRemindersMode_StillHighlights()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1"));
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null); // already in Reminders mode

        vm.NavigateToReminder("r1");

        var item = vm.Reminders.First(r => r.Id == "r1");
        Assert.True(item.IsHighlighted);
    }

    [Fact]
    public void NavigateToReminder_RefreshesBeforeHighlighting_SeesNewReminder()
    {
        // The reminder is added AFTER the VM is constructed, then navigated to.
        // NavigateToReminder must call RefreshReminders() so the new item is present.
        var rs = new FakeReminderService();
        var vm = Create(rs);

        // Add directly to service without going through vm (simulates timer-driven fire)
        var entry = MakeReminder("r_late");
        rs.Add(entry); // fires RemindersChanged → RefreshReminders() auto-called via subscription
        // now explicitly navigate (mirrors OnNotificationNavigateRequested)
        vm.NavigateToReminder("r_late");

        var item = vm.Reminders.FirstOrDefault(r => r.Id == "r_late");
        Assert.NotNull(item);
        Assert.True(item!.IsHighlighted);
    }

    [Fact]
    public void NavigateToReminder_AfterRemindersChangedFires_StillHighlights()
    {
        // Even if RemindersChanged fires right before NavigateToReminder
        // (rebuilding the collection), the highlight must survive because
        // NavigateToReminder re-applies it on the fresh items.
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1"));
        var vm = Create(rs);

        rs.RaiseRemindersChanged(); // simulates a mid-flight RemindersChanged
        vm.NavigateToReminder("r1");

        Assert.True(vm.Reminders.First(r => r.Id == "r1").IsHighlighted);
    }

    // ── Reminders collection refresh ──────────────────────────────────────────

    [Fact]
    public void RemindersChanged_Event_RebuildsCollection()
    {
        var rs = new FakeReminderService();
        var vm = Create(rs);
        Assert.Empty(vm.Reminders);

        rs.Add(MakeReminder("r1")); // fires RemindersChanged

        Assert.Single(vm.Reminders);
        Assert.Equal("r1", vm.Reminders[0].Id);
    }

    [Fact]
    public void RefreshReminders_ClearsAndRebuilds()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1"));
        rs.Add(MakeReminder("r2"));
        var vm = Create(rs);

        Assert.Equal(2, vm.Reminders.Count);

        rs.Delete("r1"); // fires RemindersChanged

        Assert.Single(vm.Reminders);
        Assert.Equal("r2", vm.Reminders[0].Id);
    }

    [Fact]
    public void HasReminders_FalseWhenEmpty()
    {
        var vm = Create(new FakeReminderService());
        Assert.False(vm.HasReminders);
    }

    [Fact]
    public void HasReminders_TrueAfterAdd()
    {
        var rs = new FakeReminderService();
        var vm = Create(rs);
        rs.Add(MakeReminder("r1"));
        Assert.True(vm.HasReminders);
    }

    // ── DeleteReminderCommand ─────────────────────────────────────────────────

    [Fact]
    public void DeleteReminderCommand_RemovesItemFromCollection()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1"));
        rs.Add(MakeReminder("r2"));
        var vm = Create(rs);

        vm.DeleteReminderCommand.Execute("r1");

        Assert.Single(vm.Reminders);
        Assert.Equal("r2", vm.Reminders[0].Id);
    }

    [Fact]
    public void DeleteReminderCommand_NullId_DoesNotThrow()
    {
        var vm = Create(new FakeReminderService());
        var ex = Record.Exception(() => vm.DeleteReminderCommand.Execute(null));
        Assert.Null(ex);
    }

    // ── EditReminderCommand opens inline form ─────────────────────────────────

    [Fact]
    public void EditReminderCommand_OpensInlineForm()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", title: "My Meeting"));
        var vm = Create(rs);

        vm.EditReminderCommand.Execute("r1");

        Assert.True(vm.IsReminderFormOpen);
        Assert.Equal("My Meeting", vm.ReminderFormTitle);
    }

    // ── Labels ────────────────────────────────────────────────────────────────

    [Fact]
    public void Labels_ArePopulatedOnConstruction()
    {
        var vm = Create();
        Assert.False(string.IsNullOrEmpty(vm.RemindersHeadingLabel));
        Assert.False(string.IsNullOrEmpty(vm.NotesHeadingLabel));
        Assert.False(string.IsNullOrEmpty(vm.NoRemindersLabel));
        Assert.False(string.IsNullOrEmpty(vm.SaveLabel));
        Assert.False(string.IsNullOrEmpty(vm.DeleteLabel));
    }

    [Fact]
    public void OnLanguageChanged_UpdatesLabels()
    {
        var loc = MakeLoc("en");
        var vm = new MoreViewModel(loc);
        var enLabel = vm.RemindersHeadingLabel;

        loc.SetLanguage("ne");
        vm.OnLanguageChanged();

        Assert.NotEqual(enLabel, vm.RemindersHeadingLabel);
    }

    // ── ReminderItemViewModel ─────────────────────────────────────────────────

    [Fact]
    public void ReminderItem_IsHighlighted_DefaultsFalse()
    {
        var item = new ReminderItemViewModel(MakeReminder("r1"));
        Assert.False(item.IsHighlighted);
    }

    [Fact]
    public void ReminderItem_IsHighlighted_CanBeSet()
    {
        var item = new ReminderItemViewModel(MakeReminder("r1"));
        item.IsHighlighted = true;
        Assert.True(item.IsHighlighted);
    }

    [Fact]
    public void ReminderItem_Properties_MirrorEntry()
    {
        var entry = new ReminderEntry
        {
            Id = "test-id",
            Title = "My Reminder",
            BsDate = "2082/12/20",
            Time = "09:00",
            Notes = "Some notes",
            IsCompleted = false,
        };
        var item = new ReminderItemViewModel(entry);
        Assert.Equal("test-id", item.Id);
        Assert.Equal("My Reminder", item.Title);
        Assert.Equal("2082/12/20", item.Date);
        Assert.Equal("09:00", item.Time);
        Assert.Equal("Some notes", item.Notes);
        Assert.True(item.HasNotes);
        Assert.False(item.IsCompleted);
    }

    // ── ReminderFormNotes truncation (#27) ────────────────────────────────────

    [Fact]
    public void ReminderFormNotes_BelowLimit_StoresExact()
    {
        var vm = Create();

        vm.ReminderFormNotes = "hello";

        Assert.Equal("hello", vm.ReminderFormNotes);
    }

    [Fact]
    public void ReminderFormNotes_ExactlyAtLimit_Stored()
    {
        var vm = Create();
        var at500 = new string('a', 500);

        vm.ReminderFormNotes = at500;

        Assert.Equal(500, vm.ReminderFormNotes.Length);
        Assert.Equal(500, vm.ReminderFormNotesLength);
    }

    [Fact]
    public void ReminderFormNotes_ExceedsLimit_TruncatedAt500()
    {
        var vm = Create();
        var over = new string('z', 600);

        vm.ReminderFormNotes = over;

        Assert.Equal(500, vm.ReminderFormNotes.Length);
        Assert.Equal(500, vm.ReminderFormNotesLength);
    }

    [Fact]
    public void ReminderFormNotes_NullAssigned_TreatedAsEmpty()
    {
        var vm = Create();
        vm.ReminderFormNotes = "something";

        vm.ReminderFormNotes = null!;

        Assert.Equal(string.Empty, vm.ReminderFormNotes);
        Assert.Equal(0, vm.ReminderFormNotesLength);
    }

    [Fact]
    public void ReminderFormNotes_Empty_LengthIsZero()
    {
        var vm = Create();

        vm.ReminderFormNotes = "";

        Assert.Equal(0, vm.ReminderFormNotesLength);
    }

    // ── ReminderFormNotesLength counter raises PropertyChanged (#27) ──────────

    [Fact]
    public void ReminderFormNotes_Set_RaisesNotesLengthPropertyChanged()
    {
        var vm = Create();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.ReminderFormNotes = "hello";

        Assert.Contains(nameof(vm.ReminderFormNotesLength), raised);
    }

    [Fact]
    public void ReminderFormNotes_SetToSameValue_DoesNotRaiseLengthChanged()
    {
        var vm = Create();
        vm.ReminderFormNotes = "abc";

        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.ReminderFormNotes = "abc"; // same value, SetProperty should short-circuit

        Assert.DoesNotContain(nameof(vm.ReminderFormNotesLength), raised);
    }

    [Fact]
    public void ReminderFormNotesLength_TracksTruncatedLength_NotInputLength()
    {
        var vm = Create();

        vm.ReminderFormNotes = new string('x', 700);

        // Length must reflect what's actually stored (500), not what was input (700)
        Assert.Equal(500, vm.ReminderFormNotesLength);
    }

    // ── ShowReminderEndDate (#7) ──────────────────────────────────────────────

    [Fact]
    public void ShowReminderEndDate_DefaultFalse_WhenRecurrenceIsNone()
    {
        var vm = Create();

        Assert.Equal(0, vm.ReminderFormRecurrenceIndex);
        Assert.False(vm.ShowReminderEndDate);
    }

    [Fact]
    public void ShowReminderEndDate_TrueWhenRecurrenceIndexAboveZero()
    {
        var vm = Create();

        vm.ReminderFormRecurrenceIndex = 1;

        Assert.True(vm.ShowReminderEndDate);
    }

    [Fact]
    public void ShowReminderEndDate_BackToFalseWhenRecurrenceResetToZero()
    {
        var vm = Create();
        vm.ReminderFormRecurrenceIndex = 2;
        Assert.True(vm.ShowReminderEndDate);

        vm.ReminderFormRecurrenceIndex = 0;

        Assert.False(vm.ShowReminderEndDate);
    }

    [Fact]
    public void ReminderFormRecurrenceIndex_Set_RaisesShowReminderEndDatePropertyChanged()
    {
        var vm = Create();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.ReminderFormRecurrenceIndex = 1;

        Assert.Contains(nameof(vm.ShowReminderEndDate), raised);
    }

    // ── ReminderSearch / FilteredReminders (#8) ───────────────────────────────

    [Fact]
    public void ReminderSearchText_Empty_AllRemindersVisible()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Alpha Meeting"));
        rs.Add(MakeReminder("r2", "Beta Review"));
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        vm.ReminderSearchText = "";

        Assert.Equal(2, vm.FilteredReminders.Count);
    }

    [Fact]
    public void ReminderSearchText_MatchingPrefix_FiltersCorrectly()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Alpha Meeting"));
        rs.Add(MakeReminder("r2", "Beta Review"));
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        vm.ReminderSearchText = "alpha";

        Assert.Single(vm.FilteredReminders);
        Assert.Equal("r1", vm.FilteredReminders[0].Id);
    }

    [Fact]
    public void ReminderSearchText_CaseInsensitive()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "ALPHA"));
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        vm.ReminderSearchText = "alpha";

        Assert.Single(vm.FilteredReminders);
    }

    [Fact]
    public void ReminderSearchText_NoMatch_FilteredRemindersEmpty()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Alpha"));
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        vm.ReminderSearchText = "zzz";

        Assert.Empty(vm.FilteredReminders);
        Assert.False(vm.HasFilteredReminders);
    }

    [Fact]
    public void ReminderSearchText_NoMatch_ShowReminderNoResultsTrue()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Alpha"));
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        vm.ReminderSearchText = "zzz";

        Assert.True(vm.ShowReminderNoResults);
    }

    [Fact]
    public void IsReminderSearchActive_FalseWhenEmpty()
    {
        var vm = Create();

        vm.ReminderSearchText = "";

        Assert.False(vm.IsReminderSearchActive);
    }

    [Fact]
    public void IsReminderSearchActive_TrueWhenNonEmpty()
    {
        var vm = Create();

        vm.ReminderSearchText = "x";

        Assert.True(vm.IsReminderSearchActive);
        Assert.True(vm.ReminderSearchClearVisible);
    }

    [Fact]
    public void ClearReminderSearchCommand_ResetsSearchText()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Alpha"));
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);
        vm.ReminderSearchText = "alpha";

        vm.ClearReminderSearchCommand.Execute(null);

        Assert.Equal(string.Empty, vm.ReminderSearchText);
        Assert.False(vm.IsReminderSearchActive);
        Assert.Single(vm.FilteredReminders); // all back
    }

    [Fact]
    public void ReminderSearchText_Set_UpdatesFilteredRemindersImmediately()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Alpha"));
        rs.Add(MakeReminder("r2", "Beta"));
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        Assert.Equal(2, vm.FilteredReminders.Count);

        vm.ReminderSearchText = "beta";

        Assert.Single(vm.FilteredReminders);
        Assert.Equal("r2", vm.FilteredReminders[0].Id);
    }

    // ── Show / hide completed reminders ──────────────────────────────────────

    [Fact]
    public void CompletedReminders_HiddenByDefault()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Active"));
        rs.Add(new ReminderEntry { Id = "r2", Title = "Done", BsDate = "2082/12/20", Time = "09:00", IsCompleted = true });
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        Assert.False(vm.ShowCompletedReminders);
        Assert.Single(vm.FilteredReminders);
        Assert.Equal("r1", vm.FilteredReminders[0].Id);
    }

    [Fact]
    public void HasCompletedReminders_TrueWhenAnyCompleted()
    {
        var rs = new FakeReminderService();
        rs.Add(new ReminderEntry { Id = "r1", Title = "Done", BsDate = "2082/12/20", Time = "09:00", IsCompleted = true });
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        Assert.True(vm.HasCompletedReminders);
        Assert.Equal(1, vm.CompletedRemindersCount);
    }

    [Fact]
    public void HasCompletedReminders_FalseWhenNoneCompleted()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1"));
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        Assert.False(vm.HasCompletedReminders);
        Assert.Equal(0, vm.CompletedRemindersCount);
    }

    [Fact]
    public void ToggleShowCompletedCommand_ShowsCompletedReminders()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Active"));
        rs.Add(new ReminderEntry { Id = "r2", Title = "Done", BsDate = "2082/12/20", Time = "09:00", IsCompleted = true });
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        vm.ToggleShowCompletedCommand.Execute(null);

        Assert.True(vm.ShowCompletedReminders);
        Assert.Equal(2, vm.FilteredReminders.Count);
    }

    [Fact]
    public void ToggleShowCompletedCommand_HidesCompletedRemindersOnSecondToggle()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Active"));
        rs.Add(new ReminderEntry { Id = "r2", Title = "Done", BsDate = "2082/12/20", Time = "09:00", IsCompleted = true });
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        vm.ToggleShowCompletedCommand.Execute(null); // show
        vm.ToggleShowCompletedCommand.Execute(null); // hide again

        Assert.False(vm.ShowCompletedReminders);
        Assert.Single(vm.FilteredReminders);
    }

    [Fact]
    public void ToggleCompletedLabel_ReflectsCurrentState()
    {
        var rs = new FakeReminderService();
        rs.Add(new ReminderEntry { Id = "r1", Title = "Done", BsDate = "2082/12/20", Time = "09:00", IsCompleted = true });
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        var showLabel = vm.ToggleCompletedLabel;   // before toggle - should say "Show completed"
        vm.ToggleShowCompletedCommand.Execute(null);
        var hideLabel = vm.ToggleCompletedLabel;   // after toggle - should say "Hide completed"

        Assert.NotEqual(showLabel, hideLabel);
        Assert.NotEmpty(showLabel);
        Assert.NotEmpty(hideLabel);
    }

    [Fact]
    public void Search_ExcludesCompletedReminders_WhenHidden()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Meeting"));
        rs.Add(new ReminderEntry { Id = "r2", Title = "Meeting Done", BsDate = "2082/12/20", Time = "09:00", IsCompleted = true });
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        vm.ReminderSearchText = "meeting";

        // only the active one should match - completed is hidden
        Assert.Single(vm.FilteredReminders);
        Assert.Equal("r1", vm.FilteredReminders[0].Id);
    }

    [Fact]
    public void Search_IncludesCompletedReminders_WhenShown()
    {
        var rs = new FakeReminderService();
        rs.Add(MakeReminder("r1", "Meeting"));
        rs.Add(new ReminderEntry { Id = "r2", Title = "Meeting Done", BsDate = "2082/12/20", Time = "09:00", IsCompleted = true });
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);
        vm.ToggleShowCompletedCommand.Execute(null); // show completed

        vm.ReminderSearchText = "meeting";

        Assert.Equal(2, vm.FilteredReminders.Count);
    }

    [Fact]
    public void AllRemindersCompleted_FilteredListEmpty_WhenHidden()
    {
        var rs = new FakeReminderService();
        rs.Add(new ReminderEntry { Id = "r1", Title = "Done1", BsDate = "2082/12/20", Time = "09:00", IsCompleted = true });
        rs.Add(new ReminderEntry { Id = "r2", Title = "Done2", BsDate = "2082/12/20", Time = "09:00", IsCompleted = true });
        var vm = Create(rs);
        vm.SetModeRemindersCommand.Execute(null);

        Assert.True(vm.HasReminders);
        Assert.Empty(vm.FilteredReminders);
        Assert.True(vm.HasCompletedReminders);
        Assert.Equal(2, vm.CompletedRemindersCount);
    }
}
