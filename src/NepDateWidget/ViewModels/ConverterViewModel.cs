using NepDateWidget.Helpers;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// View model for the Tools tab.
/// Three modes: Convert (BS↔AD), Days (Add/Subtract or Difference), and Time (timezone conversion).
/// All modes auto-compute on valid input and clear outputs while the user edits.
/// Input dates are validated through the NepDate adapter (BsToAd returns null for invalid dates).
/// </summary>
public sealed class ConverterViewModel : ViewModelBase
{
    private readonly IConversionService _conversionService;
    private readonly ILocalizationService _loc;
    private readonly INepaliDateAdapter _adapter;
    private readonly string _homeTimezoneId;
    private bool _initializing;

    // ═════════════════════════════════════════════════════════════════════════
    // MODE SELECTION  (0 = Convert, 1 = Days, 2 = Time)
    // ═════════════════════════════════════════════════════════════════════════

    private int _activeMode;
    public int ActiveMode
    {
        get => _activeMode;
        set
        {
            if (SetProperty(ref _activeMode, value))
            {
                var name = value switch { 0 => "Convert", 1 => "Days", 2 => "Time", _ => value.ToString() };
                Log.Action($"tools mode → {name}");
                OnPropertyChanged(nameof(IsModeConvert));
                OnPropertyChanged(nameof(IsModeDays));
                OnPropertyChanged(nameof(IsModeTime));
            }
        }
    }

    public bool IsModeConvert { get => _activeMode == 0; set { if (value) ActiveMode = 0; } }
    public bool IsModeDays    { get => _activeMode == 1; set { if (value) ActiveMode = 1; } }
    public bool IsModeTime    { get => _activeMode == 2; set { if (value) ActiveMode = 2; } }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE: CONVERT (BS↔AD)
    // ═════════════════════════════════════════════════════════════════════════

    private bool _isAdToBs = true;
    public bool IsAdToBs
    {
        get => _isAdToBs;
        set
        {
            if (SetProperty(ref _isAdToBs, value))
            {
                OnPropertyChanged(nameof(IsBsToAd));
                Log.Action($"tools convert dir → {(value ? "AD→BS" : "BS→AD")}");
                ConvertOutputShort = string.Empty;
                ConvertOutputLong = string.Empty;
                ConvertHasError = false;
                ConvertError = string.Empty;
                if (!string.IsNullOrWhiteSpace(_convertInput))
                    AutoConvert(_convertInput);
                UpdateConvertPlaceholder();
            }
        }
    }
    public bool IsBsToAd => !_isAdToBs;

    private string _convertInput = string.Empty;
    public string ConvertInput
    {
        get => _convertInput;
        set
        {
            if (SetProperty(ref _convertInput, value))
            {
                ConvertOutputShort = string.Empty;
                ConvertOutputLong = string.Empty;
                ConvertHasError = false;
                ConvertError = string.Empty;
                if (!string.IsNullOrWhiteSpace(value))
                    AutoConvert(value);
            }
        }
    }

    private string _convertOutputShort = string.Empty;
    public string ConvertOutputShort { get => _convertOutputShort; private set => SetProperty(ref _convertOutputShort, value); }

    private string _convertOutputLong = string.Empty;
    public string ConvertOutputLong { get => _convertOutputLong; private set => SetProperty(ref _convertOutputLong, value); }

    private bool _convertHasError;
    public bool ConvertHasError { get => _convertHasError; private set => SetProperty(ref _convertHasError, value); }

    private string _convertError = string.Empty;
    public string ConvertError { get => _convertError; private set => SetProperty(ref _convertError, value); }

    private string _convertPlaceholder = "2025-04-15";
    public string ConvertPlaceholder { get => _convertPlaceholder; private set => SetProperty(ref _convertPlaceholder, value); }

    // Legacy compat properties
    public string InputText { get => _convertInput; set => ConvertInput = value; }
    public bool HasError => _convertHasError;
    public string ErrorMessage => _convertError;
    public string OutputText => _convertOutputShort;

