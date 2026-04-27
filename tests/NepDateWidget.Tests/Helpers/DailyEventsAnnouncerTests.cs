using NepDateWidget.Helpers;

namespace NepDateWidget.Tests.Helpers;

public sealed class DailyEventsAnnouncerTests
{
    private static readonly DateTime Today = new(2026, 4, 24);

    [Fact]
    public void ShouldFire_WhenDisabled_ReturnsFalse()
    {
        Assert.False(DailyEventsAnnouncer.ShouldFire(Today, lastShownIsoDate: "", isEnabled: false, eventCount: 3));
    }

    [Fact]
    public void ShouldFire_WhenNoEvents_ReturnsFalse()
    {
        Assert.False(DailyEventsAnnouncer.ShouldFire(Today, lastShownIsoDate: "", isEnabled: true, eventCount: 0));
    }

    [Fact]
    public void ShouldFire_WhenAlreadyShownToday_ReturnsFalse()
    {
        Assert.False(DailyEventsAnnouncer.ShouldFire(Today, lastShownIsoDate: "2026-04-24", isEnabled: true, eventCount: 2));
    }

    [Fact]
    public void ShouldFire_WhenLastShownYesterday_ReturnsTrue()
    {
        Assert.True(DailyEventsAnnouncer.ShouldFire(Today, lastShownIsoDate: "2026-04-23", isEnabled: true, eventCount: 1));
    }

    [Fact]
    public void ShouldFire_WhenNeverShown_ReturnsTrue()
    {
        Assert.True(DailyEventsAnnouncer.ShouldFire(Today, lastShownIsoDate: null, isEnabled: true, eventCount: 1));
        Assert.True(DailyEventsAnnouncer.ShouldFire(Today, lastShownIsoDate: "",   isEnabled: true, eventCount: 1));
    }

    [Fact]
    public void ShouldFire_IsCaseAndFormatStrict()
    {
        // Legacy or hand-edited values must not accidentally match.
        Assert.True(DailyEventsAnnouncer.ShouldFire(Today, "24-04-2026", true, 1));
        Assert.True(DailyEventsAnnouncer.ShouldFire(Today, "2026/04/24", true, 1));
    }

    [Fact]
    public void FormatBody_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DailyEventsAnnouncer.FormatBody(null));
        Assert.Equal(string.Empty, DailyEventsAnnouncer.FormatBody(Array.Empty<string>()));
    }

    [Fact]
    public void FormatBody_SingleEvent_ProducesOneBullet()
    {
        var body = DailyEventsAnnouncer.FormatBody(new[] { "Loktantra Diwas" });
        Assert.Equal("• Loktantra Diwas", body);
    }

    [Fact]
    public void FormatBody_MultipleEvents_OneBulletPerLine_NoTrailingNewline()
    {
        var body = DailyEventsAnnouncer.FormatBody(new[] { "Event A", "Event B", "Event C" });
        Assert.Equal("• Event A\n• Event B\n• Event C", body);
    }

    [Fact]
    public void ToIsoDate_FormatsAsYyyyMmDd_Invariant()
    {
        Assert.Equal("2026-04-24", DailyEventsAnnouncer.ToIsoDate(new DateTime(2026, 4, 24, 23, 59, 59)));
        Assert.Equal("2026-01-01", DailyEventsAnnouncer.ToIsoDate(new DateTime(2026, 1, 1)));
    }
}
