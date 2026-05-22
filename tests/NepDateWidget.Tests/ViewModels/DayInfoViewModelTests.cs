using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests for DayInfoViewModel - the day-detail popup.
/// Covers: date header, note display, reminder display, commands, navigation events.
/// </summary>
public sealed class DayInfoViewModelTests
{
    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeSettings : ISettingsService
    {
        public WidgetSettings Current { get; } = new();
        public bool IsFirstLaunch => false;
        public void Load() { }
        public void Save() { }
        public void ResetToDefaults() { }
    }

    private sealed class FakeNotes : INotesService
    {
        private readonly Dictionary<string, string> _data = new();
        public event EventHandler? NotesChanged;

        public FakeNotes(string? key = null, string? text = null)
        {
            if (key != null && text != null)
            {
                _data[key] = text;
            }
        }

        public string? GetNote(string dateKey) => _data.GetValueOrDefault(dateKey);
        public IReadOnlyDictionary<string, string> GetAll() => _data;
        public HashSet<int> GetHasNotesForMonth(int bsYear, int bsMonth) => new();
        public void SetNote(string dateKey, string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) _data.Remove(dateKey);
            else _data[dateKey] = text;
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }
        public void DeleteNote(string dateKey)
        {
            _data.Remove(dateKey);
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Load() { }
        public void Save() { }
    }

    private sealed class FakeReminders : IReminderService
    {
        private readonly List<ReminderEntry> _specific;
        private readonly List<ReminderEntry> _recurring;

        public event EventHandler? RemindersChanged;

        public FakeReminders(
            IEnumerable<ReminderEntry>? specific = null,
            IEnumerable<ReminderEntry>? recurring = null)
        {
            _specific = specific?.ToList() ?? new();
            _recurring = recurring?.ToList() ?? new();
        }

        public IReadOnlyList<ReminderEntry> GetAll() => _specific.Concat(_recurring).ToList();
        public IReadOnlyList<ReminderEntry> GetForDate(int y, int m, int d) => _specific;
        public bool HasRemindersForDate(int y, int m, int d) => _specific.Count > 0;
        public bool HasRemindersForDateExpanded(int y, int m, int d) => false;
        public IReadOnlyList<ReminderEntry> GetRecurringForDate(int y, int m, int d) => _recurring;
        public HashSet<int> GetHasRemindersForMonth(int y, int m) => new();
        public void Add(ReminderEntry entry) { _specific.Add(entry); RemindersChanged?.Invoke(this, EventArgs.Empty); }
        public void Update(ReminderEntry entry)
        {
            var idx = _specific.FindIndex(r => r.Id == entry.Id);
            if (idx >= 0) _specific[idx] = entry;
        }
        public void Delete(string id)
        {
            _specific.RemoveAll(r => r.Id == id);
            _recurring.RemoveAll(r => r.Id == id);
            RemindersChanged?.Invoke(this, EventArgs.Empty);
        }
        public void Load() { }
        public void Save() { }
        public IReadOnlyList<ReminderEntry> CheckAndFireDueReminders(DateTime nowUtc) => Array.Empty<ReminderEntry>();
        public IReadOnlyList<ReminderEntry> GetMissedReminders() => Array.Empty<ReminderEntry>();
    }

    private static LocalizationService MakeLoc(string lang = "en")
    {
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage(lang);
        return loc;
    }

    private static DayInfoViewModel MakeVm(
        int bsYear = 2082, int bsMonth = 12, int bsDay = 20,
        FakeNotes? notes = null,
        FakeReminders? reminders = null,
        string lang = "en",
        INepaliDateAdapter? adapter = null)
    {
        return new DayInfoViewModel(
            bsYear, bsMonth, bsDay,
            new FakeSettings(),
            adapter ?? new FakeNepaliDateAdapter(),
            MakeLoc(lang),
            reminders,
            notes);
    }

    // ── Date header ───────────────────────────────────────────────────────────

    [Fact]
    public void BsDateLong_ContainsMonthName()
    {
        // FakeAdapter.FormatBsLongEn returns "MonthName day, year"
        // For 2082-12-20 that is "Chaitra 20, 2082"
        var vm = MakeVm(2082, 12, 20);
        Assert.Contains("Chaitra", vm.BsDateLong);
        Assert.Contains("2082", vm.BsDateLong);
    }

    [Fact]
    public void AdDateLong_ContainsMonthAndYear()
    {
        // BS 2082/12/20 → AD 2026-04-03 (April 3, 2026)
        // "MMMM d, yyyy" format produces "April 3, 2026"
        var vm = MakeVm(2082, 12, 20);
        Assert.Contains("April", vm.AdDateLong);
        Assert.Contains("2026", vm.AdDateLong);
    }

