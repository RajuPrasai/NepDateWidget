using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Behaviour of the upcoming-holiday banner on the calendar header. The
/// HolidayLookupService is exercised directly via HolidayWalkingFakeAdapter
/// so these assertions stay deterministic regardless of the real BS calendar
/// data file.
/// </summary>
public class CalendarViewModelHolidayCountdownTests
{
    private static (CalendarViewModel Vm, LocalizationService Loc) Build(
        HolidayWalkingFakeAdapter adapter,
        bool showCountdown = true,
        string language = "en")
    {
        var calSvc  = new CalendarService(adapter);
        var loc     = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var convSvc = new ConversionService(adapter);
        loc.SetLanguage(language);
        var holidayLookup = new HolidayLookupService(adapter);

        var vm = new CalendarViewModel(
            calSvc, loc, convSvc,
            showEnglishDayNumbers: false,
            highlightSaturdays: true,
            highlightSundays: false,
            selectedTimezoneId: "",
            reminderService: null,
            showTithi: false,
            showEvents: false,
            highlightPublicHolidays: true,
            showFiscalYear: false,
            showHolidayCountdown: showCountdown,
            holidayLookupService: holidayLookup);

        return (vm, loc);
    }

    [Fact]
    public void Countdown_NoLookupService_ProducesEmptyLines()
    {
        var calAdapter = new FakeNepaliDateAdapter();
        var calSvc  = new CalendarService(calAdapter);
        var loc     = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var convSvc = new ConversionService(calAdapter);
        var vm = new CalendarViewModel(calSvc, loc, convSvc,
            showHolidayCountdown: true, holidayLookupService: null);

        Assert.False(vm.HasHolidayCountdown);
        Assert.Empty(vm.HolidayCountdownLines);
    }

