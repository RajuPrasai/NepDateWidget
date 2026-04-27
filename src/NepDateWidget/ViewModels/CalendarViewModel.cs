using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// View model for the expanded calendar panel.
/// Owns the currently displayed BS month, navigation commands,
/// day-of-week header labels, and the grid of CalendarDayViewModels.
/// </summary>
public sealed class CalendarViewModel : ViewModelBase
{
    private readonly ICalendarService _calendarService;
    private readonly ILocalizationService _loc;
    private readonly IReadOnlyList<string> _highlightedDays;
    private readonly INepaliDateAdapter _adapter;
    private readonly IReminderService? _reminderService;
    private readonly IHolidayLookupService? _holidayLookup;
    private readonly IClipboardService _clipboard;

    // ── BS month name lists (static, language-independent per set) ──────────

    private static readonly string[] BsMonthNamesEn =
    {
        "Baishakh", "Jestha", "Ashadh", "Shrawan", "Bhadra", "Ashwin",
        "Kartik", "Mangshir", "Poush", "Magh", "Falgun", "Chaitra",
    };

    private static readonly string[] BsMonthNamesNe =
    {
        "बैशाख", "जेठ", "असार", "साउन", "भदौ", "असोज",
        "कार्तिक", "मंसिर", "पुष", "माघ", "फागुन", "चैत",
    };

    // ── Converter panel child VM ──────────────────────────────────────────────

    public ConverterViewModel Converter { get; }

    // ── Displayed month ───────────────────────────────────────────────────────

    private int _displayYear;
    public int DisplayYear
    {
        get => _displayYear;
        set
        {
            if (value < 1901 || value > 2199 || value == _displayYear) return;
            NavigateTo(value, _displayMonth);
        }
    }

    private int _displayMonth;
    public int DisplayMonth
    {
        get => _displayMonth;
        private set => SetProperty(ref _displayMonth, value);
    }

    /// <summary>0-based index of the displayed month (for ComboBox SelectedIndex binding).</summary>
    public int SelectedMonthIndex
    {
        get => _displayMonth - 1;
        set
        {
            if (value < 0 || value > 11 || value == _displayMonth - 1) return;
            NavigateTo(_displayYear, value + 1);
        }
    }

    private string _monthYearLabel = string.Empty;
    public string MonthYearLabel
    {
        get => _monthYearLabel;
        private set => SetProperty(ref _monthYearLabel, value);
    }

    private string _adMonthLabel = string.Empty;
    /// <summary>English AD month range (e.g. "| Mar/Apr 2026") for display alongside the BS month selector.</summary>
    public string AdMonthLabel
    {
        get => _adMonthLabel;
        private set => SetProperty(ref _adMonthLabel, value);
    }

    public IReadOnlyList<string> NepaliMonthNames { get; private set; } = Array.Empty<string>();

    // ── Year dropdown (2000–2100 BS) ──────────────────────────────────────────

    private const int YearRangeStart = 2000;
    private const int YearRangeEnd = 2100;

    /// <summary>Display strings for the year ComboBox (Arabic or Nepali digits).</summary>
    public IReadOnlyList<string> YearNames { get; private set; } = Array.Empty<string>();

    /// <summary>0-based index into <see cref="YearNames"/> for the displayed year.</summary>
    public int SelectedYearIndex
    {
        get
        {
            int idx = _displayYear - YearRangeStart;
            return idx >= 0 && idx < YearNames.Count ? idx : -1;
        }
        set
        {
            int year = value + YearRangeStart;
            if (year < YearRangeStart || year > YearRangeEnd || year == _displayYear) return;
            NavigateTo(year, _displayMonth);
        }
    }

    // ── Fiscal year footer ────────────────────────────────────────────────────

    private string _fiscalFooterText = string.Empty;
    public string FiscalFooterText
    {
        get => _fiscalFooterText;
        private set => SetProperty(ref _fiscalFooterText, value);
    }

    // ── Day-of-week headers (7 items, Sunday first) ───────────────────────────

