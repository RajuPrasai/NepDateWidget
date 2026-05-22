using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// UI/UX consistency tests.
/// Verifies that ViewModel contracts required for consistent UI behaviour are met:
/// commands exist and are executable, toggle semantics are correct, IsBusy resets
/// after operations, labels are non-empty, and navigation properties stay coherent.
/// These tests act as a regression guard against regressions that cause disabled
/// buttons, stale labels, or broken toggle state.
/// </summary>
public class UiConsistencyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MainViewModel CreateMain()
    {
        var svc = new FakeSettingsService();
        svc.Current.ExpandedWidth   = 538;
        svc.Current.ExpandedHeight  = 497.33333;

        var adapter             = new FakeNepaliDateAdapter();
        var calendarService     = new CalendarService(adapter);
        var conversionService   = new ConversionService(adapter);
        var localizationService = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var themeService        = new FakeThemeService();
        var autoStartService    = new FakeAutoStartService(false);

        return new MainViewModel(svc, calendarService, localizationService,
                                 conversionService, themeService, autoStartService);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public WidgetSettings Current { get; private set; } = new();
        public bool IsFirstLaunch => false;
        public void Load()  { }
        public void Save()  { }
        public void ResetToDefaults() { Current = new WidgetSettings(); }
    }

    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentTheme  { get; private set; } = "Dark";
        public string CurrentPreset { get; private set; } = "Default";
        public void Apply(string theme, string preset)
        {
            CurrentTheme  = theme;
            CurrentPreset = preset;
        }
        public void OverrideHighlightColor(string colorHex) { }
    }

    // ── NetworkToolsViewModel: IsBusy + button state ─────────────────────────

    [Fact]
    public void Network_IsBusy_StartsAsFalse()
    {
        var vm = new NetworkToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void Network_IsNotBusy_StartsAsTrue()
    {
        var vm = new NetworkToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        Assert.True(vm.IsNotBusy);
    }

    [Fact]
    public void Network_FetchMyIpCommand_CanExecute_WhenNotBusy()
    {
        var vm = new NetworkToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        Assert.True(vm.FetchMyIpCommand.CanExecute(null));
    }

    [Fact]
    public void Network_PingCommand_CannotExecute_WhenHostIsEmpty()
    {
        var vm = new NetworkToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        vm.PingHost = string.Empty;
        Assert.False(vm.PingCommand.CanExecute(null));
    }

    [Fact]
    public void Network_PingCommand_CanExecute_WhenHostProvided()
    {
        var vm = new NetworkToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        vm.PingHost = "8.8.8.8";
        Assert.True(vm.PingCommand.CanExecute(null));
    }

    // ── Mode selectors stay consistent ────────────────────────────────────────

    [Fact]
    public void Network_ModeSelectors_OneActiveAtATime()
    {
        var vm = new NetworkToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        vm.ActiveMode = 2; // Scan

        int active = new[]
        {
            vm.IsModeMyIp, vm.IsModePing, vm.IsModeScan,
            vm.IsModeTrace, vm.IsModeWhois, vm.IsModeDns
        }.Count(b => b);

        Assert.Equal(1, active);
    }

    [Fact]
    public void Text_ModeSelectors_OneActiveAtATime()
    {
        var vm = new TextToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        vm.ActiveMode = 1; // Word

        int active = new[]
        {
            vm.IsModeUnicode, vm.IsModeWord, vm.IsModePassword, vm.IsModeScript
        }.Count(b => b);

        Assert.Equal(1, active);
    }

    // ── Labels non-empty ─────────────────────────────────────────────────────

    [Fact]
    public void Network_AllModeLabels_NonEmpty()
    {
        var vm = new NetworkToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        Assert.NotEmpty(vm.ModeMyIpLabel);
        Assert.NotEmpty(vm.ModePingLabel);
        Assert.NotEmpty(vm.ModeScanLabel);
        Assert.NotEmpty(vm.ModeTraceLabel);
        Assert.NotEmpty(vm.ModeWhoisLabel);
        Assert.NotEmpty(vm.ModeDnsLabel);
    }

    [Fact]
    public void Text_AllModeLabels_NonEmpty()
    {
        var vm = new TextToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        Assert.NotEmpty(vm.ModeUnicodeLabel);
        Assert.NotEmpty(vm.ModeWordLabel);
        Assert.NotEmpty(vm.ModePasswordLabel);
        Assert.NotEmpty(vm.ModeScriptLabel);
    }

    // ── Minimum expanded size contract ────────────────────────────────────────

    [Fact]
    public void MainViewModel_ExpandedHeight_AtLeast497()
    {
        var vm = CreateMain();
        vm.ToggleExpandedCommand.Execute(null); // expand
        Assert.True(vm.WindowHeight >= 497,
            $"Expanded height {vm.WindowHeight} is below the 497 minimum.");
    }

    [Fact]
    public void MainViewModel_ExpandedWidth_AtLeast538()
    {
        var vm = CreateMain();
        vm.ToggleExpandedCommand.Execute(null); // expand
        Assert.True(vm.WindowWidth >= 538,
            $"Expanded width {vm.WindowWidth} is below the 538 minimum.");
    }

    // ── CalendarViewModel: navigation button commands exist ───────────────────

    [Fact]
    public void Calendar_NavCommands_AllExist()
    {
        var adapter  = new FakeNepaliDateAdapter();
        var cal      = new CalendarService(adapter);
        var loc      = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv     = new ConversionService(adapter);
        var vm       = new CalendarViewModel(cal, loc, conv,
                                              true, true, selectedTimezoneId: null!);

        Assert.NotNull(vm.PrevMonthCommand);
        Assert.NotNull(vm.NextMonthCommand);
        Assert.NotNull(vm.GoTodayCommand);
    }

    // ── TextToolsViewModel: direction action commands exist ───────────────────

    [Fact]
    public void Text_DirectionCommands_AllExist()
    {
        var vm = new TextToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        Assert.NotNull(vm.PreetiToUnicodeCommand);
        Assert.NotNull(vm.UnicodeToPreetiCommand);
        Assert.NotNull(vm.ScriptRomanToDevaCommand);
        Assert.NotNull(vm.ScriptDevaToRomanCommand);
    }

    // ── AutoCollapse persists across toggle cycles ────────────────────────────

    // ── App always starts collapsed ──────────────────────────────────────────

    [Fact]
    public void MainViewModel_AlwaysStartsCollapsed()
    {
        var vm = CreateMain();
        Assert.False(vm.IsExpanded);
    }

    // ── IsCollapsedTransparent semantics ──────────────────────────────────────

    [Fact]
    public void IsCollapsedTransparent_TrueWhenExpandedAndTransparencyOn()
    {
        var vm = CreateMain();
        vm.TransparentWhenCollapsed = true;
        vm.ToggleExpandedCommand.Execute(null); // expand
        Assert.True(vm.IsCollapsedTransparent);
    }

    [Fact]
    public void IsCollapsedTransparent_FalseWhenTransparencyOff()
    {
        var vm = CreateMain();
        vm.TransparentWhenCollapsed = false;
        Assert.False(vm.IsCollapsedTransparent);
    }

    [Fact]
    public void IsCollapsedTransparent_TrueWhenCollapsedAndTransparencyOn()
    {
        var vm = CreateMain();
        vm.TransparentWhenCollapsed = true;
        // starts collapsed, so IsCollapsedTransparent should be true
        Assert.True(vm.IsCollapsedTransparent);
    }

    // ── Year dropdown: 101 items (2000 to 2100 inclusive) ─────────────────────

    [Fact]
    public void Calendar_YearNames_Has101Items()
    {
        var vm = CreateCalendar();
        Assert.Equal(101, vm.YearNames.Count);
    }

    [Fact]
    public void Calendar_YearNames_FirstIs2000_LastIs2100_InEnglish()
    {
        var vm = CreateCalendar();
        Assert.Equal("2000", vm.YearNames[0]);
        Assert.Equal("2100", vm.YearNames[^1]);
    }

    [Fact]
    public void Calendar_YearNames_NepaliMode_UsesNepaliDigits()
    {
        var adapter = new FakeNepaliDateAdapter();
        var cal     = new CalendarService(adapter);
        var loc     = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv    = new ConversionService(adapter);
        loc.SetLanguage("ne");
        var vm = new CalendarViewModel(cal, loc, conv);
        vm.OnLanguageChanged();

        // Nepali digit for "2" is "२", so first year starts with "२"
        Assert.StartsWith("२", vm.YearNames[0]);
        Assert.DoesNotContain("2000", vm.YearNames);
    }

    [Fact]
    public void Calendar_SelectedYearIndex_MapsToDisplayYear()
    {
        var vm = CreateCalendar();
        // DisplayYear is 2082 (from FakeNepaliDateAdapter), so index = 2082 - 2000 = 82
        Assert.Equal(82, vm.SelectedYearIndex);
    }

    [Fact]
    public void Calendar_SelectedYearIndex_NegativeOne_WhenOutOfRange()
    {
        // Navigate to a year at the boundary to ensure index is valid
        var adapter = new FakeNepaliDateAdapter { TodayBsYear = 1901, TodayBsMonth = 1 };
        var cal     = new CalendarService(adapter);
        var loc     = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv    = new ConversionService(adapter);
        var vm      = new CalendarViewModel(cal, loc, conv);
        // 1901 is outside 2000-2100, so SelectedYearIndex should be -1
        Assert.Equal(-1, vm.SelectedYearIndex);
    }

    // ── Month dropdown: exactly 12 items ──────────────────────────────────────

    [Fact]
    public void Calendar_NepaliMonthNames_Has12Items()
    {
        var vm = CreateCalendar();
        Assert.Equal(12, vm.NepaliMonthNames.Count);
    }

    [Fact]
    public void Calendar_NepaliMonthNames_AllNonEmpty()
    {
        var vm = CreateCalendar();
        Assert.All(vm.NepaliMonthNames, n => Assert.NotEmpty(n));
    }

    [Fact]
    public void Calendar_SelectedMonthIndex_IsZeroBased()
    {
        var vm = CreateCalendar();
        // DisplayMonth is 12 (Chaitra), so SelectedMonthIndex = 11
        Assert.Equal(11, vm.SelectedMonthIndex);
    }

    // ── Calendar grid invariants ──────────────────────────────────────────────

    [Fact]
    public void Calendar_Days_AlwaysMultipleOf7()
    {
        var vm = CreateCalendar();
        Assert.Equal(0, vm.Days.Count % 7);
    }

    [Fact]
    public void Calendar_Days_ExactlyOneTodayCell()
    {
        var vm = CreateCalendar();
        var todayCells = vm.Days.Where(d => d.IsCurrentMonth && d.IsToday).ToList();
        Assert.Single(todayCells);
    }

    [Fact]
    public void Calendar_DayOfWeekHeaders_Always7()
    {
        var vm = CreateCalendar();
        Assert.Equal(7, vm.DayOfWeekHeaders.Count);
        Assert.All(vm.DayOfWeekHeaders, h => Assert.NotEmpty(h.Label));
    }

    // ── First expand restores last tab (index 8 by default) ──────────────────

    [Fact]
    public void MainViewModel_FirstExpand_OpensLastTab()
    {
        var vm = CreateMain();
        vm.ToggleExpandedCommand.Execute(null); // expand
        Assert.Equal(8, vm.SelectedTabIndex);
    }

    // ── Tab label properties all non-empty ────────────────────────────────────

    [Fact]
    public void TabLabels_AllNonEmpty_InEnglish()
    {
        var vm = CreateMain();
        Assert.NotEmpty(vm.TabHomeLabel);
        Assert.NotEmpty(vm.TabDateLabel);
        Assert.NotEmpty(vm.TabSettingsLabel);
        Assert.NotEmpty(vm.TabUnitLabel);
        Assert.NotEmpty(vm.TabBankLabel);
        Assert.NotEmpty(vm.TabNetworkLabel);
        Assert.NotEmpty(vm.TabTextLabel);
        Assert.NotEmpty(vm.TabAboutLabel);
        Assert.NotEmpty(vm.TabMoreLabel);
    }

    [Fact]
    public void TabLabels_AllNonEmpty_InNepali()
    {
        var vm = CreateMain();
        vm.Language = "ne";
        Assert.NotEmpty(vm.TabHomeLabel);
        Assert.NotEmpty(vm.TabDateLabel);
        Assert.NotEmpty(vm.TabSettingsLabel);
    }

    // ── Context menu labels all non-empty ─────────────────────────────────────

    [Fact]
    public void ContextMenuLabels_AllNonEmpty_InEnglish()
    {
        var vm = CreateMain();
        Assert.NotEmpty(vm.MenuLanguageLabel);
        Assert.NotEmpty(vm.MenuShowClockLabel);
        Assert.NotEmpty(vm.MenuThemeLabel);
        Assert.NotEmpty(vm.MenuThemeDarkLabel);
        Assert.NotEmpty(vm.MenuThemeLightLabel);
        Assert.NotEmpty(vm.MenuBackgroundLabel);
        Assert.NotEmpty(vm.MenuCornerLabel);
        Assert.NotEmpty(vm.MenuCornerRoundedLabel);
        Assert.NotEmpty(vm.MenuCornerSharpLabel);
        Assert.NotEmpty(vm.MenuAnimationLabel);
        Assert.NotEmpty(vm.MenuExitLabel);
        Assert.NotEmpty(vm.MenuSettingsLabel);
    }

    [Fact]
    public void TooltipLabels_AllNonEmpty()
    {
        var vm = CreateMain();
        Assert.NotEmpty(vm.TooltipAbout);
        Assert.NotEmpty(vm.TooltipMinimize);
        Assert.NotEmpty(vm.TooltipSettings);
    }

    // ── CornerRadius mapping ──────────────────────────────────────────────────

    [Fact]
    public void CornerRadiusValue_Is8_WhenRounded()
    {
        var vm = CreateMain();
        vm.CornerStyle = "Rounded";
        Assert.Equal(8, vm.CornerRadiusValue);
    }

    [Fact]
    public void CornerRadiusValue_Is0_WhenSharp()
    {
        var vm = CreateMain();
        vm.CornerStyle = "Sharp";
        Assert.Equal(0, vm.CornerRadiusValue);
    }

    // ── Language boolean helpers are exclusive ─────────────────────────────────

    [Fact]
    public void LanguageBooleans_ExactlyOneTrue_InEn()
    {
        var vm = CreateMain();
        vm.Language = "en";
        Assert.True(vm.IsLanguageEn);
        Assert.False(vm.IsLanguageNe);
    }

    [Fact]
    public void LanguageBooleans_ExactlyOneTrue_InNe()
    {
        var vm = CreateMain();
        vm.Language = "ne";
        Assert.False(vm.IsLanguageEn);
        Assert.True(vm.IsLanguageNe);
    }

    // ── Converter mode selector: exactly one active ───────────────────────────

    [Fact]
    public void Converter_ModeSelectors_OneActiveAtATime()
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc     = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv    = new ConversionService(adapter);
        var vm      = new ConverterViewModel(conv, loc, adapter: adapter);
        vm.ActiveMode = 2; // Time

        int active = new[] { vm.IsModeConvert, vm.IsModeDays, vm.IsModeTime }
            .Count(b => b);
        Assert.Equal(1, active);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Converter_EachMode_HasExactlyOneActive(int mode)
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc     = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv    = new ConversionService(adapter);
        var vm      = new ConverterViewModel(conv, loc, adapter: adapter);
        vm.ActiveMode = mode;

        int active = new[] { vm.IsModeConvert, vm.IsModeDays, vm.IsModeTime }
            .Count(b => b);
        Assert.Equal(1, active);
    }

    // ── Converter direction: exactly one active ───────────────────────────────

    [Fact]
    public void Converter_DirectionBooleans_ExclusiveForAdToBs()
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc     = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv    = new ConversionService(adapter);
        var vm      = new ConverterViewModel(conv, loc, adapter: adapter);
        vm.IsAdToBs = true;

        Assert.True(vm.IsAdToBs);
        Assert.False(vm.IsBsToAd);
    }

    [Fact]
    public void Converter_DirectionBooleans_ExclusiveForBsToAd()
    {
        var adapter = new FakeNepaliDateAdapter();
        var loc     = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv    = new ConversionService(adapter);
        var vm      = new ConverterViewModel(conv, loc, adapter: adapter);

        Assert.False(vm.IsAdToBs);
        Assert.True(vm.IsBsToAd);
    }

    // ── OnMouseWheel only works when expanded ─────────────────────────────────

    [Fact]
    public void OnMouseWheel_WhenCollapsed_DoesNotNavigate()
    {
        var vm = CreateMain();
        int monthBefore = vm.Calendar.DisplayMonth;

        vm.OnMouseWheel(120); // scroll up

        Assert.Equal(monthBefore, vm.Calendar.DisplayMonth);
    }

    [Fact]
    public void OnMouseWheel_WhenExpanded_NavigatesCalendar()
    {
        var vm = CreateMain();
        vm.ToggleExpandedCommand.Execute(null); // expand
        int monthBefore = vm.Calendar.DisplayMonth;

        vm.OnMouseWheel(120); // scroll up = prev month

        Assert.NotEqual(monthBefore, vm.Calendar.DisplayMonth);
    }

    // ── WidgetSettings defaults are sensible ──────────────────────────────────

    [Fact]
    public void WidgetSettings_Defaults_AnimationEnabled_True()
    {
        var s = new WidgetSettings();
        Assert.True(s.AnimationEnabled);
    }

    [Fact]
    public void WidgetSettings_Defaults_CornerStyle_Rounded()
    {
        var s = new WidgetSettings();
        Assert.Equal("Rounded", s.CornerStyle);
    }

    [Fact]
    public void WidgetSettings_Defaults_IsExpanded_False()
    {
        var s = new WidgetSettings();
        Assert.False(s.IsExpanded);
    }

    [Fact]
    public void WidgetSettings_Defaults_ShowEnglishDayNumbers_True()
    {
        var s = new WidgetSettings();
        Assert.True(s.ShowEnglishDayNumbers);
    }

    [Fact]
    public void WidgetSettings_Defaults_HighlightSaturdays_True()
    {
        var s = new WidgetSettings();
        Assert.True(s.HighlightSaturdays);
    }

    [Fact]
    public void WidgetSettings_Defaults_ShowDayOfWeek_True()
    {
        var s = new WidgetSettings();
        Assert.True(s.ShowDayOfWeek);
    }

    [Fact]
    public void WidgetSettings_Defaults_ShowEnglishDate_True()
    {
        var s = new WidgetSettings();
        Assert.True(s.ShowEnglishDate);
    }

    [Fact]
    public void WidgetSettings_Defaults_LastExpandedTab_Is8()
    {
        var s = new WidgetSettings();
        Assert.Equal(8, s.LastExpandedTab);
    }

    [Fact]
    public void WidgetSettings_Defaults_ShowHelpBadges_True()
    {
        var s = new WidgetSettings();
        Assert.True(s.ShowHelpBadges);
    }

    // ── SettingsValidator clamps expanded dimensions ──────────────────────────

    [Fact]
    public void SettingsValidator_ClampsExpandedWidth_MinIs560()
    {
        var s = new WidgetSettings { ExpandedWidth = 100 };
        SettingsValidator.Validate(s);
        Assert.True(s.ExpandedWidth >= 560,
            $"ExpandedWidth {s.ExpandedWidth} should be at least 560 after validation.");
    }

    [Fact]
    public void SettingsValidator_ClampsExpandedHeight_MinIs497()
    {
        var s = new WidgetSettings { ExpandedHeight = 100 };
        SettingsValidator.Validate(s);
        Assert.True(s.ExpandedHeight >= 497,
            $"ExpandedHeight {s.ExpandedHeight} should be at least 497 after validation.");
    }

    [Fact]
    public void SettingsValidator_LastExpandedTab_ClampedToValidRange()
    {
        var s = new WidgetSettings { LastExpandedTab = 99 };
        SettingsValidator.Validate(s);
        Assert.InRange(s.LastExpandedTab, 0, 8);
    }

    [Fact]
    public void SettingsValidator_InvalidCornerStyle_DefaultsToRounded()
    {
        var s = new WidgetSettings { CornerStyle = "banana" };
        SettingsValidator.Validate(s);
        Assert.Equal("Rounded", s.CornerStyle);
    }

    [Fact]
    public void SettingsValidator_InvalidLanguage_DefaultsToEn()
    {
        var s = new WidgetSettings { Language = "fr" };
        SettingsValidator.Validate(s);
        Assert.Equal("en", s.Language);
    }

    // ── Tab index mapping contracts ───────────────────────────────────────────

    [Fact]
    public void OpenSettingsCommand_ExpandsIfCollapsed_ThenGoesToTab8()
    {
        var vm = CreateMain();
        Assert.False(vm.IsExpanded);

        vm.OpenSettingsCommand.Execute(null);

        Assert.True(vm.IsExpanded);
        Assert.Equal(8, vm.SelectedTabIndex);
    }

    [Fact]
    public void OpenAboutCommand_ExpandsIfCollapsed_ThenGoesToTab7()
    {
        var vm = CreateMain();

        vm.OpenAboutCommand.Execute(null);

        Assert.True(vm.IsExpanded);
        Assert.Equal(7, vm.SelectedTabIndex);
    }

    [Fact]
    public void OpenMoreCommand_ExpandsIfCollapsed_ThenGoesToTab6()
    {
        var vm = CreateMain();

        vm.OpenMoreCommand.Execute(null);

        Assert.True(vm.IsExpanded);
        Assert.Equal(6, vm.SelectedTabIndex);
    }

    // ── Expand remembers last tab ─────────────────────────────────────────────

    [Fact]
    public void ExpandCollapse_RestoresLastTab_OnSecondExpand()
    {
        var vm = CreateMain();
        vm.ToggleExpandedCommand.Execute(null); // expand (tab = 0, the default)
        vm.SelectedTabIndex = 3; // switch to Text Tools
        vm.ToggleExpandedCommand.Execute(null); // collapse
        vm.ToggleExpandedCommand.Execute(null); // expand again

        Assert.Equal(3, vm.SelectedTabIndex);
    }

    // ── MiniBar sub-VM has non-empty text ─────────────────────────────────────

    [Fact]
    public void MiniBar_Line2Text_NonEmpty()
    {
        var vm = CreateMain();
        Assert.NotEmpty(vm.MiniBar.Line2Text);
    }

    // ── Network mode count ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Network_EachMode_ExactlyOneActive(int mode)
    {
        var vm = new NetworkToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        vm.ActiveMode = mode;

        int active = new[]
        {
            vm.IsModeMyIp, vm.IsModePing, vm.IsModeScan,
            vm.IsModeTrace, vm.IsModeWhois, vm.IsModeDns
        }.Count(b => b);

        Assert.Equal(1, active);
    }

    // ── Text mode count ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Text_EachMode_ExactlyOneActive(int mode)
    {
        var vm = new TextToolsViewModel(new LocalizationService(TestPaths.DefaultLocalizationPath));
        vm.ActiveMode = mode;

        int active = new[]
        {
            vm.IsModeUnicode, vm.IsModeWord, vm.IsModePassword, vm.IsModeScript
        }.Count(b => b);

        Assert.Equal(1, active);
    }

    // ── CalendarViewModel helper ──────────────────────────────────────────────

    private static CalendarViewModel CreateCalendar(string language = "en")
    {
        var adapter = new FakeNepaliDateAdapter();
        var cal     = new CalendarService(adapter);
        var loc     = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var conv    = new ConversionService(adapter);
        loc.SetLanguage(language);
        return new CalendarViewModel(cal, loc, conv);
    }
}
