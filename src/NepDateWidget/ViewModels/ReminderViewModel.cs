using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

public sealed class ReminderViewModel : ViewModelBase
{
    private readonly IReminderService _reminderService;
    private readonly ILocalizationService _loc;
    private readonly INepaliDateAdapter _adapter;

    public int BsYear { get; }
    public int BsMonth { get; }
    public int BsDay { get; }

    public string DateHeader { get; }

    public ObservableCollection<ReminderEntryViewModel> Reminders { get; } = new();

    /// <summary>
    /// Raised when the popup should close itself (after confirmed discard).
    /// </summary>
    public event Action? RequestClose;

    // ── Form state ────────────────────────────────────────────────────────────

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    private string? _editingId;
    private bool _isDirty;
    public bool IsDirty => _isDirty;

    private string _editTitle = string.Empty;
    public string EditTitle
    {
        get => _editTitle;
        set
        {
            if (SetProperty(ref _editTitle, value))
            {
                TitleError = string.Empty;
                _isDirty = true;
            }
        }
    }

    private string _editDate = string.Empty;
    public string EditDate
    {
        get => _editDate;
        set
        {
            if (SetProperty(ref _editDate, value))
            {
                DateError = string.Empty;
                HasDateError = false;
                _isDirty = true;
            }
        }
    }

    private string _dateError = string.Empty;
    public string DateError
    {
        get => _dateError;
        set => SetProperty(ref _dateError, value);
    }

    private bool _hasDateError;
    public bool HasDateError
    {
        get => _hasDateError;
        set => SetProperty(ref _hasDateError, value);
    }

    // Single time input (e.g. "9:30") + AM/PM toggle
    private string _editTime = "9:00";
    public string EditTime
    {
        get => _editTime;
        set
        {
            if (SetProperty(ref _editTime, value))
            {
                TimeError = string.Empty;
                HasTimeError = false;
                _isDirty = true;
            }
        }
    }

    private string _timeError = string.Empty;
    public string TimeError
    {
        get => _timeError;
        set => SetProperty(ref _timeError, value);
    }

    private bool _hasTimeError;
    public bool HasTimeError
    {
        get => _hasTimeError;
        set => SetProperty(ref _hasTimeError, value);
    }

    private bool _isAm = true;
    public bool IsAm
    {
        get => _isAm;
        set
        {
            if (SetProperty(ref _isAm, value))
            {
                OnPropertyChanged(nameof(IsPm));
                _isDirty = true;
            }
        }
    }
    public bool IsPm => !_isAm;

    private string _editNotes = string.Empty;
    public string EditNotes
    {
        get => _editNotes;
        set
        {
            if (SetProperty(ref _editNotes, value?.Length > 500 ? value[..500] : value ?? string.Empty))
                _isDirty = true;
        }
    }

    private int _editRecurrenceIndex;
    public int EditRecurrenceIndex
    {
        get => _editRecurrenceIndex;
        set
        {
            if (SetProperty(ref _editRecurrenceIndex, value))
                _isDirty = true;
        }
    }

    private string _editEndDate = string.Empty;
    public string EditEndDate
    {
        get => _editEndDate;
        set
        {
            if (SetProperty(ref _editEndDate, value))
            {
                EndDateError = string.Empty;
                HasEndDateError = false;
                _isDirty = true;
            }
        }
    }

    private string _endDateError = string.Empty;
    public string EndDateError
    {
        get => _endDateError;
        set => SetProperty(ref _endDateError, value);
    }

    private bool _hasEndDateError;
    public bool HasEndDateError
    {
        get => _hasEndDateError;
        set => SetProperty(ref _hasEndDateError, value);
    }

    private bool _showDiscardBanner;
    public bool ShowDiscardBanner
    {
        get => _showDiscardBanner;
        set => SetProperty(ref _showDiscardBanner, value);
    }

    private string _titleError = string.Empty;
    public string TitleError
    {
        get => _titleError;
        set => SetProperty(ref _titleError, value);
    }

    // ── Localized labels ──────────────────────────────────────────────────────

