using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;

namespace NepDateWidget.ViewModels;

/// <summary>
/// Wraps a <see cref="CalendarDay"/> for display in the month grid.
/// Exposes everything the grid template needs as direct bindable properties.
/// </summary>
public sealed class CalendarDayViewModel : ViewModelBase
{
    private CalendarDay _day;

    public int BsYear => _day.Year;
    public int BsMonth => _day.Month;
    public int Day => _day.Day;
    public bool IsCurrentMonth => _day.IsCurrentMonth;
    public bool IsPadding => _day.IsPadding;
    public bool IsToday => _day.IsToday;
    public bool IsSaturday => _day.IsSaturday;
    public bool IsSunday => _day.IsSunday;
    public bool IsHighlighted => _day.IsHighlighted;

    private bool _hasReminders;
    public bool HasReminders
    {
        get => _hasReminders;
        set => SetProperty(ref _hasReminders, value);
    }

    private bool _hasNote;
    public bool HasNote
    {
        get => _hasNote;
        set => SetProperty(ref _hasNote, value);
    }

    private string? _reminderTooltip;
    public string? ReminderTooltip
    {
        get => _reminderTooltip;
        set => SetProperty(ref _reminderTooltip, value);
    }

    /// <summary>
    /// Main BS day number text. Shown in Nepali digits when isNepali is true.
    /// Empty for padding cells.
    /// </summary>
    private string _dayText = string.Empty;
    public string DayText { get => _dayText; private set => SetProperty(ref _dayText, value); }

    /// <summary>
    /// Small AD day number shown at the bottom-right of the cell (always Arabic digits).
    /// Empty for padding cells.
    /// </summary>
    private string _englishDayText = string.Empty;
    public string EnglishDayText { get => _englishDayText; private set => SetProperty(ref _englishDayText, value); }

    /// <summary>Whether to show the English day badge (factoring in the global toggle).</summary>
    private bool _showEnglishBadge;
    public bool ShowEnglishBadge { get => _showEnglishBadge; private set => SetProperty(ref _showEnglishBadge, value); }

    /// <summary>Whether to apply Saturday/holiday highlighting for this cell.</summary>
    private bool _showSaturdayHighlight;
    public bool ShowSaturdayHighlight { get => _showSaturdayHighlight; private set => SetProperty(ref _showSaturdayHighlight, value); }

    /// <summary>Whether to apply Sunday highlighting for this cell.</summary>
    private bool _showSundayHighlight;
    public bool ShowSundayHighlight { get => _showSundayHighlight; private set => SetProperty(ref _showSundayHighlight, value); }

    /// <summary>Lunar day (Tithi) text in the current widget language. Empty for padding cells.</summary>
    private string _tithiText = string.Empty;
    public string TithiText { get => _tithiText; private set => SetProperty(ref _tithiText, value); }

    /// <summary>True when TithiText is non-empty and the ShowTithi setting is on.</summary>
    private bool _showTithiText;
    public bool ShowTithiText { get => _showTithiText; private set => SetProperty(ref _showTithiText, value); }

    /// <summary>True when this day is Purnima (full moon), including Kshaya Purnima.</summary>
    private bool _isPurnima;
    public bool IsPurnima { get => _isPurnima; private set => SetProperty(ref _isPurnima, value); }

    /// <summary>True when this day is Aunsi (new moon / Amavasya).</summary>
    private bool _isAunsi;
    public bool IsAunsi { get => _isAunsi; private set => SetProperty(ref _isAunsi, value); }

    /// <summary>
    /// Events visible in the cell (bounded by the count computed from available cell height).
    /// Updated via <see cref="UpdateVisibleEventCount"/>.
    /// </summary>
    public IReadOnlyList<string> VisibleEvents => _visibleEventsArray;

    /// <summary>True when at least one event is visible.</summary>
    public bool HasVisibleEvents { get => _hasVisibleEvents; private set => SetProperty(ref _hasVisibleEvents, value); }

