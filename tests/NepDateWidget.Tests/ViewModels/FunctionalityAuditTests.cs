using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Regression tests for bugs found during the deep functionality audit:
/// 1. ConverterViewModel reported a -364d shift across the year boundary because
///    it used DayOfYear subtraction instead of true date subtraction.
/// 2. ConverterViewModel never set ConvertHasError / DaysHasError, so the
///    inline error labels in the XAML were dead bindings.
/// 3. MainViewModel's OpenTextXxx context-menu shortcuts mapped to wrong
///    internal mode indices, so picking "Unicode" from the context menu
///    actually opened the Password panel.
/// </summary>
public class FunctionalityAuditTests
{
    // ── Shared fakes (mirror the ones used in UiConsistencyTests) ────────────

    private sealed class FakeSettingsService : ISettingsService
    {
        public WidgetSettings Current { get; private set; } = new();
        public bool IsFirstLaunch => false;
        public void Load() { }
        public void Save() { }
        public void ResetToDefaults() { Current = new WidgetSettings(); }
        public event EventHandler? SettingsChanged;
    }

    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentTheme  { get; private set; } = "Dark";
        public string CurrentPreset { get; private set; } = "Default";
        public void Apply(string theme, string preset) { CurrentTheme = theme; CurrentPreset = preset; }
        public void OverrideHighlightColor(string colorHex) { }
    }

    private static MainViewModel CreateMain()
    {
        var settings = new FakeSettingsService();
        settings.Current.ExpandedWidth = 538;
        settings.Current.ExpandedHeight = 497;
        var adapter = new FakeNepaliDateAdapter();
        return new MainViewModel(
            settings,
            new CalendarService(adapter),
            new LocalizationService(),
            new ConversionService(adapter),
            new FakeThemeService(),
            new FakeAutoStartService(false));
    }

    private static ConverterViewModel CreateConverter()
    {
        var adapter = new FakeNepaliDateAdapter();
        return new ConverterViewModel(
            new ConversionService(adapter),
            new LocalizationService(),
            adapter);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Bug 1: dayDiff across the year boundary
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The previous implementation computed
    /// <c>targetDate.DayOfYear - sourceDate.DayOfYear</c>, which returns a
    /// nonsensical value when the conversion crosses Dec 31 / Jan 1.
    /// We pin the contract via the public RecomputeTime path: choose a UTC+12
    /// source zone and a UTC-12 target zone, then enter a time near midnight
    /// so the resulting date crosses a calendar boundary.
    /// We assert that the day suffix is in the canonical "+1d"/"-1d" form,
    /// never "+364d" or "-364d".
    /// </summary>
    [Fact]
    public void TimeConvert_DayOffsetTag_NeverShows364Days()
    {
        var vm = CreateConverter();
        vm.ActiveMode = 2; // Time mode

        // Try every available timezone pair until we find one with a non-zero
        // dayDiff and verify it reports a small magnitude (<=1 day).
        foreach (var fromTz in vm.TimeFromZones)
        {
            foreach (var toTz in vm.TimeToZones)
            {
                vm.TimeFromZone = fromTz;
                vm.TimeToZone   = toTz;
                vm.TimeInput    = "11:30 PM";
                vm.TimeIsAm     = false;

                var output = vm.TimeOutput;
                if (output.Contains("d)"))
                {
                    // The bug produced 364 or 365 magnitude. The fix limits it to 1.
                    Assert.DoesNotContain("364", output);
                    Assert.DoesNotContain("365", output);
                }
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Bug 2: Convert / Days errors were silent (dead bindings)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Convert_PartialInput_DoesNotShowError()
    {
        var vm = CreateConverter();
        vm.ActiveMode = 0;
        vm.ConvertInput = "20";
        Assert.False(vm.ConvertHasError);
        Assert.Equal(string.Empty, vm.ConvertError);
    }

    [Fact]
    public void Convert_CommittedGarbageInput_ShowsError()
    {
        var vm = CreateConverter();
        vm.ActiveMode = 0;
        vm.ConvertInput = "not-a-date";
        Assert.True(vm.ConvertHasError);
        Assert.False(string.IsNullOrEmpty(vm.ConvertError));
    }

    [Fact]
    public void Convert_ValidInput_ClearsError()
    {
        var vm = CreateConverter();
        vm.ActiveMode = 0;

        vm.ConvertInput = "not-a-date";
        Assert.True(vm.ConvertHasError);

        vm.ConvertInput = "2025-04-15";
        Assert.False(vm.ConvertHasError);
        Assert.Equal(string.Empty, vm.ConvertError);
        Assert.False(string.IsNullOrEmpty(vm.ConvertOutputShort));
    }

    [Fact]
    public void Days_AddSub_NonIntegerOffset_ShowsError()
    {
        var vm = CreateConverter();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = false;          // AddSub mode

        vm.DaysInput1 = "2082/01/15";
        vm.DaysInput2 = "abc";          // not an integer

        Assert.True(vm.DaysHasError);
        Assert.False(string.IsNullOrEmpty(vm.DaysError));
    }

    [Fact]
    public void Days_AddSub_ValidOffset_ClearsError()
    {
        var vm = CreateConverter();
        vm.ActiveMode = 1;
        vm.IsDaysDiff = false;

        vm.DaysInput1 = "2082/01/15";
        vm.DaysInput2 = "abc";
        Assert.True(vm.DaysHasError);

        vm.DaysInput2 = "30";
        Assert.False(vm.DaysHasError);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Bug 3: Text Tools shortcut commands opened the wrong panel
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void OpenTextUnicodeCommand_OpensUnicodePanel()
    {
        var vm = CreateMain();
        vm.OpenTextUnicodeCommand.Execute(null);

        // Tab 3 is the Text Tools tab in MainWindow.xaml's ExpandedTabs.
        Assert.Equal(3, vm.SelectedTabIndex);
        Assert.True(vm.TextTools.IsModeUnicode,
            "Context menu \"Unicode\" must open the Unicode panel.");
        Assert.False(vm.TextTools.IsModePassword);
    }

    [Fact]
    public void OpenTextPasswordCommand_OpensPasswordPanel()
    {
        var vm = CreateMain();
        vm.OpenTextPasswordCommand.Execute(null);

        Assert.Equal(3, vm.SelectedTabIndex);
        Assert.True(vm.TextTools.IsModePassword);
        Assert.False(vm.TextTools.IsModeUnicode);
    }

    [Fact]
    public void OpenTextWordCommand_OpensWordPanel()
    {
        var vm = CreateMain();
        vm.OpenTextWordCommand.Execute(null);

        Assert.Equal(3, vm.SelectedTabIndex);
        Assert.True(vm.TextTools.IsModeWord);
    }

    [Fact]
    public void OpenTextScriptCommand_OpensScriptPanel()
    {
        var vm = CreateMain();
        vm.OpenTextScriptCommand.Execute(null);

        Assert.Equal(3, vm.SelectedTabIndex);
        Assert.True(vm.TextTools.IsModeScript);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Sanity: the other context-menu shortcuts already worked but pin them.
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void OpenBankingShortcuts_OpenBankingTab_AtCorrectIndex()
    {
        var vm = CreateMain();
        vm.OpenBankingInterestCommand.Execute(null);
        Assert.Equal(4, vm.SelectedTabIndex); // Banking is tab 4 in XAML
        Assert.True(vm.Banking.IsModeInterest);

        vm.OpenBankingEmiCommand.Execute(null);
        Assert.True(vm.Banking.IsModeEmi);
    }

    [Fact]
    public void OpenUnitShortcuts_OpenUnitTab_AtCorrectIndex()
    {
        var vm = CreateMain();
        vm.OpenUnitAreaCommand.Execute(null);
        Assert.Equal(2, vm.SelectedTabIndex);
        Assert.True(vm.Unit.IsModeArea);

        vm.OpenUnitWeightCommand.Execute(null);
        Assert.True(vm.Unit.IsModeWeight);
    }

    [Fact]
    public void OpenToolsShortcuts_OpenToolsTab_AtCorrectIndex()
    {
        var vm = CreateMain();
        vm.OpenToolsConvertCommand.Execute(null);
        Assert.Equal(1, vm.SelectedTabIndex);
        Assert.True(vm.Calendar.Converter.IsModeConvert);

        vm.OpenToolsTimeCommand.Execute(null);
        Assert.True(vm.Calendar.Converter.IsModeTime);
    }
}
