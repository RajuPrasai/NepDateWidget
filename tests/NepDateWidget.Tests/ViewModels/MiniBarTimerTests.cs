using System.Reflection;
using System.Windows.Threading;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="MiniBarViewModel"/>'s smart timer scheduling.
/// When <c>ShowSeconds = false</c> the dispatcher must wake at most once per
/// minute; when <c>ShowSeconds = true</c> it must tick every second. The
/// internal interval is observed via reflection.
/// </summary>
public class MiniBarTimerTests
{
    private static MiniBarViewModel Build(bool showSeconds)
    {
        var calendar = new CalendarService(new FakeNepaliDateAdapter());
        var loc = new LocalizationService();
        return new MiniBarViewModel(
            calendarService: calendar,
            localizationService: loc,
            showTimezone: false,
            selectedTimezoneId: string.Empty,
            showOffset: false,
            showDayOfWeek: false,
            showEnglishDate: false,
            clockFormat: "12h",
            showSeconds: showSeconds);
    }

    private static TimeSpan ReadInterval(MiniBarViewModel vm)
    {
        var f = typeof(MiniBarViewModel).GetField(
            "_clockTimer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var timer = (DispatcherTimer)f.GetValue(vm)!;
        return timer.Interval;
    }

    [Fact]
    public void Constructor_ShowSecondsTrue_ScheduledAtOneSecond()
    {
        var vm = Build(showSeconds: true);
        Assert.Equal(TimeSpan.FromSeconds(1), ReadInterval(vm));
    }

    [Fact]
    public void Constructor_ShowSecondsFalse_ScheduledUnderOneMinute()
    {
        var vm = Build(showSeconds: false);
        var interval = ReadInterval(vm);
        // Smart scheduling: never larger than ~60s, never smaller than 200ms.
        Assert.InRange(interval.TotalMilliseconds, 200, 60_050);
    }

    [Fact]
    public void Setter_ShowSecondsFalseToTrue_RestoresOneSecondInterval()
    {
        var vm = Build(showSeconds: false);
        Assert.NotEqual(TimeSpan.FromSeconds(1), ReadInterval(vm));

        vm.ShowSeconds = true;
        Assert.Equal(TimeSpan.FromSeconds(1), ReadInterval(vm));
    }

    [Fact]
    public void Setter_ShowSecondsTrueToFalse_SwitchesToMinuteSchedule()
    {
        var vm = Build(showSeconds: true);
        Assert.Equal(TimeSpan.FromSeconds(1), ReadInterval(vm));

        vm.ShowSeconds = false;
        var interval = ReadInterval(vm);
        Assert.InRange(interval.TotalMilliseconds, 200, 60_050);
    }

    [Fact]
    public void Setter_ShowSecondsUnchanged_DoesNotResetInterval()
    {
        var vm = Build(showSeconds: true);
        vm.ShowSeconds = true; // no change
        Assert.Equal(TimeSpan.FromSeconds(1), ReadInterval(vm));
    }
}