    /// <summary>True when there are more events than currently shown. Triggers "..." indicator.</summary>
    public bool HasHiddenEvents { get => _hasHiddenEvents; private set => SetProperty(ref _hasHiddenEvents, value); }

    /// <summary>True when this day is a public holiday and HighlightPublicHolidays is on.</summary>
    private bool _showHolidayHighlight;
    public bool ShowHolidayHighlight { get => _showHolidayHighlight; private set => SetProperty(ref _showHolidayHighlight, value); }

    /// <summary>
    /// Pre-built rows for the right-click "copy date" context menu.
    /// Empty for padding cells, which suppresses the menu entirely.
    /// Built once per VM instance using the current language; the calendar
    /// rebuilds every cell on language change so this stays in sync.
    /// </summary>
    private IReadOnlyList<DateFormatOption> _copyFormatOptions = Array.Empty<DateFormatOption>();
    public IReadOnlyList<DateFormatOption> CopyFormatOptions { get => _copyFormatOptions; private set => SetProperty(ref _copyFormatOptions, value); }

    /// <summary>True when CopyFormatOptions has at least one row (used by XAML to gate the menu).</summary>
    public bool HasCopyOptions => _copyFormatOptions.Count > 0;

    /// <summary>Localized "Copy" label shown as a non-clickable header at the top of the right-click menu.</summary>
    private string _copyMenuTitle = string.Empty;
    public string CopyMenuTitle { get => _copyMenuTitle; private set => SetProperty(ref _copyMenuTitle, value); }

    // ── Multi-event state ────────────────────────────────────────────────────
    private string[] _allEvents;
    private bool _canShowEvents;
    private string[] _visibleEventsArray = Array.Empty<string>();
    private bool _hasVisibleEvents;
    private bool _hasHiddenEvents;

    private static string ToNepaliDigits(int n) => NepaliScriptConverter.ToNepaliDigits(n);

    public CalendarDayViewModel(CalendarDay day, bool isNepali = false,
                                bool showEnglishDayNumbers = true, bool highlightSaturdays = true,
                                bool highlightSundays = false, bool showTithi = true,
                                bool showEvents = true, bool highlightPublicHolidays = true,
                                INepaliDateAdapter? adapter = null,
                                ILocalizationService? localization = null)
    {
        _day = day ?? throw new ArgumentNullException(nameof(day));

        DayText = day.IsCurrentMonth
            ? (isNepali ? ToNepaliDigits(day.Day) : day.Day.ToString())
            : string.Empty;

        EnglishDayText = (day.IsCurrentMonth && day.AdDay > 0)
            ? day.AdDay.ToString()
            : string.Empty;

        ShowEnglishBadge = day.IsCurrentMonth && day.AdDay > 0 && showEnglishDayNumbers;
        ShowSaturdayHighlight = day.IsSaturday && highlightSaturdays;
        ShowSundayHighlight = day.IsSunday && highlightSundays;

        // Tithi: language-aware, only shown for current-month cells
        string rawTithi = isNepali ? day.TithiNp : day.TithiEn;
        TithiText = day.IsCurrentMonth ? rawTithi : string.Empty;
        ShowTithiText = day.IsCurrentMonth && showTithi && !string.IsNullOrEmpty(rawTithi);
        IsPurnima = day.IsCurrentMonth && day.TithiEn.StartsWith("Purnima", StringComparison.Ordinal);
        IsAunsi = day.IsCurrentMonth && day.TithiEn == "Aunsi";

        // Multi-event: store all events and initialise with 1 visible row
        string[] events = isNepali ? day.EventsNp : day.EventsEn;
        _allEvents = (day.IsCurrentMonth && showEvents) ? events : Array.Empty<string>();
        _canShowEvents = day.IsCurrentMonth && showEvents;
        if (_allEvents.Length > 0)
        {
            string firstText = _allEvents.Length > 1
                ? _allEvents[0] + " +" + (_allEvents.Length - 1)
                : _allEvents[0];
            _visibleEventsArray = new[] { firstText };
            _hasVisibleEvents = true;
            _hasHiddenEvents = _allEvents.Length > 1;
        }

        // Public holiday: same highlight color as Saturday/Sunday
        ShowHolidayHighlight = day.IsCurrentMonth && day.IsPublicHoliday && highlightPublicHolidays;

        // Copy-date menu options: only for current-month cells.
        // If CalendarDay carries pre-computed format strings (populated by CalendarService
        // via GetCellData), use those directly to avoid any further adapter/NepaliDate calls.
        // Fall back to the adapter-based path only when those strings are absent (test stubs).
        if (day.IsCurrentMonth && localization is not null)
        {
            if (day.AdDate.HasValue && !string.IsNullOrEmpty(day.BsShortEn))
            {
                CopyFormatOptions = DateFormatter.Build(day, localization, isNepali);
            }
            else if (adapter is not null)
            {
                CopyFormatOptions = DateFormatter.Build(day.Year, day.Month, day.Day, adapter, localization, isNepali);
            }
            if (CopyFormatOptions.Count > 0)
            {
                CopyMenuTitle = localization.Get("calendar.copy.title");
            }
        }
    }