    public string PopupTitleLabel { get; }
    public string AddLabel { get; }
    public string TitleLabel { get; }
    public string TimeLabel { get; }
    public string DateLabel { get; }
    public string NotesLabel { get; }
    public string RecurrenceLabel { get; }
    public string EndDateLabel { get; }
    public string SaveLabel { get; }
    public string CancelLabel { get; }
    public string DeleteLabel { get; }
    public string NoRemindersLabel { get; }
    public string DateInvalidLabel { get; }
    public string DatePastLabel { get; }
    public string TimeInvalidLabel { get; }
    public string EndDateInvalidLabel { get; }
    public string EndDateBeforeStartLabel { get; }
    public string DiscardTitleLabel { get; }
    public string DiscardYesLabel { get; }
    public IReadOnlyList<string> RecurrenceOptions { get; }

    public string HintReminderTitle { get; }
    public string HintReminderDate  { get; }
    public string HintReminderTime  { get; }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand StartAddCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ToggleAmPmCommand { get; }
    public ICommand ConfirmDiscardCommand { get; }
    public ICommand CancelDiscardCommand { get; }

    public ReminderViewModel(
        IReminderService reminderService,
        ILocalizationService localizationService,
        INepaliDateAdapter adapter,
        int bsYear, int bsMonth, int bsDay)
    {
        _reminderService = reminderService;
        _loc = localizationService;
        _adapter = adapter;
        BsYear = bsYear;
        BsMonth = bsMonth;
        BsDay = bsDay;

        DateHeader = adapter.FormatBsLongEn(bsYear, bsMonth, bsDay);

        PopupTitleLabel = _loc.Get("reminder.popup_title");
        AddLabel = _loc.Get("reminder.add");
        TitleLabel = _loc.Get("reminder.title");
        TimeLabel = _loc.Get("reminder.time");
        DateLabel = _loc.Get("reminder.date");
        NotesLabel = _loc.Get("reminder.notes");
        RecurrenceLabel = _loc.Get("reminder.recurrence");
        EndDateLabel = _loc.Get("reminder.end_date");
        SaveLabel = _loc.Get("reminder.save");
        CancelLabel = _loc.Get("reminder.cancel");
        DeleteLabel = _loc.Get("reminder.delete");
        NoRemindersLabel = _loc.Get("reminder.no_reminders");
        DateInvalidLabel = _loc.Get("reminder.date_invalid");
        DatePastLabel = _loc.Get("reminder.date_past");
        TimeInvalidLabel = _loc.Get("reminder.time_invalid");
        EndDateInvalidLabel = _loc.Get("reminder.end_date_invalid");
        EndDateBeforeStartLabel = _loc.Get("reminder.end_date_before_start");
        DiscardTitleLabel = _loc.Get("reminder.discard_title");
        DiscardYesLabel = _loc.Get("reminder.discard_yes");
        HintReminderTitle = _loc.Get("hint.reminder_title");
        HintReminderDate  = _loc.Get("hint.date_bs");
        HintReminderTime  = _loc.Get("hint.reminder_time");
        RecurrenceOptions = new[]
        {
            _loc.Get("reminder.recurrence_none"),
            _loc.Get("reminder.recurrence_daily"),
            _loc.Get("reminder.recurrence_weekly"),
            _loc.Get("reminder.recurrence_monthly"),
            _loc.Get("reminder.recurrence_yearly"),
        };

        StartAddCommand = new RelayCommand(StartAdd);
        SaveCommand = new RelayCommand(DoSave);
        CancelEditCommand = new RelayCommand(CancelEdit);
        EditCommand = new RelayCommand<string>(StartEdit);
        DeleteCommand = new RelayCommand<string>(DoDelete);
        ToggleAmPmCommand = new RelayCommand(() => IsAm = !IsAm);
        ConfirmDiscardCommand = new RelayCommand(() => { ShowDiscardBanner = false; RequestClose?.Invoke(); });
        CancelDiscardCommand = new RelayCommand(() => ShowDiscardBanner = false);

        RefreshList();

        // Auto-show add form when no active reminders exist for this date
        if (Reminders.Count == 0 || Reminders.All(r => r.IsCompleted))
            StartAdd();
    }

