using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Windows.Threading;

namespace NepDateWidget.ViewModels;

/// <summary>
/// View model for the collapsed mini-bar state.
/// Produces up to two display lines:
///   When timezone ON:  Line 1 = [DayOfWeek] | [TzLabel Time] [Offset]   Line 2 = [NepaliDate] | [EnglishDate]
///   When timezone OFF: Line 1 = empty   Line 2 = [DayOfWeek] | [NepaliDate] | [EnglishDate]
/// When Line1 is empty, HasLine1=false so the View can auto-collapse to a single centered line.
/// Each element is independently toggleable. Separators are clean for all combos.
/// </summary>
public sealed class MiniBarViewModel : ViewModelBase, IDisposable
{
    private readonly ICalendarService _calendarService;
    private readonly ILocalizationService _loc;
    private readonly DispatcherTimer _clockTimer;
    private DateTime _lastRefreshedDate = DateTime.MinValue.Date;

    // ── Computed display lines ────────────────────────────────────────────────

    private string _line1Text = string.Empty;
    public string Line1Text
    {
        get => _line1Text;
        private set => SetProperty(ref _line1Text, value);
    }

    private string _line2Text = string.Empty;
    public string Line2Text
    {
        get => _line2Text;
        private set => SetProperty(ref _line2Text, value);
    }

    private bool _hasLine1;
    /// <summary>True when Line1 has content. The View uses this to auto-collapse to a single line.</summary>
    public bool HasLine1
    {
        get => _hasLine1;
        private set => SetProperty(ref _hasLine1, value);
    }

    // ── Toggle flags ─────────────────────────────────────────────────────────

    private bool _showTimezone;
    public bool ShowTimezone
    {
        get => _showTimezone;
        set { if (SetProperty(ref _showTimezone, value)) Refresh(); }
    }

    private bool _showOffset;
    public bool ShowOffset
    {
        get => _showOffset;
        set { if (SetProperty(ref _showOffset, value)) Refresh(); }
    }

    private bool _showDayOfWeek;
    public bool ShowDayOfWeek
    {
        get => _showDayOfWeek;
        set { if (SetProperty(ref _showDayOfWeek, value)) Refresh(); }
    }

    private bool _showEnglishDate;
    public bool ShowEnglishDate
    {
        get => _showEnglishDate;
        set { if (SetProperty(ref _showEnglishDate, value)) Refresh(); }
    }

    private string _selectedTimezoneId = string.Empty;
    public string SelectedTimezoneId
    {
        get => _selectedTimezoneId;
        set { if (SetProperty(ref _selectedTimezoneId, value)) Refresh(); }
    }

    private string _clockFormat = "12h";
    public string ClockFormat
    {
        get => _clockFormat;
        set { if (SetProperty(ref _clockFormat, value)) Refresh(); }
    }

    private bool _showSeconds;
    public bool ShowSeconds
    {
        get => _showSeconds;
        set { if (SetProperty(ref _showSeconds, value)) { Refresh(); ScheduleNextTick(); } }
    }

    // ── Hint ──────────────────────────────────────────────────────────────────

    private string _expandHint = string.Empty;
    public string ExpandHint
    {
        get => _expandHint;
        private set => SetProperty(ref _expandHint, value);
    }

    private string _fullDateTooltip = string.Empty;
    public string FullDateTooltip
    {
        get => _fullDateTooltip;
        private set => SetProperty(ref _fullDateTooltip, value);
    }

    // ── Construction ─────────────────────────────────────────────────────────

    public MiniBarViewModel(
        ICalendarService calendarService,
        ILocalizationService localizationService,
        bool showTimezone,
        string selectedTimezoneId,
        bool showOffset,
        bool showDayOfWeek,
        bool showEnglishDate,
        string clockFormat = "12h",
        bool showSeconds = false)
    {
        _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _showTimezone = showTimezone;
        _selectedTimezoneId = selectedTimezoneId ?? string.Empty;
        _showOffset = showOffset;
        _showDayOfWeek = showDayOfWeek;
        _showEnglishDate = showEnglishDate;
        _clockFormat = clockFormat;
        _showSeconds = showSeconds;

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += OnClockTick;
        ScheduleNextTick();
        _clockTimer.Start();

        Refresh();
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        Refresh();
        ScheduleNextTick();
    }

