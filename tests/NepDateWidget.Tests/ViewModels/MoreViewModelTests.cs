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
            if (idx >= 0) _items[idx] = entry;
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

        public void RaiseRemindersChanged() => RemindersChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeNotesService : INotesService
    {
        private readonly Dictionary<string, string> _notes = new();

        public event EventHandler? NotesChanged;

        public string? GetNote(string dateKey) => _notes.GetValueOrDefault(dateKey);
        public IReadOnlyDictionary<string, string> GetAll() => _notes;
        public void SetNote(string dateKey, string? text)
        {
            if (string.IsNullOrEmpty(text)) _notes.Remove(dateKey);
            else _notes[dateKey] = text;
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
        var loc = new LocalizationService();
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
}
