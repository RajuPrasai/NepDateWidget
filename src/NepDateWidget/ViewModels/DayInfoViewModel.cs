using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>Reminder row shown in the day-detail popup.</summary>
public sealed record PopupReminderItem(string Id, string Title);

/// <summary>
/// ViewModel for the day-detail popup.
/// Shows calendar metadata (tithi, public holiday, events), per-day note, and a reminder summary.
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

    // ── Events ──────────────────────────────────────────────────────────────

    /// <summary>Raised when the popup should close.</summary>
    public event Action? RequestClose;

    /// <summary>
    /// Raised when the user wants to add/edit a note or add a reminder.
    /// mode=0 = Notes tab, mode=1 = Reminders tab.
    /// </summary>
    public event Action<int, string>? NavigateToMoreRequested;

    /// <summary>Raised when the user wants to edit an existing reminder by ID.</summary>
    public event Action<string>? EditReminderRequested;

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

    // ── Note ─────────────────────────────────────────────────────────────────

    private string _noteText = string.Empty;
    public string NoteText
    {
        get => _noteText;
        private set
        {
            if (SetProperty(ref _noteText, value))
                OnPropertyChanged(nameof(HasExistingNote));
        }
    }
    public bool HasExistingNote => !string.IsNullOrEmpty(_noteText);

    // ── Reminders ────────────────────────────────────────────────────────────

    public ObservableCollection<PopupReminderItem> Reminders { get; } = new();
    public bool HasReminders => Reminders.Count > 0;

    // ── Labels ──────────────────────────────────────────────────────────────

    public string TithiLabel { get; }
    public string EventsLabel { get; }
    public string NoEventsLabel { get; }
    public string NoteLabel { get; }
    public string AddNoteLabel { get; }
    public string EditNoteLabel { get; }
    public string AddReminderLabel { get; }
    public string RemindersLabel { get; }
    public string NoRemindersLabel { get; }
    public string NoNoteLabel { get; }
    public string DeleteLabel { get; }

    // ── Commands ────────────────────────────────────────────────────────────

    public ICommand AddNoteCommand { get; }
    public ICommand EditNoteCommand { get; }
    public ICommand DeleteNoteCommand { get; }
    public ICommand AddReminderCommand { get; }
    public ICommand EditReminderCommand { get; }
    public ICommand DeleteReminderCommand { get; }
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

        // Note
        _noteText = _notesService?.GetNote(_dateKey) ?? string.Empty;

        // Reminders
        LoadReminders();

        // Labels
        TithiLabel       = _loc.Get("dayinfo.tithi_label");
        EventsLabel      = _loc.Get("dayinfo.events_label");
        NoEventsLabel    = _loc.Get("dayinfo.no_events");
        NoteLabel        = _loc.Get("dayinfo.note_label");
        AddNoteLabel     = _loc.Get("dayinfo.add_note");
        EditNoteLabel    = _loc.Get("dayinfo.edit_note");
        AddReminderLabel = _loc.Get("dayinfo.add_reminder");
        RemindersLabel   = _loc.Get("dayinfo.reminders_label");
        NoRemindersLabel = _loc.Get("dayinfo.no_reminders");
        NoNoteLabel      = _loc.Get("dayinfo.no_note");
        DeleteLabel      = _loc.Get("dayinfo.delete");

        // Commands
        AddNoteCommand       = new RelayCommand(DoAddNote);
        EditNoteCommand      = new RelayCommand(DoEditNote);
        DeleteNoteCommand    = new RelayCommand(DoDeleteNote);
        AddReminderCommand   = new RelayCommand(DoAddReminder);
        EditReminderCommand  = new RelayCommand<string>(DoEditReminder);
        DeleteReminderCommand = new RelayCommand<string>(DoDeleteReminder);
        CloseCommand         = new RelayCommand(() => RequestClose?.Invoke());
    }

    // ── Private ─────────────────────────────────────────────────────────────

    private void DoAddNote()
    {
        RequestClose?.Invoke();
        NavigateToMoreRequested?.Invoke(0, _dateKey);
    }

    private void DoEditNote()
    {
        RequestClose?.Invoke();
        NavigateToMoreRequested?.Invoke(0, _dateKey);
    }

    private void DoDeleteNote()
    {
        if (_notesService is null) return;
        _notesService.SetNote(_dateKey, null);
        RequestClose?.Invoke();
    }

    private void DoAddReminder()
    {
        RequestClose?.Invoke();
        NavigateToMoreRequested?.Invoke(1, _dateKey);
    }

    private void DoEditReminder(string? id)
    {
        if (id is null) return;
        RequestClose?.Invoke();
        EditReminderRequested?.Invoke(id);
    }

    private void DoDeleteReminder(string? id)
    {
        if (id is null || _reminderService is null) return;
        _reminderService.Delete(id);
        RequestClose?.Invoke();
    }

    private void LoadReminders()
    {
        Reminders.Clear();
        if (_reminderService is null) return;

        foreach (var r in _reminderService.GetForDate(_bsYear, _bsMonth, _bsDay))
            if (!r.IsCompleted) Reminders.Add(new PopupReminderItem(r.Id, r.Title));

        foreach (var r in _reminderService.GetRecurringForDate(_bsYear, _bsMonth, _bsDay))
            Reminders.Add(new PopupReminderItem(r.Id, r.Title));

        OnPropertyChanged(nameof(HasReminders));
    }
}