    // ═════════════════════════════════════════════════════════════════════════
    // MODE: DAYS
    // ═════════════════════════════════════════════════════════════════════════

    // false = Add/Subtract a number of days to a BS date
    // true  = Difference between two BS dates (shows breakdown + total days)
    private bool _isDaysDiff;
    public bool IsDaysDiff
    {
        get => _isDaysDiff;
        set
        {
            if (SetProperty(ref _isDaysDiff, value))
            {
                OnPropertyChanged(nameof(IsDaysAddSub));
                if (!_initializing) Log.Action($"tools days sub-mode → {(value ? "Diff" : "AddSub")}");
                if (value)
                {
                    // Diff mode: pre-fill Input2 with today's BS date if it's currently empty
                    if (string.IsNullOrWhiteSpace(_daysInput2))
                    {
                        var (ty, tm, td) = _adapter.GetTodayBs();
                        _daysInput2 = $"{ty}/{tm:D2}/{td:D2}";
                        OnPropertyChanged(nameof(DaysInput2));
                    }
                }
                else
                {
                    // AddSub mode: clear Input2 so the user can type an integer offset
                    _daysInput2 = string.Empty;
                    OnPropertyChanged(nameof(DaysInput2));
                }
                ClearDaysOutputs();
                RecomputeDays();
            }
        }
    }
    public bool IsDaysAddSub => !_isDaysDiff;

    // Input 1: BS date for both sub-modes  (e.g. "2082-01-01")
    private string _daysInput1 = string.Empty;
    public string DaysInput1
    {
        get => _daysInput1;
        set
        {
            if (SetProperty(ref _daysInput1, value))
            {
                ClearDaysOutputs();
                RecomputeDays();
            }
        }
    }

    // Input 2:  AddSub = integer days offset (e.g. "30" or "-10")
    //           Diff   = second BS date (pre-filled with today)
    private string _daysInput2 = string.Empty;
    public string DaysInput2
    {
        get => _daysInput2;
        set
        {
            if (SetProperty(ref _daysInput2, value))
            {
                ClearDaysOutputs();
                RecomputeDays();
            }
        }
    }

    // ── Add/Sub outputs ───────────────────────────────────────────────────────
    // Short = "2082/02/02",  Long = "Baisakh 31, 2082"
    private string _daysOutputShort = string.Empty;
    public string DaysOutputShort { get => _daysOutputShort; private set => SetProperty(ref _daysOutputShort, value); }

    private string _daysOutputLong = string.Empty;
    public string DaysOutputLong { get => _daysOutputLong; private set => SetProperty(ref _daysOutputLong, value); }

    // ── Diff outputs ──────────────────────────────────────────────────────────
    // Breakdown = "43 years, 2 months, 15 days",  Total = "16019"
    private string _daysOutputBreakdown = string.Empty;
    public string DaysOutputBreakdown { get => _daysOutputBreakdown; private set => SetProperty(ref _daysOutputBreakdown, value); }

    private string _daysOutputTotal = string.Empty;
    public string DaysOutputTotal { get => _daysOutputTotal; private set => SetProperty(ref _daysOutputTotal, value); }

    private bool _daysHasError;
    public bool DaysHasError { get => _daysHasError; private set => SetProperty(ref _daysHasError, value); }

    private string _daysError = string.Empty;
    public string DaysError { get => _daysError; private set => SetProperty(ref _daysError, value); }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE: TIME (timezone conversion)
    // ═════════════════════════════════════════════════════════════════════════

    public ObservableCollection<TimezoneItem> TimeFromZones { get; } = new();
    public ObservableCollection<TimezoneItem> TimeToZones { get; } = new();

    private TimezoneItem? _timeFromZone;
    public TimezoneItem? TimeFromZone
    {
        get => _timeFromZone;
        set
        {
            if (SetProperty(ref _timeFromZone, value))
            {
                ClearTimeOutputs();
                RecomputeTime();
            }
        }
    }

    private TimezoneItem? _timeToZone;
    public TimezoneItem? TimeToZone
    {
        get => _timeToZone;
        set
        {
            if (SetProperty(ref _timeToZone, value))
            {
                ClearTimeOutputs();
                RecomputeTime();
            }
        }
    }

