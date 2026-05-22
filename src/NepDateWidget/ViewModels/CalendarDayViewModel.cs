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
    public bool ShowSaturdayHighlight
    {
        get => _showSaturdayHighlight;
        private set
        {
            bool oldTint = ShowWeekendTint;
            if (SetProperty(ref _showSaturdayHighlight, value) && oldTint != ShowWeekendTint)
                OnPropertyChanged(nameof(ShowWeekendTint));
        }
    }

    /// <summary>Whether to apply Sunday highlighting for this cell.</summary>
    private bool _showSundayHighlight;
    public bool ShowSundayHighlight
    {
        get => _showSundayHighlight;
        private set
        {
            bool oldTint = ShowWeekendTint;
            if (SetProperty(ref _showSundayHighlight, value) && oldTint != ShowWeekendTint)
                OnPropertyChanged(nameof(ShowWeekendTint));
        }
    }

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

    /// <summary>First visible event text slot. Empty when no events are shown.</summary>
    private string _eventText0 = string.Empty;
    public string EventText0 { get => _eventText0; private set => SetProperty(ref _eventText0, value); }

    /// <summary>Second visible event text slot. Empty when fewer than two events are shown.</summary>
    private string _eventText1 = string.Empty;
    public string EventText1
    {
        get => _eventText1;
        private set
        {
            if (SetProperty(ref _eventText1, value))
                OnPropertyChanged(nameof(HasEventText1));
        }
    }

    /// <summary>True when EventText1 has content. Used by XAML to show/hide the second event TextBlock.</summary>
    public bool HasEventText1 => !string.IsNullOrEmpty(_eventText1);

    /// <summary>Third visible event text slot. Empty when fewer than three events are shown.</summary>
    private string _eventText2 = string.Empty;
    public string EventText2
    {
        get => _eventText2;
        private set
        {
            if (SetProperty(ref _eventText2, value))
                OnPropertyChanged(nameof(HasEventText2));
        }
    }

    /// <summary>True when EventText2 has content. Used by XAML to show/hide the third event TextBlock.</summary>
    public bool HasEventText2 => !string.IsNullOrEmpty(_eventText2);

    /// <summary>True when this day is a public holiday and HighlightPublicHolidays is on.</summary>
    private bool _showHolidayHighlight;
    public bool ShowHolidayHighlight
    {
        get => _showHolidayHighlight;
        private set
        {
            bool oldTint = ShowWeekendTint;
            if (SetProperty(ref _showHolidayHighlight, value) && oldTint != ShowWeekendTint)
                OnPropertyChanged(nameof(ShowWeekendTint));
        }
    }

    /// <summary>
    /// True when any of ShowSaturdayHighlight, ShowSundayHighlight, or ShowHolidayHighlight is set.
    /// Single binding target replacing the three separate weekend/holiday XAML DataTriggers,
    /// reducing per-cell trigger evaluations from 9 to 3.
    /// </summary>
    public bool ShowWeekendTint => _showSaturdayHighlight || _showSundayHighlight || _showHolidayHighlight;

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

    // ── Lazy copy-menu build state ────────────────────────────────────────────
    // DateFormatter.Build() is deferred until first right-click (ContextMenuOpening).
    // _placeholderOptions is a static sentinel so HasCopyOptions stays true for
    // current-month cells before the real options are built, keeping the ContextMenu
    // accessible without suppressing it via the DataTrigger.
    private static readonly IReadOnlyList<DateFormatOption> _placeholderOptions =
        new[] { new DateFormatOption("_", "_", "_") };
    private bool _lazyBuildPending;
    private (bool isNepali, INepaliDateAdapter? adapter, ILocalizationService? localization) _lazyBuildArgs;

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
            _eventText0 = firstText;
            // _eventText1 and _eventText2 stay empty - constructor always initialises with a single visible row
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
        int visibleEventCount = 1,
        string copyMenuTitle = "")
    {
        // Capture all _day-delegating property values BEFORE replacing _day so we can
        // compare and only raise PropertyChanged for values that actually changed.
        // 42 cells × up to 9 events = up to 378 notifications per navigation; in practice
        // IsSaturday/IsSunday never change (column position is fixed), IsToday rarely changes,
        // and padding status only flips on slow-path navigations - so most properties are
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

        bool oldHasCopy = _copyFormatOptions.Count > 0;
        if (day.IsCurrentMonth && localization is not null)
        {
            // Defer DateFormatter.Build() until the user actually opens the context menu.
            // EnsureCopyOptionsBuilt() will be called from CalendarView.ContextMenuOpening.
            _lazyBuildPending = true;
            _lazyBuildArgs = (isNepali, adapter, localization);
            // If no options yet (e.g. padding→current transition), set sentinel so
            // HasCopyOptions returns true and the ContextMenu is not suppressed by the DataTrigger.
            // If a previous lazy build produced real items, keep them - they're stale but
            // HasCopyOptions stays true; EnsureCopyOptionsBuilt() refreshes them on first open.
            if (_copyFormatOptions.Count == 0)
                _copyFormatOptions = _placeholderOptions;
        }
        else
        {
            _lazyBuildPending = false;
            _copyFormatOptions = Array.Empty<DateFormatOption>();
        }
        if (oldHasCopy != (_copyFormatOptions.Count > 0))
            OnPropertyChanged(nameof(HasCopyOptions));
        CopyMenuTitle = (day.IsCurrentMonth && localization is not null && !string.IsNullOrEmpty(copyMenuTitle))
            ? copyMenuTitle
            : string.Empty;
    }

    /// <summary>
    /// Builds the real date-format options for this cell, called from
    /// <see cref="CalendarView"/> ContextMenuOpening before the menu is shown.
    /// No-op when the lazy build has already been completed for this navigation cycle.
    /// </summary>
    internal void EnsureCopyOptionsBuilt()
    {
        if (!_lazyBuildPending) return;
        _lazyBuildPending = false;

        var (isNepali, adapter, localization) = _lazyBuildArgs;
        // _lazyBuildPending is only set to true when localization is not null (see Update()),
        // so this guard is unreachable in practice but required to narrow the nullable type.
        if (localization is null) return;

        IReadOnlyList<DateFormatOption> built = Array.Empty<DateFormatOption>();
        if (_day.AdDate.HasValue && !string.IsNullOrEmpty(_day.BsShortEn))
            built = DateFormatter.Build(_day, localization, isNepali);
        else if (adapter is not null)
            built = DateFormatter.Build(_day.Year, _day.Month, _day.Day, adapter, localization, isNepali);

        // Replaces the sentinel (or stale items) with real format options and
        // fires PropertyChanged so the open ContextMenu's bindings update.
        // Also fires HasCopyOptions if the count transitions (e.g. build returned empty).
        bool hadCopy = _copyFormatOptions.Count > 0;
        CopyFormatOptions = built;
        if (hadCopy != (_copyFormatOptions.Count > 0))
            OnPropertyChanged(nameof(HasCopyOptions));
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
            EventText0 = string.Empty;
            EventText1 = string.Empty;
            EventText2 = string.Empty;
            return;
        }

        string[] newVisible;
        bool newHasHidden;

        if (count <= 0)
        {
            newVisible = Array.Empty<string>();
            newHasHidden = false;
        }
        else
        {
            // Cap at the number of fixed XAML event slots. Without this, a count > 3
            // (possible when the widget is very tall) would produce a newVisible array
            // larger than 3. EventText0/1/2 only consume indices 0–2, so events beyond
            // index 2 would be silently dropped and the overflow indicator suppressed.
            int displayCount = Math.Min(count, 3);
            if (displayCount >= _allEvents.Length)
            {
                newVisible = _allEvents;
                newHasHidden = false;
            }
            else
            {
                // Show 'displayCount' events; append " +N" to the last one so the
                // overflow indicator sits on the same line (trimmed by TextTrimming).
                int hiddenCount = _allEvents.Length - displayCount;
                newVisible = new string[displayCount];
                for (int i = 0; i < displayCount; i++)
                {
                    newVisible[i] = _allEvents[i];
                }
                newVisible[displayCount - 1] = _allEvents[displayCount - 1] + " +" + hiddenCount;
                newHasHidden = true;
            }
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
        EventText0 = newVisible.Length > 0 ? newVisible[0] : string.Empty;
        EventText1 = newVisible.Length > 1 ? newVisible[1] : string.Empty;
        EventText2 = newVisible.Length > 2 ? newVisible[2] : string.Empty;
    }
}
