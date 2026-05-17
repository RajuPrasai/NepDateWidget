using NepDateWidget.Models;
using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

public class CalendarServiceTests
{
    private static CalendarService CreateService(FakeNepaliDateAdapter? adapter = null)
        => new(adapter ?? new FakeNepaliDateAdapter());

    // ── GetCurrentDateInfo ────────────────────────────────────────────────────

    [Fact]
    public void GetCurrentDateInfo_ReturnsTodayBsComponents()
    {
        var svc  = CreateService();
        var info = svc.GetCurrentDateInfo();

        Assert.Equal(2082, info.BsYear);
        Assert.Equal(12,   info.BsMonth);
        Assert.Equal(20,   info.BsDay);
    }

    [Fact]
    public void GetCurrentDateInfo_ReturnsFormattedStrings_NonEmpty()
    {
        var info = CreateService().GetCurrentDateInfo();

        Assert.NotEmpty(info.BsLongEn);
        Assert.NotEmpty(info.BsShortEn);
        Assert.NotEmpty(info.AdLong);
        Assert.NotEmpty(info.AdShort);
        Assert.NotEmpty(info.DayOfWeekShortEn);
    }

    [Fact]
    public void GetCurrentDateInfo_AdDate_MatchesExpected()
    {
        var info = CreateService().GetCurrentDateInfo();
        Assert.Equal(new DateTime(2026, 4, 3), info.AdDate);
    }

    // ── GetMonth - grid structure ─────────────────────────────────────────────

    [Fact]
    public void GetMonth_DayCount_IsMultipleOf7()
    {
        var svc   = CreateService();
        var month = svc.GetMonth(2082, 12);

        Assert.Equal(0, month.Days.Count % 7);
    }

    [Fact]
    public void GetMonth_FirstCurrentDayCell_IsDay1()
    {
        var svc   = CreateService();
        var month = svc.GetMonth(2082, 12);

        var firstReal = month.Days.First(d => d.IsCurrentMonth);
        Assert.Equal(1, firstReal.Day);
    }

    [Fact]
    public void GetMonth_FirstCurrentDayCell_DayOfWeekMatchesFirstDayOfMonth()
    {
        var adapter = new FakeNepaliDateAdapter();
        var svc     = CreateService(adapter);
        var month   = svc.GetMonth(2082, 12);

        var expectedDow = adapter.GetFirstDayOfMonth(2082, 12);
        var firstReal   = month.Days.First(d => d.IsCurrentMonth);

        Assert.Equal(expectedDow, firstReal.DayOfWeek);
    }

    [Fact]
    public void GetMonth_AllCurrentMonthDays_AreContiguous()
    {
        var svc   = CreateService();
        var month = svc.GetMonth(2082, 12);

        var realDays = month.Days.Where(d => d.IsCurrentMonth).Select(d => d.Day).ToList();
        for (int i = 0; i < realDays.Count - 1; i++)
        {
            Assert.Equal(realDays[i] + 1, realDays[i + 1]);
        }
    }

    [Fact]
    public void GetMonth_TotalCurrentDays_MatchesDaysInMonth()
    {
        var svc   = CreateService();
        var month = svc.GetMonth(2082, 12);

        int realCount = month.Days.Count(d => d.IsCurrentMonth);
        Assert.Equal(month.DaysInMonth, realCount);
    }

    [Fact]
    public void GetMonth_AllCells_StartOnSunday()
    {
        var svc   = CreateService();
        var month = svc.GetMonth(2082, 12);

        // Cell 0, 7, 14, … should all be Sunday
        for (int i = 0; i < month.Days.Count; i++)
        {
            Assert.Equal((DayOfWeek)(i % 7), month.Days[i].DayOfWeek);
        }
    }

    [Fact]
    public void GetMonth_MonthMetadata_IsPopulated()
    {
        var svc   = CreateService();
        var month = svc.GetMonth(2082, 12);

        Assert.Equal(2082, month.BsYear);
        Assert.Equal(12,   month.BsMonth);
        Assert.NotEmpty(month.MonthNameEn);
    }

    // ── Today marks ──────────────────────────────────────────────────────────

    [Fact]
    public void GetMonth_TodayCell_IsMarkedIsToday()
    {
        var svc   = CreateService();
        var month = svc.GetMonth(2082, 12);  // today is 2082/12/20

        var todayCell = month.Days.SingleOrDefault(d => d.IsCurrentMonth && d.Day == 20);
        Assert.NotNull(todayCell);
        Assert.True(todayCell.IsToday);
    }

    [Fact]
    public void GetMonth_OtherMonth_ContainsTodayFalse()
    {
        var svc   = CreateService();
        var month = svc.GetMonth(2082, 1);  // today is in month 12, not 1

        Assert.False(month.ContainsToday);
        Assert.All(month.Days, d => Assert.False(d.IsToday));
    }

    // ── Saturday highlight ────────────────────────────────────────────────────

    [Fact]
    public void GetMonth_SaturdayCells_IsSaturdayTrue()
    {
        var svc   = CreateService();
        var month = svc.GetMonth(2082, 12);

        var saturdays = month.Days.Where(d => d.DayOfWeek == DayOfWeek.Saturday).ToList();
        Assert.NotEmpty(saturdays);
        Assert.All(saturdays, d => Assert.True(d.IsSaturday));
    }

    // ── NavigateMonth ─────────────────────────────────────────────────────────

    [Fact]
    public void NavigateMonth_ForwardOneMonth_IncrementsMonth()
    {
        var svc = CreateService();
        var (y, m) = svc.NavigateMonth(2082, 6, 1);
        Assert.Equal(2082, y);
        Assert.Equal(7, m);
    }

    [Fact]
    public void NavigateMonth_ForwardAcrossYearBoundary_IncrementsYear()
    {
        var svc = CreateService();
        var (y, m) = svc.NavigateMonth(2082, 12, 1);
        Assert.Equal(2083, y);
        Assert.Equal(1, m);
    }

    [Fact]
    public void NavigateMonth_BackwardAcrossYearBoundary_DecrementsYear()
    {
        var svc = CreateService();
        var (y, m) = svc.NavigateMonth(2082, 1, -1);
        Assert.Equal(2081, y);
        Assert.Equal(12, m);
    }

    [Fact]
    public void NavigateMonth_BeyondMaxRange_ClampsAt2199_12()
    {
        var svc = CreateService();
        var (y, m) = svc.NavigateMonth(2199, 12, 100);
        Assert.Equal(2199, y);
        Assert.Equal(12, m);
    }

    [Fact]
    public void NavigateMonth_BeyondMinRange_ClampsAt1901_1()
    {
        var svc = CreateService();
        var (y, m) = svc.NavigateMonth(1901, 1, -100);
        Assert.Equal(1901, y);
        Assert.Equal(1, m);
    }
}