    private string _timeInput = string.Empty;
    public string TimeInput
    {
        get => _timeInput;
        set
        {
            if (SetProperty(ref _timeInput, value))
            {
                ClearTimeOutputs();
                RecomputeTime();
            }
        }
    }

    private string _timeOutput = string.Empty;
    public string TimeOutput { get => _timeOutput; private set => SetProperty(ref _timeOutput, value); }

    private bool _timeIsAm = true;
    private bool _amPmToggledByUser;
    public bool TimeIsAm
    {
        get => _timeIsAm;
        set
        {
            if (SetProperty(ref _timeIsAm, value))
            {
                OnPropertyChanged(nameof(TimeAmPmLabel));
                ClearTimeOutputs();
                _amPmToggledByUser = true;
                RecomputeTime();
                _amPmToggledByUser = false;
            }
        }
    }
    public string TimeAmPmLabel => _timeIsAm ? "AM" : "PM";

    private bool _timeHasError;
    public bool TimeHasError { get => _timeHasError; private set => SetProperty(ref _timeHasError, value); }

    private string _timeError = string.Empty;
    public string TimeError { get => _timeError; private set => SetProperty(ref _timeError, value); }

    public ICommand TimeSwapCommand { get; private set; } = null!;
    public ICommand TimeToggleAmPmCommand { get; private set; } = null!;

    // ═════════════════════════════════════════════════════════════════════════
    // LABELS
    // ═════════════════════════════════════════════════════════════════════════

    private string _adToBsLabel = string.Empty;
    public string AdToBsLabel { get => _adToBsLabel; private set => SetProperty(ref _adToBsLabel, value); }

    private string _bsToAdLabel = string.Empty;
    public string BsToAdLabel { get => _bsToAdLabel; private set => SetProperty(ref _bsToAdLabel, value); }

    private string _titleLabel = string.Empty;
    public string TitleLabel { get => _titleLabel; private set => SetProperty(ref _titleLabel, value); }

    public string ModeLabelConvert { get; private set; } = string.Empty;
    public string ModeLabelDays    { get; private set; } = string.Empty;
    public string ModeLabelTime    { get; private set; } = string.Empty;
    public string DaysAddSubLabel { get; private set; } = string.Empty;
    public string DaysDiffLabel { get; private set; } = string.Empty;
    public string SwitchLabel { get; private set; } = string.Empty;
    public string InputLabel  { get; private set; } = string.Empty;
    public string TimeFromLabel { get; private set; } = string.Empty;
    public string TimeToLabel { get; private set; } = string.Empty;
    public string TooltipSwapTz      { get; private set; } = string.Empty;
    public string TooltipToggleFormat { get; private set; } = string.Empty;
    public string HintTime           { get; private set; } = string.Empty;

    // ═════════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═════════════════════════════════════════════════════════════════════════

    public ICommand SetModeConvertCommand { get; }
    public ICommand SetModeDaysCommand    { get; }
    public ICommand SetModeTimeCommand    { get; }
    public ICommand SetAdToBsCommand { get; }
    public ICommand SetBsToAdCommand { get; }
    public ICommand SetDaysAddSubCommand { get; }
    public ICommand SetDaysDiffCommand     { get; }
    public ICommand OpenHelpCommand        { get; }

    // ═════════════════════════════════════════════════════════════════════════
    // CONSTRUCTION
    // ═════════════════════════════════════════════════════════════════════════

