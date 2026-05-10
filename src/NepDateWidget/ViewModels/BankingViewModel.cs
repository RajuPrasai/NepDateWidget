using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// View model for the Banking tab.
/// Two modes: Interest (0) - simple interest with variable rate periods,
///            EMI (1)      - equated monthly instalment on a reducing balance.
/// </summary>
public sealed class BankingViewModel : ViewModelBase
{
    private readonly ILocalizationService _loc;
    private readonly INepaliDateAdapter _adapter;

    // ═════════════════════════════════════════════════════════════════════════
    // MODE SELECTION  (0 = Interest, 1 = EMI)
    // ═════════════════════════════════════════════════════════════════════════

    private int _activeMode;
    public int ActiveMode
    {
        get => _activeMode;
        set
        {
            if (SetProperty(ref _activeMode, value))
            {
                var name = value switch { 0 => "Interest", 1 => "EMI", _ => value.ToString() };
                Log.Action($"banking mode → {name}");
                OnPropertyChanged(nameof(IsModeInterest));
                OnPropertyChanged(nameof(IsModeEmi));
            }
        }
    }

    public bool IsModeInterest { get => _activeMode == 0; set { if (value) ActiveMode = 0; } }
    public bool IsModeEmi      { get => _activeMode == 1; set { if (value) ActiveMode = 1; } }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE: INTEREST CALCULATOR
    // ═════════════════════════════════════════════════════════════════════════

    private string _interestPrincipal = string.Empty;
    public string InterestPrincipal
    {
        get => _interestPrincipal;
        set { if (SetProperty(ref _interestPrincipal, value)) ClearInterestResult(); }
    }

    private string _interestFromDate = string.Empty;
    public string InterestFromDate
    {
        get => _interestFromDate;
        set
        {
            if (SetProperty(ref _interestFromDate, value))
            {
                ClearInterestResult();
                if (InterestRows.Count > 0)
                    InterestRows[0].SyncFromDate(value);
            }
        }
    }

    private string _interestToDate = string.Empty;
    public string InterestToDate
    {
        get => _interestToDate;
        set { if (SetProperty(ref _interestToDate, value)) ClearInterestResult(); }
    }

    private bool _interestUseBs = true;
    public bool InterestUseBs
    {
        get => _interestUseBs;
        set
        {
            if (SetProperty(ref _interestUseBs, value))
            {
                OnPropertyChanged(nameof(InterestDatePlaceholder));
                ClearInterestResult();
                ConvertInterestDates(toBS: value);
                Log.Action($"banking interest date mode → {(value ? "BS" : "AD")}");
            }
        }
    }

    public string InterestDatePlaceholder => _interestUseBs ? "2082/01/01" : "2026-04-01";

    public ObservableCollection<InterestRateRowViewModel> InterestRows { get; } = new();

    private bool _interestHasResult;
    public bool InterestHasResult { get => _interestHasResult; private set => SetProperty(ref _interestHasResult, value); }

    private bool _interestHasError;
    public bool InterestHasError { get => _interestHasError; private set => SetProperty(ref _interestHasError, value); }

    private string _interestError = string.Empty;
    public string InterestError { get => _interestError; private set => SetProperty(ref _interestError, value); }

    private string _interestBreakdownText = string.Empty;
    public string InterestBreakdownText { get => _interestBreakdownText; private set => SetProperty(ref _interestBreakdownText, value); }

    private string _interestTotalText = string.Empty;
    public string InterestTotalText { get => _interestTotalText; private set => SetProperty(ref _interestTotalText, value); }

