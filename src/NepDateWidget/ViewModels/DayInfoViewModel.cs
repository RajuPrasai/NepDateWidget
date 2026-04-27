using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// ViewModel for the day-detail popup.
/// Shows calendar metadata (tithi, public holiday, events), per-day note, and a reminder summary.
/// The note is stored in WidgetSettings.DayNotes (keyed "YYYY-MM-DD" in BS).
/// </summary>
public sealed class DayInfoViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly INotesService? _notesService;
    private readonly IReminderService? _reminderService;
    private readonly INepaliDateAdapter _adapter;
    private readonly ILocalizationService _loc;
    private readonly int _bsYear, _bsMonth, _bsDay;
    private readonly string _dateKey;  // "YYYY-MM-DD"
    private bool _isNepali;

    // ── Event to signal the View ────────────────────────────────────────────

    /// <summary>Raised when the popup should close.</summary>
    public event Action? RequestClose;

    /// <summary>Raised when the user wants to open the reminder add form for this date.</summary>
    public event Action<int, int, int>? AddReminderRequested;

    // ── Date header ─────────────────────────────────────────────────────────

    public string BsDateLong { get; }
    public string AdDateLong { get; }
    public string DayOfWeekLabel { get; }

    // ── Calendar info ───────────────────────────────────────────────────────

    public bool IsHoliday { get; }
    public string HolidayBadgeLabel { get; }
    public string TithiText { get; }
    public bool HasTithi => !string.IsNullOrEmpty(TithiText);

    public IReadOnlyList<string> Events { get; }
    public bool HasEvents => Events.Count > 0;

    // ── Note ────────────────────────────────────────────────────────────────

    private string _noteText = string.Empty;
    public string NoteText
    {
        get => _noteText;
        set => SetProperty(ref _noteText, value);
    }

    private bool _isEditingNote;
    public bool IsEditingNote
    {
        get => _isEditingNote;
        set
        {
            if (SetProperty(ref _isEditingNote, value))
            {
                OnPropertyChanged(nameof(IsNotEditingNote));
                OnPropertyChanged(nameof(NoteEditButtonLabel));
            }
        }
    }
    public bool IsNotEditingNote => !_isEditingNote;

    private string _noteEditBuffer = string.Empty;
    public string NoteEditBuffer
    {
        get => _noteEditBuffer;
        set => SetProperty(ref _noteEditBuffer, value);
    }

    public bool HasExistingNote => !string.IsNullOrEmpty(_noteText);

    // ── Reminders ────────────────────────────────────────────────────────────

    public ObservableCollection<string> ReminderTitles { get; } = new();
    public bool HasReminders => ReminderTitles.Count > 0;

    // ── Labels ──────────────────────────────────────────────────────────────

    public string TithiLabel { get; }
    public string EventsLabel { get; }
    public string NoEventsLabel { get; }
    public string NoteLabel { get; }
    public string NoteEditButtonLabel => _isEditingNote
        ? _loc.Get("dayinfo.save_note")
        : (HasExistingNote ? _loc.Get("dayinfo.edit_note") : _loc.Get("dayinfo.add_note"));
    public string CancelLabel { get; }
    public string AddReminderLabel { get; }
    public string RemindersLabel { get; }
    public string NoRemindersLabel { get; }
    public string NoNoteLabel { get; }

    // ── Commands ────────────────────────────────────────────────────────────

    public ICommand ToggleNoteEditCommand { get; }
    public ICommand CancelNoteCommand { get; }
    public ICommand AddReminderCommand { get; }
    public ICommand CloseCommand { get; }

    // ── Construction ────────────────────────────────────────────────────────

    public DayInfoViewModel(
        int bsYear, int bsMonth, int bsDay,
        ISettingsService settingsService,
        INepaliDateAdapter adapter,
        ILocalizationService localizationService,
        IReminderService? reminderService = null,
        INotesService? notesService = null)
    {
        _bsYear = bsYear;
        _bsMonth = bsMonth;
        _bsDay = bsDay;
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _reminderService = reminderService;
        _notesService = notesService;
        _dateKey = $"{bsYear:D4}-{bsMonth:D2}-{bsDay:D2}";
        _isNepali = string.Equals(_loc.CurrentLanguage, "ne", StringComparison.OrdinalIgnoreCase);

        // Build date header
        BsDateLong = adapter.FormatBsLongEn(bsYear, bsMonth, bsDay);
        var adDate = adapter.BsToAd(bsYear, bsMonth, bsDay);
        AdDateLong = adDate.HasValue ? adDate.Value.ToString("MMMM d, yyyy") : string.Empty;
        DayOfWeekLabel = adDate.HasValue
            ? adDate.Value.DayOfWeek.ToString()
            : string.Empty;

        // Calendar info
        var (isHoliday, tithiEn, tithiNp, eventsEn, eventsNp) = adapter.GetCalendarInfo(bsYear, bsMonth, bsDay);
        IsHoliday = isHoliday;
        HolidayBadgeLabel = _loc.Get("dayinfo.holiday_badge");
        TithiText = _isNepali ? tithiNp : tithiEn;
        Events = (_isNepali ? eventsNp : eventsEn).ToList().AsReadOnly();

        // Note from notes service (or fallback to settings for backward compat)
        _noteText = _notesService?.GetNote(_dateKey) ?? string.Empty;

        // Reminders
        LoadReminders();

        // Labels
        TithiLabel = _loc.Get("dayinfo.tithi_label");
        EventsLabel = _loc.Get("dayinfo.events_label");
        NoEventsLabel = _loc.Get("dayinfo.no_events");
        NoteLabel = _loc.Get("dayinfo.note_label");
        CancelLabel = _loc.Get("dayinfo.cancel");
        AddReminderLabel = _loc.Get("dayinfo.add_reminder");
        RemindersLabel = _loc.Get("dayinfo.reminders_label");
        NoRemindersLabel = _loc.Get("dayinfo.no_reminders");
        NoNoteLabel = _loc.Get("dayinfo.no_note");

        // Commands
        ToggleNoteEditCommand = new RelayCommand(ToggleNoteEdit);
        CancelNoteCommand = new RelayCommand(CancelNote);
        AddReminderCommand = new RelayCommand(DoAddReminder);
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
    }

    // ── Private ─────────────────────────────────────────────────────────────

    private void ToggleNoteEdit()
    {
        if (_isEditingNote)
        {
            // Save
            string trimmed = _noteEditBuffer.Trim();
            NoteText = trimmed;
            _notesService?.SetNote(_dateKey, string.IsNullOrEmpty(trimmed) ? null : trimmed);
            IsEditingNote = false;
            OnPropertyChanged(nameof(HasExistingNote));
            OnPropertyChanged(nameof(NoteEditButtonLabel));
        }
        else
        {
            // Enter edit mode with current text pre-filled
            NoteEditBuffer = _noteText;
            IsEditingNote = true;
        }
    }

    private void CancelNote()
    {
        NoteEditBuffer = string.Empty;
        IsEditingNote = false;
    }

    private void DoAddReminder()
    {
        RequestClose?.Invoke();
        AddReminderRequested?.Invoke(_bsYear, _bsMonth, _bsDay);
    }

    private void LoadReminders()
    {
        ReminderTitles.Clear();
        if (_reminderService is null) return;

        foreach (var r in _reminderService.GetForDate(_bsYear, _bsMonth, _bsDay))
            if (!r.IsCompleted) ReminderTitles.Add(r.Title);

        foreach (var r in _reminderService.GetRecurringForDate(_bsYear, _bsMonth, _bsDay))
            ReminderTitles.Add(r.Title);

        OnPropertyChanged(nameof(HasReminders));
    }
}
