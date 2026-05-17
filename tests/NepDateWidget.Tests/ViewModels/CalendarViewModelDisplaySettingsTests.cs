using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests covering CalendarViewModel display-flag effects:
/// UpdateDisplaySettings wiring to RefreshGrid, ShowFiscalYear toggle,
/// FiscalFooterText content, and DisplayYear out-of-range guard.
/// </summary>
public class CalendarViewModelDisplaySettingsTests
{
    private static (CalendarViewModel vm, FakeNepaliDateAdapter adapter) Create()
    {
        var adapter = new FakeNepaliDateAdapter();
        var svc = new CalendarService(adapter);
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        var conv = new ConversionService(adapter);
        var vm = new CalendarViewModel(svc, loc, conv, adapter: adapter);
        return (vm, adapter);
    }

    // ── ShowFiscalYear → FiscalFooterText ─────────────────────────────────────

    [Fact]
    public void ShowFiscalYear_DefaultTrue_FiscalFooterText_IsNonEmpty()
    {
        var (vm, _) = Create();
        // Default is showFiscalYear=true; adapter returns a label for today 2082/12/20
        Assert.NotEmpty(vm.FiscalFooterText);
    }

    [Fact]
    public void ShowFiscalYear_SetFalse_ClearsFiscalFooterText()
    {
        var (vm, _) = Create();
        Assert.NotEmpty(vm.FiscalFooterText); // precondition

        vm.ShowFiscalYear = false;

        Assert.Equal(string.Empty, vm.FiscalFooterText);
    }

    [Fact]
    public void ShowFiscalYear_SetFalse_ThenTrue_RestoresFiscalFooterText()
    {
        var (vm, _) = Create();
        vm.ShowFiscalYear = false;
        Assert.Equal(string.Empty, vm.FiscalFooterText);

        vm.ShowFiscalYear = true;

        Assert.NotEmpty(vm.FiscalFooterText);
    }

    [Fact]
    public void ShowFiscalYear_True_FiscalFooterText_ContainsFiscalLabel()
    {
        // FakeNepaliDateAdapter.GetFiscalYearInfo(2082, 12, 20)
        //   fyStart = 2082 (month 12 >= 4), label = "2082/83", quarter = 3
        //   daysToQEnd = 30, daysToYrEnd = 90
        // RefreshFiscalFooter: daysToQEnd (30) <= daysToYrEnd (90) → includes quarter end countdown
        // FiscalFooterText = "{label} {fyLabel} • {qLabel}{quarter} • {days} {daysLabel}"
        // Month 12 (Chaitra) falls in Q3 of the Nepali fiscal year (months 10-12).
        var (vm, _) = Create();

        Assert.Contains("2082/83", vm.FiscalFooterText);
        Assert.Contains("Q3", vm.FiscalFooterText); // quarter prefix + number, not just "3"
    }

    [Fact]
    public void ShowFiscalYear_True_FiscalFooterText_ContainsCountdownDays()
    {
        // daysToQEnd = 30 (per fake adapter), daysToYrEnd = 90 → quarter-end path chosen
        var (vm, _) = Create();

        Assert.Contains("30", vm.FiscalFooterText);
    }

    // ── UpdateDisplaySettings → ShowEnglishBadge ──────────────────────────────

    [Fact]
    public void UpdateDisplaySettings_ShowEnglishDayNumbersFalse_AllCurrentMonthCells_HaveNoBadge()
    {
        var (vm, _) = Create();
        // Default is showEnglishDayNumbers=true so some cells should already have a badge.
        // The FakeNepaliDateAdapter.BsToAd returns non-null for month 12 cells, so AdDay > 0.
        Assert.True(vm.Days.Any(d => d.IsCurrentMonth && d.ShowEnglishBadge),
            "Precondition: some current-month cells should show the English badge by default.");

        vm.UpdateDisplaySettings(
            showEnglishDayNumbers: false,
            highlightSaturdays: true,
            highlightSundays: false,
            showTithi: true,
            showEvents: true,
            highlightPublicHolidays: true);

        Assert.All(
            vm.Days.Where(d => d.IsCurrentMonth),
            cell => Assert.False(cell.ShowEnglishBadge,
                $"Cell day {cell.Day} should have no English badge after disabling the flag."));
    }

