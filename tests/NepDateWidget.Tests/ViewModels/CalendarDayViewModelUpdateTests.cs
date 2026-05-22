using System.ComponentModel;
using NepDateWidget.Models;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Regression + correctness tests for CalendarDayViewModel.Update() and
/// UpdateVisibleEventCount(). Every test maps to a specific on-screen rendering
/// scenario: stale events text, stale tithi, stale flags, PropertyChanged firing,
/// and correct clearing / populating during in-place month navigation.
///
/// Background: before H1 (in-place update), the grid always rebuilt VMs from scratch,
/// so staleness was impossible. After H1 the VM is reused across navigations, and
/// every property transition must be explicitly tested.
/// </summary>
public class CalendarDayViewModelUpdateTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a current-month CalendarDay for BS 2082/4.</summary>
    private static CalendarDay CurrentMonth(
        int day = 5,
        string tithiEn = "",
        string[]? eventsEn = null,
        bool isToday = false,
        bool isHoliday = false,
        DayOfWeek dow = DayOfWeek.Monday)
    {
        var en = eventsEn ?? Array.Empty<string>();
        return new CalendarDay
        {
            Year = 2082, Month = 4, Day = day,
            AdDay = day + 13,
            DayOfWeek = dow,
            IsCurrentMonth = true,
            IsToday = isToday,
            IsPublicHoliday = isHoliday,
            TithiEn = tithiEn,
            TithiNp = tithiEn.Length > 0 ? tithiEn + "-np" : string.Empty,
            EventsEn = en,
            EventsNp = en.Length > 0 ? en.Select(e => e + "-np").ToArray() : Array.Empty<string>(),
        };
    }

    /// <summary>Creates a padding (out-of-month) CalendarDay.</summary>
    private static CalendarDay Padding(DayOfWeek dow = DayOfWeek.Sunday)
        => new CalendarDay { IsCurrentMonth = false, DayOfWeek = dow };

    /// <summary>Calls vm.Update() with a standard settings set so tests only vary what they care about.</summary>
    private static void DoUpdate(
        CalendarDayViewModel vm,
        CalendarDay day,
        bool showTithi = true,
        bool showEvents = true,
        bool isNepali = false,
        int visibleEventCount = 1,
        bool showEnglishDayNumbers = true,
        bool highlightPublicHolidays = true)
        => vm.Update(day, isNepali,
            showEnglishDayNumbers: showEnglishDayNumbers,
            highlightSaturdays: true,
            highlightSundays: false,
            showTithi: showTithi,
            showEvents: showEvents,
            highlightPublicHolidays: highlightPublicHolidays,
            adapter: null,
            localization: null,
            visibleEventCount: visibleEventCount);

    /// <summary>Captures all PropertyChanged property names raised during <paramref name="act"/>.</summary>
    private static List<string?> Capture(CalendarDayViewModel vm, Action act)
    {
        var fired = new List<string?>();
        PropertyChangedEventHandler h = (_, e) => fired.Add(e.PropertyName);
        vm.PropertyChanged += h;
        act();
        vm.PropertyChanged -= h;
        return fired;
    }

    // ════════════════════════════════════════════════════════════════════════
    // EVENTS: clearing - core regression for "events repeating after navigation"
    // When a cell transitions from a day WITH events to one WITHOUT (or to a
    // padding cell), VisibleEvents must be empty and WPF bindings must be notified.
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_EventsToNoEvents_ClearsVisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Festival A"]), showEvents: true);
        Assert.Single(vm.VisibleEvents); // precondition: events visible
        DoUpdate(vm, CurrentMonth());    // same cell position, day without events
        Assert.Empty(vm.VisibleEvents);
    }

    [Fact]
    public void Update_EventsToNoEvents_ClearsHasVisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Festival A"]), showEvents: true);
        DoUpdate(vm, CurrentMonth());
        Assert.False(vm.HasVisibleEvents);
    }

    [Fact]
    public void Update_EventsToNoEvents_ClearsHasHiddenEvents()
    {
        // Start with overflow (2 events, count=1 → HasHiddenEvents=true)
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["A", "B"]), showEvents: true);
        Assert.True(vm.HasHiddenEvents); // precondition
        DoUpdate(vm, CurrentMonth());    // new day has no events
        Assert.False(vm.HasHiddenEvents);
    }

    [Fact]
    public void Update_EventsToNoEvents_FiresPropertyChanged_VisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Festival"]), showEvents: true);
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth()));
        Assert.Contains(nameof(vm.VisibleEvents), fired);
    }

    [Fact]
    public void Update_EventsToNoEvents_FiresPropertyChanged_HasVisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Festival"]), showEvents: true);
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth()));
        Assert.Contains(nameof(vm.HasVisibleEvents), fired);
    }

    [Fact]
    public void Update_CurrentMonthWithEvents_ToPaddingCell_ClearsVisibleEvents()
    {
        // Padding cells must never show events from the previous current-month cell.
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Festival"]), showEvents: true);
        DoUpdate(vm, Padding());
        Assert.Empty(vm.VisibleEvents);
        Assert.False(vm.HasVisibleEvents);
    }

    [Fact]
    public void Update_ShowEventsOff_NoEventsShownEvenWhenDayHasEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Festival"]), showEvents: false);
        Assert.Empty(vm.VisibleEvents); // constructor: showEvents=false → empty
        DoUpdate(vm, CurrentMonth(eventsEn: ["Festival"]), showEvents: false);
        Assert.Empty(vm.VisibleEvents);
    }

    // ════════════════════════════════════════════════════════════════════════
    // EVENTS: populating - transition from no-events to events
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_NoEventsToEvents_PopulatesVisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEvents: true);
        Assert.Empty(vm.VisibleEvents); // precondition
        DoUpdate(vm, CurrentMonth(eventsEn: ["Dashain"]));
        Assert.Single(vm.VisibleEvents);
        Assert.Equal("Dashain", vm.VisibleEvents[0]);
    }

    [Fact]
    public void Update_NoEventsToEvents_SetsHasVisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEvents: true);
        DoUpdate(vm, CurrentMonth(eventsEn: ["Dashain"]));
        Assert.True(vm.HasVisibleEvents);
    }

    [Fact]
    public void Update_NoEventsToEvents_FiresPropertyChanged_VisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEvents: true);
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth(eventsEn: ["Dashain"])));
        Assert.Contains(nameof(vm.VisibleEvents), fired);
    }

    // ════════════════════════════════════════════════════════════════════════
    // EVENTS: overflow suffix and hidden-events flag
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_TwoEvents_CountOne_ShowsFirstWithOverflowSuffix()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEvents: true);
        DoUpdate(vm, CurrentMonth(eventsEn: ["Alpha", "Beta"]), visibleEventCount: 1);
        Assert.Single(vm.VisibleEvents);
        Assert.Equal("Alpha +1", vm.VisibleEvents[0]);
        Assert.True(vm.HasHiddenEvents);
    }

    [Fact]
    public void Update_ThreeEvents_CountOne_SuffixReflectsHiddenCount()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEvents: true);
        DoUpdate(vm, CurrentMonth(eventsEn: ["A", "B", "C"]), visibleEventCount: 1);
        Assert.Equal("A +2", vm.VisibleEvents[0]);
    }

    [Fact]
    public void Update_TwoEvents_CountTwo_ShowsBothAndNoOverflow()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEvents: true);
        DoUpdate(vm, CurrentMonth(eventsEn: ["Alpha", "Beta"]), visibleEventCount: 2);
        Assert.Equal(2, vm.VisibleEvents.Count);
        Assert.Equal("Alpha", vm.VisibleEvents[0]);
        Assert.Equal("Beta", vm.VisibleEvents[1]);
        Assert.False(vm.HasHiddenEvents);
    }

    [Fact]
    public void Update_CountExceedsEventCount_ShowsAllWithNoOverflow()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEvents: true);
        DoUpdate(vm, CurrentMonth(eventsEn: ["A", "B"]), visibleEventCount: 99);
        Assert.Equal(2, vm.VisibleEvents.Count);
        Assert.False(vm.HasHiddenEvents);
    }

    // ════════════════════════════════════════════════════════════════════════
    // EVENTS: content-same optimisation - no unnecessary PropertyChanged
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_SameEventsContent_DoesNotFirePropertyChanged_VisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Dashain"]), showEvents: true);
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth(eventsEn: ["Dashain"])));
        Assert.DoesNotContain(nameof(vm.VisibleEvents), fired);
    }

    [Fact]
    public void Update_DifferentEventsContent_FiresPropertyChanged_VisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Dashain"]), showEvents: true);
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth(eventsEn: ["Tihar"])));
        Assert.Contains(nameof(vm.VisibleEvents), fired);
    }

    // ════════════════════════════════════════════════════════════════════════
    // TITHI: clearing - core regression for "tithi repeating after navigation"
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_TithiToNoTithi_ClearsTithiText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Dashami"), showTithi: true);
        Assert.Equal("Dashami", vm.TithiText); // precondition
        DoUpdate(vm, CurrentMonth()); // new day has no tithi
        Assert.Equal(string.Empty, vm.TithiText);
    }

    [Fact]
    public void Update_TithiToNoTithi_ClearsShowTithiText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Dashami"), showTithi: true);
        Assert.True(vm.ShowTithiText); // precondition
        DoUpdate(vm, CurrentMonth());
        Assert.False(vm.ShowTithiText);
    }

    [Fact]
    public void Update_TithiCell_ToPadding_ClearsTithiText()
    {
        // Padding cells must never show tithi from the previous current-month cell.
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Dashami"), showTithi: true);
        DoUpdate(vm, Padding());
        Assert.Equal(string.Empty, vm.TithiText);
    }

    [Fact]
    public void Update_TithiCell_ToPadding_ClearsShowTithiText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Dashami"), showTithi: true);
        DoUpdate(vm, Padding());
        Assert.False(vm.ShowTithiText);
    }

    [Fact]
    public void Update_NoTithiToTithi_SetsTithiText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showTithi: true);
        DoUpdate(vm, CurrentMonth(tithiEn: "Purnima"));
        Assert.Equal("Purnima", vm.TithiText);
        Assert.True(vm.ShowTithiText);
    }

    [Fact]
    public void Update_ShowTithiOff_NeverShowsTithiTextEvenWhenPresent()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Dashami"), showTithi: false);
        Assert.False(vm.ShowTithiText);
        DoUpdate(vm, CurrentMonth(tithiEn: "Dashami"), showTithi: false);
        Assert.False(vm.ShowTithiText);
    }

    // ════════════════════════════════════════════════════════════════════════
    // TITHI: PropertyChanged notifications
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_TithiToNoTithi_FiresPropertyChanged_TithiText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Dashami"), showTithi: true);
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth()));
        Assert.Contains(nameof(vm.TithiText), fired);
    }

    [Fact]
    public void Update_TithiToNoTithi_FiresPropertyChanged_ShowTithiText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Dashami"), showTithi: true);
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth()));
        Assert.Contains(nameof(vm.ShowTithiText), fired);
    }

    [Fact]
    public void Update_SameTithiContent_DoesNotFirePropertyChanged_TithiText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Dashami"), showTithi: true);
        // Same tithi in new day - SetProperty should suppress the notification.
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth(tithiEn: "Dashami")));
        Assert.DoesNotContain(nameof(vm.TithiText), fired);
    }

    // ════════════════════════════════════════════════════════════════════════
    // TITHI: Purnima / Aunsi special flags
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_PurnimaDay_SetsIsPurnima_True()
    {
        var vm = new CalendarDayViewModel(CurrentMonth());
        DoUpdate(vm, CurrentMonth(tithiEn: "Purnima"));
        Assert.True(vm.IsPurnima);
        Assert.False(vm.IsAunsi);
    }

    [Fact]
    public void Update_KshayaPurnima_SetsIsPurnima_True()
    {
        // Tithi name starts with "Purnima" for all Purnima variants
        var vm = new CalendarDayViewModel(CurrentMonth());
        DoUpdate(vm, CurrentMonth(tithiEn: "Purnima (Kshaya)"));
        Assert.True(vm.IsPurnima);
    }

    [Fact]
    public void Update_PurnimaToRegularDay_ClearsIsPurnima()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Purnima"), showTithi: true);
        Assert.True(vm.IsPurnima); // precondition
        DoUpdate(vm, CurrentMonth());
        Assert.False(vm.IsPurnima);
    }

    [Fact]
    public void Update_PurnimaCell_ToPadding_ClearsIsPurnima()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Purnima"), showTithi: true);
        DoUpdate(vm, Padding());
        Assert.False(vm.IsPurnima);
    }

    [Fact]
    public void Update_AunsiDay_SetsIsAunsi_True()
    {
        var vm = new CalendarDayViewModel(CurrentMonth());
        DoUpdate(vm, CurrentMonth(tithiEn: "Aunsi"));
        Assert.True(vm.IsAunsi);
        Assert.False(vm.IsPurnima);
    }

    [Fact]
    public void Update_AunsiToRegularDay_ClearsIsAunsi()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Aunsi"), showTithi: true);
        Assert.True(vm.IsAunsi); // precondition
        DoUpdate(vm, CurrentMonth());
        Assert.False(vm.IsAunsi);
    }

    // ════════════════════════════════════════════════════════════════════════
    // TITHI: language switching
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_NepaliLanguage_UsesTithiNp()
    {
        var vm = new CalendarDayViewModel(CurrentMonth());
        DoUpdate(vm, CurrentMonth(tithiEn: "Dashami"), showTithi: true, isNepali: true);
        // CurrentMonth sets TithiNp = tithiEn + "-np"
        Assert.Equal("Dashami-np", vm.TithiText);
    }

    [Fact]
    public void Update_NepaliLanguage_UsesEventsNp()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEvents: true);
        DoUpdate(vm, CurrentMonth(eventsEn: ["Festival"]), isNepali: true);
        // CurrentMonth sets EventsNp[i] = EventsEn[i] + "-np"
        Assert.Single(vm.VisibleEvents);
        Assert.Equal("Festival-np", vm.VisibleEvents[0]);
    }

    [Fact]
    public void Update_LanguageSwitchEnToNe_UpdatesTithiText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(tithiEn: "Dashami"), isNepali: false, showTithi: true);
        Assert.Equal("Dashami", vm.TithiText); // English

        DoUpdate(vm, CurrentMonth(tithiEn: "Dashami"), showTithi: true, isNepali: true);
        Assert.Equal("Dashami-np", vm.TithiText); // Nepali
    }

    [Fact]
    public void Update_LanguageSwitchEnToNe_UpdatesVisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Dashain"]), isNepali: false, showEvents: true);
        Assert.Equal("Dashain", vm.VisibleEvents[0]);

        DoUpdate(vm, CurrentMonth(eventsEn: ["Dashain"]), isNepali: true);
        Assert.Equal("Dashain-np", vm.VisibleEvents[0]);
    }

    // ════════════════════════════════════════════════════════════════════════
    // PADDING ↔ CURRENT-MONTH transitions: DayText, badges, holiday highlight
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_CurrentMonthToPadding_ClearsDayText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(day: 15));
        Assert.Equal("15", vm.DayText);
        DoUpdate(vm, Padding());
        Assert.Equal(string.Empty, vm.DayText);
    }

    [Fact]
    public void Update_CurrentMonthToPadding_ClearsEnglishDayText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(day: 5), showEnglishDayNumbers: true);
        Assert.NotEmpty(vm.EnglishDayText);
        DoUpdate(vm, Padding());
        Assert.Equal(string.Empty, vm.EnglishDayText);
    }

    [Fact]
    public void Update_CurrentMonthToPadding_ClearsShowEnglishBadge()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEnglishDayNumbers: true);
        Assert.True(vm.ShowEnglishBadge);
        DoUpdate(vm, Padding());
        Assert.False(vm.ShowEnglishBadge);
    }

    [Fact]
    public void Update_CurrentMonthToPadding_ClearsShowHolidayHighlight()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(isHoliday: true), highlightPublicHolidays: true);
        Assert.True(vm.ShowHolidayHighlight);
        DoUpdate(vm, Padding());
        Assert.False(vm.ShowHolidayHighlight);
    }

    [Fact]
    public void Update_PaddingToCurrentMonth_SetsDayText()
    {
        var vm = new CalendarDayViewModel(Padding());
        Assert.Equal(string.Empty, vm.DayText);
        DoUpdate(vm, CurrentMonth(day: 12));
        Assert.Equal("12", vm.DayText);
    }

    [Fact]
    public void Update_PaddingToCurrentMonth_SetsShowEnglishBadge()
    {
        var vm = new CalendarDayViewModel(Padding());
        Assert.False(vm.ShowEnglishBadge);
        DoUpdate(vm, CurrentMonth());
        Assert.True(vm.ShowEnglishBadge);
    }

    [Fact]
    public void Update_PaddingToCurrentMonthWithHoliday_SetsShowHolidayHighlight()
    {
        var vm = new CalendarDayViewModel(Padding());
        DoUpdate(vm, CurrentMonth(isHoliday: true), highlightPublicHolidays: true);
        Assert.True(vm.ShowHolidayHighlight);
    }

    [Fact]
    public void Update_Nepali_CurrentMonthToPadding_ClearsDayText()
    {
        // Nepali day text uses Unicode digits - must also clear to empty on padding.
        var vm = new CalendarDayViewModel(CurrentMonth(day: 7), isNepali: true);
        Assert.Equal("७", vm.DayText);
        DoUpdate(vm, Padding(), isNepali: true);
        Assert.Equal(string.Empty, vm.DayText);
    }

    // ════════════════════════════════════════════════════════════════════════
    // _day-delegating properties: fire PropertyChanged only when value changes.
    // Each test has two assertions: fires when the value transitions, and is
    // silent when the same value is re-applied (the optimization that removes
    // up to 378 redundant notifications per month navigation).
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_FiresPropertyChanged_IsCurrentMonth_WhenValueChanges()
    {
        // current-month → padding: IsCurrentMonth changes true→false, must fire.
        var vm = new CalendarDayViewModel(CurrentMonth());
        var fired = Capture(vm, () => DoUpdate(vm, Padding()));
        Assert.Contains(nameof(vm.IsCurrentMonth), fired);
    }

    [Fact]
    public void Update_SilentPropertyChanged_IsCurrentMonth_WhenValueUnchanged()
    {
        // current-month → current-month: IsCurrentMonth stays true, must NOT fire.
        var vm = new CalendarDayViewModel(CurrentMonth());
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth()));
        Assert.DoesNotContain(nameof(vm.IsCurrentMonth), fired);
    }

    [Fact]
    public void Update_FiresPropertyChanged_IsPadding_WhenValueChanges()
    {
        // current-month → padding: IsPadding changes false→true, must fire.
        var vm = new CalendarDayViewModel(CurrentMonth());
        var fired = Capture(vm, () => DoUpdate(vm, Padding()));
        Assert.Contains(nameof(vm.IsPadding), fired);
    }

    [Fact]
    public void Update_SilentPropertyChanged_IsPadding_WhenValueUnchanged()
    {
        // current-month → current-month: IsPadding stays false, must NOT fire.
        var vm = new CalendarDayViewModel(CurrentMonth());
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth()));
        Assert.DoesNotContain(nameof(vm.IsPadding), fired);
    }

    [Fact]
    public void Update_FiresPropertyChanged_IsToday_WhenValueChanges()
    {
        // non-today → today: IsToday changes false→true, must fire.
        var vm = new CalendarDayViewModel(CurrentMonth(isToday: false));
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth(isToday: true)));
        Assert.Contains(nameof(vm.IsToday), fired);
    }

    [Fact]
    public void Update_SilentPropertyChanged_IsToday_WhenValueUnchanged()
    {
        // non-today → non-today: IsToday stays false, must NOT fire.
        var vm = new CalendarDayViewModel(CurrentMonth(isToday: false));
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth(isToday: false)));
        Assert.DoesNotContain(nameof(vm.IsToday), fired);
    }

    [Fact]
    public void Update_FiresPropertyChanged_IsSaturday_WhenValueChanges()
    {
        // Saturday → non-Saturday: IsSaturday changes true→false, must fire.
        var vm = new CalendarDayViewModel(CurrentMonth(dow: DayOfWeek.Saturday));
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth(dow: DayOfWeek.Monday)));
        Assert.Contains(nameof(vm.IsSaturday), fired);
    }

    [Fact]
    public void Update_SilentPropertyChanged_IsSaturday_WhenValueUnchanged()
    {
        // Saturday → Saturday: IsSaturday stays true, must NOT fire.
        var vm = new CalendarDayViewModel(CurrentMonth(dow: DayOfWeek.Saturday));
        var fired = Capture(vm, () => DoUpdate(vm, CurrentMonth(dow: DayOfWeek.Saturday)));
        Assert.DoesNotContain(nameof(vm.IsSaturday), fired);
    }

    // ════════════════════════════════════════════════════════════════════════
    // UpdateVisibleEventCount: direct-call scenarios (not via Update)
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void UpdateVisibleEventCount_CountZero_EmptiesVisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["A"]), showEvents: true);
        vm.UpdateVisibleEventCount(0);
        Assert.Empty(vm.VisibleEvents);
        Assert.False(vm.HasVisibleEvents);
    }

    [Fact]
    public void UpdateVisibleEventCount_CountZero_FiresPropertyChanged_VisibleEvents()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["A"]), showEvents: true);
        var fired = Capture(vm, () => vm.UpdateVisibleEventCount(0));
        Assert.Contains(nameof(vm.VisibleEvents), fired);
    }

    [Fact]
    public void UpdateVisibleEventCount_OneEvent_CountOne_ShowsEventText()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEvents: true);
        DoUpdate(vm, CurrentMonth(eventsEn: ["Dashain"]), visibleEventCount: 1);
        Assert.Single(vm.VisibleEvents);
        Assert.Equal("Dashain", vm.VisibleEvents[0]);
        Assert.False(vm.HasHiddenEvents);
    }

    [Fact]
    public void UpdateVisibleEventCount_ThreeEvents_CountOne_ShowsSuffixCorrectly()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["A", "B", "C"]), showEvents: true);
        vm.UpdateVisibleEventCount(1);
        Assert.Single(vm.VisibleEvents);
        Assert.Equal("A +2", vm.VisibleEvents[0]);
        Assert.True(vm.HasHiddenEvents);
        Assert.True(vm.HasVisibleEvents);
    }

    [Fact]
    public void UpdateVisibleEventCount_CountGreaterThanAll_ShowsAllWithNoOverflow()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["A", "B"]), showEvents: true);
        vm.UpdateVisibleEventCount(99);
        Assert.Equal(2, vm.VisibleEvents.Count);
        Assert.False(vm.HasHiddenEvents);
    }

    [Fact]
    public void UpdateVisibleEventCount_CountIncrease_RemovesOverflowSuffix()
    {
        // Constructor default count=1 → "Alpha +1". Increasing to 2 must remove the suffix.
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Alpha", "Beta"]), showEvents: true);
        Assert.Equal("Alpha +1", vm.VisibleEvents[0]); // precondition

        vm.UpdateVisibleEventCount(2);

        Assert.Equal(2, vm.VisibleEvents.Count);
        Assert.Equal("Alpha", vm.VisibleEvents[0]);
        Assert.Equal("Beta", vm.VisibleEvents[1]);
        Assert.False(vm.HasHiddenEvents);
    }

    [Fact]
    public void UpdateVisibleEventCount_SameContent_DoesNotFirePropertyChanged_VisibleEvents()
    {
        // After constructor count=1, _visibleEventsArray = ["Event"].
        // Calling again with count=1 and same content must not re-fire.
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["Event"]), showEvents: true);
        var fired = Capture(vm, () => vm.UpdateVisibleEventCount(1));
        Assert.DoesNotContain(nameof(vm.VisibleEvents), fired);
    }

    [Fact]
    public void UpdateVisibleEventCount_AfterClearThenRepopulate_FiresPropertyChanged()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["A"]), showEvents: true);
        vm.UpdateVisibleEventCount(0); // clear
        var fired = Capture(vm, () => vm.UpdateVisibleEventCount(1));
        Assert.Contains(nameof(vm.VisibleEvents), fired);
        Assert.Equal("A", vm.VisibleEvents[0]);
    }

    [Fact]
    public void UpdateVisibleEventCount_PaddingCell_NeverShowsEvents()
    {
        // Padding cells have _canShowEvents=false; any count is ignored.
        var vm = new CalendarDayViewModel(Padding());
        vm.UpdateVisibleEventCount(99);
        Assert.Empty(vm.VisibleEvents);
        Assert.False(vm.HasVisibleEvents);
    }

    [Fact]
    public void UpdateVisibleEventCount_ShowEventsOffCell_NeverShowsEvents()
    {
        // Current-month cell where showEvents=false also has _canShowEvents=false.
        var vm = new CalendarDayViewModel(CurrentMonth(eventsEn: ["A"]), showEvents: false);
        vm.UpdateVisibleEventCount(99);
        Assert.Empty(vm.VisibleEvents);
        Assert.False(vm.HasVisibleEvents);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Multiple in-place updates: round-trip stress scenarios
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Update_MultipleNavigations_EventsAlwaysReflectCurrentDay()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showEvents: true);

        DoUpdate(vm, CurrentMonth(eventsEn: ["Dashain"]));
        Assert.Single(vm.VisibleEvents);
        Assert.Equal("Dashain", vm.VisibleEvents[0]);

        DoUpdate(vm, CurrentMonth(eventsEn: ["Tihar"]));
        Assert.Equal("Tihar", vm.VisibleEvents[0]);

        DoUpdate(vm, CurrentMonth()); // no events
        Assert.Empty(vm.VisibleEvents);

        DoUpdate(vm, CurrentMonth(eventsEn: ["New Year"]));
        Assert.Equal("New Year", vm.VisibleEvents[0]);
    }

    [Fact]
    public void Update_MultipleNavigations_TithiAlwaysReflectsCurrentDay()
    {
        var vm = new CalendarDayViewModel(CurrentMonth(), showTithi: true);

        DoUpdate(vm, CurrentMonth(tithiEn: "Purnima"));
        Assert.Equal("Purnima", vm.TithiText);
        Assert.True(vm.IsPurnima);

        DoUpdate(vm, CurrentMonth(tithiEn: "Aunsi"));
        Assert.Equal("Aunsi", vm.TithiText);
        Assert.False(vm.IsPurnima);
        Assert.True(vm.IsAunsi);

        DoUpdate(vm, Padding());
        Assert.Equal(string.Empty, vm.TithiText);
        Assert.False(vm.IsAunsi);

        DoUpdate(vm, CurrentMonth(tithiEn: "Dashami"));
        Assert.Equal("Dashami", vm.TithiText);
    }

    [Fact]
    public void Update_PaddingThenCurrentMonthThenPadding_FullRoundTrip()
    {
        // Simulates a cell at a position that alternates between padding and
        // current-month across month navigations. Nothing should be stale.
        var vm = new CalendarDayViewModel(Padding());

        DoUpdate(vm, CurrentMonth(day: 3, tithiEn: "Dashami", eventsEn: ["Festival"]));
        Assert.Equal("3", vm.DayText);
        Assert.Equal("Dashami", vm.TithiText);
        Assert.Single(vm.VisibleEvents);

        DoUpdate(vm, Padding());
        Assert.Equal(string.Empty, vm.DayText);
        Assert.Equal(string.Empty, vm.TithiText);
        Assert.Empty(vm.VisibleEvents);
        Assert.False(vm.HasVisibleEvents);
        Assert.False(vm.ShowTithiText);
        Assert.False(vm.ShowEnglishBadge);
    }
}
