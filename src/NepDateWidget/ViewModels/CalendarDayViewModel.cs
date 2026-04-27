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
    private readonly CalendarDay _day;

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
    public string DayText { get; }

    /// <summary>
    /// Small AD day number shown at the bottom-right of the cell (always Arabic digits).
    /// Empty for padding cells.
    /// </summary>
    public string EnglishDayText { get; }

    /// <summary>Whether to show the English day badge (factoring in the global toggle).</summary>
    public bool ShowEnglishBadge { get; }

    /// <summary>Whether to apply Saturday/holiday highlighting for this cell.</summary>
    public bool ShowSaturdayHighlight { get; }

    /// <summary>Whether to apply Sunday highlighting for this cell.</summary>
    public bool ShowSundayHighlight { get; }

    /// <summary>Lunar day (Tithi) text in the current widget language. Empty for padding cells.</summary>
    public string TithiText { get; }

    /// <summary>True when TithiText is non-empty and the ShowTithi setting is on.</summary>
    public bool ShowTithiText { get; }

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
    public bool ShowHolidayHighlight { get; }

    /// <summary>
    /// Pre-built rows for the right-click "copy date" context menu.
    /// Empty for padding cells, which suppresses the menu entirely.
    /// Built once per VM instance using the current language; the calendar
    /// rebuilds every cell on language change so this stays in sync.
    /// </summary>
    public IReadOnlyList<DateFormatOption> CopyFormatOptions { get; } = Array.Empty<DateFormatOption>();

    /// <summary>True when CopyFormatOptions has at least one row (used by XAML to gate the menu).</summary>
    public bool HasCopyOptions => CopyFormatOptions.Count > 0;

    /// <summary>Localized "Copy" label shown as a non-clickable header at the top of the right-click menu.</summary>
    public string CopyMenuTitle { get; } = string.Empty;

    // ── Multi-event state ────────────────────────────────────────────────────
    private readonly string[] _allEvents;
    private readonly bool _canShowEvents;
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

        // Copy-date menu options: only for current-month cells where we have
        // both an adapter (date math) and a localization service (labels).
        // Padding cells get an empty list so the menu is suppressed.
        if (day.IsCurrentMonth && adapter is not null && localization is not null)
        {
            CopyFormatOptions = DateFormatter.Build(day.Year, day.Month, day.Day, adapter, localization, isNepali);
            CopyMenuTitle = localization.Get("calendar.copy.title");
        }
    }

    /// <summary>
    /// Called by <see cref="CalendarViewModel.UpdateCellLayout"/> when available cell height changes.
    /// Updates the visible event count without rebuilding the VM.
    /// </summary>
    public void UpdateVisibleEventCount(int count)
    {
        if (!_canShowEvents || _allEvents.Length == 0) return;

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
                newVisible[i] = _allEvents[i];
            newVisible[count - 1] = _allEvents[count - 1] + " +" + hiddenCount;
            newHasHidden = true;
        }

        // Length alone doesn’t detect “+N” text changes when count stays equal.
        bool contentSame = newVisible.Length == _visibleEventsArray.Length;
        if (contentSame)
            for (int i = 0; i < newVisible.Length; i++)
                if (newVisible[i] != _visibleEventsArray[i]) { contentSame = false; break; }
        if (contentSame && newHasHidden == _hasHiddenEvents) return;

        _visibleEventsArray = newVisible;
        OnPropertyChanged(nameof(VisibleEvents));
        HasHiddenEvents = newHasHidden;
        HasVisibleEvents = newVisible.Length > 0;
    }
}