    /// <summary>
    /// Updates this VM in-place with new <paramref name="day"/> data and settings.
    /// Raises PropertyChanged only for values that actually changed, avoiding full
    /// container teardown when <see cref="CalendarViewModel.RefreshGrid"/> navigates months.
    /// </summary>
    internal void Update(
        CalendarDay day,
        bool isNepali,
        bool showEnglishDayNumbers,
        bool highlightSaturdays,
        bool highlightSundays,
        bool showTithi,
        bool showEvents,
        bool highlightPublicHolidays,
        INepaliDateAdapter? adapter,
        ILocalizationService? localization,
        int visibleEventCount = 1)
    {
        // Capture all _day-delegating property values BEFORE replacing _day so we can
        // compare and only raise PropertyChanged for values that actually changed.
        // 42 cells × up to 9 events = up to 378 notifications per navigation; in practice
        // IsSaturday/IsSunday never change (column position is fixed), IsToday rarely changes,
        // and padding status only flips on slow-path navigations — so most properties are
        // silent on most navigations.
        int oldBsYear        = _day.Year;
        int oldBsMonth       = _day.Month;
        int oldDay           = _day.Day;
        bool oldIsCurrentMonth = _day.IsCurrentMonth;
        bool oldIsPadding    = _day.IsPadding;
        bool oldIsToday      = _day.IsToday;
        bool oldIsSaturday   = _day.IsSaturday;
        bool oldIsSunday     = _day.IsSunday;
        bool oldIsHighlighted = _day.IsHighlighted;

        _day = day ?? throw new ArgumentNullException(nameof(day));

        if (oldBsYear           != _day.Year)           OnPropertyChanged(nameof(BsYear));
        if (oldBsMonth          != _day.Month)          OnPropertyChanged(nameof(BsMonth));
        if (oldDay              != _day.Day)            OnPropertyChanged(nameof(Day));
        if (oldIsCurrentMonth   != _day.IsCurrentMonth) OnPropertyChanged(nameof(IsCurrentMonth));
        if (oldIsPadding        != _day.IsPadding)      OnPropertyChanged(nameof(IsPadding));
        if (oldIsToday          != _day.IsToday)        OnPropertyChanged(nameof(IsToday));
        if (oldIsSaturday       != _day.IsSaturday)     OnPropertyChanged(nameof(IsSaturday));
        if (oldIsSunday         != _day.IsSunday)       OnPropertyChanged(nameof(IsSunday));
        if (oldIsHighlighted    != _day.IsHighlighted)  OnPropertyChanged(nameof(IsHighlighted));

        DayText = day.IsCurrentMonth
            ? (isNepali ? ToNepaliDigits(day.Day) : day.Day.ToString())
            : string.Empty;

        EnglishDayText = (day.IsCurrentMonth && day.AdDay > 0)
            ? day.AdDay.ToString()
            : string.Empty;

        ShowEnglishBadge = day.IsCurrentMonth && day.AdDay > 0 && showEnglishDayNumbers;
        ShowSaturdayHighlight = day.IsSaturday && highlightSaturdays;
        ShowSundayHighlight = day.IsSunday && highlightSundays;

        string rawTithi = isNepali ? day.TithiNp : day.TithiEn;
        TithiText = day.IsCurrentMonth ? rawTithi : string.Empty;
        ShowTithiText = day.IsCurrentMonth && showTithi && !string.IsNullOrEmpty(rawTithi);
        IsPurnima = day.IsCurrentMonth && day.TithiEn.StartsWith("Purnima", StringComparison.Ordinal);
        IsAunsi = day.IsCurrentMonth && day.TithiEn == "Aunsi";

        string[] events = isNepali ? day.EventsNp : day.EventsEn;
        _allEvents = (day.IsCurrentMonth && showEvents) ? events : Array.Empty<string>();
        _canShowEvents = day.IsCurrentMonth && showEvents;
        UpdateVisibleEventCount(visibleEventCount);

        ShowHolidayHighlight = day.IsCurrentMonth && day.IsPublicHoliday && highlightPublicHolidays;

        IReadOnlyList<DateFormatOption> newOpts = Array.Empty<DateFormatOption>();
        if (day.IsCurrentMonth && localization is not null)
        {
            if (day.AdDate.HasValue && !string.IsNullOrEmpty(day.BsShortEn))
            {
                newOpts = DateFormatter.Build(day, localization, isNepali);
            }
            else if (adapter is not null)
            {
                newOpts = DateFormatter.Build(day.Year, day.Month, day.Day, adapter, localization, isNepali);
            }
        }
        CopyFormatOptions = newOpts;
        OnPropertyChanged(nameof(HasCopyOptions));
        CopyMenuTitle = (localization is not null && newOpts.Count > 0)
            ? localization.Get("calendar.copy.title")
            : string.Empty;
    }

