using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests covering ConverterViewModel Days mode actual output values (Diff breakdown
/// + total, AddSub short + long formats) and Time mode conversion accuracy.
///
/// Seeds use the FakeNepaliDateAdapter which applies simple arithmetic:
///   DiffBreakdown(y1,m1,d1, y2,m2,d2) = (y2-y1, m2-m1, d2-d1) with carry
///   DiffTotalDays(...)                 = (y2-y1)*365 + (m2-m1)*30 + (d2-d1)
///   AddDays(y,m,d, n)                  = (y, m, clamp(d + n%30, 1..30))
///   FormatBsShortEn(y,m,d)            = "YYYY/MM/DD"
///   FormatBsLongEn(y,m,d)             = "{MonthName} D, YYYY"
/// </summary>
public class ConverterViewModelDaysOutputTests
{
    private static ConverterViewModel Create()
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        var conv = new ConversionService(adapter);
        return new ConverterViewModel(conv, loc, adapter: adapter);
    }

    // ── Days Diff: breakdown format ───────────────────────────────────────────

    [Fact]
    public void DaysDiff_KnownDates_BreakdownFormat_IsYearsMonthsDays()
    {
        // DiffBreakdown(2082, 1, 1, 2082, 4, 15):
        //   years=0, months=3, days=14  (no carry needed - all positive)
        // DiffTotalDays: 0*365 + 3*30 + 14 = 104
        var vm = Create();
        vm.ActiveMode = 1; // Days mode
        vm.IsDaysDiff = true;

        vm.DaysInput1 = "2082/01/01";
        vm.DaysInput2 = "2082/04/15";

        Assert.Equal("0 years, 3 months, 14 days", vm.DaysOutputBreakdown);
        Assert.Equal("104", vm.DaysOutputTotal);
        Assert.False(vm.DaysHasError);
    }

    [Fact]
    public void DaysDiff_SameDates_ZeroBreakdown()
    {
        // DiffBreakdown(2082, 6, 15, 2082, 6, 15) = (0, 0, 0)
        // DiffTotalDays = 0
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = true;

        vm.DaysInput1 = "2082/06/15";
        vm.DaysInput2 = "2082/06/15";

        Assert.Equal("0 years, 0 months, 0 days", vm.DaysOutputBreakdown);
        Assert.Equal("0", vm.DaysOutputTotal);
    }

    [Fact]
    public void DaysDiff_YearCrossing_BreakdownAndTotal()
    {
        // DiffBreakdown(2080, 3, 10, 2082, 8, 25):
        //   years=2, months=5, days=15  (all positive, no carry)
        // DiffTotalDays: 2*365 + 5*30 + 15 = 730 + 150 + 15 = 895
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = true;

        vm.DaysInput1 = "2080/03/10";
        vm.DaysInput2 = "2082/08/25";

        Assert.Equal("2 years, 5 months, 15 days", vm.DaysOutputBreakdown);
        Assert.Equal("895", vm.DaysOutputTotal);
    }

    [Fact]
    public void DaysDiff_Total_IsAlwaysAbsoluteValue_WhenInputsReversed()
    {
        // DiffTotalDays(2082, 4, 15, 2082, 1, 1) = 0 + (1-4)*30 + (1-15) = -90 - 14 = -104
        // DaysOutputTotal = Math.Abs(-104) = "104"
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = true;

        vm.DaysInput1 = "2082/04/15";
        vm.DaysInput2 = "2082/01/01";

        Assert.Equal("104", vm.DaysOutputTotal);
    }

    [Fact]
    public void DaysDiff_BothOutputs_SetTogether_WhenBothInputsValid()
    {
        // DiffBreakdown(2082, 2, 1, 2082, 5, 1): years=0, months=3, days=0 (no carry)
        // DiffTotalDays: 0*365 + 3*30 + 0 = 90
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = true;

        vm.DaysInput1 = "2082/02/01";
        vm.DaysInput2 = "2082/05/01";

        Assert.Equal("0 years, 3 months, 0 days", vm.DaysOutputBreakdown);
        Assert.Equal("90", vm.DaysOutputTotal);
        Assert.Equal(string.Empty, vm.DaysOutputShort);  // AddSub outputs stay empty in Diff mode
        Assert.Equal(string.Empty, vm.DaysOutputLong);
    }

    // ── Days Diff: error paths ────────────────────────────────────────────────

    [Fact]
    public void DaysDiff_InvalidDate_SetsError()
    {
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = true;

        vm.DaysInput1 = "9999/99/99"; // outside 1901..2199
        vm.DaysInput2 = "2082/06/01";

        Assert.True(vm.DaysHasError);
        Assert.Equal(string.Empty, vm.DaysOutputBreakdown);
        Assert.Equal(string.Empty, vm.DaysOutputTotal);
    }

    // ── Days AddSub: output values ────────────────────────────────────────────

    [Fact]
    public void DaysAddSub_PositiveOffset_OutputShort_MatchesFormula()
    {
        // AddDays(2082, 1, 5, 7):
        //   clamp(5 + 7%30, 1..30) = clamp(12, 1..30) = 12
        //   result = (2082, 1, 12)
        // FormatBsShortEn(2082, 1, 12) = "2082/01/12"
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = false; // AddSub mode

        vm.DaysInput1 = "2082/01/05";
        vm.DaysInput2 = "7";

        Assert.Equal("2082/01/12", vm.DaysOutputShort);
        Assert.False(vm.DaysHasError);
    }

    [Fact]
    public void DaysAddSub_PositiveOffset_OutputLong_IsFormattedMonthName()
    {
        // result = (2082, 1, 12) → FormatBsLongEn = "Baisakh 12, 2082"
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = false;

        vm.DaysInput1 = "2082/01/05";
        vm.DaysInput2 = "7";

        Assert.Equal("Baisakh 12, 2082", vm.DaysOutputLong);
    }

    [Fact]
    public void DaysAddSub_NegativeOffset_OutputShort_MatchesFormula()
    {
        // AddDays(2082, 1, 20, -15):
        //   -15 % 30 = -15 (C# preserves sign)
        //   20 + (-15) = 5
        //   clamp(5, 1..30) = 5 → result = (2082, 1, 5)
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = false;

        vm.DaysInput1 = "2082/01/20";
        vm.DaysInput2 = "-15";

        Assert.Equal("2082/01/05", vm.DaysOutputShort);
    }

    [Fact]
    public void DaysAddSub_ZeroOffset_OutputShort_IsSameDate()
    {
        // AddDays(2082, 6, 15, 0): clamp(15 + 0, 1..30) = 15 → "2082/06/15"
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = false;

        vm.DaysInput1 = "2082/06/15";
        vm.DaysInput2 = "0";

        Assert.Equal("2082/06/15", vm.DaysOutputShort);
    }

    [Fact]
    public void DaysAddSub_InvalidOffset_SetsError()
    {
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = false;

        vm.DaysInput1 = "2082/01/01";
        vm.DaysInput2 = "abc";

        Assert.True(vm.DaysHasError);
        Assert.Equal(string.Empty, vm.DaysOutputShort);
    }

    [Fact]
    public void DaysAddSub_AddSubOutputs_AreEmpty_InDiffMode()
    {
        // DaysOutputShort/Long are AddSub outputs; switching to Diff mode clears them
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = false;
        vm.DaysInput1 = "2082/01/05";
        vm.DaysInput2 = "7";
        Assert.NotEmpty(vm.DaysOutputShort); // precondition

        vm.IsDaysDiff = true;

        Assert.Equal(string.Empty, vm.DaysOutputShort);
        Assert.Equal(string.Empty, vm.DaysOutputLong);
    }

    [Fact]
    public void DaysDiff_DiffOutputs_AreEmpty_InAddSubMode()
    {
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = true;
        vm.DaysInput1 = "2082/01/01";
        vm.DaysInput2 = "2082/04/15";
        Assert.NotEmpty(vm.DaysOutputBreakdown); // precondition

        vm.IsDaysDiff = false;

        Assert.Equal(string.Empty, vm.DaysOutputBreakdown);
        Assert.Equal(string.Empty, vm.DaysOutputTotal);
    }

    // ── Days: input1 change clears outputs ────────────────────────────────────

    [Fact]
    public void DaysDiff_ChangingInput1_ClearsOutput_BeforeRecompute()
    {
        var vm = Create();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = true;
        vm.DaysInput1 = "2082/01/01";
        vm.DaysInput2 = "2082/04/15";
        Assert.NotEmpty(vm.DaysOutputBreakdown); // precondition

        // Set to invalid - output should clear and error should set
        vm.DaysInput1 = "9999/99/99";

        Assert.Equal(string.Empty, vm.DaysOutputBreakdown);
        Assert.Equal(string.Empty, vm.DaysOutputTotal);
    }
}