    /// <summary>
    /// When seconds are visible, tick every second. When seconds are hidden,
    /// schedule the next tick to land just after the next minute boundary so
    /// we wake the dispatcher at most once per minute. Saves ~59 wake-ups per
    /// minute when the user is idle, which materially helps battery and CPU
    /// load on low-end systems.
    /// </summary>
    private void ScheduleNextTick()
    {
        if (_showSeconds)
        {
            if (_clockTimer.Interval != TimeSpan.FromSeconds(1))
                _clockTimer.Interval = TimeSpan.FromSeconds(1);
            return;
        }

        var now = DateTime.Now;
        var msToNextMinute = 60_000 - (now.Second * 1000 + now.Millisecond) + 50;
        // Clamp to a sane range; avoid 0 (would re-fire instantly) and >60s.
        if (msToNextMinute < 200) msToNextMinute = 200;
        if (msToNextMinute > 60_050) msToNextMinute = 60_050;
        _clockTimer.Interval = TimeSpan.FromMilliseconds(msToNextMinute);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Refresh()
    {
        RefreshLines();
        RefreshLabels();
    }

    public void OnLanguageChanged() => Refresh();

    // ── Private ───────────────────────────────────────────────────────────────

    private TimeZoneInfo GetSelectedTimezone()
    {
        if (!string.IsNullOrEmpty(_selectedTimezoneId))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(_selectedTimezoneId); }
            catch { /* fall through to local */ }
        }
        return TimeZoneInfo.Local;
    }

    private string GetDayOfWeekText(DateTimeOffset nowInTz)
    {
        string dowKey = nowInTz.DayOfWeek switch
        {
            System.DayOfWeek.Sunday => "dow.full.sun",
            System.DayOfWeek.Monday => "dow.full.mon",
            System.DayOfWeek.Tuesday => "dow.full.tue",
            System.DayOfWeek.Wednesday => "dow.full.wed",
            System.DayOfWeek.Thursday => "dow.full.thu",
            System.DayOfWeek.Friday => "dow.full.fri",
            System.DayOfWeek.Saturday => "dow.full.sat",
            _ => "dow.full.sun"
        };
        return _loc.Get(dowKey);
    }

    private void RefreshLines()
    {
        // Detect midnight rollover
        if (DateTime.Now.Date != _lastRefreshedDate)
            _lastRefreshedDate = DateTime.Now.Date;

        var tz = GetSelectedTimezone();
        var nowUtc = DateTimeOffset.UtcNow;
        var nowInTz = TimeZoneInfo.ConvertTime(nowUtc, tz);

        // ── Build Line 1 ──────────────────────────────────────────────────
        // Line 1 is populated only when timezone is ON.
        // When timezone is ON: [DayOfWeek] | [TzLabel Time] [Offset]
        var parts1 = new List<string>();

        if (_showTimezone)
        {
            // Timezone display name (short label) + time
            string tzLabel = GetTimezoneShortLabel(tz);
            string time = string.Equals(_clockFormat, "24h", StringComparison.OrdinalIgnoreCase)
                ? nowInTz.ToString(_showSeconds ? "HH:mm:ss" : "HH:mm")
                : nowInTz.ToString(_showSeconds ? "h:mm:ss tt" : "h:mm tt");

            string timePart = $"{tzLabel} {time}";
            if (_showOffset)
            {
                var utcOffset = tz.GetUtcOffset(nowInTz.DateTime);
                string sign = utcOffset >= TimeSpan.Zero ? "+" : "-";
                var abs = utcOffset.Duration();
                timePart += $" {sign}{(int)abs.TotalHours:D2}:{abs.Minutes:D2}";
            }
            parts1.Add(timePart);
        }

        // ── Fetch date info ────────────────────────────────────────────────
        CurrentDateInfo info;
        try { info = _calendarService.GetCurrentDateInfo(); }
        catch
        {
            Line1Text = string.Empty;
            HasLine1 = false;
            Line2Text = "--";
            return;
        }

        bool isNepali = string.Equals(_loc.CurrentLanguage, "ne", StringComparison.OrdinalIgnoreCase);
        string nepaliDate = isNepali ? info.BsLongNe : info.BsLongEn;

        FullDateTooltip = $"{info.BsLongEn}\n{info.AdShort}\n{GetDayOfWeekText(nowInTz)}";

        if (_showTimezone)
        {
            // When EngDate is OFF and DOW is ON, move DOW to Line 2 with NepaliDate.
            // Otherwise DOW stays on Line 1 with timezone info.
            bool allOn     = _showDayOfWeek && _showEnglishDate;
            bool dowOnLine2 = _showDayOfWeek && !_showEnglishDate;
            bool engOnLine2 = _showEnglishDate && !_showDayOfWeek;

            if (_showDayOfWeek && !dowOnLine2)
                parts1.Insert(0, GetDayOfWeekText(nowInTz));

            if (_showEnglishDate && !engOnLine2 && !allOn)
                parts1.Add(info.AdShort);

            Line1Text = string.Join(" | ", parts1);
            HasLine1 = !string.IsNullOrEmpty(Line1Text);

            var parts2 = new List<string>();
            if (dowOnLine2)
                parts2.Add(GetDayOfWeekText(nowInTz));
            parts2.Add(nepaliDate);
            if (engOnLine2 || allOn)
                parts2.Add(info.AdShort);
            Line2Text = string.Join(" | ", parts2);
        }
        else
        {
            // Timezone OFF: Line 1 = [DayOfWeek] [| EnglishDate]  (top, smaller)
            //               Line 2 = NepaliDate                   (bottom, bigger)
            // Single line (NepaliDate only) when both DayOfWeek and EnglishDate are OFF.
            var top = new List<string>();
            if (_showDayOfWeek)
                top.Add(GetDayOfWeekText(nowInTz));
            if (_showEnglishDate)
                top.Add(info.AdShort);

            if (top.Count > 0)
            {
                Line1Text = string.Join(" | ", top);
                HasLine1 = true;
            }
            else
            {
                Line1Text = string.Empty;
                HasLine1 = false;
            }
            Line2Text = nepaliDate;
        }
    }

    private static string GetTimezoneShortLabel(TimeZoneInfo tz)
    {
        // Extract a short label from the DisplayName, e.g.
        // "(UTC+05:45) Kathmandu" -> "Kathmandu"
        // "(UTC+08:00) Kuala Lumpur, Singapore" -> "Singapore"
        string display = tz.DisplayName;
        int closeParen = display.IndexOf(')');
        if (closeParen >= 0 && closeParen + 2 < display.Length)
        {
            string afterParen = display[(closeParen + 1)..].Trim();
            // If there's a comma, take the last part for brevity
            int lastComma = afterParen.LastIndexOf(',');
            if (lastComma >= 0)
                return afterParen[(lastComma + 1)..].Trim();
            return afterParen;
        }
        return tz.StandardName;
    }

    private void RefreshLabels()
    {
        ExpandHint = _loc.Get("minibar.expand_hint");
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _clockTimer.Stop();
    }
}
