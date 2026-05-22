using NepDateWidget.Models;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

public class CalendarDayViewModelTests
{
    private static CalendarDay MakeDay(int day = 15, bool isCurrentMonth = true, bool isToday = false,
        DayOfWeek dow = DayOfWeek.Monday, int adDay = 28, bool isHighlighted = false)
    {
        return new CalendarDay
        {
            Year = 2082, Month = 12, Day = day,
            AdDay = adDay, DayOfWeek = dow,
            IsCurrentMonth = isCurrentMonth, IsToday = isToday, IsHighlighted = isHighlighted
        };
    }

    private static CalendarDay MakePadding()
    {
        return new CalendarDay
        {
            Year = 0, Month = 0, Day = 0,
            AdDay = 0, DayOfWeek = DayOfWeek.Sunday,
            IsCurrentMonth = false, IsToday = false
        };
    }

    // ── Basic property pass-through ──────────────────────────────────────────

    [Fact]
    public void Properties_PassThrough_FromCalendarDay()
    {
        var day = MakeDay(day: 20, isToday: true, dow: DayOfWeek.Saturday, adDay: 3, isHighlighted: true);
        var vm = new CalendarDayViewModel(day);
        Assert.Equal(2082, vm.BsYear);
        Assert.Equal(12, vm.BsMonth);
        Assert.Equal(20, vm.Day);
        Assert.True(vm.IsCurrentMonth);
        Assert.False(vm.IsPadding);
        Assert.True(vm.IsToday);
        Assert.True(vm.IsSaturday);
        Assert.True(vm.IsHighlighted);
    }

    // ── DayText: English digits by default ───────────────────────────────────

    [Fact]
    public void DayText_EnglishDigits_ByDefault()
    {
        var vm = new CalendarDayViewModel(MakeDay(day: 7));
        Assert.Equal("7", vm.DayText);
    }

    [Fact]
    public void DayText_NepaliDigits_WhenIsNepali()
    {
        var vm = new CalendarDayViewModel(MakeDay(day: 7), isNepali: true);
        Assert.Equal("७", vm.DayText);
    }

    [Fact]
    public void DayText_TwoDigit_NepaliDigits()
    {
        var vm = new CalendarDayViewModel(MakeDay(day: 25), isNepali: true);
        Assert.Equal("२५", vm.DayText);
    }

    [Fact]
    public void DayText_Empty_ForPaddingCells()
    {
        var vm = new CalendarDayViewModel(MakePadding());
        Assert.Equal(string.Empty, vm.DayText);
    }

    // ── EnglishDayText ───────────────────────────────────────────────────────

    [Fact]
    public void EnglishDayText_ShowsAdDay_ForCurrentMonth()
    {
        var vm = new CalendarDayViewModel(MakeDay(adDay: 28));
        Assert.Equal("28", vm.EnglishDayText);
    }

    [Fact]
    public void EnglishDayText_Empty_ForPadding()
    {
        var vm = new CalendarDayViewModel(MakePadding());
        Assert.Equal(string.Empty, vm.EnglishDayText);
    }

    [Fact]
    public void EnglishDayText_Empty_WhenAdDayZero()
    {
        var vm = new CalendarDayViewModel(MakeDay(adDay: 0));
        Assert.Equal(string.Empty, vm.EnglishDayText);
    }

    // ── ShowEnglishBadge ─────────────────────────────────────────────────────

    [Fact]
    public void ShowEnglishBadge_True_WhenCurrentMonthAndAdDayPositiveAndToggleOn()
    {
        var vm = new CalendarDayViewModel(MakeDay(adDay: 5), showEnglishDayNumbers: true);
        Assert.True(vm.ShowEnglishBadge);
    }

    [Fact]
    public void ShowEnglishBadge_False_WhenToggleOff()
    {
        var vm = new CalendarDayViewModel(MakeDay(adDay: 5), showEnglishDayNumbers: false);
        Assert.False(vm.ShowEnglishBadge);
    }

    [Fact]
    public void ShowEnglishBadge_False_ForPadding()
    {
        var vm = new CalendarDayViewModel(MakePadding(), showEnglishDayNumbers: true);
        Assert.False(vm.ShowEnglishBadge);
    }

