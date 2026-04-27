using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

public sealed class ReminderViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;
    private readonly FakeNepaliDateAdapter _adapter;
    private readonly LocalizationService _loc;

    public ReminderViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"NepDateWidget_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "reminders.json");
        _adapter = new FakeNepaliDateAdapter();
        _loc = new LocalizationService();
        _loc.SetLanguage("en");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private ReminderService CreateService()
    {
        var svc = new ReminderService(_filePath, _adapter);
        svc.Load();
        return svc;
    }

    private ReminderViewModel CreateVm(IReminderService? svc = null, int y = 2082, int m = 12, int d = 20)
        => new(svc ?? CreateService(), _loc, _adapter, y, m, d);

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsDateHeader()
    {
        var vm = CreateVm();
        Assert.Equal("Chaitra 20, 2082", vm.DateHeader);
    }

    [Fact]
    public void Constructor_NoReminders_AutoShowsAddForm()
    {
        var vm = CreateVm();
        Assert.True(vm.IsEditing);
    }

    [Fact]
    public void Constructor_WithActiveReminder_DoesNotAutoShowForm()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "Exists", BsDate = "2082/12/20", Time = "09:00" });
        var vm = CreateVm(svc);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void Constructor_AllCompletedReminders_AutoShowsAddForm()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "Done", BsDate = "2082/12/20", Time = "09:00", IsCompleted = true });
        var vm = CreateVm(svc);
        Assert.True(vm.IsEditing);
    }

    [Fact]
    public void Constructor_PopulatesLabels()
    {
        var vm = CreateVm();
        Assert.False(string.IsNullOrEmpty(vm.PopupTitleLabel));
        Assert.False(string.IsNullOrEmpty(vm.SaveLabel));
        Assert.False(string.IsNullOrEmpty(vm.CancelLabel));
        Assert.False(string.IsNullOrEmpty(vm.DeleteLabel));
        Assert.False(string.IsNullOrEmpty(vm.NoRemindersLabel));
    }

    [Fact]
    public void Constructor_RecurrenceOptionsHas4Items()
    {
        var vm = CreateVm();
        Assert.Equal(4, vm.RecurrenceOptions.Count);
    }

    // ── StartAddCommand ───────────────────────────────────────────────────────

    [Fact]
    public void StartAddCommand_SetsIsEditing()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "X", BsDate = "2082/12/20", Time = "09:00" });
        var vm = CreateVm(svc);
        Assert.False(vm.IsEditing);

        vm.StartAddCommand.Execute(null);

        Assert.True(vm.IsEditing);
        Assert.Equal(string.Empty, vm.EditTitle);
        Assert.Equal("9:00", vm.EditTime);
        Assert.True(vm.IsAm);
        Assert.Equal(string.Empty, vm.EditNotes);
        Assert.Equal(0, vm.EditRecurrenceIndex);
        Assert.Equal(string.Empty, vm.EditEndDate);
    }

    [Fact]
    public void StartAddCommand_ClearsErrors()
    {
        var vm = CreateVm();
        // Trigger a title error first
        vm.SaveCommand.Execute(null);
        Assert.False(string.IsNullOrEmpty(vm.TitleError));

        // Start add again
        vm.StartAddCommand.Execute(null);
        Assert.Equal(string.Empty, vm.TitleError);
        Assert.Equal(string.Empty, vm.DateError);
        Assert.Equal(string.Empty, vm.TimeError);
        Assert.Equal(string.Empty, vm.EndDateError);
        Assert.False(vm.HasDateError);
        Assert.False(vm.HasTimeError);
        Assert.False(vm.HasEndDateError);
    }

    // ── SaveCommand validation ────────────────────────────────────────────────

    [Fact]
    public void SaveCommand_EmptyTitle_SetsTitleError()
    {
        var vm = CreateVm();
        vm.EditTitle = "";
        vm.SaveCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.TitleError));
        Assert.True(vm.IsEditing);
    }

    [Fact]
    public void SaveCommand_WhitespaceTitle_SetsTitleError()
    {
        var vm = CreateVm();
        vm.EditTitle = "   ";
        vm.SaveCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.TitleError));
    }

    [Fact]
    public void SaveCommand_InvalidDate_SetsDateError()
    {
        var vm = CreateVm();
        vm.EditTitle = "Test";
        vm.EditDate = "invalid";
        vm.SaveCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.DateError));
        Assert.True(vm.HasDateError);
    }

    [Fact]
    public void SaveCommand_InvalidTime_SetsTimeError()
    {
        var vm = CreateVm();
        vm.EditTitle = "Test";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "abc";
        vm.SaveCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.TimeError));
        Assert.True(vm.HasTimeError);
    }

    [Fact]
    public void SaveCommand_InvalidEndDate_SetsEndDateError()
    {
        var vm = CreateVm();
        vm.EditTitle = "Test";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "9:00";
        vm.EditRecurrenceIndex = 1; // Daily
        vm.EditEndDate = "invalid";
        vm.SaveCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.EndDateError));
        Assert.True(vm.HasEndDateError);
    }

    [Fact]
    public void SaveCommand_EndDateBeforeStart_SetsEndDateError()
    {
        var vm = CreateVm();
        vm.EditTitle = "Test";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "9:00";
        vm.EditRecurrenceIndex = 1;
        vm.EditEndDate = "2082/12/10"; // Before start
        vm.SaveCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.EndDateError));
        Assert.True(vm.HasEndDateError);
    }

    // ── SaveCommand success ───────────────────────────────────────────────────

    [Fact]
    public void SaveCommand_ValidNewEntry_AddsToService()
    {
        var svc = CreateService();
        var vm = CreateVm(svc);

        vm.EditTitle = "New Reminder";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "9:00";
        vm.IsAm = true;
        vm.EditRecurrenceIndex = 1; // Daily recurrence bypasses past-date check
        vm.SaveCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Single(svc.GetAll());
        Assert.Equal("New Reminder", svc.GetAll()[0].Title);
        Assert.Equal("09:00", svc.GetAll()[0].Time);
    }

    [Fact]
    public void SaveCommand_ValidNewEntry_PMTime_Converts()
    {
        var svc = CreateService();
        var vm = CreateVm(svc);

        vm.EditTitle = "PM Reminder";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "3:30";
        vm.IsAm = false;
        vm.EditRecurrenceIndex = 1; // Daily recurrence bypasses past-date check
        vm.SaveCommand.Execute(null);

        Assert.Equal("15:30", svc.GetAll()[0].Time);
    }

    [Fact]
    public void SaveCommand_ValidEdit_UpdatesExisting()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Original",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.Daily, // Recurring to bypass past-date check
        });
        var vm = CreateVm(svc);
        var id = svc.GetAll()[0].Id;

        vm.EditCommand.Execute(id);
        vm.EditTitle = "Updated";
        vm.SaveCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Single(svc.GetAll());
        Assert.Equal("Updated", svc.GetAll()[0].Title);
    }

    [Fact]
    public void SaveCommand_WithRecurrence_SetsRecurrence()
    {
        var svc = CreateService();
        var vm = CreateVm(svc);

        vm.EditTitle = "Daily Reminder";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "9:00";
        vm.EditRecurrenceIndex = 1; // Daily
        vm.SaveCommand.Execute(null);

        Assert.Equal(ReminderRecurrence.Daily, svc.GetAll()[0].Recurrence);
    }

    [Fact]
    public void SaveCommand_WithEndDate_SetsEndDate()
    {
        var svc = CreateService();
        var vm = CreateVm(svc);

        vm.EditTitle = "Ends";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "9:00";
        vm.EditRecurrenceIndex = 1;
        vm.EditEndDate = "2082/12/25";
        vm.SaveCommand.Execute(null);

        Assert.Equal("2082/12/25", svc.GetAll()[0].EndDate);
    }

    // ── EditCommand ───────────────────────────────────────────────────────────

    [Fact]
    public void EditCommand_PopulatesForm()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Edit Me",
            BsDate = "2082/12/20",
            Time = "14:30",
            Notes = "Some notes",
            Recurrence = ReminderRecurrence.Weekly,
            EndDate = "2082/12/30",
        });
        var vm = CreateVm(svc);
        var id = svc.GetAll()[0].Id;

        vm.EditCommand.Execute(id);

        Assert.True(vm.IsEditing);
        Assert.Equal("Edit Me", vm.EditTitle);
        Assert.Equal("2082/12/20", vm.EditDate);
        Assert.Equal("2:30", vm.EditTime);
        Assert.False(vm.IsAm); // 14:30 = PM
        Assert.Equal("Some notes", vm.EditNotes);
        Assert.Equal(2, vm.EditRecurrenceIndex); // Weekly
        Assert.Equal("2082/12/30", vm.EditEndDate);
    }

    [Fact]
    public void EditCommand_NullId_NoOp()
    {
        var vm = CreateVm();
        vm.IsEditing.GetType(); // ensure no throw
        vm.EditCommand.Execute(null);
        // Should not crash
    }

    [Fact]
    public void EditCommand_NonexistentId_NoOp()
    {
        var vm = CreateVm();
        vm.EditCommand.Execute("nonexistent");
        // IsEditing may or may not change depending on auto-show; just ensure no crash
    }

    // ── DeleteCommand ─────────────────────────────────────────────────────────

    [Fact]
    public void DeleteCommand_RemovesReminder()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "Delete Me", BsDate = "2082/12/20", Time = "09:00" });
        var vm = CreateVm(svc);
        var id = svc.GetAll()[0].Id;

        vm.DeleteCommand.Execute(id);

        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void DeleteCommand_RecurringReminder_DeletesDirectly()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Recurring",
            BsDate = "2082/12/20",
            Time = "09:00",
            Recurrence = ReminderRecurrence.Daily,
        });
        var vm = CreateVm(svc);
        var id = svc.GetAll()[0].Id;

        // After removing "this only" feature, recurring delete should be immediate
        vm.DeleteCommand.Execute(id);

        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void DeleteCommand_NullId_NoOp()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "Keep", BsDate = "2082/12/20", Time = "09:00" });
        var vm = CreateVm(svc);

        vm.DeleteCommand.Execute(null);
        Assert.Single(svc.GetAll());
    }

    [Fact]
    public void DeleteCommand_RefreshesList()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry { Title = "A", BsDate = "2082/12/20", Time = "09:00" });
        svc.Add(new ReminderEntry { Title = "B", BsDate = "2082/12/20", Time = "10:00" });
        var vm = CreateVm(svc);
        Assert.Equal(2, vm.Reminders.Count);

        vm.DeleteCommand.Execute(svc.GetAll()[0].Id);
        Assert.Single(vm.Reminders);
    }

    // ── CancelEditCommand ─────────────────────────────────────────────────────

    [Fact]
    public void CancelEditCommand_HidesForm()
    {
        var vm = CreateVm();
        Assert.True(vm.IsEditing); // auto-shown
        vm.CancelEditCommand.Execute(null);
        Assert.False(vm.IsEditing);
    }

    // ── ToggleAmPmCommand ─────────────────────────────────────────────────────

    [Fact]
    public void ToggleAmPmCommand_FlipsIsAm()
    {
        var vm = CreateVm();
        Assert.True(vm.IsAm);

        vm.ToggleAmPmCommand.Execute(null);
        Assert.False(vm.IsAm);
        Assert.True(vm.IsPm);

        vm.ToggleAmPmCommand.Execute(null);
        Assert.True(vm.IsAm);
        Assert.False(vm.IsPm);
    }

    // ── ConfirmDiscardCommand / CancelDiscardCommand ──────────────────────────

    [Fact]
    public void ConfirmDiscardCommand_HidesBannerAndRaisesClose()
    {
        var vm = CreateVm();
        vm.ShowDiscardBanner = true;
        bool closed = false;
        vm.RequestClose += () => closed = true;

        vm.ConfirmDiscardCommand.Execute(null);

        Assert.False(vm.ShowDiscardBanner);
        Assert.True(closed);
    }

    [Fact]
    public void CancelDiscardCommand_HidesBanner()
    {
        var vm = CreateVm();
        vm.ShowDiscardBanner = true;

        vm.CancelDiscardCommand.Execute(null);

        Assert.False(vm.ShowDiscardBanner);
    }

    // ── Property setters clear errors ─────────────────────────────────────────

    [Fact]
    public void EditTitle_Set_ClearsTitleError()
    {
        var vm = CreateVm();
        vm.SaveCommand.Execute(null); // triggers title error
        Assert.False(string.IsNullOrEmpty(vm.TitleError));

        vm.EditTitle = "Fixed";
        Assert.Equal(string.Empty, vm.TitleError);
    }

    [Fact]
    public void EditDate_Set_ClearsDateError()
    {
        var vm = CreateVm();
        vm.EditTitle = "Test";
        vm.EditDate = "invalid";
        vm.SaveCommand.Execute(null);
        Assert.True(vm.HasDateError);

        vm.EditDate = "2082/12/20";
        Assert.Equal(string.Empty, vm.DateError);
        Assert.False(vm.HasDateError);
    }

    [Fact]
    public void EditTime_Set_ClearsTimeError()
    {
        var vm = CreateVm();
        vm.EditTitle = "Test";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "abc";
        vm.SaveCommand.Execute(null);
        Assert.True(vm.HasTimeError);

        vm.EditTime = "9:00";
        Assert.Equal(string.Empty, vm.TimeError);
        Assert.False(vm.HasTimeError);
    }

    [Fact]
    public void EditNotes_TruncatesAt500()
    {
        var vm = CreateVm();
        vm.EditNotes = new string('A', 600);
        Assert.Equal(500, vm.EditNotes.Length);
    }

    // ── RefreshList shows recurring reminders ─────────────────────────────────

    [Fact]
    public void RefreshList_ShowsRecurringRemindersOnTargetDate()
    {
        var svc = CreateService();
        // Add a daily recurring reminder anchored at day 10
        svc.Add(new ReminderEntry
        {
            Title = "Daily",
            BsDate = "2082/12/10",
            Recurrence = ReminderRecurrence.Daily,
            Time = "09:00",
        });

        // View for day 20 (10 days later). Daily recurrence should show it.
        var vm = CreateVm(svc, 2082, 12, 20);
        Assert.Single(vm.Reminders);
        Assert.Equal("Daily", vm.Reminders[0].Title);
    }

    [Fact]
    public void RefreshList_NoDuplicatesForExactDateAndRecurring()
    {
        var svc = CreateService();
        svc.Add(new ReminderEntry
        {
            Title = "Daily",
            BsDate = "2082/12/20",
            Recurrence = ReminderRecurrence.Daily,
            Time = "09:00",
        });

        // Exact date match + recurrence should not duplicate
        var vm = CreateVm(svc, 2082, 12, 20);
        Assert.Single(vm.Reminders);
    }

    // ── ReminderEntryViewModel ────────────────────────────────────────────────

    [Theory]
    [InlineData("09:00", "9:00 AM")]
    [InlineData("00:00", "12:00 AM")]
    [InlineData("12:00", "12:00 PM")]
    [InlineData("13:30", "1:30 PM")]
    [InlineData("23:59", "11:59 PM")]
    public void ReminderEntryViewModel_Time12_FormatsCorrectly(string time24, string expected)
    {
        var entry = new ReminderEntry { Title = "T", BsDate = "2082/12/20", Time = time24 };
        var evm = new ReminderEntryViewModel(entry, _loc);
        Assert.Equal(expected, evm.Time12);
    }

    [Fact]
    public void ReminderEntryViewModel_RecurrenceText_Maps()
    {
        var daily = new ReminderEntry { Title = "T", BsDate = "2082/12/20", Recurrence = ReminderRecurrence.Daily };
        var evm = new ReminderEntryViewModel(daily, _loc);
        Assert.False(string.IsNullOrEmpty(evm.RecurrenceText));

        var none = new ReminderEntry { Title = "T", BsDate = "2082/12/20", Recurrence = ReminderRecurrence.None };
        var evm2 = new ReminderEntryViewModel(none, _loc);
        Assert.Equal(string.Empty, evm2.RecurrenceText);
    }

    [Fact]
    public void ReminderEntryViewModel_HasNotes_WhenNotesExist()
    {
        var entry = new ReminderEntry { Title = "T", BsDate = "2082/12/20", Notes = "Note" };
        var evm = new ReminderEntryViewModel(entry, _loc);
        Assert.True(evm.HasNotes);
    }

    [Fact]
    public void ReminderEntryViewModel_HasNotes_FalseWhenEmpty()
    {
        var entry = new ReminderEntry { Title = "T", BsDate = "2082/12/20", Notes = "" };
        var evm = new ReminderEntryViewModel(entry, _loc);
        Assert.False(evm.HasNotes);
    }

    [Fact]
    public void ReminderEntryViewModel_CompletedLabel_WhenCompleted()
    {
        var entry = new ReminderEntry { Title = "T", BsDate = "2082/12/20", IsCompleted = true };
        var evm = new ReminderEntryViewModel(entry, _loc);
        Assert.True(evm.IsCompleted);
        Assert.False(string.IsNullOrEmpty(evm.CompletedLabel));
    }

    [Fact]
    public void ReminderEntryViewModel_CompletedLabel_EmptyWhenNotCompleted()
    {
        var entry = new ReminderEntry { Title = "T", BsDate = "2082/12/20", IsCompleted = false };
        var evm = new ReminderEntryViewModel(entry, _loc);
        Assert.False(evm.IsCompleted);
        Assert.Equal(string.Empty, evm.CompletedLabel);
    }

    // ── Time conversion edge cases ────────────────────────────────────────────

    [Fact]
    public void SaveCommand_12AM_ConvertsTo0000()
    {
        var svc = CreateService();
        var vm = CreateVm(svc);
        vm.EditTitle = "Midnight";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "12:00";
        vm.IsAm = true;
        vm.EditRecurrenceIndex = 1; // Daily recurrence bypasses past-date check
        vm.SaveCommand.Execute(null);

        Assert.Equal("00:00", svc.GetAll()[0].Time);
    }

    [Fact]
    public void SaveCommand_12PM_ConvertsTo1200()
    {
        var svc = CreateService();
        var vm = CreateVm(svc);
        vm.EditTitle = "Noon";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "12:00";
        vm.IsAm = false;
        vm.EditRecurrenceIndex = 1; // Daily recurrence bypasses past-date check
        vm.SaveCommand.Execute(null);

        Assert.Equal("12:00", svc.GetAll()[0].Time);
    }

    [Fact]
    public void SaveCommand_TimeOutOfRange_SetsTimeError()
    {
        var vm = CreateVm();
        vm.EditTitle = "Test";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "13:00"; // > 12 in 12h format
        vm.SaveCommand.Execute(null);

        Assert.True(vm.HasTimeError);
    }

    [Fact]
    public void SaveCommand_MinuteOutOfRange_SetsTimeError()
    {
        var vm = CreateVm();
        vm.EditTitle = "Test";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "9:60";
        vm.SaveCommand.Execute(null);

        Assert.True(vm.HasTimeError);
    }

    [Fact]
    public void SaveCommand_Hour0_SetsTimeError()
    {
        var vm = CreateVm();
        vm.EditTitle = "Test";
        vm.EditDate = "2082/12/20";
        vm.EditTime = "0:00"; // 0 not valid in 12h format
        vm.SaveCommand.Execute(null);

        Assert.True(vm.HasTimeError);
    }
}