    [Fact]
    public void Countdown_DisabledByToggle_HidesBanner()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 5, "Fest", "पर्व");
        var (vm, _) = Build(adapter, showCountdown: false);

        Assert.False(vm.HasHolidayCountdown);
        Assert.Empty(vm.HolidayCountdownLines);
    }

    [Fact]
    public void Countdown_NoUpcomingHoliday_HidesBanner()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        var (vm, _) = Build(adapter);

        Assert.False(vm.HasHolidayCountdown);
        Assert.Empty(vm.HolidayCountdownLines);
    }

    [Fact]
    public void Countdown_HolidayInFuture_RendersInDaysTemplate()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 6, "Dashain", "दशैं");
        var (vm, _) = Build(adapter);

        Assert.True(vm.HasHolidayCountdown);
        Assert.Single(vm.HolidayCountdownLines);
        Assert.Equal("5 days until Dashain", vm.HolidayCountdownLines[0]);
    }

    [Fact]
    public void Countdown_HolidayToday_UsesTodayTemplate()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 1, "New Year", "नयाँ वर्ष");
        var (vm, _) = Build(adapter);

        Assert.Single(vm.HolidayCountdownLines);
        Assert.Equal("Today: New Year", vm.HolidayCountdownLines[0]);
    }

    [Fact]
    public void Countdown_HolidayTomorrow_UsesTomorrowTemplate()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 2, "Eve", "साँझ");
        var (vm, _) = Build(adapter);

        Assert.Single(vm.HolidayCountdownLines);
        Assert.Equal("Tomorrow: Eve", vm.HolidayCountdownLines[0]);
    }

    [Fact]
    public void Countdown_TwoEventsOnSameDay_RendersPrimaryPlusOneMoreEvent()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHolidayMulti(2082, 6, 4,
            en: new[] { "Tihar", "Bhai Tika" },
            np: new[] { "तिहार", "भाइटीका" });
        var (vm, _) = Build(adapter);

        Assert.Equal(2, vm.HolidayCountdownLines.Count);
        Assert.Equal("3 days until Tihar", vm.HolidayCountdownLines[0]);
        Assert.Equal("+1 more",            vm.HolidayCountdownLines[1]);
    }

    [Fact]
    public void Countdown_ManyEventsOnSameDay_RendersPluralMoreEvents()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHolidayMulti(2082, 6, 8,
            en: new[] { "A", "B", "C", "D", "E", "F" },
            np: new[] { "क", "ख", "ग", "घ", "ङ", "च" });
        var (vm, _) = Build(adapter);

        Assert.Equal(2, vm.HolidayCountdownLines.Count);
        Assert.Equal("7 days until A",  vm.HolidayCountdownLines[0]);
        Assert.Equal("+5 more",         vm.HolidayCountdownLines[1]);
    }

    [Fact]
    public void Countdown_MultiEventsToday_UsesTodayPrimary()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHolidayMulti(2082, 6, 1,
            en: new[] { "X", "Y" },
            np: new[] { "क्ष", "य" });
        var (vm, _) = Build(adapter);

        Assert.Equal(2, vm.HolidayCountdownLines.Count);
        Assert.Equal("Today: X",      vm.HolidayCountdownLines[0]);
        Assert.Equal("+1 more",       vm.HolidayCountdownLines[1]);
    }

    [Fact]
    public void Popup_Entries_AreFlattenedOnePerName_InOrder()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 3, "A", "क");
        adapter.AddHolidayMulti(2082, 6, 9,
            en: new[] { "B1", "B2" }, np: new[] { "ख१", "ख२" });
        var (vm, _) = Build(adapter);

        Assert.Equal(3, vm.HolidayPopupEntries.Count);
        Assert.Equal("A",  vm.HolidayPopupEntries[0].Name);
        Assert.Equal("B1", vm.HolidayPopupEntries[1].Name);
        Assert.Equal("B2", vm.HolidayPopupEntries[2].Name);
        Assert.Equal("in 2 days", vm.HolidayPopupEntries[0].WhenLabel);
        Assert.Equal("in 8 days", vm.HolidayPopupEntries[1].WhenLabel);
        Assert.Equal("in 8 days", vm.HolidayPopupEntries[2].WhenLabel);
        // Same-day entries share the same date label.
        Assert.Equal(vm.HolidayPopupEntries[1].DateLabel, vm.HolidayPopupEntries[2].DateLabel);
    }

    [Fact]
    public void Countdown_NepaliLanguage_UsesDevanagariDigits()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 6, "Dashain", "दशैं");
        var (vm, _) = Build(adapter, language: "ne");

        Assert.Single(vm.HolidayCountdownLines);
        // Nepali template: "{1} सम्म {0} दिन" → "दशैं सम्म ५ दिन"
        Assert.Equal("दशैं सम्म ५ दिन बाँकी", vm.HolidayCountdownLines[0]);
    }

    [Fact]
    public void Countdown_LanguageSwitch_RefreshesLines()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 6, "Dashain", "दशैं");
        var (vm, loc) = Build(adapter, language: "en");

        Assert.Equal("5 days until Dashain", vm.HolidayCountdownLines[0]);

        loc.SetLanguage("ne");
        vm.OnLanguageChanged();

        Assert.Equal("दशैं सम्म ५ दिन बाँकी", vm.HolidayCountdownLines[0]);
    }

    [Fact]
    public void Countdown_ToggleOff_AfterEnabled_ClearsLines()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 6, "Dashain", "दशैं");
        var (vm, _) = Build(adapter);

        Assert.True(vm.HasHolidayCountdown);

        vm.ShowHolidayCountdown = false;

        Assert.False(vm.HasHolidayCountdown);
        Assert.Empty(vm.HolidayCountdownLines);
    }

    [Fact]
    public void Popup_AcrossMultipleDays_IncludesAllNames()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 3, "A", "क");
        adapter.AddHoliday(2082, 6, 9, "B", "ख");
        var (vm, _) = Build(adapter);

        var names = vm.HolidayPopupEntries.Select(e => e.Name).ToList();
        Assert.Contains("A", names);
        Assert.Contains("B", names);
    }

    [Fact]
    public void Countdown_HolidayWithoutNames_HidesBanner()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        // Public holiday flag set but no event names. GetNextHoliday returns
        // this empty-named day; BuildCountdownLines yields no lines, so the
        // banner stays hidden rather than rendering a blank pill.
        adapter.AddHolidayWithoutEvents(2082, 6, 2);
        var (vm, _) = Build(adapter);

        Assert.False(vm.HasHolidayCountdown);
        Assert.Empty(vm.HolidayCountdownLines);
    }
}