    /// <summary>
    /// Called by <see cref="CalendarViewModel.UpdateCellLayout"/> when available cell height changes.
    /// Updates the visible event count without rebuilding the VM.
    /// </summary>
    public void UpdateVisibleEventCount(int count)
    {
        if (!_canShowEvents || _allEvents.Length == 0)
        {
            // No events to show. Clear any previously-visible events with proper
            // notifications so WPF bindings (VisibleEvents, HasVisibleEvents) update.
            if (_visibleEventsArray.Length > 0)
            {
                _visibleEventsArray = Array.Empty<string>();
                OnPropertyChanged(nameof(VisibleEvents));
            }
            HasVisibleEvents = false;
            HasHiddenEvents = false;
            return;
        }

        string[] newVisible;
        bool newHasHidden;

        if (count <= 0)
        {
            newVisible = Array.Empty<string>();
            newHasHidden = false;
        }
        else if (count >= _allEvents.Length)
        {
            newVisible = _allEvents;
            newHasHidden = false;
        }
        else
        {
            // Show 'count' real events; append " +N" to the last one so the
            // overflow indicator sits on the same line (trimmed by TextTrimming).
            int hiddenCount = _allEvents.Length - count;
            newVisible = new string[count];
            for (int i = 0; i < count; i++)
            {
                newVisible[i] = _allEvents[i];
            }

            newVisible[count - 1] = _allEvents[count - 1] + " +" + hiddenCount;
            newHasHidden = true;
        }

        // Length alone doesn’t detect “+N” text changes when count stays equal.
        bool contentSame = newVisible.Length == _visibleEventsArray.Length;
        if (contentSame)
        {
            for (int i = 0; i < newVisible.Length; i++)
            {
                if (newVisible[i] != _visibleEventsArray[i]) { contentSame = false; break; }
            }
        }

        if (contentSame && newHasHidden == _hasHiddenEvents)
        {
            return;
        }

        _visibleEventsArray = newVisible;
        OnPropertyChanged(nameof(VisibleEvents));
        HasHiddenEvents = newHasHidden;
        HasVisibleEvents = newVisible.Length > 0;
    }
}