    public ICommand SetModeInterestCommand   { get; }
    public ICommand SetModeEmiCommand        { get; }
    public ICommand AddInterestRowCommand    { get; }
    public ICommand CalculateInterestCommand { get; }
    public ICommand SetInterestBsCommand     { get; }
    public ICommand SetInterestAdCommand     { get; }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE: EMI CALCULATOR
    // ═════════════════════════════════════════════════════════════════════════
    private bool _emiUseBs = true;
    public bool EmiUseBs
    {
        get => _emiUseBs;
        set
        {
            if (SetProperty(ref _emiUseBs, value))
            {
                if (!string.IsNullOrWhiteSpace(_emiStartDate))
                {
                    string converted = value ? ConvertAdToBsStr(_emiStartDate) : ConvertBsToAdStr(_emiStartDate);
                    _emiStartDate = !string.IsNullOrEmpty(converted) ? converted : string.Empty;
                    OnPropertyChanged(nameof(EmiStartDate));
                }
                ClearEmiResult();
                OnPropertyChanged(nameof(EmiDatePlaceholder));
                OnPropertyChanged(nameof(EmiStartDateLabel));
                Log.Action($"banking emi date mode → {(value ? "BS" : "AD")}");
            }
        }
    }
    public ICommand SetEmiBsCommand { get; }
    public ICommand SetEmiAdCommand { get; }
    public string EmiDatePlaceholder => _emiUseBs ? "2082/01/01" : "2026-04-01";
    private string _emiLoanAmount = string.Empty;
    public string EmiLoanAmount
    {
        get => _emiLoanAmount;
        set { if (SetProperty(ref _emiLoanAmount, value)) ClearEmiResult(); }
    }

    private string _emiAnnualRate = string.Empty;
    public string EmiAnnualRate
    {
        get => _emiAnnualRate;
        set { if (SetProperty(ref _emiAnnualRate, value)) ClearEmiResult(); }
    }

    private string _emiMonths = string.Empty;
    public string EmiMonths
    {
        get => _emiMonths;
        set { if (SetProperty(ref _emiMonths, value)) ClearEmiResult(); }
    }

    private string _emiStartDate = string.Empty;
    public string EmiStartDate
    {
        get => _emiStartDate;
        set { if (SetProperty(ref _emiStartDate, value)) ClearEmiResult(); }
    }

    private bool _emiHasResult;
    public bool EmiHasResult { get => _emiHasResult; private set => SetProperty(ref _emiHasResult, value); }

    private bool _emiHasError;
    public bool EmiHasError { get => _emiHasError; private set => SetProperty(ref _emiHasError, value); }

    private string _emiError = string.Empty;
    public string EmiError { get => _emiError; private set => SetProperty(ref _emiError, value); }

    private string _emiMonthlyText = string.Empty;
    public string EmiMonthlyText { get => _emiMonthlyText; private set => SetProperty(ref _emiMonthlyText, value); }

    private string _emiTotalPaymentText = string.Empty;
    public string EmiTotalPaymentText { get => _emiTotalPaymentText; private set => SetProperty(ref _emiTotalPaymentText, value); }

    private string _emiTotalInterestText = string.Empty;
    public string EmiTotalInterestText { get => _emiTotalInterestText; private set => SetProperty(ref _emiTotalInterestText, value); }

    // All visible schedule rows (year headers + expanded month rows)
    public ObservableCollection<EmiScheduleRow> EmiScheduleRows { get; } = new();

    // Full grouped data retained so toggle re-renders without recomputing
    private readonly List<(EmiScheduleRow YearRow, List<EmiScheduleRow> Months)> _emiGroups = new();

    public ICommand CalculateEmiCommand   { get; }
    public ICommand ToggleEmiYearCommand  { get; }

    // ═════════════════════════════════════════════════════════════════════════

    public string ModeInterestLabel      { get; private set; } = string.Empty;
    public string ModeEmiLabel           { get; private set; } = string.Empty;
    public string InterestPrincipalLabel { get; private set; } = string.Empty;
    public string InterestFromLabel      { get; private set; } = string.Empty;
    public string InterestToLabel        { get; private set; } = string.Empty;
    public string InterestRateColLabel   { get; private set; } = string.Empty;
    public string InterestAddLabel       { get; private set; } = string.Empty;
    public string InterestCalcLabel      { get; private set; } = string.Empty;
    public string EmiLoanLabel           { get; private set; } = string.Empty;
    public string EmiRateLabel           { get; private set; } = string.Empty;
    public string EmiMonthsLabel         { get; private set; } = string.Empty;
    public string EmiStartDateLabel      => _loc.Get(_emiUseBs ? "banking.emi_start_date_bs" : "banking.emi_start_date_ad");
    public string EmiCalcLabel           { get; private set; } = string.Empty;
    public string EmiMonthlyLabel        { get; private set; } = string.Empty;
    public string EmiTotalPaymentLabel   { get; private set; } = string.Empty;
    public string EmiTotalInterestLabel  { get; private set; } = string.Empty;
    public string EmiColYearLabel        { get; private set; } = string.Empty;
    public string EmiColPrincipalLabel   { get; private set; } = string.Empty;
    public string EmiColInterestLabel    { get; private set; } = string.Empty;
    public string EmiColTotalLabel       { get; private set; } = string.Empty;
    public string EmiColBalanceLabel     { get; private set; } = string.Empty;