    private void StartAdd()
    {
        _editingId = null;
        EditTitle = string.Empty;
        EditDate = _adapter.FormatBsShortEn(BsYear, BsMonth, BsDay);
        EditTime = "9:00";
        IsAm = true;
        EditNotes = string.Empty;
        EditRecurrenceIndex = 0;
        EditEndDate = string.Empty;
        TitleError = string.Empty;
        DateError = string.Empty;
        TimeError = string.Empty;
        EndDateError = string.Empty;
        HasDateError = false;
        HasTimeError = false;
        HasEndDateError = false;
        _isDirty = false;
        IsEditing = true;
    }

    private void StartEdit(string? id)
    {
        if (id is null) return;
        var entry = _reminderService.GetAll().FirstOrDefault(r => r.Id == id);
        if (entry is null) return;

        _editingId = id;
        EditTitle = entry.Title;
        EditDate = entry.BsDate;
        SetTimeFrom24(entry.Time);
        EditNotes = entry.Notes;
        EditRecurrenceIndex = (int)entry.Recurrence;
        EditEndDate = entry.EndDate ?? string.Empty;
        TitleError = string.Empty;
        DateError = string.Empty;
        TimeError = string.Empty;
        EndDateError = string.Empty;
        HasDateError = false;
        HasTimeError = false;
        HasEndDateError = false;
        _isDirty = false;
        IsEditing = true;
    }

    private void DoSave()
    {
        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            TitleError = _loc.Get("reminder.title_required");
            return;
        }

        // Validate date using NepDate
        if (!_adapter.TryParseSmartBsDate(EditDate, out int bsY, out int bsM, out int bsD))
        {
            DateError = DateInvalidLabel;
            HasDateError = true;
            return;
        }

        // Validate time
        if (!TryParseTime12(EditTime, IsAm, out string time24))
        {
            TimeError = TimeInvalidLabel;
            HasTimeError = true;
            return;
        }

        var recurrence = (ReminderRecurrence)EditRecurrenceIndex;

        // Past date check: reject one-shot past dates
        if (recurrence == ReminderRecurrence.None)
        {
            var adDate = _adapter.BsToAd(bsY, bsM, bsD);
            if (adDate is not null && adDate.Value.Date < DateTime.Now.Date)
            {
                DateError = DatePastLabel;
                HasDateError = true;
                return;
            }
        }

        HasDateError = false;
        HasTimeError = false;
        HasEndDateError = false;
        DateError = string.Empty;
        TimeError = string.Empty;
        EndDateError = string.Empty;

        string? endDate = string.IsNullOrWhiteSpace(EditEndDate) ? null : EditEndDate.Trim();

        // Validate end date if provided
        if (endDate is not null)
        {
            if (!_adapter.TryParseSmartBsDate(endDate, out int endY, out int endM, out int endD))
            {
                EndDateError = EndDateInvalidLabel;
                HasEndDateError = true;
                return;
            }

            // End date must be on or after start date
            var startAd = _adapter.BsToAd(bsY, bsM, bsD);
            var endAd = _adapter.BsToAd(endY, endM, endD);
            if (startAd is not null && endAd is not null && endAd.Value.Date < startAd.Value.Date)
            {
                EndDateError = EndDateBeforeStartLabel;
                HasEndDateError = true;
                return;
            }
        }

        if (_editingId is null)
        {
            var entry = new ReminderEntry
            {
                Title = EditTitle.Trim(),
                Notes = EditNotes,
                BsDate = ReminderEntry.FormatDate(bsY, bsM, bsD),
                Time = time24,
                Recurrence = recurrence,
                EndDate = endDate,
                CreatedUtc = DateTime.UtcNow,
            };
            _reminderService.Add(entry);
            Log.Action($"reminder added: {entry.Title} on {entry.BsDate}");
        }
        else
        {
            var existing = _reminderService.GetAll().FirstOrDefault(r => r.Id == _editingId);
            if (existing is not null)
            {
                existing.Title = EditTitle.Trim();
                existing.Notes = EditNotes;
                existing.BsDate = ReminderEntry.FormatDate(bsY, bsM, bsD);
                existing.Time = time24;
                existing.Recurrence = recurrence;
                existing.EndDate = endDate;
                _reminderService.Update(existing);
                Log.Action($"reminder updated: {existing.Title}");
            }
        }