    [Fact]
    public void DayOfWeekLabel_NotEmpty_ForValidDate()
    {
        // BS 2082/12/20 = AD 2026-04-03 = Friday
        var vm = MakeVm(2082, 12, 20);
        Assert.NotEmpty(vm.DayOfWeekLabel);
        Assert.Equal("Friday", vm.DayOfWeekLabel);
    }

    [Fact]
    public void DayOfWeekLabel_Empty_WhenAdConversionFails()
    {
        // A fake adapter that returns null for all BsToAd
        var vm = MakeVm(1800, 1, 1); // out-of-range → BsToAd returns null
        Assert.Empty(vm.AdDateLong);
        Assert.Empty(vm.DayOfWeekLabel);
    }

    // ── Note state ────────────────────────────────────────────────────────────

    [Fact]
    public void NoNotes_HasExistingNote_False()
    {
        var vm = MakeVm(notes: new FakeNotes());
        Assert.False(vm.HasExistingNote);
        Assert.Empty(vm.NoteText);
    }

    [Fact]
    public void WithNote_HasExistingNote_True()
    {
        // FormatKey(2082, 12, 20) = "2082-12-20"
        var key = "2082-12-20";
        var vm = MakeVm(notes: new FakeNotes(key, "Meeting with client"));
        Assert.True(vm.HasExistingNote);
        Assert.Equal("Meeting with client", vm.NoteText);
    }

    [Fact]
    public void NoNotesService_HasExistingNote_False()
    {
        // null notesService is valid; should not throw
        var vm = MakeVm();
        Assert.False(vm.HasExistingNote);
    }

    // ── Reminder state ────────────────────────────────────────────────────────

    [Fact]
    public void NoReminders_HasReminders_False()
    {
        var vm = MakeVm(reminders: new FakeReminders());
        Assert.False(vm.HasReminders);
        Assert.Empty(vm.Reminders);
    }

    [Fact]
    public void SpecificReminder_LoadedIntoCollection()
    {
        var reminder = new ReminderEntry { Id = "r1", Title = "Doctor visit", BsDate = "2082/12/20" };
        var vm = MakeVm(reminders: new FakeReminders(specific: new[] { reminder }));
        Assert.True(vm.HasReminders);
        Assert.Single(vm.Reminders);
        Assert.Equal("r1", vm.Reminders[0].Id);
        Assert.Equal("Doctor visit", vm.Reminders[0].Title);
    }

    [Fact]
    public void CompletedReminder_IsNotShownInPopup()
    {
        var completed = new ReminderEntry { Id = "r1", Title = "Done task", BsDate = "2082/12/20", IsCompleted = true };
        var vm = MakeVm(reminders: new FakeReminders(specific: new[] { completed }));
        Assert.False(vm.HasReminders);
    }

    [Fact]
    public void RecurringReminder_LoadedIntoCollection()
    {
        var recurring = new ReminderEntry { Id = "rec1", Title = "Weekly standup", BsDate = "2082/12/20" };
        var vm = MakeVm(reminders: new FakeReminders(recurring: new[] { recurring }));
        Assert.True(vm.HasReminders);
        Assert.Single(vm.Reminders);
        Assert.Equal("rec1", vm.Reminders[0].Id);
    }

    [Fact]
    public void NoReminderService_HasReminders_False()
    {
        var vm = MakeVm();
        Assert.False(vm.HasReminders);
    }

    // ── IsHoliday / HasTithi / HasEvents ─────────────────────────────────────

    [Fact]
    public void DefaultAdapter_IsHoliday_False()
    {
        // FakeNepaliDateAdapter.GetCalendarInfo always returns isHoliday=false
        var vm = MakeVm();
        Assert.False(vm.IsHoliday);
    }

    [Fact]
    public void DefaultAdapter_HasTithi_False()
    {
        var vm = MakeVm();
        Assert.False(vm.HasTithi);
    }

    [Fact]
    public void DefaultAdapter_HasEvents_False()
    {
        var vm = MakeVm();
        Assert.False(vm.HasEvents);
        Assert.Empty(vm.Events);
    }

    // ── Labels ────────────────────────────────────────────────────────────────