    [Fact]
    public void UpdateDisplaySettings_ShowEnglishDayNumbersTrue_CurrentMonthCells_HaveBadge()
    {
        // Start with false so we can verify turning it back on restores the badge.
        var adapter = new FakeNepaliDateAdapter();
        var svc = new CalendarService(adapter);
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        var conv = new ConversionService(adapter);
        var vm = new CalendarViewModel(svc, loc, conv,
            showEnglishDayNumbers: false, adapter: adapter);

        Assert.All(
            vm.Days.Where(d => d.IsCurrentMonth),
            cell => Assert.False(cell.ShowEnglishBadge));

        vm.UpdateDisplaySettings(
            showEnglishDayNumbers: true,
            highlightSaturdays: true,
            highlightSundays: false,
            showTithi: true,
            showEvents: true,
            highlightPublicHolidays: true);

        Assert.True(vm.Days.Any(d => d.IsCurrentMonth && d.ShowEnglishBadge),
            "At least one current-month cell should show the English badge after re-enabling.");
    }

    [Fact]
    public void UpdateDisplaySettings_HighlightSaturdaysFalse_PropertyUpdates()
    {
        var (vm, _) = Create();
        Assert.True(vm.HighlightSaturdays); // precondition: default is true

        vm.UpdateDisplaySettings(
            showEnglishDayNumbers: true,
            highlightSaturdays: false,
            highlightSundays: false,
            showTithi: true,
            showEvents: true,
            highlightPublicHolidays: true);

        Assert.False(vm.HighlightSaturdays);
    }

    [Fact]
    public void UpdateDisplaySettings_HighlightSaturdaysTrue_PropertyUpdates()
    {
        var adapter = new FakeNepaliDateAdapter();
        var svc = new CalendarService(adapter);
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        var conv = new ConversionService(adapter);
        var vm = new CalendarViewModel(svc, loc, conv,
            highlightSaturdays: false, adapter: adapter);

        Assert.False(vm.HighlightSaturdays);

        vm.UpdateDisplaySettings(
            showEnglishDayNumbers: true,
            highlightSaturdays: true,
            highlightSundays: false,
            showTithi: true,
            showEvents: true,
            highlightPublicHolidays: true);

        Assert.True(vm.HighlightSaturdays);
    }

    // ── DisplayYear clamping ─────────────────────────────────────────────────

    [Fact]
    public void DisplayYear_Set_BelowMin_IsIgnored()
    {
        var (vm, _) = Create();
        int before = vm.DisplayYear;

        vm.DisplayYear = 1899; // below 1901

        Assert.Equal(before, vm.DisplayYear);
    }

    [Fact]
    public void DisplayYear_Set_AboveMax_IsIgnored()
    {
        var (vm, _) = Create();
        int before = vm.DisplayYear;

        vm.DisplayYear = 2200; // above 2199

        Assert.Equal(before, vm.DisplayYear);
    }

    [Fact]
    public void DisplayYear_Set_AtMinBound_IsAccepted()
    {
        // The adapter only has data for 2082; CalendarService uses GetDaysInMonth which falls back to 30.
        var (vm, _) = Create();
        vm.DisplayYear = 1901;
        Assert.Equal(1901, vm.DisplayYear);
    }

    [Fact]
    public void DisplayYear_Set_AtMaxBound_IsAccepted()
    {
        var (vm, _) = Create();
        vm.DisplayYear = 2199;
        Assert.Equal(2199, vm.DisplayYear);
    }

    [Fact]
    public void DisplayYear_Set_SameAsCurrentYear_NoNavigation()
    {
        var (vm, _) = Create();
        int month = vm.DisplayMonth;
        vm.DisplayYear = vm.DisplayYear; // same value
        Assert.Equal(month, vm.DisplayMonth); // month unchanged
    }

    // ── SelectedMonthIndex ─────────────────────────────────────────────────────

    [Fact]
    public void SelectedMonthIndex_SetOutOfRange_IsIgnored()
    {
        var (vm, _) = Create();
        int before = vm.SelectedMonthIndex;

        vm.SelectedMonthIndex = -1;
        Assert.Equal(before, vm.SelectedMonthIndex);

        vm.SelectedMonthIndex = 12;
        Assert.Equal(before, vm.SelectedMonthIndex);
    }

    [Fact]
    public void SelectedMonthIndex_SetValid_ChangesDisplayMonth()
    {
        var (vm, _) = Create(); // starts at 2082/12 → SelectedMonthIndex = 11
        Assert.Equal(11, vm.SelectedMonthIndex);

        vm.SelectedMonthIndex = 0; // January (Baisakh)

        Assert.Equal(0, vm.SelectedMonthIndex);
        Assert.Equal(1, vm.DisplayMonth);
    }
}
