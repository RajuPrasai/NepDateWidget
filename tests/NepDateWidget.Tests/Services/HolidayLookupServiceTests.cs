using NepDateWidget.Services;
using Xunit;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Tests for <see cref="HolidayLookupService"/>. Uses a focused fake adapter
/// that walks BS days correctly so the lookup service's "walk forward" logic
/// can be exercised end-to-end without depending on real NepDate metadata.
/// </summary>
public sealed class HolidayLookupServiceTests
{
    [Fact]
    public void GetNextHoliday_ReturnsNull_WhenNoHolidaysInRange()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        var svc = new HolidayLookupService(adapter);

        Assert.Null(svc.GetNextHoliday());
        Assert.Empty(svc.GetUpcomingHolidays(10));
    }

    [Fact]
    public void GetNextHoliday_ReturnsHolidayOnToday_WithDaysUntilZero()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 1, "Today Fest", "आज पर्व");
        var svc = new HolidayLookupService(adapter);

        var next = svc.GetNextHoliday();
        Assert.NotNull(next);
        Assert.Equal(0, next!.DaysUntil);
        Assert.Equal(new[] { "Today Fest" }, next.NamesEn);
        Assert.Equal(new[] { "आज पर्व" }, next.NamesNp);
        Assert.Equal(2082, next.BsYear);
        Assert.Equal(6, next.BsMonth);
        Assert.Equal(1, next.BsDay);
    }

    [Fact]
    public void GetNextHoliday_SkipsNonHolidayDays_AndReturnsCorrectDaysUntil()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 6, "Dashain", "दशैं");
        var svc = new HolidayLookupService(adapter);

        var next = svc.GetNextHoliday();
        Assert.NotNull(next);
        Assert.Equal(5, next!.DaysUntil);
        Assert.Single(next.NamesEn);
        Assert.Equal("Dashain", next.NamesEn[0]);
    }

    [Fact]
    public void GetUpcomingHolidays_ReturnsInChronologicalOrder()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 10, "B", "ख");
        adapter.AddHoliday(2082, 6, 3,  "A", "क");
        adapter.AddHoliday(2082, 6, 20, "C", "ग");
        var svc = new HolidayLookupService(adapter);

        var list = svc.GetUpcomingHolidays(5);
        Assert.Equal(3, list.Count);
        Assert.Equal("A", list[0].NamesEn[0]); Assert.Equal(2,  list[0].DaysUntil);
        Assert.Equal("B", list[1].NamesEn[0]); Assert.Equal(9,  list[1].DaysUntil);
        Assert.Equal("C", list[2].NamesEn[0]); Assert.Equal(19, list[2].DaysUntil);
    }

    [Fact]
    public void GetUpcomingHolidays_RespectsMaxCount()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        for (int d = 2; d <= 20; d++)
        {
            adapter.AddHoliday(2082, 6, d, $"H{d}", $"प{d}");
        }

        var svc = new HolidayLookupService(adapter);

        var list = svc.GetUpcomingHolidays(3);
        Assert.Equal(3, list.Count);
        Assert.Equal("H2", list[0].NamesEn[0]);
        Assert.Equal("H4", list[2].NamesEn[0]);
    }

    [Fact]
    public void Cache_IsReusedWithinSameAdDate()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 5, "Fest", "पर्व");
        var svc = new HolidayLookupService(adapter);

        _ = svc.GetNextHoliday();
        int callsAfterFirst = adapter.GetCalendarInfoCalls;
        _ = svc.GetNextHoliday();
        Assert.Equal(callsAfterFirst, adapter.GetCalendarInfoCalls);
    }

    [Fact]
    public void Cache_RefreshesAfterAdDateChange()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 5, "Fest", "पर्व");
        var svc = new HolidayLookupService(adapter);

        var first = svc.GetNextHoliday();
        Assert.Equal(4, first!.DaysUntil);

        adapter.SetToday(2082, 6, 4, advanceAdByDays: 3);
        var second = svc.GetNextHoliday();
        Assert.Equal(1, second!.DaysUntil);
    }

    [Fact]
    public void InvalidateCache_ForcesFullRewalk()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHoliday(2082, 6, 5, "Fest", "पर्व");
        var svc = new HolidayLookupService(adapter);

        _ = svc.GetNextHoliday();
        int before = adapter.GetCalendarInfoCalls;
        svc.InvalidateCache();
        _ = svc.GetNextHoliday();
        Assert.True(adapter.GetCalendarInfoCalls > before);
    }

    [Fact]
    public void Holiday_WithEmptyEvents_FallsBackToEmptyName()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHolidayWithoutEvents(2082, 6, 2);
        var svc = new HolidayLookupService(adapter);

        var next = svc.GetNextHoliday();
        Assert.NotNull(next);
        Assert.Empty(next!.NamesEn);
        Assert.Empty(next.NamesNp);
    }

    [Fact]
    public void Holiday_WithMultipleEventsOnSameDay_PreservesAllNames()
    {
        var adapter = new HolidayWalkingFakeAdapter(today: (2082, 6, 1));
        adapter.AddHolidayMulti(2082, 6, 4,
            en: new[] { "Tihar", "Bhai Tika" },
            np: new[] { "तिहार", "भाइटीका" });
        var svc = new HolidayLookupService(adapter);

        var next = svc.GetNextHoliday();
        Assert.NotNull(next);
        Assert.Equal(3, next!.DaysUntil);
        Assert.Equal(new[] { "Tihar", "Bhai Tika" }, next.NamesEn);
        Assert.Equal(new[] { "तिहार", "भाइटीका" }, next.NamesNp);
    }
}