    public string HintPrincipal { get; private set; } = string.Empty;
    public string HintRate      { get; private set; } = string.Empty;
    public string HintMonths    { get; private set; } = string.Empty;
    public string HintLoan      { get; private set; } = string.Empty;
    public string HintDateBs    { get; private set; } = string.Empty;

    // ═════════════════════════════════════════════════════════════════════════
    // CONSTRUCTION
    // ═════════════════════════════════════════════════════════════════════════

    public BankingViewModel(ILocalizationService localizationService, INepaliDateAdapter? adapter = null)
    {
        _loc     = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _adapter = adapter ?? new NepaliDateAdapter();

        SetModeInterestCommand   = new RelayCommand(() => ActiveMode = 0);
        SetModeEmiCommand        = new RelayCommand(() => ActiveMode = 1);
        AddInterestRowCommand    = new RelayCommand(DoAddInterestRow);
        CalculateInterestCommand = new RelayCommand(DoCalculateInterest);
        SetInterestBsCommand     = new RelayCommand(() => InterestUseBs = true);
        SetInterestAdCommand     = new RelayCommand(() => InterestUseBs = false);
        CalculateEmiCommand      = new RelayCommand(DoCalculateEmi);
        ToggleEmiYearCommand     = new RelayCommand<EmiScheduleRow>(DoToggleEmiYear);
        SetEmiBsCommand          = new RelayCommand(() => EmiUseBs = true);
        SetEmiAdCommand          = new RelayCommand(() => EmiUseBs = false);

        // Seed one empty first row for interest.
        AddRowInternal(string.Empty, string.Empty, isFirstRow: true);

        RefreshLabels();
        InitializeDefaults();
    }

    public void OnLanguageChanged() => RefreshLabels();