        _isDirty = false;
        IsEditing = false;
        RefreshList();
    }

    private void CancelEdit()
    {
        _isDirty = false;
        IsEditing = false;
    }

    private void DoDelete(string? id)
    {
        if (id is null) return;
        _reminderService.Delete(id);
        Log.Action($"reminder deleted: {id}");
        RefreshList();
    }

    private void RefreshList()
    {
        Reminders.Clear();
        // Show reminders that are stored at this exact date
        var items = _reminderService.GetForDate(BsYear, BsMonth, BsDay);
        foreach (var r in items)
            Reminders.Add(new ReminderEntryViewModel(r, _loc));

        // Also show recurring reminders that land on this date
        foreach (var r in _reminderService.GetRecurringForDate(BsYear, BsMonth, BsDay))
        {
            // Avoid duplicates (already in list by exact date match)
            if (items.Any(i => i.Id == r.Id)) continue;
            Reminders.Add(new ReminderEntryViewModel(r, _loc));
        }
    }

    // ── Time helpers ──────────────────────────────────────────────────────────

    private static readonly Regex TimePattern = new(@"^(\d{1,2}):(\d{2})$", RegexOptions.Compiled);

    private void SetTimeFrom24(string hhmm)
    {
        if (!TimeSpan.TryParse(hhmm, out var ts))
        {
            EditTime = "9:00";
            IsAm = true;
            return;
        }

        int h = ts.Hours;
        int m = ts.Minutes;

        if (h == 0) { IsAm = true; EditTime = $"12:{m:D2}"; }
        else if (h < 12) { IsAm = true; EditTime = $"{h}:{m:D2}"; }
        else if (h == 12) { IsAm = false; EditTime = $"12:{m:D2}"; }
        else { IsAm = false; EditTime = $"{h - 12}:{m:D2}"; }
    }

    /// <summary>
    /// Parses "H:MM" or "HH:MM" with the given AM/PM flag into 24h "HH:mm".
    /// Returns false if the input is invalid.
    /// </summary>
    private static bool TryParseTime12(string input, bool isAm, out string time24)
    {
        time24 = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var match = TimePattern.Match(input.Trim());
        if (!match.Success) return false;

        int h = int.Parse(match.Groups[1].Value);
        int m = int.Parse(match.Groups[2].Value);

        if (h < 1 || h > 12 || m < 0 || m > 59) return false;

        int h24;
        if (isAm)
            h24 = h == 12 ? 0 : h;
        else
            h24 = h == 12 ? 12 : h + 12;

        time24 = $"{h24:D2}:{m:D2}";
        return true;
    }
}

public sealed class ReminderEntryViewModel
{
    public string Id { get; }
    public string Title { get; }
    public string Time12 { get; }
    public string Notes { get; }
    public string RecurrenceText { get; }
    public string EndDateText { get; }
    public bool HasNotes { get; }
    public bool IsCompleted { get; }
    public string CompletedLabel { get; }

    public ReminderEntryViewModel(ReminderEntry entry, ILocalizationService loc)
    {
        Id = entry.Id;
        Title = entry.Title;
        Notes = entry.Notes;
        HasNotes = !string.IsNullOrWhiteSpace(entry.Notes);
        IsCompleted = entry.IsCompleted;
        CompletedLabel = entry.IsCompleted ? loc.Get("reminder.completed") : string.Empty;
        Time12 = FormatTime12(entry.Time);

        RecurrenceText = entry.Recurrence switch
        {
            ReminderRecurrence.Daily => loc.Get("reminder.recurrence_daily"),
            ReminderRecurrence.Weekly => loc.Get("reminder.recurrence_weekly"),
            ReminderRecurrence.Monthly => loc.Get("reminder.recurrence_monthly"),
            ReminderRecurrence.Yearly => loc.Get("reminder.recurrence_yearly"),
            _ => string.Empty,
        };

        EndDateText = entry.EndDate ?? string.Empty;
    }

    private static string FormatTime12(string hhmm)
    {
        if (!TimeSpan.TryParse(hhmm, out var ts))
            return hhmm;

        int h = ts.Hours;
        int m = ts.Minutes;
        string period = h < 12 ? "AM" : "PM";
        int h12 = h % 12;
        if (h12 == 0) h12 = 12;
        return $"{h12}:{m:D2} {period}";
    }
}