    public ConverterViewModel(
        IConversionService conversionService,
        ILocalizationService localizationService,
        string defaultDirection = "ADtoBS",
        INepaliDateAdapter? adapter = null,
        string selectedTimezoneId = "")
    {
        _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _adapter = adapter ?? new NepaliDateAdapter();
        _homeTimezoneId = string.IsNullOrEmpty(selectedTimezoneId)
            ? TimeZoneInfo.Local.Id
            : selectedTimezoneId;

        _isAdToBs = !string.Equals(defaultDirection, "BStoAD", StringComparison.OrdinalIgnoreCase);

        SetModeConvertCommand = new RelayCommand(() => ActiveMode = 0);
        SetModeDaysCommand    = new RelayCommand(() => ActiveMode = 1);
        SetModeTimeCommand    = new RelayCommand(() => ActiveMode = 2);
        SetAdToBsCommand = new RelayCommand(() => IsAdToBs = true);
        SetBsToAdCommand = new RelayCommand(() => IsAdToBs = false);
        SetDaysAddSubCommand = new RelayCommand(() => IsDaysDiff = false);
        SetDaysDiffCommand = new RelayCommand(() => IsDaysDiff = true);
        TimeSwapCommand = new RelayCommand(SwapTimeZones);
        TimeToggleAmPmCommand = new RelayCommand(() => TimeIsAm = !TimeIsAm);
        OpenHelpCommand = new RelayCommand<string>(key =>
        {
            var shell = System.Windows.Application.Current.Windows
                .OfType<NepDateWidget.Views.ExpandedShellWindow>()
                .FirstOrDefault(w => w.IsVisible)
                ?? (System.Windows.Window)System.Windows.Application.Current.MainWindow!;
            NepDateWidget.Views.HelpPopup.ShowFor(key!, _loc, shell);
        });

        PopulateTimezones();
        RefreshLabels();
        _initializing = true;
        InitializeDefaults();
        _initializing = false;
    }

    public void OnLanguageChanged() => RefreshLabels();