    private void InitializeDefaults()
    {
        try
        {
            var (y, m, d) = _adapter.GetTodayBs();
            var todayBs = $"{y}/{m:D2}/{d:D2}";

            // Interest: default From date to today, first row date to today
            InterestFromDate = todayBs;
            if (InterestRows.Count > 0)
                InterestRows[0].FromDate = todayBs;

            // EMI: default start date to today
            EmiStartDate = todayBs;
        }
        catch
        {
            // Non-critical
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: INTEREST
    // ═════════════════════════════════════════════════════════════════════════

    private void AddRowInternal(string fromDate, string rate, bool isFirstRow)
    {
        var row = new InterestRateRowViewModel(fromDate, rate, isFirstRow, RemoveInterestRow);
        row.Changed += (_, _) => ClearInterestResult();
        InterestRows.Add(row);
    }

    private void RemoveInterestRow(InterestRateRowViewModel row)
    {
        if (row.IsFirstRow) return;
        InterestRows.Remove(row);
        ClearInterestResult();
        Log.Action("banking interest: removed row");
    }

    private void DoAddInterestRow()
    {
        string nextFrom = string.Empty;
        if (InterestRows.Count > 0)
            nextFrom = GetNextMonthFirstDay(InterestRows[^1].FromDate);
        AddRowInternal(nextFrom, string.Empty, isFirstRow: false);
        ClearInterestResult();
        Log.Action("banking interest: added row");
    }

    private void DoCalculateInterest()
    {
        ClearInterestResult();

        if (!double.TryParse(
                _interestPrincipal.Replace(",", "").Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double principal) || principal <= 0)
        {
            InterestHasError = true;
            InterestError    = _loc.Get("interest.error_principal");
            return;
        }

        bool toDateValid = _interestUseBs
            ? TryParseInterestDate(_interestToDate, out _, out _, out _)
            : TryParseAdDate(_interestToDate, out _);
        if (!toDateValid)
        {
            InterestHasError = true;
            InterestError    = _loc.Get("interest.error_to");
            return;
        }

        if (InterestRows.Count == 0) return;

        var    breakdown     = new StringBuilder();
        double totalInterest = 0;

        for (int i = 0; i < InterestRows.Count; i++)
        {
            var row     = InterestRows[i];
            var fromStr = row.FromDate;
            var toStr   = (i + 1 < InterestRows.Count)
                ? DateMinusOneDay(InterestRows[i + 1].FromDate)
                : _interestToDate;

            if (!double.TryParse(
                    row.Rate.Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double rate) || rate <= 0)
            {
                InterestHasError = true;
                InterestError    = $"{_loc.Get("interest.error_row_rate")} {i + 1}";
                return;
            }

            int days;
            if (_interestUseBs)
            {
                if (!TryParseInterestDate(fromStr, out int y1, out int m1, out int d1) ||
                    !TryParseInterestDate(toStr,   out int y2, out int m2, out int d2))
                {
                    InterestHasError = true;
                    InterestError    = $"{_loc.Get("interest.error_row_date")} {i + 1}";
                    return;
                }
                int? diff = _adapter.DiffTotalDays(y1, m1, d1, y2, m2, d2);
                if (diff == null || diff.Value <= 0)
                {
                    InterestHasError = true;
                    InterestError    = $"{_loc.Get("interest.error_negative_days")} (row {i + 1})";
                    return;
                }
                days = diff.Value;
            }
            else
            {
                if (!TryParseAdDate(fromStr, out DateTime adFrom) ||
                    !TryParseAdDate(toStr,   out DateTime adTo))
                {
                    InterestHasError = true;
                    InterestError    = $"{_loc.Get("interest.error_row_date")} {i + 1}";
                    return;
                }
                days = (int)(adTo.Date - adFrom.Date).TotalDays;
                if (days <= 0)
                {
                    InterestHasError = true;
                    InterestError    = $"{_loc.Get("interest.error_negative_days")} (row {i + 1})";
                    return;
                }
            }

            // Simple interest: I = (P × R × D) / DaysPerYear100
            const double DaysPerYear100 = 36500.0;
            double interest = (principal * rate * days) / DaysPerYear100;
            totalInterest += interest;

            if (breakdown.Length > 0) breakdown.Append('\n');
            breakdown.Append(
                $"{fromStr}  \u2013  {toStr}  ({days} days)  @ {rate:0.##}%  =  {FormatAmount(interest)}");
        }

        InterestBreakdownText = breakdown.ToString();
        InterestTotalText     =
            $"{FormatAmount(principal)}  +  {FormatAmount(totalInterest)}  =  {FormatNpr(principal + totalInterest)}";
        InterestHasResult = true;

        Log.Action($"banking interest | principal={principal} rows={InterestRows.Count} total={totalInterest:F2}");
    }

    private void ClearInterestResult()
    {
        InterestHasResult     = false;
        InterestHasError      = false;
        InterestError         = string.Empty;
        InterestBreakdownText = string.Empty;
        InterestTotalText     = string.Empty;
    }

    private string GetNextMonthFirstDay(string dateStr)
    {
        if (_interestUseBs)
        {
            if (TryParseInterestDate(dateStr, out int y, out int m, out _))
            {
                m++;
                if (m > 12) { m = 1; y++; }
                return $"{y:D4}/{m:D2}/01";
            }
        }
        else
        {
            if (TryParseAdDate(dateStr, out DateTime dt))
            {
                var next = new DateTime(dt.Year, dt.Month, 1).AddMonths(1);
                return next.ToString("yyyy-MM-dd");
            }
        }
        return string.Empty;
    }

    private string DateMinusOneDay(string dateStr)
    {
        if (_interestUseBs)
        {
            if (TryParseInterestDate(dateStr, out int y, out int m, out int d))
            {
                DateTime? adDate = _adapter.BsToAd(y, m, d);
                if (adDate.HasValue)
                {
                    DateTime prev = adDate.Value.AddDays(-1);
                    var bs = _adapter.AdToBs(prev);
                    if (bs.HasValue)
                        return $"{bs.Value.Year:D4}/{bs.Value.Month:D2}/{bs.Value.Day:D2}";
                }
            }
        }
        else
        {
            if (TryParseAdDate(dateStr, out DateTime dt))
                return dt.AddDays(-1).ToString("yyyy-MM-dd");
        }
        return dateStr;
    }

    private bool TryParseInterestDate(string text, out int y, out int m, out int d)
    {
        y = m = d = 0;
        return !string.IsNullOrWhiteSpace(text) && _adapter.TryParseSmartBsDate(text.Trim(), out y, out m, out d);
    }

    private static bool TryParseAdDate(string text, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return DateTime.TryParse(
            text.Trim(),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out result);
    }

    /// <summary>
    /// Parses a Nepali (BS) date string intelligently.
    /// Accepts: YYYY/MM/DD, YYYY-MM-DD, YYYY MM DD, YYYY/MM (day defaults to 1).
    /// Converts to AD using the adapter.
    /// </summary>
    private bool TryParseBsDate(string text, out DateTime adResult)
    {
        adResult = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();

        if (_adapter.TryParseSmartBsDate(text, out int year, out int month, out int day))
        {
            var dt = _adapter.BsToAd(year, month, day);
            if (dt.HasValue) { adResult = dt.Value; return true; }
        }

        // Fallback: accept YYYY/MM with day defaulting to 1
        var parts = text.Split(new[] { '/', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out year)
            && int.TryParse(parts[1], out month))
        {
            var dt = _adapter.BsToAd(year, month, 1);
            if (dt.HasValue) { adResult = dt.Value; return true; }
        }

        return false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: EMI
    // ═════════════════════════════════════════════════════════════════════════

    private void DoCalculateEmi()
    {
        ClearEmiResult();

        if (!double.TryParse(
                _emiLoanAmount.Replace(",", "").Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double principal) || principal <= 0)
        {
            EmiHasError = true;
            EmiError    = _loc.Get("banking.emi_error_loan");
            return;
        }

        if (!double.TryParse(
                _emiAnnualRate.Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out double annualRate) || annualRate < 0)
        {
            EmiHasError = true;
            EmiError    = _loc.Get("banking.emi_error_rate");
            return;
        }

        if (!int.TryParse(_emiMonths.Trim(), out int months) || months <= 0)
        {
            EmiHasError = true;
            EmiError    = _loc.Get("banking.emi_error_months");
            return;
        }

        // Parse start date. If blank, use today. Supports both BS and AD depending on EmiUseBs.
        DateTime startAd;
        if (string.IsNullOrWhiteSpace(_emiStartDate))
        {
            startAd = _adapter.GetTodayAd();
        }
        else if (_emiUseBs)
        {
            if (!TryParseBsDate(_emiStartDate, out startAd))
            {
                EmiHasError = true;
                EmiError    = _loc.Get("banking.emi_error_start_date");
                return;
            }
        }
        else
        {
            if (!TryParseAdDate(_emiStartDate, out startAd))
            {
                EmiHasError = true;
                EmiError    = _loc.Get("banking.emi_error_start_date_ad");
                return;
            }
        }

        // Reducing balance EMI: EMI = P * r * (1+r)^n / ((1+r)^n - 1)
        double r = annualRate / 12.0 / 100.0;
        double emi = r == 0
            ? principal / months
            : principal * r * Math.Pow(1 + r, months) / (Math.Pow(1 + r, months) - 1);

        double totalPayment  = emi * months;
        double totalInterest = totalPayment - principal;

        EmiMonthlyText       = FormatNpr(emi);
        EmiTotalPaymentText  = FormatNpr(totalPayment);
        EmiTotalInterestText = FormatNpr(totalInterest);

        // Build amortisation schedule grouped by calendar year
        _emiGroups.Clear();
        double balance = principal;
        DateTime paymentDate = new DateTime(startAd.Year, startAd.Month, 1).AddMonths(1);

        // Accumulate month rows first, then group
        var allMonths = new List<(int CalYear, string Period, string BsDate, string AdDate, double Princ, double Int, double Total, double Bal)>();

        for (int i = 0; i < months; i++)
        {
            double interest = balance * r;
            double principalPart;
            if (i == months - 1)
            {
                // Last payment: clear the remaining balance
                principalPart = balance;
            }
            else
            {
                principalPart = emi - interest;
                if (principalPart > balance) principalPart = balance;
            }
            balance -= principalPart;
            if (balance < 0) balance = 0;

            // Convert payment date to BS
            var bs = _adapter.AdToBs(paymentDate);
            string bsStr = bs.HasValue
                ? $"{bs.Value.Year:D4}/{bs.Value.Month:D2}/{bs.Value.Day:D2}"
                : string.Empty;
            string adStr  = paymentDate.ToString("MMM yyyy");
            string period = paymentDate.ToString("MMM");

            allMonths.Add((paymentDate.Year, period, bsStr, adStr,
                           principalPart, interest, principalPart + interest, balance));

            paymentDate = paymentDate.AddMonths(1);
        }

        // Group by calendar year
        foreach (var yearGroup in allMonths.GroupBy(m => m.CalYear))
        {
            double yPrinc = yearGroup.Sum(m => m.Princ);
            double yInt   = yearGroup.Sum(m => m.Int);
            double yTotal = yearGroup.Sum(m => m.Total);
            double yBal   = yearGroup.Last().Bal;

            var yearRow = new EmiScheduleRow
            {
                IsYearRow   = true,
                IsExpanded  = yearGroup.Key == allMonths[0].CalYear, // first year open by default
                Period      = yearGroup.Key.ToString(),
                Principal   = FormatAmount(yPrinc),
                Interest    = FormatAmount(yInt),
                TotalPayment = FormatAmount(yTotal),
                Balance     = FormatAmount(yBal),
                CalendarYear = yearGroup.Key,
            };

            var monthRows = yearGroup.Select(m => new EmiScheduleRow
            {
                IsYearRow    = false,
                Period       = m.Period,
                BsDate       = m.BsDate,
                AdDate       = m.AdDate,
                Principal    = FormatAmount(m.Princ),
                Interest     = FormatAmount(m.Int),
                TotalPayment = FormatAmount(m.Total),
                Balance      = FormatAmount(m.Bal),
                CalendarYear = m.CalYear,
            }).ToList();

            _emiGroups.Add((yearRow, monthRows));
        }

        RebuildScheduleRows();
        EmiHasResult = true;

        Log.Action($"banking emi | principal={principal} rate={annualRate}% months={months} emi={emi:F2}");
    }

    private void DoToggleEmiYear(EmiScheduleRow? row)
    {
        if (row == null || !row.IsYearRow) return;
        row.IsExpanded = !row.IsExpanded;
        RebuildScheduleRows();
    }

    private void RebuildScheduleRows()
    {
        EmiScheduleRows.Clear();
        foreach (var (yearRow, months) in _emiGroups)
        {
            EmiScheduleRows.Add(yearRow);
            if (yearRow.IsExpanded)
            {
                foreach (var m in months)
                    EmiScheduleRows.Add(m);
            }
        }
    }

    private void ClearEmiResult()
    {
        EmiHasResult         = false;
        EmiHasError          = false;
        EmiError             = string.Empty;
        EmiMonthlyText       = string.Empty;
        EmiTotalPaymentText  = string.Empty;
        EmiTotalInterestText = string.Empty;
        _emiGroups.Clear();
        EmiScheduleRows.Clear();
    }

    // Converts all Interest date fields between BS and AD when the mode toggle changes.
    private void ConvertInterestDates(bool toBS)
    {
        string newFrom = toBS ? ConvertAdToBsStr(_interestFromDate) : ConvertBsToAdStr(_interestFromDate);
        if (!string.IsNullOrWhiteSpace(_interestFromDate))
            InterestFromDate = newFrom; // also syncs InterestRows[0] via setter

        string newTo = toBS ? ConvertAdToBsStr(_interestToDate) : ConvertBsToAdStr(_interestToDate);
        if (!string.IsNullOrWhiteSpace(_interestToDate))
            InterestToDate = newTo;

        // Skip index 0: it is already synced from InterestFromDate above.
        for (int i = 1; i < InterestRows.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(InterestRows[i].FromDate)) continue;
            InterestRows[i].FromDate = toBS
                ? ConvertAdToBsStr(InterestRows[i].FromDate)
                : ConvertBsToAdStr(InterestRows[i].FromDate);
        }
    }

    // BS "YYYY/MM/DD" → AD "yyyy-MM-dd". Returns empty string on parse failure.
    private string ConvertBsToAdStr(string bsDate)
    {
        if (string.IsNullOrWhiteSpace(bsDate)) return bsDate;
        if (!TryParseInterestDate(bsDate, out int y, out int m, out int d)) return string.Empty;
        var ad = _adapter.BsToAd(y, m, d);
        return ad.HasValue ? ad.Value.ToString("yyyy-MM-dd") : string.Empty;
    }

    // AD "yyyy-MM-dd" → BS "YYYY/MM/DD". Returns empty string on parse failure.
    private string ConvertAdToBsStr(string adDate)
    {
        if (string.IsNullOrWhiteSpace(adDate)) return adDate;
        if (!TryParseAdDate(adDate, out DateTime dt)) return string.Empty;
        var bs = _adapter.AdToBs(dt);
        return bs.HasValue ? $"{bs.Value.Year:D4}/{bs.Value.Month:D2}/{bs.Value.Day:D2}" : string.Empty;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: SHARED HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    private static string FormatAmount(double amount)
        => $"{(long)Math.Round(amount, MidpointRounding.AwayFromZero):N0}";

    private static string FormatNpr(double amount)
        => $"NPR {(long)Math.Round(amount, MidpointRounding.AwayFromZero):N0}/-";

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE: LABELS
    // ═════════════════════════════════════════════════════════════════════════

    private void RefreshLabels()
    {
        ModeInterestLabel      = _loc.Get("banking.mode_interest");
        ModeEmiLabel           = _loc.Get("banking.mode_emi");
        InterestPrincipalLabel = _loc.Get("interest.principal");
        InterestFromLabel      = _loc.Get("interest.from");
        InterestToLabel        = _loc.Get("interest.to");
        InterestRateColLabel   = _loc.Get("interest.rate_col");
        InterestAddLabel       = _loc.Get("interest.add_period");
        InterestCalcLabel      = _loc.Get("interest.calculate");
        EmiLoanLabel           = _loc.Get("banking.emi_loan");
        EmiRateLabel           = _loc.Get("banking.emi_rate");
        EmiMonthsLabel         = _loc.Get("banking.emi_months");
        // EmiStartDateLabel is a computed property - no assignment needed here.
        EmiCalcLabel           = _loc.Get("banking.emi_calculate");
        EmiMonthlyLabel        = _loc.Get("banking.emi_monthly");
        EmiTotalPaymentLabel   = _loc.Get("banking.emi_total_payment");
        EmiTotalInterestLabel  = _loc.Get("banking.emi_total_interest");
        EmiColYearLabel        = _loc.Get("banking.emi_col_year");
        EmiColPrincipalLabel   = _loc.Get("banking.emi_col_principal");
        EmiColInterestLabel    = _loc.Get("banking.emi_col_interest");
        EmiColTotalLabel       = _loc.Get("banking.emi_col_total");
        EmiColBalanceLabel     = _loc.Get("banking.emi_col_balance");

        HintPrincipal = _loc.Get("hint.principal");
        HintRate      = _loc.Get("hint.rate");
        HintMonths    = _loc.Get("hint.months");
        HintLoan      = _loc.Get("hint.loan");
        HintDateBs    = _loc.Get("hint.date_bs");

        OnPropertyChanged(nameof(ModeInterestLabel));
        OnPropertyChanged(nameof(ModeEmiLabel));
        OnPropertyChanged(nameof(InterestPrincipalLabel));
        OnPropertyChanged(nameof(InterestFromLabel));
        OnPropertyChanged(nameof(InterestToLabel));
        OnPropertyChanged(nameof(InterestRateColLabel));
        OnPropertyChanged(nameof(InterestAddLabel));
        OnPropertyChanged(nameof(InterestCalcLabel));
        OnPropertyChanged(nameof(EmiLoanLabel));
        OnPropertyChanged(nameof(EmiRateLabel));
        OnPropertyChanged(nameof(EmiMonthsLabel));
        OnPropertyChanged(nameof(EmiStartDateLabel));
        OnPropertyChanged(nameof(EmiCalcLabel));
        OnPropertyChanged(nameof(EmiMonthlyLabel));
        OnPropertyChanged(nameof(EmiTotalPaymentLabel));
        OnPropertyChanged(nameof(EmiTotalInterestLabel));
        OnPropertyChanged(nameof(EmiColYearLabel));
        OnPropertyChanged(nameof(EmiColPrincipalLabel));
        OnPropertyChanged(nameof(EmiColInterestLabel));
        OnPropertyChanged(nameof(EmiColTotalLabel));
        OnPropertyChanged(nameof(EmiColBalanceLabel));
        OnPropertyChanged(nameof(InterestDatePlaceholder));
        OnPropertyChanged(nameof(HintPrincipal));
        OnPropertyChanged(nameof(HintRate));
        OnPropertyChanged(nameof(HintMonths));
        OnPropertyChanged(nameof(HintLoan));
        OnPropertyChanged(nameof(HintDateBs));
    }
}