    // ── ShowSaturdayHighlight ────────────────────────────────────────────────

    [Fact]
    public void ShowSaturdayHighlight_True_WhenSaturdayAndToggleOn()
    {
        var vm = new CalendarDayViewModel(MakeDay(dow: DayOfWeek.Saturday), highlightSaturdays: true);
        Assert.True(vm.ShowSaturdayHighlight);
    }

    [Fact]
    public void ShowSaturdayHighlight_False_WhenNotSaturday()
    {
        var vm = new CalendarDayViewModel(MakeDay(dow: DayOfWeek.Monday), highlightSaturdays: true);
        Assert.False(vm.ShowSaturdayHighlight);
    }

    [Fact]
    public void ShowSaturdayHighlight_False_WhenToggleOff()
    {
        var vm = new CalendarDayViewModel(MakeDay(dow: DayOfWeek.Saturday), highlightSaturdays: false);
        Assert.False(vm.ShowSaturdayHighlight);
    }

    // ── HasReminders / ReminderTooltip ───────────────────────────────────────

    [Fact]
    public void HasReminders_DefaultFalse()
    {
        var vm = new CalendarDayViewModel(MakeDay());
        Assert.False(vm.HasReminders);
    }

    [Fact]
    public void HasReminders_CanBeSet()
    {
        var vm = new CalendarDayViewModel(MakeDay());
        vm.HasReminders = true;
        Assert.True(vm.HasReminders);
    }

    [Fact]
    public void ReminderTooltip_DefaultNull()
    {
        var vm = new CalendarDayViewModel(MakeDay());
        Assert.Null(vm.ReminderTooltip);
    }

    [Fact]
    public void ReminderTooltip_CanBeSet()
    {
        var vm = new CalendarDayViewModel(MakeDay());
        vm.ReminderTooltip = "Meeting at 10 AM";
        Assert.Equal("Meeting at 10 AM", vm.ReminderTooltip);
    }

    // ── PropertyChanged notifications ────────────────────────────────────────

    [Fact]
    public void HasReminders_RaisesPropertyChanged()
    {
        var vm = new CalendarDayViewModel(MakeDay());
        var raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.HasReminders)) { raised = true; } };
        vm.HasReminders = true;
        Assert.True(raised);
    }

    [Fact]
    public void ReminderTooltip_RaisesPropertyChanged()
    {
        var vm = new CalendarDayViewModel(MakeDay());
        var raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.ReminderTooltip)) { raised = true; } };
        vm.ReminderTooltip = "test";
        Assert.True(raised);
    }

    // ── Null guard ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsOnNullDay()
    {
        Assert.Throws<ArgumentNullException>(() => new CalendarDayViewModel(null!));
    }

    // ── Nepali digit mapping covers all digits ───────────────────────────────

    [Theory]
    [InlineData(1, "१")]
    [InlineData(2, "२")]
    [InlineData(3, "३")]
    [InlineData(4, "४")]
    [InlineData(5, "५")]
    [InlineData(6, "६")]
    [InlineData(7, "७")]
    [InlineData(8, "८")]
    [InlineData(9, "९")]
    [InlineData(10, "१०")]
    [InlineData(30, "३०")]
    public void DayText_NepaliDigits_AllDigitsCovered(int day, string expected)
    {
        var vm = new CalendarDayViewModel(MakeDay(day: day), isNepali: true);
        Assert.Equal(expected, vm.DayText);
    }

    // ── CalendarDay model ────────────────────────────────────────────────────

    [Fact]
    public void CalendarDay_IsSaturday_DerivedFromDayOfWeek()
    {
        var sat = new CalendarDay { DayOfWeek = DayOfWeek.Saturday };
        var mon = new CalendarDay { DayOfWeek = DayOfWeek.Monday };
        Assert.True(sat.IsSaturday);
        Assert.False(mon.IsSaturday);
    }

    [Fact]
    public void CalendarDay_IsPadding_InverseOfIsCurrentMonth()
    {
        var current = new CalendarDay { IsCurrentMonth = true };
        var padding = new CalendarDay { IsCurrentMonth = false };
        Assert.False(current.IsPadding);
        Assert.True(padding.IsPadding);
    }
}