    private void InitializeDefaults()
    {
        try
        {
            // Convert: default to today's date in the active direction
            if (_isAdToBs)
            {
                var today = _adapter.GetTodayAd();
                ConvertInput = today.ToString("yyyy-MM-dd");
            }
            else
            {
                var (y, m, d) = _adapter.GetTodayBs();
                ConvertInput = $"{y}/{m:D2}/{d:D2}";
            }

            // Days: default to Diff mode with Input1 = today's BS date
            var bs = _adapter.GetTodayBs();
            DaysInput1 = $"{bs.Year}/{bs.Month:D2}/{bs.Day:D2}";
            IsDaysDiff = true;

            // Time: default to current local time in 24h format
            TimeInput = DateTime.Now.ToString("h:mm");
            _timeIsAm = DateTime.Now.Hour < 12;
            OnPropertyChanged(nameof(TimeIsAm));
            OnPropertyChanged(nameof(TimeAmPmLabel));
        }
        catch
        {
            // Non-critical; leave fields empty on failure
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: CONVERT
    // ═════════════════════════════════════════════════════════════════════════

    private void AutoConvert(string text)
    {
        var trimmed = text.Trim();
        var result = _conversionService.ConvertFromText(trimmed, _isAdToBs);
        if (result.IsSuccess)
        {
            ConvertOutputShort = result.Result;
            ConvertOutputLong = result.ResultLong;
            if (!_initializing) Log.Action($"convert {(_isAdToBs ? "AD→BS" : "BS→AD")} | {trimmed} → {result.Result}");
            return;
        }

        // Only surface an error once the user has clearly attempted a full date,
        // so partial input like "20" does not light up the error label while typing.
        if (LooksLikeCommittedDateAttempt(trimmed))
        {
            ConvertHasError = true;
            ConvertError    = result.ErrorMessage;
        }
    }

    /// <summary>
    /// True when the input is long enough or contains a date separator, indicating
    /// the user meant to type a full date (rather than mid-typing the first digits).
    /// </summary>
    private static bool LooksLikeCommittedDateAttempt(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (s.Length >= 6) return true;
        foreach (var c in s)
            if (c == '/' || c == '-' || c == '.' || c == ' ') return true;
        return false;
    }

    private void UpdateConvertPlaceholder()
    {
        ConvertPlaceholder = _isAdToBs ? "2025-04-15" : "2081/01/15";
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: DAYS
    // ═════════════════════════════════════════════════════════════════════════

    private void ClearDaysOutputs()
    {
        DaysOutputShort = string.Empty;
        DaysOutputLong = string.Empty;
        DaysOutputBreakdown = string.Empty;
        DaysOutputTotal = string.Empty;
        DaysHasError = false;
        DaysError = string.Empty;
    }

    private void RecomputeDays()
    {
        if (string.IsNullOrWhiteSpace(_daysInput1)) return;
        if (!TryParseBsDate(_daysInput1, out int y1, out int m1, out int d1))
        {
            if (LooksLikeCommittedDateAttempt(_daysInput1.Trim()))
            {
                DaysHasError = true;
                DaysError    = "Invalid date";
            }
            return;
        }

        if (_isDaysDiff)
        {
            // Difference mode: two BS dates → years/months/days breakdown + total integer days
            if (string.IsNullOrWhiteSpace(_daysInput2)) return;
            if (!TryParseBsDate(_daysInput2, out int y2, out int m2, out int d2))
            {
                if (LooksLikeCommittedDateAttempt(_daysInput2.Trim()))
                {
                    DaysHasError = true;
                    DaysError    = "Invalid date";
                }
                return;
            }

            var breakdown = _adapter.DiffBreakdown(y1, m1, d1, y2, m2, d2);
            var total = _adapter.DiffTotalDays(y1, m1, d1, y2, m2, d2);

            if (breakdown is null || total is null)
            {
                DaysHasError = true;
                DaysError = "Invalid date";
                return;
            }

            var (years, months, days) = breakdown.Value;
            DaysOutputBreakdown = $"{years} years, {months} months, {days} days";
            DaysOutputTotal = Math.Abs(total.Value).ToString();
            if (!_initializing) Log.Action($"days diff | {_daysInput1}→{_daysInput2} | {DaysOutputBreakdown} | {DaysOutputTotal} days");
        }
        else
        {
            // Add/Subtract mode: BS date + integer offset → result BS date
            if (string.IsNullOrWhiteSpace(_daysInput2)) return;
            if (!int.TryParse(_daysInput2.Trim(), out int offset))
            {
                // Anything non-empty that isn't an integer is a real error - tell the user.
                DaysHasError = true;
                DaysError    = "Enter a whole number of days (e.g. 30 or -10)";
                return;
            }

            var result = _adapter.AddDays(y1, m1, d1, offset);
            if (result is null)
            {
                DaysHasError = true;
                DaysError = "Date out of range";
                return;
            }

            var (ry, rm, rd) = result.Value;
            bool isNe = string.Equals(_loc.CurrentLanguage, "ne", StringComparison.OrdinalIgnoreCase);
            DaysOutputShort = isNe ? _adapter.FormatBsShortNe(ry, rm, rd) : _adapter.FormatBsShortEn(ry, rm, rd);
            DaysOutputLong = isNe ? _adapter.FormatBsLongNe(ry, rm, rd) : _adapter.FormatBsLongEn(ry, rm, rd);
            Log.Action($"days addsub | {_daysInput1}{(offset >= 0 ? "+" : "")}{offset} → {DaysOutputShort}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: TIME
    // ═════════════════════════════════════════════════════════════════════════

    private void PopulateTimezones()
    {
        TimeFromZones.Clear();
        TimeToZones.Clear();

        TimezoneItem? homeFrom = null;
        TimezoneItem? homeTo = null;
        TimezoneItem? nepalFrom = null;

        foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        {
            var fromItem = TimezoneItem.FromTimeZoneInfo(tz);
            var toItem   = TimezoneItem.FromTimeZoneInfo(tz);
            TimeFromZones.Add(fromItem);
            TimeToZones.Add(toItem);

            if (string.Equals(tz.Id, _homeTimezoneId, StringComparison.OrdinalIgnoreCase))
            {
                homeFrom = fromItem;
                homeTo = toItem;
            }
            if (string.Equals(tz.Id, "Nepal Standard Time", StringComparison.OrdinalIgnoreCase))
            {
                nepalFrom = fromItem;
            }
        }

        // Default: From = Nepal, To = home timezone (from settings)
        _timeFromZone = nepalFrom ?? TimeFromZones.FirstOrDefault();
        _timeToZone = homeTo ?? TimeToZones.FirstOrDefault();
        OnPropertyChanged(nameof(TimeFromZone));
        OnPropertyChanged(nameof(TimeToZone));
    }

    private void SwapTimeZones()
    {
        var fromId = _timeFromZone?.Id;
        var toId = _timeToZone?.Id;

        _timeFromZone = TimeFromZones.FirstOrDefault(z => z.Id == toId);
        _timeToZone = TimeToZones.FirstOrDefault(z => z.Id == fromId);
        OnPropertyChanged(nameof(TimeFromZone));
        OnPropertyChanged(nameof(TimeToZone));

        ClearTimeOutputs();
        RecomputeTime();
        Log.Action("time swap zones");
    }

    private void ClearTimeOutputs()
    {
        TimeOutput = string.Empty;
        TimeHasError = false;
        TimeError = string.Empty;
    }

    private void RecomputeTime()
    {
        if (string.IsNullOrWhiteSpace(_timeInput)) return;
        if (_timeFromZone is null || _timeToZone is null) return;

        if (!TryParseTime(_timeInput.Trim(), out DateTime parsed, out bool hadExplicitAmPm))
        {
            TimeHasError = true;
            TimeError = "Invalid time (e.g. 2:30 PM, 1430, 9am, now)";
            return;
        }

        // Sync AM/PM button with input text, or apply button override.
        // When user typed explicit AM/PM: sync button to match (unless button was just toggled).
        // When no explicit AM/PM: apply the button state to the parsed time.
        // When button was toggled: override the parsed time to match the button.
        if (hadExplicitAmPm && !_amPmToggledByUser)
        {
            // Input says "2:30 PM" → sync button to PM
            _timeIsAm = parsed.Hour < 12;
            OnPropertyChanged(nameof(TimeIsAm));
            OnPropertyChanged(nameof(TimeAmPmLabel));
        }
        else
        {
            // Apply button state: shift parsed time to match AM/PM toggle
            if (parsed.Hour < 12 && !_timeIsAm)
                parsed = parsed.AddHours(12);
            else if (parsed.Hour >= 12 && _timeIsAm)
                parsed = parsed.AddHours(-12);
        }

        try
        {
            var fromTz = TimeZoneInfo.FindSystemTimeZoneById(_timeFromZone.Id);
            var toTz = TimeZoneInfo.FindSystemTimeZoneById(_timeToZone.Id);

            // Build a DateTimeOffset in the source timezone using today's date
            var today = DateTime.Today;
            var sourceDateTime = new DateTime(today.Year, today.Month, today.Day,
                parsed.Hour, parsed.Minute, parsed.Second, DateTimeKind.Unspecified);
            var sourceOffset = fromTz.GetUtcOffset(sourceDateTime);
            var sourceDateTimeOffset = new DateTimeOffset(sourceDateTime, sourceOffset);

            // Convert to target timezone
            var targetDateTimeOffset = TimeZoneInfo.ConvertTime(sourceDateTimeOffset, toTz);

            // Format output always in 12h with AM/PM
            var fromUtc = FormatUtcOffset(sourceOffset);
            var toUtc = FormatUtcOffset(toTz.GetUtcOffset(targetDateTimeOffset.DateTime));
            string timeStr = targetDateTimeOffset.ToString("h:mm tt");

            // Add day indicator if date changed.
            // Use a true date subtraction so December 31 → January 1 reports +1d,
            // not -364d (which the previous DayOfYear-difference produced).
            int dayDiff = (int)(targetDateTimeOffset.Date - sourceDateTimeOffset.Date).TotalDays;
            if (dayDiff != 0)
            {
                string dayTag = dayDiff > 0 ? $" (+{dayDiff}d)" : $" ({dayDiff}d)";
                timeStr += dayTag;
            }

            TimeOutput = $"{timeStr}  (UTC{fromUtc} → UTC{toUtc})";

            if (!_initializing) Log.Action($"time convert | {_timeInput} {_timeFromZone.Id} → {TimeOutput} {_timeToZone.Id}");
        }
        catch (Exception ex)
        {
            TimeHasError = true;
            TimeError = ex.Message;
        }
    }

    /// <summary>
    /// Flexibly parses any reasonable time input - mirrors NepDate's "parse anything" philosophy.
    /// Supported inputs include:
    ///   2:30 PM, 2:30PM, 2:30pm, 2:30 pm, 02:30 PM
    ///   14:30, 14:30:00, 1430, 230pm, 0230
    ///   2 PM, 2pm, 2PM, 2 am
    ///   2.30 PM, 2.30pm, 14.30
    ///   2-30 PM, 14-30
    ///   noon, midnight, mid
    ///   now
    /// </summary>
    private static bool TryParseTime(string input, out DateTime result, out bool hadExplicitAmPm)
    {
        result = default;
        hadExplicitAmPm = false;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var text = input.Trim();

        // ── Special keywords ──────────────────────────────────────────────
        if (text.Equals("now", StringComparison.OrdinalIgnoreCase))
        {
            var n = DateTime.Now;
            result = new DateTime(1, 1, 1, n.Hour, n.Minute, n.Second);
            hadExplicitAmPm = true;
            return true;
        }
        if (text.Equals("noon", StringComparison.OrdinalIgnoreCase))
        {
            result = new DateTime(1, 1, 1, 12, 0, 0);
            hadExplicitAmPm = true;
            return true;
        }
        if (text.Equals("midnight", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("mid", StringComparison.OrdinalIgnoreCase))
        {
            result = new DateTime(1, 1, 1, 0, 0, 0);
            hadExplicitAmPm = true;
            return true;
        }

        // Detect explicit AM/PM in input for downstream sync
        bool hasAmPmInText = System.Text.RegularExpressions.Regex.IsMatch(text,
            @"(am|pm|a\.m\.|p\.m\.)\s*$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // ── Try exact formats first (covers most common inputs) ───────────
        string[] formats =
        [
            "h:mm tt", "h:mmtt", "hh:mm tt", "hh:mmtt",
            "h:mm:ss tt", "hh:mm:ss tt",
            "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss",
            "h tt", "htt", "hh tt", "hhtt",
            "h.mm tt", "h.mmtt", "hh.mm tt", "hh.mmtt",
            "H.mm", "HH.mm",
            "h-mm tt", "h-mmtt",
            "H-mm", "HH-mm",
        ];
        if (DateTime.TryParseExact(text, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.NoCurrentDateDefault,
                out result))
        {
            hadExplicitAmPm = hasAmPmInText;
            return true;
        }

        // ── Flexible regex: extract digits + optional am/pm ───────────────
        // Handles: "230pm", "1430", "0230", "930am", "14", "9p", "9 a", etc.
        var match = System.Text.RegularExpressions.Regex.Match(text,
            @"^(\d{1,4})\s*([.:,-])?\s*(am|pm|a|p)?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            string digits = match.Groups[1].Value;
            string meridiem = match.Groups[3].Value.ToLowerInvariant();
            hadExplicitAmPm = meridiem.Length > 0;
            return TryBuildTime(digits, null, meridiem, out result);
        }

        // ── Space-separated: "2 30pm", "2 30 pm", "02 30am", "14 30" ─────
        var spaceMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"^(\d{1,2})\s+(\d{1,2})\s*(am|pm|a|p)?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (spaceMatch.Success)
        {
            string hourStr = spaceMatch.Groups[1].Value;
            string minStr = spaceMatch.Groups[2].Value;
            string meridiem = spaceMatch.Groups[3].Value.ToLowerInvariant();
            hadExplicitAmPm = meridiem.Length > 0;
            return TryBuildTime(hourStr, minStr, meridiem, out result);
        }

        return false;
    }

    private static bool TryBuildTime(string hourOrDigits, string? minuteStr, string meridiem, out DateTime result)
    {
        result = default;
        int hour, minute;

        if (minuteStr is not null)
        {
            // Separate hour + minute strings (space-separated path)
            if (!int.TryParse(hourOrDigits, out hour) || !int.TryParse(minuteStr, out minute))
                return false;
        }
        else
        {
            // Combined digits: "9", "14", "230", "1430"
            minute = 0;
            switch (hourOrDigits.Length)
            {
                case 1 or 2:
                    hour = int.Parse(hourOrDigits);
                    break;
                case 3:
                    hour = int.Parse(hourOrDigits[..1]);
                    minute = int.Parse(hourOrDigits[1..]);
                    break;
                case 4:
                    hour = int.Parse(hourOrDigits[..2]);
                    minute = int.Parse(hourOrDigits[2..]);
                    break;
                default:
                    return false;
            }
        }

        // Apply AM/PM
        if (meridiem is "pm" or "p")
        {
            if (hour < 12) hour += 12;
        }
        else if (meridiem is "am" or "a")
        {
            if (hour == 12) hour = 0;
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
            return false;

        result = new DateTime(1, 1, 1, hour, minute, 0);
        return true;
    }

    private static string FormatUtcOffset(TimeSpan offset)
    {
        string sign = offset >= TimeSpan.Zero ? "+" : "-";
        var abs = offset.Duration();
        return abs.Minutes == 0
            ? $"{sign}{abs.Hours}"
            : $"{sign}{abs.Hours}:{abs.Minutes:D2}";
    }

    /// <summary>
    /// Called by MainViewModel when the user changes the timezone in Settings.
    /// Updates the "To" zone to the newly selected home timezone.
    /// </summary>
    public void UpdateHomeTimezone(string newTimezoneId)
    {
        var match = TimeToZones.FirstOrDefault(z =>
            string.Equals(z.Id, newTimezoneId, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            TimeToZone = match;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parses a BS date string ("2082-12-21", "2082/12/21", "2082.12.21") and validates it
    /// through the NepDate adapter - meaning the adapter calls NepDate.NepaliDate constructor
    /// which enforces per-month day limits and the library's supported range.
    /// </summary>
    private bool TryParseBsDate(string input, out int year, out int month, out int day)
    {
        year = month = day = 0;
        return !string.IsNullOrWhiteSpace(input)
            && _adapter.TryParseSmartBsDate(input, out year, out month, out day);
    }

    private void RefreshLabels()
    {
        AdToBsLabel = _loc.Get("converter.ad_to_bs");
        BsToAdLabel = _loc.Get("converter.bs_to_ad");
        TitleLabel = _loc.Get("converter.title");

        ModeLabelConvert = _loc.Get("tools.mode_convert");
        ModeLabelDays    = _loc.Get("tools.mode_days");
        ModeLabelTime    = _loc.Get("tools.mode_time");
        DaysAddSubLabel = _loc.Get("tools.days_addsub");
        DaysDiffLabel = _loc.Get("tools.days_diff");
        SwitchLabel = _loc.Get("converter.switch_label");
        InputLabel  = _loc.Get("converter.input_label");
        TimeFromLabel = _loc.Get("tools.time_from");
        TimeToLabel = _loc.Get("tools.time_to");
        TooltipSwapTz      = _loc.Get("tooltip.swap_tz");
        TooltipToggleFormat = _loc.Get("tooltip.toggle_format");
        HintTime           = _loc.Get("hint.time");

        UpdateConvertPlaceholder();

        OnPropertyChanged(nameof(AdToBsLabel));
        OnPropertyChanged(nameof(BsToAdLabel));
        OnPropertyChanged(nameof(TitleLabel));
        OnPropertyChanged(nameof(ModeLabelConvert));
        OnPropertyChanged(nameof(ModeLabelDays));
        OnPropertyChanged(nameof(ModeLabelTime));
        OnPropertyChanged(nameof(DaysAddSubLabel));
        OnPropertyChanged(nameof(DaysDiffLabel));
        OnPropertyChanged(nameof(SwitchLabel));
        OnPropertyChanged(nameof(InputLabel));
        OnPropertyChanged(nameof(TimeFromLabel));
        OnPropertyChanged(nameof(TimeToLabel));
        OnPropertyChanged(nameof(TooltipSwapTz));
        OnPropertyChanged(nameof(TooltipToggleFormat));
        OnPropertyChanged(nameof(HintTime));
    }
}