    [Fact]
    public void AllLabels_NonEmpty_English()
    {
        var vm = MakeVm();
        Assert.NotEmpty(vm.TithiLabel);
        Assert.NotEmpty(vm.EventsLabel);
        Assert.NotEmpty(vm.NoteLabel);
        Assert.NotEmpty(vm.AddNoteLabel);
        Assert.NotEmpty(vm.EditNoteLabel);
        Assert.NotEmpty(vm.AddReminderLabel);
        Assert.NotEmpty(vm.RemindersLabel);
        Assert.NotEmpty(vm.NoRemindersLabel);
        Assert.NotEmpty(vm.NoNoteLabel);
        Assert.NotEmpty(vm.DeleteLabel);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [Fact]
    public void AllCommands_NotNull()
    {
        var vm = MakeVm();
        Assert.NotNull(vm.AddNoteCommand);
        Assert.NotNull(vm.EditNoteCommand);
        Assert.NotNull(vm.DeleteNoteCommand);
        Assert.NotNull(vm.AddReminderCommand);
        Assert.NotNull(vm.EditReminderCommand);
        Assert.NotNull(vm.DeleteReminderCommand);
        Assert.NotNull(vm.CloseCommand);
    }

    [Fact]
    public void CloseCommand_FiresRequestClose()
    {
        var vm = MakeVm();
        bool fired = false;
        vm.RequestClose += () => fired = true;

        vm.CloseCommand.Execute(null);

        Assert.True(fired);
    }

    [Fact]
    public void AddNoteCommand_FiresRequestCloseAndNavigatesToNotes()
    {
        var vm = MakeVm();
        bool closed = false;
        int? navigatedMode = null;
        vm.RequestClose += () => closed = true;
        vm.NavigateToMoreRequested += (mode, _) => navigatedMode = mode;

        vm.AddNoteCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(0, navigatedMode); // mode 0 = Notes tab
    }

    [Fact]
    public void EditNoteCommand_FiresRequestCloseAndNavigatesToNotes()
    {
        var vm = MakeVm();
        bool closed = false;
        int? navigatedMode = null;
        vm.RequestClose += () => closed = true;
        vm.NavigateToMoreRequested += (mode, _) => navigatedMode = mode;

        vm.EditNoteCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(0, navigatedMode);
    }

    [Fact]
    public void AddNoteCommand_NavigationIncludesCorrectDateKey()
    {
        // FormatKey(2082, 12, 20) = "2082-12-20"
        var vm = MakeVm(2082, 12, 20);
        string? receivedKey = null;
        vm.NavigateToMoreRequested += (_, key) => receivedKey = key;

        vm.AddNoteCommand.Execute(null);

        Assert.Equal("2082-12-20", receivedKey);
    }

    [Fact]
    public void AddReminderCommand_NavigatesToRemindersTab()
    {
        var vm = MakeVm();
        int? mode = null;
        vm.NavigateToMoreRequested += (m, _) => mode = m;

        vm.AddReminderCommand.Execute(null);

        Assert.Equal(1, mode); // mode 1 = Reminders tab
    }

    [Fact]
    public void EditReminderCommand_NullId_DoesNotFireClose()
    {
        var vm = MakeVm();
        bool closed = false;
        vm.RequestClose += () => closed = true;

        vm.EditReminderCommand.Execute(null);

        Assert.False(closed);
    }

    [Fact]
    public void EditReminderCommand_ValidId_FiresEditReminderRequested()
    {
        var vm = MakeVm();
        string? editedId = null;
        vm.EditReminderRequested += id => editedId = id;

        vm.EditReminderCommand.Execute("r42");

        Assert.Equal("r42", editedId);
    }

    [Fact]
    public void DeleteNoteCommand_NullNotesService_DoesNotThrow()
    {
        // No notesService passed - should be a no-op
        var vm = MakeVm();
        var ex = Record.Exception(() => vm.DeleteNoteCommand.Execute(null));
        Assert.Null(ex);
    }

    [Fact]
    public void DeleteNoteCommand_WithNotesService_RemovesNoteAndCloses()
    {
        var key = "2082-12-20";
        var notes = new FakeNotes(key, "Some note");
        var vm = MakeVm(notes: notes);
        bool closed = false;
        vm.RequestClose += () => closed = true;

        vm.DeleteNoteCommand.Execute(null);

        Assert.True(closed);
        Assert.Null(notes.GetNote(key));
    }

    [Fact]
    public void DeleteReminderCommand_NullId_DoesNotFireClose()
    {
        var reminders = new FakeReminders(specific: new[] { new ReminderEntry { Id = "r1", Title = "T" } });
        var vm = MakeVm(reminders: reminders);
        bool closed = false;
        vm.RequestClose += () => closed = true;

        vm.DeleteReminderCommand.Execute(null);

        Assert.False(closed);
    }

    [Fact]
    public void DeleteReminderCommand_ValidId_RemovesAndCloses()
    {
        var reminders = new FakeReminders(specific: new[] { new ReminderEntry { Id = "r1", Title = "T" } });
        var vm = MakeVm(reminders: reminders);
        bool closed = false;
        vm.RequestClose += () => closed = true;

        vm.DeleteReminderCommand.Execute("r1");

        Assert.True(closed);
        Assert.Empty(reminders.GetAll());
    }

    [Fact]
    public void DeleteReminderCommand_NullReminderService_DoesNotThrow()
    {
        var vm = MakeVm(); // no reminder service
        var ex = Record.Exception(() => vm.DeleteReminderCommand.Execute("anyId"));
        Assert.Null(ex);
    }
}
