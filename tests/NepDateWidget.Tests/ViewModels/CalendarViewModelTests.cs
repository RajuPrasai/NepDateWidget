using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

public class CalendarViewModelTests
{
    private static CalendarViewModel Create(
        FakeNepaliDateAdapter? adapter = null,
        string language = "en")
    {
        var a   = adapter ?? new FakeNepaliDateAdapter();
        var svc = new CalendarService(a);
        var loc = new LocalizationService();
        var conv = new ConversionService(a);
        loc.SetLanguage(language);
        return new CalendarViewModel(svc, loc, conv);
    }

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_DisplaysToday_ByDefault()
    {
        var vm = Create();
        Assert.Equal(2082, vm.DisplayYear);
        Assert.Equal(12,   vm.DisplayMonth);
    }

    [Fact]
    public void Constructor_Days_NotEmpty()
    {
        var vm = Create();
        Assert.NotEmpty(vm.Days);
    }

    [Fact]
    public void Constructor_Days_MultipleOf7()
    {
        var vm = Create();
        Assert.Equal(0, vm.Days.Count % 7);
    }

    [Fact]
    public void Constructor_MonthYearLabel_NonEmpty()
    {
        var vm = Create();
        Assert.NotEmpty(vm.MonthYearLabel);
    }

    [Fact]
    public void Constructor_IsShowingToday_True_WhenAtCurrentMonth()
    {
        var vm = Create();
        Assert.True(vm.IsShowingToday);
    }

    [Fact]
    public void Constructor_DayOfWeekHeaders_Has7Items()
    {
        var vm = Create();
        Assert.Equal(7, vm.DayOfWeekHeaders.Count);
    }

    [Fact]
    public void Constructor_DayOfWeekHeaders_AllNonEmpty()
    {
        var vm = Create();
        Assert.All(vm.DayOfWeekHeaders, h => Assert.NotEmpty(h.Label));
    }

    // ── Navigation via commands ───────────────────────────────────────────────

    [Fact]
    public void PrevMonthCommand_DecrementsMonth()
    {
        var vm = Create();
        vm.PrevMonthCommand.Execute(null);
        Assert.Equal(2082, vm.DisplayYear);
        Assert.Equal(11,   vm.DisplayMonth);
    }

    [Fact]
    public void NextMonthCommand_IncrementsMonth()
    {
        var vm = Create();
        vm.NextMonthCommand.Execute(null);
        Assert.Equal(2083, vm.DisplayYear);
        Assert.Equal(1,    vm.DisplayMonth);
    }

    [Fact]
    public void GoTodayCommand_WhenNavigatedAway_ReturnsToToday()
    {
        var vm = Create();
        vm.PrevMonthCommand.Execute(null);
        Assert.Equal(11, vm.DisplayMonth);

        vm.GoTodayCommand.Execute(null);
        Assert.Equal(12, vm.DisplayMonth);
    }

    // ── Navigation via NavigateMonths ─────────────────────────────────────────

    [Fact]
    public void NavigateMonths_Positive_MovesForward()
    {
        var vm = Create();
        vm.NavigateMonths(3);
        Assert.Equal(2083, vm.DisplayYear);
        Assert.Equal(3,    vm.DisplayMonth);
    }

    [Fact]
    public void NavigateMonths_Negative_MovesBackward()
    {
        var vm = Create();
        vm.NavigateMonths(-12);
        Assert.Equal(2081, vm.DisplayYear);
        Assert.Equal(12,   vm.DisplayMonth);
    }

    [Fact]
    public void NavigateMonths_ToSameMonthYear_NoGridRebuild_NoThrow()
    {
        var vm = Create();
        var ex = Record.Exception(() => vm.NavigateMonths(0));
        Assert.Null(ex);
    }

    // ── IsShowingToday ────────────────────────────────────────────────────────

    [Fact]
    public void IsShowingToday_FalseAfterNavigation()
    {
        var vm = Create();
        vm.PrevMonthCommand.Execute(null);
        Assert.False(vm.IsShowingToday);
    }

    [Fact]
    public void IsShowingToday_TrueAfterGoToday()
    {
        var vm = Create();
        vm.PrevMonthCommand.Execute(null);
        vm.GoTodayCommand.Execute(null);
        Assert.True(vm.IsShowingToday);
    }

    // ── CanGoPrev / CanGoNext ─────────────────────────────────────────────────

    [Fact]
    public void CanGoPrev_FalseAtMinimumDate()
    {
        var adapter = new FakeNepaliDateAdapter { TodayBsYear = 1901, TodayBsMonth = 1 };
        // Navigate to 1901/01 first
        var vm = Create(adapter);
        // Already at min - CanGoPrev should be false
        Assert.False(vm.CanGoPrev);
    }

    [Fact]
    public void CanGoNext_TrueInNormalRange()
    {
        var vm = Create();
        Assert.True(vm.CanGoNext);
    }

    [Fact]
    public void CanGoNext_FalseAtMaximumDate()
    {
        var adapter = new FakeNepaliDateAdapter { TodayBsYear = 2199, TodayBsMonth = 12 };
        var vm = Create(adapter);
        Assert.False(vm.CanGoNext);
    }

    // ── Language ──────────────────────────────────────────────────────────────

    [Fact]
    public void OnLanguageChanged_UpdatesMonthYearLabel()
    {
        var a    = new FakeNepaliDateAdapter();
        var svc  = new CalendarService(a);
        var loc  = new LocalizationService();
        var conv = new ConversionService(a);
        loc.SetLanguage("en");
        var vm   = new CalendarViewModel(svc, loc, conv);

        var enLabel = vm.MonthYearLabel;

        loc.SetLanguage("ne");
        vm.OnLanguageChanged();

        Assert.NotEqual(enLabel, vm.MonthYearLabel);
    }

    [Fact]
    public void OnLanguageChanged_UpdatesDayOfWeekHeaders()
    {
        var a    = new FakeNepaliDateAdapter();
        var svc  = new CalendarService(a);
        var loc  = new LocalizationService();
        var conv = new ConversionService(a);
        loc.SetLanguage("en");
        var vm   = new CalendarViewModel(svc, loc, conv);

        var enHeaders = vm.DayOfWeekHeaders.ToList();

        loc.SetLanguage("ne");
        vm.OnLanguageChanged();

        Assert.NotEqual(enHeaders[0].Label, vm.DayOfWeekHeaders[0].Label);
    }

    // ── Today marking in grid ─────────────────────────────────────────────────

    [Fact]
    public void Days_TodayCell_HasIsToday_True()
    {
        var vm      = Create();
        var today   = vm.Days.Where(d => d.IsCurrentMonth && d.IsToday).ToList();
        Assert.Single(today);
        Assert.Equal(20, today[0].Day);
    }

    [Fact]
    public void Days_SaturdayCells_HasIsSaturday_True()
    {
        var vm = Create();
        var saturdays = vm.Days.Where(d => d.IsSaturday).ToList();
        Assert.NotEmpty(saturdays);
    }

    // ── DayText ───────────────────────────────────────────────────────────────

    [Fact]
    public void DayText_PaddingCells_IsEmpty()
    {
        var vm = Create();
        var paddingCells = vm.Days.Where(d => d.IsPadding).ToList();
        Assert.All(paddingCells, d => Assert.Empty(d.DayText));
    }

    [Fact]
    public void DayText_RealCells_IsNonEmpty()
    {
        var vm = Create();
        var realCells = vm.Days.Where(d => d.IsCurrentMonth).ToList();
        Assert.All(realCells, d => Assert.NotEmpty(d.DayText));
    }
}