/// <summary>
/// Tests covering ConverterViewModel Time mode conversion output accuracy.
/// Uses deterministic, DST-free timezone pairs: "Nepal Standard Time" (+05:45)
/// as the From zone and "UTC" (+00:00) as the To zone.
/// Nepal Standard Time does not observe DST so output is stable across dates.
/// </summary>
public class ConverterViewModelTimeOutputTests
{
    // Nepal Standard Time is the VM's default "from" zone.
    // We pass "UTC" as the home timezone so the "to" zone is UTC.
    private static ConverterViewModel Create()
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        var conv = new ConversionService(adapter);
        return new ConverterViewModel(conv, loc, adapter: adapter, selectedTimezoneId: "UTC");
    }

    [Fact]
    public void Time_NepalToUtc_6Am_Outputs_12_15_Am()
    {
        // Nepal Standard Time (+05:45) → UTC (+00:00)
        // Input: 6:00 AM Nepal
        // Expected UTC: 6:00 - 5h45m = 0:15 AM → "12:15 AM"
        // Full output: "12:15 AM  (UTC+05:45 → UTC+00:00)"
        if (!TimeZoneInfo.GetSystemTimeZones().Any(z => z.Id == "Nepal Standard Time")) { return; }

        var vm = Create();
        vm.ActiveMode = 2;

        // Use explicit "AM" so hadExplicitAmPm=true - result is deterministic regardless of
        // when the test runs (no dependency on _timeIsAm button state initialised from DateTime.Now).
        vm.TimeInput = "6:00 AM";

        Assert.False(vm.TimeHasError);
        Assert.Contains("12:15 AM", vm.TimeOutput);
        Assert.Contains("UTC+5:45", vm.TimeOutput);
        Assert.Contains("UTC+0", vm.TimeOutput);
    }

    [Fact]
    public void Time_NepalToUtc_Noon_Outputs_6_15_Am()
    {
        // 12:00 PM Nepal → UTC: 12:00 - 5h45m = 06:15 UTC → "6:15 AM"
        if (!TimeZoneInfo.GetSystemTimeZones().Any(z => z.Id == "Nepal Standard Time")) { return; }

        var vm = Create();
        vm.ActiveMode = 2;

        // "noon" is a special keyword that resolves to 12:00 with hadExplicitAmPm=true
        // Button is synced to PM (hour 12 → 12 >= 12 → isAm = false)
        vm.TimeInput = "noon";

        Assert.False(vm.TimeHasError);
        Assert.Contains("6:15 AM", vm.TimeOutput);
    }

    [Fact]
    public void Time_NepalToUtc_Midnight_Outputs_18_15_Previous_Day()
    {
        // 12:00 AM Nepal → UTC: 00:00 - 5h45m = 18:15 of PREVIOUS day
        // "midnight" resolves to 00:00 with hadExplicitAmPm=true → isAm synced to true (hour=0)
        // 00:00 Nepal → UTC: 00:00 - 5:45 = -5:45 → previous day 18:15
        // dayDiff = -1 → day tag " (-1d)"
        if (!TimeZoneInfo.GetSystemTimeZones().Any(z => z.Id == "Nepal Standard Time")) { return; }

        var vm = Create();
        vm.ActiveMode = 2;

        vm.TimeInput = "midnight";

        Assert.False(vm.TimeHasError);
        Assert.Contains("6:15 PM", vm.TimeOutput);
        Assert.Contains("(-1d)", vm.TimeOutput);
    }

    [Fact]
    public void Time_InvalidInput_SetsTimeHasError()
    {
        if (!TimeZoneInfo.GetSystemTimeZones().Any(z => z.Id == "Nepal Standard Time")) { return; }

        var vm = Create();
        vm.ActiveMode = 2;

        vm.TimeInput = "not-a-time";

        Assert.True(vm.TimeHasError);
        Assert.Equal(string.Empty, vm.TimeOutput);
    }

    [Fact]
    public void Time_SwapCommand_ReversesFromAndToZones()
    {
        if (!TimeZoneInfo.GetSystemTimeZones().Any(z => z.Id == "Nepal Standard Time")) { return; }

        var vm = Create();
        vm.ActiveMode = 2;

        var fromBefore = vm.TimeFromZone?.Id;
        var toBefore = vm.TimeToZone?.Id;

        vm.TimeSwapCommand.Execute(null);

        Assert.Equal(fromBefore, vm.TimeToZone?.Id);
        Assert.Equal(toBefore, vm.TimeFromZone?.Id);
    }

    [Fact]
    public void Time_AfterSwap_UtcToNepal_6Am_Outputs_11_45_Am()
    {
        // After swap: From = UTC (+00:00), To = Nepal (+05:45)
        // Input: 6:00 AM UTC → 6:00 + 5:45 = 11:45 AM Nepal
        if (!TimeZoneInfo.GetSystemTimeZones().Any(z => z.Id == "Nepal Standard Time")) { return; }

        var vm = Create();
        vm.ActiveMode = 2;
        vm.TimeSwapCommand.Execute(null); // Now From=UTC, To=Nepal

        // Explicit "AM" makes hadExplicitAmPm=true - deterministic regardless of time of day.
        vm.TimeInput = "6:00 AM";

        Assert.False(vm.TimeHasError);
        Assert.Contains("11:45 AM", vm.TimeOutput);
    }

    [Fact]
    public void Time_OutputFormat_IncludesUtcOffsetArrow()
    {
        // Verify the "{time}  (UTC{from} → UTC{to})" format is present
        if (!TimeZoneInfo.GetSystemTimeZones().Any(z => z.Id == "Nepal Standard Time")) { return; }

        var vm = Create();
        vm.ActiveMode = 2;
        vm.TimeInput = "6:00 AM"; // explicit AM → deterministic

        Assert.False(vm.TimeHasError);
        Assert.Matches(@"UTC[+\-]\d+(:\d{2})? → UTC[+\-]\d+(:\d{2})?", vm.TimeOutput);
    }
}