    public IReadOnlyList<string> DayOfWeekHeaders { get; private set; } = Array.Empty<string>();

    // ── Grid cells ────────────────────────────────────────────────────────────

    public ObservableCollection<CalendarDayViewModel> Days { get; } = new();

    // ── Navigation state ─────────────────────────────────────────────────────

    private bool _canGoPrev = true;
    public bool CanGoPrev
    {
        get => _canGoPrev;
        private set => SetProperty(ref _canGoPrev, value);
    }

    private bool _canGoNext = true;
    public bool CanGoNext
    {
        get => _canGoNext;
        private set => SetProperty(ref _canGoNext, value);
    }

    private bool _isShowingToday;
    public bool IsShowingToday
    {
        get => _isShowingToday;
        private set => SetProperty(ref _isShowingToday, value);
    }

    // ── Localized nav button labels ───────────────────────────────────────────

    private string _prevLabel = string.Empty;
    public string PrevLabel
    {
        get => _prevLabel;
        private set => SetProperty(ref _prevLabel, value);
    }

    private string _nextLabel = string.Empty;
    public string NextLabel
    {
        get => _nextLabel;
        private set => SetProperty(ref _nextLabel, value);
    }

    private string _goTodayLabel = string.Empty;
    public string GoTodayLabel
    {
        get => _goTodayLabel;
        private set => SetProperty(ref _goTodayLabel, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand PrevMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand GoTodayCommand { get; }
    public ICommand DayCellClickCommand { get; }

    /// <summary>
    /// Right-click "copy date" command. Parameter is the
    /// <see cref="DateFormatOption"/> chosen by the user from the cell's
    /// context menu.
    /// </summary>
    public ICommand CopyDateCommand { get; }

    // ── Animation signal ──────────────────────────────────────────────────────
    /// <summary>
    /// Fired before month data changes. The View subscribes to play a slide animation.
    /// direction: -1 = prev, 0 = today, +1 = next.
    /// The continuation action must be called (possibly asynchronously) to complete navigation.
    /// </summary>
    public event Action<int, Action>? NavigationRequested;

    // ── Calendar display flags ────────────────────────────────────────────────

    private bool _showEnglishDayNumbers = true;
    private bool _highlightSaturdays = true;
    private bool _highlightSundays = false;
    private bool _showTithi = true;
    private bool _showEvents = true;
    private bool _highlightPublicHolidays = true;

    private bool _showFiscalYear = true;
    public bool ShowFiscalYear
    {
        get => _showFiscalYear;
        set
        {
            if (SetProperty(ref _showFiscalYear, value))
                RefreshGrid();
        }
    }

    // ── Holiday countdown (calendar header) ─────────────────────────────────

    /// <summary>Cap on holidays surfaced in the hover tooltip.</summary>
    private const int HolidayTooltipMax = 25;

    private bool _showHolidayCountdown = true;
    /// <summary>
    /// When true the calendar header shows a small "X days until {holiday}" line
    /// (one line per event when several public holidays share the same day) and
    /// a hover tooltip listing upcoming public holidays. Persisted via
    /// WidgetSettings; defaults to true.
    /// </summary>
    public bool ShowHolidayCountdown
    {
        get => _showHolidayCountdown;
        set { if (SetProperty(ref _showHolidayCountdown, value)) RefreshHolidayCountdown(); }
    }

    private IReadOnlyList<string> _holidayCountdownLines = Array.Empty<string>();
    /// <summary>
    /// One formatted line per holiday name on the nearest upcoming holiday day.
    /// Empty when the toggle is off, the lookup service is unavailable, or no
    /// holiday is found within the lookahead window.
    /// </summary>
    public IReadOnlyList<string> HolidayCountdownLines
    {
        get => _holidayCountdownLines;
        private set { if (SetProperty(ref _holidayCountdownLines, value)) OnPropertyChanged(nameof(HasHolidayCountdown)); }
    }

    /// <summary>True only when there is at least one line to show.</summary>
    public bool HasHolidayCountdown => _showHolidayCountdown && _holidayCountdownLines.Count > 0;

    private string _holidayCountdownTooltip = string.Empty;
    /// <summary>
    /// Short hover hint shown over the banner. The full grouped list lives in
    /// <see cref="HolidayPopupEntries"/> and surfaces on click.
    /// </summary>
    public string HolidayCountdownTooltip
    {
        get => _holidayCountdownTooltip;
        private set => SetProperty(ref _holidayCountdownTooltip, value);
    }

    private IReadOnlyList<HolidayPopupEntry> _holidayPopupEntries = Array.Empty<HolidayPopupEntry>();
    /// <summary>
    /// Flat list of upcoming public-holiday events (one entry per name) used
    /// by the click-popup attached to the countdown banner. Always populated
    /// when <see cref="HasHolidayCountdown"/> is true.
    /// </summary>
    public IReadOnlyList<HolidayPopupEntry> HolidayPopupEntries
    {
        get => _holidayPopupEntries;
        private set => SetProperty(ref _holidayPopupEntries, value);
    }

    /// <summary>Localized "Upcoming holidays" header for the popup.</summary>
    public string HolidayPopupTitle => _loc.Get("calendar.holiday.popup_title");

    // ── Responsive font sizes (updated by CalendarView.xaml.cs on SizeChanged) ──────────────

    private int _visibleEventCount = 1;

    public double DayNumberFontSize  { get; private set; } = 14.0;
    public double EventFontSize      { get; private set; } =  10;
    public double TithiFontSize      { get; private set; } =  10;
    public double AdBadgeFontSize    { get; private set; } =  10;
    public double HeaderFontSize     { get; private set; } = 13.5;  // fixed — does not scale with cell size
    public double SubHeaderFontSize  { get; private set; } = 12.0;  // fixed
    public double DowFontSize        { get; private set; } = 12.0;

    /// <summary>
    /// Called from CalendarView.xaml.cs whenever the day grid changes size.
    /// Recomputes scaled font sizes and visible-event-row count from cell dimensions.
    /// </summary>
    public void UpdateCellLayout(double cellHeight, double cellWidth)
    {
        if (cellWidth <= 0 || cellHeight <= 0) return;

        // Scale factor: ReferenceCellWidthPx cell width = reference (800px widget / 7 cols)
        // Base sizes are intentionally larger so the default view starts with comfortable text.
        const double ReferenceCellWidthPx = 114.0;
        double ws = Math.Clamp(cellWidth / ReferenceCellWidthPx, 0.65, 2.5);

        // Integer font sizes only. Fractional sizes (e.g. 16.4) cause WPF text
        // hinting to land on different sub-pixels each resize, which produces
        // visibly jagged curves ("8", "3") even with ClearType enabled.
        double newDayFont    = Math.Round(Math.Clamp(16.0 * ws, 13.0, 18.0));
        double newEventFont  = Math.Round(Math.Clamp(12.0 * ws,  9.0, 12.0));
        double newTithiFont  = Math.Round(Math.Clamp(11.0 * ws,  9.0, 11.0));
        double newBadgeFont  = Math.Round(Math.Clamp(10.0 * ws,  8.0, 11.0));
        double newDowFont    = Math.Round(Math.Clamp(13.0 * ws, 12.0, 15.0));

        // Visible-event count: use capped reference heights so that as the day-number
        // font grows (making the cell taller and wider) the event rows don't shrink.
        // Caps here must match the hard maxima above so the formula is monotone.
        const double EventRowCapPx  = 13.0 * 1.5;   // 19.5px — fixed once font hits max
        const double DayRowCapPx    = 20.0 * 1.6;   // 32px — conservative cap for day row
        const double TithiRowCapPx  = 12.0 * 1.6;   // 19.2px — cap for tithi row
        double eventRowH = Math.Min(newEventFont * 1.5, EventRowCapPx);
        double dayRowH   = Math.Min(newDayFont   * 1.6, DayRowCapPx);
        double tithiRowH = Math.Min(newTithiFont * 1.6, TithiRowCapPx);
        double eventsH   = cellHeight - dayRowH - tithiRowH - 6.0;
        int newVisibleEvents = Math.Max(1, (int)(eventsH / eventRowH));

        bool fontsChanged =
            Math.Abs(newDayFont   - DayNumberFontSize)  > 0.05 ||
            Math.Abs(newEventFont  - EventFontSize)      > 0.05 ||
            Math.Abs(newTithiFont  - TithiFontSize)      > 0.05 ||
            Math.Abs(newBadgeFont  - AdBadgeFontSize)    > 0.05 ||
            Math.Abs(newDowFont    - DowFontSize)        > 0.05;

        if (fontsChanged)
        {
            DayNumberFontSize = newDayFont;
            EventFontSize     = newEventFont;
            TithiFontSize     = newTithiFont;
            AdBadgeFontSize   = newBadgeFont;
            DowFontSize       = newDowFont;
            OnPropertyChanged(nameof(DayNumberFontSize));
            OnPropertyChanged(nameof(EventFontSize));
            OnPropertyChanged(nameof(TithiFontSize));
            OnPropertyChanged(nameof(AdBadgeFontSize));
            OnPropertyChanged(nameof(DowFontSize));
        }

        if (newVisibleEvents != _visibleEventCount)
        {
            _visibleEventCount = newVisibleEvents;
            foreach (var dayVm in Days)
                dayVm.UpdateVisibleEventCount(_visibleEventCount);
        }
    }

    // ── Reminder support ──────────────────────────────────────────────────────

    private int _missedReminderCount;
    public int MissedReminderCount
    {
        get => _missedReminderCount;
        set => SetProperty(ref _missedReminderCount, value);
    }

    /// <summary>
    /// Raised when the user clicks a calendar day cell.
    /// Parameters: bsYear, bsMonth, bsDay.
    /// The View subscribes to open the day info popup.
    /// </summary>
    public event Action<int, int, int>? OpenDayInfoRequested;

    // ── Construction ─────────────────────────────────────────────────────────

    public CalendarViewModel(
        ICalendarService calendarService,
        ILocalizationService localizationService,
        IConversionService conversionService,
        IReadOnlyList<string>? highlightedDays = null,
        string converterDefaultDirection = "ADtoBS",
        bool showEnglishDayNumbers = true,
        bool highlightSaturdays = true,
        bool highlightSundays = false,
        INepaliDateAdapter? adapter = null,
        string selectedTimezoneId = "",
        IReminderService? reminderService = null,
        bool showTithi = true,
        bool showEvents = true,
        bool highlightPublicHolidays = true,
        bool showFiscalYear = true,
        bool showHolidayCountdown = true,
        IHolidayLookupService? holidayLookupService = null,
        IClipboardService? clipboardService = null)
    {
        _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _ = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
        _highlightedDays = highlightedDays ?? Array.Empty<string>();
        _showEnglishDayNumbers = showEnglishDayNumbers;
        _highlightSaturdays = highlightSaturdays;
        _highlightSundays = highlightSundays;
        _adapter = adapter ?? new NepaliDateAdapter();
        _reminderService = reminderService;
        _showTithi = showTithi;
        _showEvents = showEvents;
        _highlightPublicHolidays = highlightPublicHolidays;
        _showFiscalYear = showFiscalYear;
        _showHolidayCountdown = showHolidayCountdown;
        _holidayLookup = holidayLookupService;
        _clipboard = clipboardService ?? new ClipboardService();

        Converter = new ConverterViewModel(conversionService, localizationService,
            converterDefaultDirection, adapter, selectedTimezoneId);

        PrevMonthCommand = new RelayCommand(() => NavigateMonths(-1));
        NextMonthCommand = new RelayCommand(() => NavigateMonths(+1));
        GoTodayCommand = new RelayCommand(GoToToday);
        DayCellClickCommand = new RelayCommand<CalendarDayViewModel>(OnDayCellClicked);
        CopyDateCommand = new RelayCommand<DateFormatOption>(OnCopyDate);

        var today = _calendarService.GetCurrentDateInfo();
        _displayYear = today.BsYear;
        _displayMonth = today.BsMonth;

        RefreshGrid();
        RefreshLabels();
        RefreshHolidayCountdown();

        if (_reminderService is not null)
        {
            _reminderService.RemindersChanged += (_, _) => RefreshReminderDots();
            RefreshMissedBadge();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called when the user clicks a day cell. Raises event for the View to handle.</summary>
    public void RequestOpenReminder(int bsYear, int bsMonth, int bsDay)
    {
        OpenDayInfoRequested?.Invoke(bsYear, bsMonth, bsDay);
    }

    /// <summary>Clears the missed-reminder badge (called when user opens the calendar tab).</summary>
    public void ClearMissedBadge()
    {
        MissedReminderCount = 0;
    }

    /// <summary>Navigates forward or backward by the given number of months.</summary>
    public void NavigateMonths(int delta)
    {
        void DoNav()
        {
            var (y, m) = _calendarService.NavigateMonth(_displayYear, _displayMonth, delta);
            if (y == _displayYear && m == _displayMonth) return;

            _displayYear = y;
            _displayMonth = m;
            OnPropertyChanged(nameof(DisplayYear));
            OnPropertyChanged(nameof(DisplayMonth));
            OnPropertyChanged(nameof(SelectedMonthIndex));
            OnPropertyChanged(nameof(SelectedYearIndex));
            RefreshGrid();
            Log.Action($"cal {(delta > 0 ? "→" : "←")} {_displayYear}/{_displayMonth:D2}");
        }

        if (NavigationRequested != null)
            NavigationRequested(delta > 0 ? 1 : -1, DoNav);
        else
            DoNav();
    }

    /// <summary>Jumps directly to the specified BS year and month.</summary>
    public void NavigateTo(int year, int month)
    {
        if (year == _displayYear && month == _displayMonth) return;

        void DoNav()
        {
            _displayYear = year;
            _displayMonth = month;
            OnPropertyChanged(nameof(DisplayYear));
            OnPropertyChanged(nameof(DisplayMonth));
            OnPropertyChanged(nameof(SelectedMonthIndex));
            OnPropertyChanged(nameof(SelectedYearIndex));
            RefreshGrid();
            Log.Action($"cal jump → {_displayYear}/{_displayMonth:D2}");
        }

        if (NavigationRequested != null)
            NavigationRequested(0, DoNav);
        else
            DoNav();
    }

    /// <summary>Called when the language changes - refreshes all labels.</summary>
    public void OnLanguageChanged()
    {
        RefreshLabels();
        RefreshGrid(); // month name in header depends on language
        RefreshHolidayCountdown();
        Converter.OnLanguageChanged();
    }

    /// <summary>Updates calendar display preferences and refreshes the day grid.</summary>
    public void UpdateDisplaySettings(bool showEnglishDayNumbers, bool highlightSaturdays,
        bool highlightSundays, bool showTithi, bool showEvents, bool highlightPublicHolidays)
    {
        _showEnglishDayNumbers = showEnglishDayNumbers;
        _highlightSaturdays = highlightSaturdays;
        _highlightSundays = highlightSundays;
        _showTithi = showTithi;
        _showEvents = showEvents;
        _highlightPublicHolidays = highlightPublicHolidays;
        RefreshGrid();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void GoToToday()
    {
        var today = _calendarService.GetCurrentDateInfo();
        if (today.BsYear == _displayYear && today.BsMonth == _displayMonth) return;

        void DoNav()
        {
            _displayYear = today.BsYear;
            _displayMonth = today.BsMonth;
            OnPropertyChanged(nameof(DisplayYear));
            OnPropertyChanged(nameof(DisplayMonth));
            OnPropertyChanged(nameof(SelectedMonthIndex));
            OnPropertyChanged(nameof(SelectedYearIndex));
            RefreshGrid();
            Log.Action($"cal today → {_displayYear}/{_displayMonth:D2}");
        }

        if (NavigationRequested != null)
            NavigationRequested(0, DoNav);
        else
            DoNav();
    }

    private void RefreshGrid()
    {
        var month = _calendarService.GetMonth(_displayYear, _displayMonth, _highlightedDays);

        bool isNepali = string.Equals(_loc.CurrentLanguage, "ne", StringComparison.OrdinalIgnoreCase);

        Days.Clear();
        foreach (var day in month.Days)
        {
            var vm = new CalendarDayViewModel(day, isNepali, _showEnglishDayNumbers, _highlightSaturdays,
                _highlightSundays, _showTithi, _showEvents, _highlightPublicHolidays,
                _adapter, _loc);
            if (_reminderService is not null && day.IsCurrentMonth)
                vm.HasReminders = _reminderService.HasRemindersForDateExpanded(day.Year, day.Month, day.Day);
            Days.Add(vm);
        }

        // Dual-format header: "Chaitra 2082 | Mar/Apr 2026"
        string bsPart = isNepali
            ? $"{month.MonthNameNe} {NepaliScriptConverter.ToNepaliDigits(_displayYear)}"
            : $"{month.MonthNameEn} {_displayYear}";

        MonthYearLabel = string.IsNullOrEmpty(month.AdMonthLabel)
            ? bsPart
            : $"{bsPart} | {month.AdMonthLabel}";

        AdMonthLabel = month.AdMonthLabel ?? string.Empty;

        IsShowingToday = month.ContainsToday;
        RefreshNavState();
        RefreshFiscalFooter();

        // Re-apply the current visible-event count to all newly created VMs.
        // RefreshGrid is called after popup close (OnLanguageChanged), navigation,
        // and settings changes — all of which rebuild the Days collection. Without
        // this, cells revert to 1 event row until the next SizeChanged fires.
        if (_visibleEventCount > 1)
            foreach (var dayVm in Days)
                dayVm.UpdateVisibleEventCount(_visibleEventCount);
    }

    private void RefreshLabels()
    {
        PrevLabel = _loc.Get("calendar.prev_month");
        NextLabel = _loc.Get("calendar.next_month");
        GoTodayLabel = _loc.Get("calendar.go_today");

        DayOfWeekHeaders = new[]
        {
            _loc.Get("dow.sun"), _loc.Get("dow.mon"), _loc.Get("dow.tue"),
            _loc.Get("dow.wed"), _loc.Get("dow.thu"), _loc.Get("dow.fri"),
            _loc.Get("dow.sat"),
        };
        OnPropertyChanged(nameof(DayOfWeekHeaders));

        bool isNepali = string.Equals(_loc.CurrentLanguage, "ne", StringComparison.OrdinalIgnoreCase);
        NepaliMonthNames = isNepali ? BsMonthNamesNe : BsMonthNamesEn;
        OnPropertyChanged(nameof(NepaliMonthNames));
        OnPropertyChanged(nameof(SelectedMonthIndex));

        // Year dropdown: rebuild display strings when language changes
        var years = new string[YearRangeEnd - YearRangeStart + 1];
        for (int y = YearRangeStart; y <= YearRangeEnd; y++)
            years[y - YearRangeStart] = isNepali ? NepaliScriptConverter.ToNepaliDigits(y) : y.ToString();
        YearNames = years;
        OnPropertyChanged(nameof(YearNames));
        OnPropertyChanged(nameof(SelectedYearIndex));

        // Refresh header label with current language
        RefreshGrid();
    }

    private void RefreshNavState()
    {
        CanGoPrev = !(_displayYear == 1901 && _displayMonth == 1);
        CanGoNext = !(_displayYear == 2199 && _displayMonth == 12);
    }

    // ── Holiday countdown ─────────────────────────────────────────────────────

    private void RefreshHolidayCountdown()
    {
        if (!_showHolidayCountdown || _holidayLookup is null)
        {
            HolidayCountdownLines = Array.Empty<string>();
            HolidayPopupEntries   = Array.Empty<HolidayPopupEntry>();
            return;
        }

        UpcomingHoliday? next;
        IReadOnlyList<UpcomingHoliday> upcoming;
        try
        {
            next = _holidayLookup.GetNextHoliday();
            upcoming = _holidayLookup.GetUpcomingHolidays(HolidayTooltipMax);
        }
        catch
        {
            HolidayCountdownLines = Array.Empty<string>();
            HolidayPopupEntries   = Array.Empty<HolidayPopupEntry>();
            return;
        }

        if (next is null)
        {
            HolidayCountdownLines = Array.Empty<string>();
            HolidayPopupEntries   = Array.Empty<HolidayPopupEntry>();
            return;
        }

        bool isNepali = string.Equals(_loc.CurrentLanguage, "ne", StringComparison.OrdinalIgnoreCase);
        HolidayCountdownLines = BuildCountdownLines(next, isNepali);
        HolidayPopupEntries   = BuildPopupEntries(upcoming, isNepali);
        HolidayCountdownTooltip = _loc.Get("calendar.holiday.popup_title");
        OnPropertyChanged(nameof(HolidayPopupTitle));
    }

    /// <summary>
    /// Returns 1 or 2 centered strings for the header banner:
    ///   line 1 : the primary "X days until {first event}" / "Today: {…}" line
    ///   line 2 : optional "+N more events" rollup when the day has multiple
    ///            events. Clicking the banner reveals everything via popup.
    /// </summary>
    private IReadOnlyList<string> BuildCountdownLines(UpcomingHoliday h, bool isNepali)
    {
        var names = PickNames(h, isNepali);
        if (names.Count == 0) return Array.Empty<string>();

        string daysText = isNepali
            ? NepaliScriptConverter.ToNepaliDigits(h.DaysUntil)
            : h.DaysUntil.ToString();

        string template = h.DaysUntil switch
        {
            0 => _loc.Get("calendar.holiday.today"),
            1 => _loc.Get("calendar.holiday.tomorrow"),
            _ => _loc.Get("calendar.holiday.in_days"),
        };

        string primary = h.DaysUntil >= 2
            ? string.Format(template, daysText, names[0])
            : string.Format(template, names[0]);

        if (names.Count == 1)
            return new[] { primary };

        int extra = names.Count - 1;
        string moreKey = extra == 1
            ? "calendar.holiday.more_events_one"
            : "calendar.holiday.more_events";
        string extraText = isNepali
            ? NepaliScriptConverter.ToNepaliDigits(extra)
            : extra.ToString();
        string more = extra == 1
            ? _loc.Get(moreKey)
            : string.Format(_loc.Get(moreKey), extraText);

        return new[] { primary, more };
    }

    /// <summary>
    /// Flattens the upcoming-holidays list into one entry per event name, with
    /// a per-day shared "when" label (today / tomorrow / in N days) and BS date.
    /// Used by the click-popup attached to the countdown banner.
    /// </summary>
    private IReadOnlyList<HolidayPopupEntry> BuildPopupEntries(
        IReadOnlyList<UpcomingHoliday> upcoming, bool isNepali)
    {
        if (upcoming is null || upcoming.Count == 0)
            return Array.Empty<HolidayPopupEntry>();

        var list = new List<HolidayPopupEntry>(upcoming.Count);
        for (int i = 0; i < upcoming.Count; i++)
        {
            var h = upcoming[i];
            var names = PickNames(h, isNepali);
            if (names.Count == 0) continue;

            string when;
            if (h.DaysUntil == 0)       when = _loc.Get("calendar.holiday.popup_today");
            else if (h.DaysUntil == 1)  when = _loc.Get("calendar.holiday.popup_tomorrow");
            else
            {
                string days = isNepali
                    ? NepaliScriptConverter.ToNepaliDigits(h.DaysUntil)
                    : h.DaysUntil.ToString();
                when = string.Format(_loc.Get("calendar.holiday.popup_in_days"), days);
            }
            string date = isNepali ? h.BsLongNp : h.BsLongEn;

            for (int j = 0; j < names.Count; j++)
                list.Add(new HolidayPopupEntry(names[j], date, when, isToday: h.DaysUntil == 0));
        }
        return list;
    }

    private static IReadOnlyList<string> PickNames(UpcomingHoliday h, bool isNepali)
    {
        var primary = isNepali ? h.NamesNp : h.NamesEn;
        if (primary.Count > 0) return primary;
        var fallback = isNepali ? h.NamesEn : h.NamesNp;
        return fallback;
    }

    private void RefreshFiscalFooter()
    {
        if (!_showFiscalYear)
        {
            FiscalFooterText = string.Empty;
            return;
        }

        try
        {
            var info = _calendarService.GetCurrentDateInfo();
            int ty = info.BsYear, tm = info.BsMonth, td = info.BsDay;

            var (fyLabel, quarter, daysToQEnd, daysToYrEnd) =
                _adapter.GetFiscalYearInfo(ty, tm, td);

            string fyPrefix = _loc.Get("fiscal.label");
            string qPrefix = _loc.Get("fiscal.quarter");

            if (daysToYrEnd <= 0)
            {
                FiscalFooterText = $"{fyPrefix} {fyLabel} • {qPrefix}{quarter}";
            }
            else if (daysToQEnd <= daysToYrEnd)
            {
                string daysLabel = _loc.Get("fiscal.days_to_qend");
                FiscalFooterText = $"{fyPrefix} {fyLabel} • {qPrefix}{quarter} • {daysToQEnd} {daysLabel}";
            }
            else
            {
                string daysLabel = _loc.Get("fiscal.days_to_yend");
                FiscalFooterText = $"{fyPrefix} {fyLabel} • {qPrefix}{quarter} • {daysToYrEnd} {daysLabel}";
            }
        }
        catch
        {
            FiscalFooterText = string.Empty;
        }
    }

    // ── Reminder helpers ─────────────────────────────────────────────────────

    private void OnDayCellClicked(CalendarDayViewModel? dayVm)
    {
        if (dayVm is null || dayVm.IsPadding) return;
        OpenDayInfoRequested?.Invoke(dayVm.BsYear, dayVm.BsMonth, dayVm.Day);
    }

    /// <summary>
    /// Copies the value of the chosen format option to the clipboard.
    /// Silently ignored when the option is null (e.g. menu closed without a
    /// selection) or when the clipboard service refuses (logged inside the
    /// service). Logs an action line for diagnostics.
    /// </summary>
    private void OnCopyDate(DateFormatOption? option)
    {
        if (option is null || string.IsNullOrEmpty(option.Value)) return;
        bool ok = _clipboard.SetText(option.Value);
        Log.Action($"cal copy {option.Key} {(ok ? "ok" : "failed")}");
    }

    private void RefreshReminderDots()
    {
        if (_reminderService is null) return;
        foreach (var dayVm in Days)
        {
            if (dayVm.IsCurrentMonth)
            {
                bool has = _reminderService.HasRemindersForDateExpanded(dayVm.BsYear, dayVm.BsMonth, dayVm.Day);
                dayVm.HasReminders = has;

                if (has)
                {
                    var titles = new List<string>();
                    foreach (var r in _reminderService.GetForDate(dayVm.BsYear, dayVm.BsMonth, dayVm.Day))
                    {
                        if (!r.IsCompleted) titles.Add(r.Title);
                    }
                    foreach (var r in _reminderService.GetRecurringForDate(dayVm.BsYear, dayVm.BsMonth, dayVm.Day))
                        titles.Add(r.Title);

                    dayVm.ReminderTooltip = titles.Count > 0 ? string.Join("\n", titles) : null;
                }
                else
                {
                    dayVm.ReminderTooltip = null;
                }
            }
        }
    }

    private void RefreshMissedBadge()
    {
        if (_reminderService is null) return;
        MissedReminderCount = _reminderService.GetMissedReminders().Count;
    }


}
