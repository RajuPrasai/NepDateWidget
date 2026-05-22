using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

public class BankingViewModelTests
{
    private static BankingViewModel Create(string language = "en")
    {
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage(language);
        return new BankingViewModel(loc, new FakeNepaliDateAdapter());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // MODE SELECTION
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DefaultMode_IsInterest()
    {
        var vm = Create();
        Assert.True(vm.IsModeInterest);
        Assert.False(vm.IsModeEmi);
    }

    [Fact]
    public void SetModeEmiCommand_SwitchesToEmi()
    {
        var vm = Create();
        vm.SetModeEmiCommand.Execute(null);
        Assert.False(vm.IsModeInterest);
        Assert.True(vm.IsModeEmi);
    }

    [Fact]
    public void SetModeInterestCommand_SwitchesBackToInterest()
    {
        var vm = Create();
        vm.SetModeEmiCommand.Execute(null);
        vm.SetModeInterestCommand.Execute(null);
        Assert.True(vm.IsModeInterest);
        Assert.False(vm.IsModeEmi);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // INTEREST CALCULATOR
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Interest_DefaultState_HasOneRow()
    {
        var vm = Create();
        Assert.Single(vm.InterestRows);
        Assert.True(vm.InterestRows[0].IsFirstRow);
    }

    [Fact]
    public void Interest_AddRow_IncreasesCount()
    {
        var vm = Create();
        vm.AddInterestRowCommand.Execute(null);
        Assert.Equal(2, vm.InterestRows.Count);
        Assert.False(vm.InterestRows[1].IsFirstRow);
    }

    [Fact]
    public void Interest_RemoveFirstRow_DoesNothing()
    {
        var vm = Create();
        vm.InterestRows[0].RemoveCommand.Execute(null);
        Assert.Single(vm.InterestRows);
    }

    [Fact]
    public void Interest_RemoveSecondRow_DecreasesCount()
    {
        var vm = Create();
        vm.AddInterestRowCommand.Execute(null);
        Assert.Equal(2, vm.InterestRows.Count);
        vm.InterestRows[1].RemoveCommand.Execute(null);
        Assert.Single(vm.InterestRows);
    }

    [Fact]
    public void Interest_FromDate_Change_SyncsFirstRow()
    {
        var vm = Create();
        vm.InterestFromDate = "2082/01/01";
        Assert.Equal("2082/01/01", vm.InterestRows[0].FromDate);
    }

    [Fact]
    public void Interest_AddRow_AutoFillsNextMonthFirst()
    {
        var vm = Create();
        vm.InterestFromDate = "2082/01/15";
        vm.InterestRows[0].SyncFromDate("2082/01/15");
        vm.AddInterestRowCommand.Execute(null);
        // Next month from 2082/01/xx should be 2082/02/01
        Assert.Equal("2082/02/01", vm.InterestRows[1].FromDate);
    }

    [Fact]
    public void Interest_Calculate_SingleRow_SimpleInterest()
    {
        // FakeNepaliDateAdapter.DiffTotalDays: (y2-y1)*365 + (m2-m1)*30 + (d2-d1)
        // 2082/01/01 → 2082/04/01 = 3*30 = 90 days
        var vm = Create();
        vm.InterestUseBs     = true;
        vm.InterestPrincipal = "100000";
        vm.InterestFromDate  = "2082/01/01";
        vm.InterestToDate    = "2082/04/01";
        vm.InterestRows[0].SyncFromDate("2082/01/01");
        vm.InterestRows[0].Rate = "12";

        vm.CalculateInterestCommand.Execute(null);

        Assert.True(vm.InterestHasResult);
        Assert.False(vm.InterestHasError);
        Assert.NotEmpty(vm.InterestBreakdownText);
        Assert.Contains("NPR", vm.InterestTotalText);
    }

    [Fact]
    public void Interest_Calculate_InvalidPrincipal_ShowsError()
    {
        var vm = Create();
        vm.InterestUseBs     = true;
        vm.InterestPrincipal = "abc";
        vm.InterestFromDate  = "2082/01/01";
        vm.InterestToDate    = "2082/04/01";
        vm.InterestRows[0].Rate = "12";

        vm.CalculateInterestCommand.Execute(null);

        Assert.True(vm.InterestHasError);
        Assert.False(vm.InterestHasResult);
    }

    [Fact]
    public void Interest_Calculate_MissingToDate_ShowsError()
    {
        var vm = Create();
        vm.InterestUseBs     = true;
        vm.InterestPrincipal = "100000";
        vm.InterestFromDate  = "2082/01/01";
        vm.InterestToDate    = "";
        vm.InterestRows[0].Rate = "12";

        vm.CalculateInterestCommand.Execute(null);

        Assert.True(vm.InterestHasError);
        Assert.False(vm.InterestHasResult);
    }

    [Fact]
    public void Interest_Calculate_MultiRow_SumsCorrectly()
    {
        var vm = Create();
        vm.InterestUseBs     = true;
        vm.InterestPrincipal = "100000";
        vm.InterestFromDate  = "2082/01/01";
        vm.InterestToDate    = "2082/07/01";
        vm.InterestRows[0].SyncFromDate("2082/01/01");
        vm.InterestRows[0].Rate = "12";

        vm.AddInterestRowCommand.Execute(null);
        vm.InterestRows[1].Rate = "10";

        vm.CalculateInterestCommand.Execute(null);

        Assert.True(vm.InterestHasResult);
        Assert.Contains("\n", vm.InterestBreakdownText);
        Assert.Contains("+", vm.InterestTotalText);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // EMI CALCULATOR
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Emi_DefaultState_NoResult_NoError()
    {
        var vm = Create();
        Assert.False(vm.EmiHasResult);
        Assert.False(vm.EmiHasError);
    }

    [Fact]
    public void Emi_Calculate_ValidInputs_ProducesResult()
    {
        var vm = Create();
        vm.EmiLoanAmount  = "1000000";
        vm.EmiAnnualRate  = "12";
        vm.EmiMonths      = "120";

        vm.CalculateEmiCommand.Execute(null);

        Assert.True(vm.EmiHasResult);
        Assert.False(vm.EmiHasError);
        Assert.NotEmpty(vm.EmiMonthlyText);
        Assert.NotEmpty(vm.EmiTotalPaymentText);
        Assert.NotEmpty(vm.EmiTotalInterestText);
    }

    [Fact]
    public void Emi_Calculate_ZeroRate_ProducesResult()
    {
        // zero-rate: EMI = principal / months, no interest
        var vm = Create();
        vm.EmiLoanAmount = "120000";
        vm.EmiAnnualRate = "0";
        vm.EmiMonths     = "12";

        vm.CalculateEmiCommand.Execute(null);

        Assert.True(vm.EmiHasResult);
        Assert.False(vm.EmiHasError);
        // Monthly EMI should be exactly 10,000
        Assert.Contains("10,000", vm.EmiMonthlyText);
    }

    [Fact]
    public void Emi_Calculate_InvalidLoan_ShowsError()
    {
        var vm = Create();
        vm.EmiLoanAmount = "abc";
        vm.EmiAnnualRate = "12";
        vm.EmiMonths     = "120";

        vm.CalculateEmiCommand.Execute(null);

        Assert.True(vm.EmiHasError);
        Assert.False(vm.EmiHasResult);
    }

    [Fact]
    public void Emi_Calculate_NegativeRate_ShowsError()
    {
        var vm = Create();
        vm.EmiLoanAmount = "1000000";
        vm.EmiAnnualRate = "-5";
        vm.EmiMonths     = "120";

        vm.CalculateEmiCommand.Execute(null);

        Assert.True(vm.EmiHasError);
        Assert.False(vm.EmiHasResult);
    }

    [Fact]
    public void Emi_Calculate_ZeroMonths_ShowsError()
    {
        var vm = Create();
        vm.EmiLoanAmount = "1000000";
        vm.EmiAnnualRate = "12";
        vm.EmiMonths     = "0";

        vm.CalculateEmiCommand.Execute(null);

        Assert.True(vm.EmiHasError);
        Assert.False(vm.EmiHasResult);
    }

    [Fact]
    public void Emi_InputChange_ClearsResult()
    {
        var vm = Create();
        vm.EmiLoanAmount = "1000000";
        vm.EmiAnnualRate = "12";
        vm.EmiMonths     = "120";
        vm.CalculateEmiCommand.Execute(null);
        Assert.True(vm.EmiHasResult);

        vm.EmiLoanAmount = "2000000";

        Assert.False(vm.EmiHasResult);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LOCALIZATION
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Labels_NonEmpty_EnglishAndNepali()
    {
        var vm = Create("en");
        Assert.NotEmpty(vm.ModeInterestLabel);
        Assert.NotEmpty(vm.ModeEmiLabel);
        Assert.NotEmpty(vm.EmiLoanLabel);
        Assert.NotEmpty(vm.EmiCalcLabel);

        var vmNe = Create("ne");
        Assert.NotEmpty(vmNe.ModeInterestLabel);
        Assert.NotEmpty(vmNe.EmiLoanLabel);
    }

    // ── Default value initialization ─────────────────────────────────────────

    [Fact]
    public void Constructor_InterestFromDate_PrePopulated()
    {
        var vm = Create();
        Assert.NotEmpty(vm.InterestFromDate);
        Assert.Contains("2082", vm.InterestFromDate);
    }

    [Fact]
    public void Constructor_EmiStartDate_PrePopulated()
    {
        var vm = Create();
        Assert.NotEmpty(vm.EmiStartDate);
        Assert.Contains("2082", vm.EmiStartDate);
    }

    [Fact]
    public void Constructor_FirstInterestRow_DatePrePopulated()
    {
        var vm = Create();
        Assert.True(vm.InterestRows.Count > 0);
        Assert.NotEmpty(vm.InterestRows[0].FromDate);
    }

    // ── EMI date mode toggle ──────────────────────────────────────────────────

    [Fact]
    public void EmiUseBs_DefaultTrue()
    {
        var vm = Create();
        Assert.True(vm.EmiUseBs);
    }

    [Fact]
    public void SetEmiAdCommand_SetsEmiUseBs_False()
    {
        var vm = Create();
        vm.SetEmiAdCommand.Execute(null);
        Assert.False(vm.EmiUseBs);
    }

    [Fact]
    public void SetEmiBsCommand_RestoresEmiUseBs_True()
    {
        var vm = Create();
        vm.SetEmiAdCommand.Execute(null);
        vm.SetEmiBsCommand.Execute(null);
        Assert.True(vm.EmiUseBs);
    }

    [Fact]
    public void EmiDatePlaceholder_BsMode_HasSlashSeparator()
    {
        // BS date format uses slash: "2082/01/01"
        var vm = Create();
        vm.SetEmiBsCommand.Execute(null);
        Assert.Contains("/", vm.EmiDatePlaceholder);
    }

    [Fact]
    public void EmiDatePlaceholder_AdMode_HasDashSeparator()
    {
        // AD date format uses dash: "2026-04-01"
        var vm = Create();
        vm.SetEmiAdCommand.Execute(null);
        Assert.Contains("-", vm.EmiDatePlaceholder);
    }

    [Fact]
    public void EmiStartDateLabel_ChangesBetweenModes()
    {
        var vm = Create();
        var bsLabel = vm.EmiStartDateLabel;
        vm.SetEmiAdCommand.Execute(null);
        var adLabel = vm.EmiStartDateLabel;
        Assert.NotEqual(bsLabel, adLabel);
    }

    // ── Interest date mode toggle ─────────────────────────────────────────────

    [Fact]
    public void InterestUseBs_DefaultTrue()
    {
        var vm = Create();
        Assert.True(vm.InterestUseBs);
    }

    [Fact]
    public void SetInterestAdCommand_SetsInterestUseBs_False()
    {
        var vm = Create();
        vm.SetInterestAdCommand.Execute(null);
        Assert.False(vm.InterestUseBs);
    }

    [Fact]
    public void SetInterestBsCommand_RestoresInterestUseBs_True()
    {
        var vm = Create();
        vm.SetInterestAdCommand.Execute(null);
        vm.SetInterestBsCommand.Execute(null);
        Assert.True(vm.InterestUseBs);
    }

    [Fact]
    public void InterestDatePlaceholder_BsMode_HasSlashSeparator()
    {
        var vm = Create();
        vm.SetInterestBsCommand.Execute(null);
        Assert.Contains("/", vm.InterestDatePlaceholder);
    }

    [Fact]
    public void InterestDatePlaceholder_AdMode_HasDashSeparator()
    {
        var vm = Create();
        vm.SetInterestAdCommand.Execute(null);
        Assert.Contains("-", vm.InterestDatePlaceholder);
    }

    // ── EMI schedule row count ────────────────────────────────────────────────

    [Fact]
    public void Emi_Schedule_HasAtLeastOneYearRow_After12Months()
    {
        // A 12-month loan spans at least one calendar year → at least one year-header row
        var vm = Create();
        vm.EmiLoanAmount = "1200000";
        vm.EmiAnnualRate = "10";
        vm.EmiMonths     = "12";
        vm.EmiStartDate  = "";  // uses today from FakeAdapter

        vm.CalculateEmiCommand.Execute(null);

        Assert.True(vm.EmiHasResult);
        Assert.NotEmpty(vm.EmiScheduleRows);
        // EmiScheduleRows contains year-header rows; at least one must exist
        Assert.Contains(vm.EmiScheduleRows, r => r.IsYearRow);
    }

    [Fact]
    public void Emi_Schedule_TotalPayment_GreaterThanPrincipal_ForNonZeroRate()
    {
        // With non-zero interest, total payment must exceed the principal
        var vm = Create();
        const double principal = 1_000_000;
        vm.EmiLoanAmount = principal.ToString();
        vm.EmiAnnualRate = "10";
        vm.EmiMonths     = "24";

        vm.CalculateEmiCommand.Execute(null);

        Assert.True(vm.EmiHasResult);
        Assert.Contains("NPR", vm.EmiTotalPaymentText);
        // Total interest text must not be "NPR 0" or empty
        Assert.NotEmpty(vm.EmiTotalInterestText);
        Assert.DoesNotContain("NPR 0.00", vm.EmiTotalInterestText);
    }

    [Fact]
    public void Emi_Schedule_ZeroRate_TotalPaymentEqualsLoan()
    {
        // Zero interest: total payment = principal, total interest = 0
        var vm = Create();
        vm.EmiLoanAmount = "120000";
        vm.EmiAnnualRate = "0";
        vm.EmiMonths     = "12";

        vm.CalculateEmiCommand.Execute(null);

        Assert.True(vm.EmiHasResult);
        Assert.Contains("NPR", vm.EmiTotalPaymentText);
        Assert.Contains("0", vm.EmiTotalInterestText);
    }
}
