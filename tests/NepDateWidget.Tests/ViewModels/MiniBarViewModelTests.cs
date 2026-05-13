using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Unit tests for MiniBarViewModel.
/// DispatcherTimer is created but does not tick (no Dispatcher loop in tests) -
/// tests call Refresh() directly to exercise the display logic.
/// </summary>
public class MiniBarViewModelTests : IDisposable
{
    private readonly FakeNepaliDateAdapter   _adapter = new();
    private readonly CalendarService         _calendar;
    private readonly LocalizationService     _loc     = new(TestPaths.DefaultLocalizationPath);
    private          MiniBarViewModel?       _vm;

    public MiniBarViewModelTests()
    {
        _calendar = new CalendarService(_adapter);
    }

    private MiniBarViewModel Create(
        bool showTimezone = true,
        string selectedTimezoneId = "",
        bool showOffset = false,
        bool showDayOfWeek = true,
        bool showEnglishDate = true)
    {
        _vm = new MiniBarViewModel(_calendar, _loc,
            showTimezone, selectedTimezoneId, showOffset,
            showDayOfWeek, showEnglishDate);
        return _vm;
    }

    public void Dispose() => _vm?.Dispose();

    // ── Line 2: Date strings ──────────────────────────────────────────────────

    [Fact]
    public void Line2Text_ShowsBothDates_ByDefault()
    {
        var vm = Create();
        Assert.NotEmpty(vm.Line2Text);
        Assert.Contains("|", vm.Line2Text);
    }

    [Fact]
    public void Line2Text_ShowsOnlyNepaliDate_WhenEnglishOff()
    {
        // When English date and day-of-week are both off, Line2 has only the Nepali date
        var vm = Create(showEnglishDate: false, showDayOfWeek: false);
        Assert.NotEmpty(vm.Line2Text);
        Assert.DoesNotContain("|", vm.Line2Text);
    }

    [Fact]
    public void Line2Text_ShowsOnlyNepaliDate_WhenEnglishOff_StillNotEmpty()
    {
        var vm = Create(showEnglishDate: false);
        Assert.NotEmpty(vm.Line2Text);
    }

    [Fact]
    public void Line2Text_InNepali_ContainsUnicode()
    {
        _loc.SetLanguage("ne");
        var vm = Create();
        vm.OnLanguageChanged();
        Assert.Contains("चैत", vm.Line2Text, StringComparison.Ordinal);
    }

    // ── Line 1: Time / offset / day-of-week ──────────────────────────────────

    [Fact]
    public void Line1Text_ShowsTime_WhenShowTimezoneTrue()
    {
        var vm = Create(showTimezone: true, showDayOfWeek: false);
        // Should contain AM or PM (time format)
        Assert.True(vm.Line1Text.Contains("AM") || vm.Line1Text.Contains("PM"));
    }

    [Fact]
    public void Line1Text_IsEmpty_WhenAllLine1ElementsOff()
    {
        // All elements that can populate Line1 must be off
        var vm = Create(showTimezone: false, showOffset: false, showDayOfWeek: false, showEnglishDate: false);
        Assert.Empty(vm.Line1Text);
    }

    [Fact]
    public void Line1Text_ShowsDayOfWeek_WhenTimezoneOnAndDayEnabled()
    {
        _loc.SetLanguage("en");
        var vm = Create(showTimezone: true, showDayOfWeek: true);
        // Should contain a day name like Monday, Tuesday, etc.
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        Assert.Contains(dayNames, d => vm.Line1Text.Contains(d));
    }

    [Fact]
    public void Line1Text_ContainsDayOfWeek_WhenTimezoneOff()
    {
        _loc.SetLanguage("en");
        var vm = Create(showTimezone: false, showDayOfWeek: true, showEnglishDate: false);
        // When timezone is off, day-of-week populates Line1 (single top line above the date)
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        Assert.Contains(dayNames, d => vm.Line1Text.Contains(d));
        Assert.True(vm.HasLine1);
    }

    [Fact]
    public void Line1Text_ShowsOffset_WhenTimezoneOnAndOffsetEnabled()
    {
        // Inject a fixed positive-offset timezone so the assertion is not
        // environment-dependent. "Nepal Standard Time" is +05:45 on all Windows machines,
        // including the UTC-based GitHub Actions runner.
        var vm = Create(showTimezone: true, showOffset: true, showDayOfWeek: false,
            selectedTimezoneId: "Nepal Standard Time");
        Assert.Contains("+", vm.Line1Text, StringComparison.Ordinal);
    }

    // ── Toggle behavior ───────────────────────────────────────────────────────

    [Fact]

    public void ShowTimezone_Toggle_UpdatesLine1()
    {
        // Start with everything off so Line1 is empty
        var vm = Create(showTimezone: false, showDayOfWeek: false, showOffset: false, showEnglishDate: false);
        Assert.Empty(vm.Line1Text);

        vm.ShowTimezone = true;
        Assert.NotEmpty(vm.Line1Text);
    }

    // ── Labels / localization ─────────────────────────────────────────────────

    [Fact]
    public void ExpandHint_IsNonEmpty()
    {
        var vm = Create();
        Assert.NotEmpty(vm.ExpandHint);
    }

    [Fact]
    public void ExpandHint_ChangesWithLanguage()
    {
        _loc.SetLanguage("en");
        var vm = Create();
        var enHint = vm.ExpandHint;

        _loc.SetLanguage("ne");
        vm.OnLanguageChanged();

        Assert.NotEqual(enHint, vm.ExpandHint);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public void Refresh_DoesNotThrow()
    {
        var vm  = Create();
        var ex  = Record.Exception(() => vm.Refresh());
        Assert.Null(ex);
    }

    // ── Midnight rollover detection ──────────────────────────────────────────

    [Fact]
    public void Refresh_WhenDateHasRolledOver_UpdatesLine2()
    {
        var vm = Create();
        Assert.Contains("20", vm.Line2Text);

        // Simulate the adapter returning a new day
        _adapter.TodayBsDay = 21;

        // Force _lastRefreshedDate to yesterday so next Refresh believes
        // the date has changed
        var field = typeof(MiniBarViewModel)
            .GetField("_lastRefreshedDate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(vm, DateTime.Now.Date.AddDays(-1));

        vm.Refresh();
        Assert.Contains("21", vm.Line2Text);
    }
}
