using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Unit tests for MainViewModel toggle and size-sync logic.
/// Uses fake implementations so no disk I/O, WPF runtime, or NepDate is required.
/// </summary>
public class MainViewModelTests
{
    // ── Fake settings service ─────────────────────────────────────────────────

    private sealed class FakeSettingsService : ISettingsService
    {
        public WidgetSettings Current { get; private set; } = new();
        public bool IsFirstLaunch => false;
        public int SaveCount  { get; private set; }
        public int LoadCount  { get; private set; }

        public void Load()  { LoadCount++;  }
        public void Save()  { SaveCount++;  Current = Current; /* no-op write */ }
        public void ResetToDefaults() { Current = new WidgetSettings(); Save(); }
        public event EventHandler? SettingsChanged;
    }
    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentTheme  { get; private set; } = "Dark";
        public string CurrentPreset { get; private set; } = "Default";
        public int    ApplyCount    { get; private set; }

        public void Apply(string theme, string preset)
        {
            CurrentTheme  = theme;
            CurrentPreset = preset;
            ApplyCount++;
        }
        public void OverrideHighlightColor(string colorHex) { }
    }
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (MainViewModel vm, FakeSettingsService svc, FakeThemeService theme) Create(
        bool startExpanded = false,
        bool autoStartEnabled = false)
    {
        var svc = new FakeSettingsService();
        svc.Current.IsExpanded      = startExpanded;
        svc.Current.ExpandedWidth   = 320;
        svc.Current.ExpandedHeight  = 420;

        var adapter             = new FakeNepaliDateAdapter();
        var calendarService     = new CalendarService(adapter);
        var conversionService   = new ConversionService(adapter);
        var localizationService = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var themeService        = new FakeThemeService();
        var autoStartService    = new FakeAutoStartService(autoStartEnabled);

        var vm = new MainViewModel(svc, calendarService, localizationService, conversionService, themeService, autoStartService);
        return (vm, svc, themeService);
    }

    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_CallsLoad_Once()
    {
        var (_, svc, _) = Create();
        Assert.Equal(1, svc.LoadCount);
    }

    [Fact]
    public void Constructor_StartCollapsed_IsCollapsed()
    {
        var (vm, _, _) = Create(startExpanded: false);
        Assert.False(vm.IsExpanded);
    }

    [Fact]
    public void Constructor_AlwaysStartsCollapsed_EvenIfSettingSaysExpanded()
    {
        var (vm, _, _) = Create(startExpanded: true);
        // Widget always starts collapsed (desktop widget convention)
        Assert.False(vm.IsExpanded);
    }

    // ── ToggleExpandedCommand ─────────────────────────────────────────────────

    [Fact]
    public void ToggleExpanded_FromCollapsed_SetsExpandedTrueAndExpandedSize()
    {
        var (vm, _, _) = Create(startExpanded: false);

        vm.ToggleExpandedCommand.Execute(null);

        Assert.True(vm.IsExpanded);
        Assert.Equal(420, vm.WindowHeight);
    }

    [Fact]
    public void ToggleExpanded_FromExpanded_SetsExpandedFalse()
    {
        var (vm, _, _) = Create(startExpanded: false);
        vm.ToggleExpandedCommand.Execute(null); // expand

        vm.ToggleExpandedCommand.Execute(null); // collapse

        Assert.False(vm.IsExpanded);
    }

    [Fact]
    public void ToggleExpanded_ToggleTwice_ReturnsToPreviousState()
    {
        var (vm, _, _) = Create(startExpanded: false);

        vm.ToggleExpandedCommand.Execute(null);
        vm.ToggleExpandedCommand.Execute(null);

        Assert.False(vm.IsExpanded);
    }

    [Fact]
    public void ToggleExpanded_UpdatesPersistenceSettingsIsExpanded()
    {
        var (vm, svc, _) = Create(startExpanded: false);

        vm.ToggleExpandedCommand.Execute(null);

        Assert.True(svc.Current.IsExpanded);
    }

    // ── UpdatePosition ────────────────────────────────────────────────────────

    [Fact]
    public void UpdatePosition_PersistsToSettings()
    {
        var (vm, svc, _) = Create();

        vm.UpdatePosition(200, 300);

        Assert.Equal(200, svc.Current.WindowLeft);
        Assert.Equal(300, svc.Current.WindowTop);
    }

    // ── UpdateSize ────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateSize_WhenExpanded_PersistsExpandedDimensions()
    {
        // Start collapsed, then expand to get into expanded mode
        var (vm, svc, _) = Create(startExpanded: false);
        vm.ToggleExpandedCommand.Execute(null); // expand

        vm.UpdateSize(360, 480);

        Assert.Equal(360, svc.Current.ExpandedWidth);
        Assert.Equal(480, svc.Current.ExpandedHeight);
    }


    // ── ExitRequested event ───────────────────────────────────────────────────

    [Fact]
    public void ExitCommand_RaisesExitRequestedEvent()
    {
        var (vm, svc, _) = Create();
        bool eventRaised = false;
        vm.ExitRequested += (_, _) => eventRaised = true;

        vm.ExitCommand.Execute(null);

        Assert.True(eventRaised);
    }

    [Fact]
    public void ExitCommand_CallsSaveBeforeRaisingEvent()
    {
        var (vm, svc, _) = Create();
        int saveCountAtEvent = 0;
        vm.ExitRequested += (_, _) => saveCountAtEvent = svc.SaveCount;

        vm.ExitCommand.Execute(null);

        Assert.Equal(1, saveCountAtEvent);
    }

    // ── Language commands (M8) ────────────────────────────────────────────────

    [Fact]
    public void SetLanguageEnCommand_SetsLanguageToEn()
    {
        var (vm, svc, _) = Create();
        vm.SetLanguageNeCommand.Execute(null);   // first switch to NE

        vm.SetLanguageEnCommand.Execute(null);

        Assert.Equal("en", vm.Language);
        Assert.True(vm.IsLanguageEn);
        Assert.False(vm.IsLanguageNe);
    }

    [Fact]
    public void SetLanguageNeCommand_SetsLanguageToNe()
    {
        var (vm, _, _) = Create();

        vm.SetLanguageNeCommand.Execute(null);

        Assert.Equal("ne", vm.Language);
        Assert.False(vm.IsLanguageEn);
        Assert.True(vm.IsLanguageNe);
    }

    [Fact]
    public void Language_WhenSet_PersistsToSettings()
    {
        var (vm, svc, _) = Create();

        vm.Language = "ne";

        Assert.Equal("ne", svc.Current.Language);
    }

    [Fact]
    public void MenuLabels_NonEmpty_InEnglish()
    {
        var (vm, _, _) = Create();

        Assert.NotEmpty(vm.MenuLanguageLabel);
        Assert.NotEmpty(vm.MenuExitLabel);
    }

    [Fact]
    public void MenuLabels_ChangeWhenLanguageSwitched()
    {
        var (vm, _, _) = Create();
        var enExit = vm.MenuExitLabel;

        vm.SetLanguageNeCommand.Execute(null);

        Assert.NotEqual(enExit, vm.MenuExitLabel);
    }

    [Fact]
    public void MenuLanguageEnLabel_IsAlwaysEnglish()
    {
        var (vm, _, _) = Create();
        Assert.Equal("English", vm.MenuLanguageEnLabel);

        vm.SetLanguageNeCommand.Execute(null);
        Assert.Equal("English", vm.MenuLanguageEnLabel); // must not change
    }

    [Fact]
    public void MenuLanguageNeLabel_IsAlwaysNepali()
    {
        var (vm, _, _) = Create();
        Assert.Equal("नेपाली", vm.MenuLanguageNeLabel);
    }

    // ── Theme commands (M9) ───────────────────────────────────────────────────

    [Fact]
    public void SetThemeLightCommand_SetsThemeLight()
    {
        var (vm, svc, ts) = Create();

        vm.SetThemeLightCommand.Execute(null);

        Assert.Equal("Light", vm.Theme);
        Assert.True(vm.IsThemeLight);
        Assert.False(vm.IsThemeDark);
        Assert.Equal("Light", svc.Current.Theme);
        Assert.Equal("Light", ts.CurrentTheme);
    }

    [Fact]
    public void SetThemeDarkCommand_SetsThemeDark()
    {
        var (vm, _, _) = Create();
        vm.SetThemeLightCommand.Execute(null);  // switch to light first

        vm.SetThemeDarkCommand.Execute(null);

        Assert.Equal("Dark", vm.Theme);
        Assert.True(vm.IsThemeDark);
        Assert.False(vm.IsThemeLight);
    }

    [Fact]
    public void SetPresetOceanCommand_SetsPreset()
    {
        var (vm, svc, ts) = Create();

        vm.SetPresetOceanCommand.Execute(null);

        Assert.Equal("Ocean", vm.BackgroundPreset);
        Assert.True(vm.IsPresetOcean);
        Assert.Equal("Ocean", svc.Current.BackgroundPreset);
        Assert.Equal("Ocean", ts.CurrentPreset);
    }

    [Theory]
    [InlineData("Default"), InlineData("Ocean"), InlineData("Forest"),
     InlineData("Sunset"), InlineData("Monochrome")]
    public void BackgroundPreset_EachIsPreset_Exclusive(string preset)
    {
        var (vm, _, _) = Create();
        vm.BackgroundPreset = preset;
        Assert.True(vm.IsPresetDefault    == (preset == "Default"));
        Assert.True(vm.IsPresetOcean      == (preset == "Ocean"));
        Assert.True(vm.IsPresetForest     == (preset == "Forest"));
        Assert.True(vm.IsPresetSunset     == (preset == "Sunset"));
        Assert.True(vm.IsPresetMonochrome == (preset == "Monochrome"));
    }

    [Fact]
    public void SetCornerSharpCommand_SetsCornerSharp()
    {
        var (vm, svc, _) = Create();

        vm.SetCornerSharpCommand.Execute(null);

        Assert.Equal("Sharp", vm.CornerStyle);
        Assert.True(vm.IsCornerSharp);
        Assert.False(vm.IsCornerRounded);
        Assert.Equal(0, vm.CornerRadiusValue);
        Assert.Equal("Sharp", svc.Current.CornerStyle);
    }

    [Fact]
    public void SetCornerRoundedCommand_SetsCornerRounded()
    {
        var (vm, _, _) = Create();
        vm.SetCornerSharpCommand.Execute(null);   // sharp first

        vm.SetCornerRoundedCommand.Execute(null);

        Assert.Equal("Rounded", vm.CornerStyle);
        Assert.True(vm.IsCornerRounded);
        Assert.Equal(8, vm.CornerRadiusValue);
    }

    [Fact]
    public void AnimationEnabled_WhenSet_PersistsToSettings()
    {
        var (vm, svc, _) = Create();

        vm.AnimationEnabled = false;

        Assert.False(svc.Current.AnimationEnabled);
    }

    [Fact]
    public void Constructor_CallsThemeApply_OnStartup()
    {
        var (_, _, ts) = Create();
        Assert.True(ts.ApplyCount >= 1);
    }

    // ── AutoStart (M10) ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_AutoStart_ReflectsServiceInitialState_False()
    {
        var (vm, _, _) = Create(autoStartEnabled: false);
        Assert.False(vm.AutoStart);
    }

    [Fact]
    public void Constructor_AutoStart_ReflectsServiceInitialState_True()
    {
        var (vm, _, _) = Create(autoStartEnabled: true);
        Assert.True(vm.AutoStart);
    }

    [Fact]
    public void AutoStart_WhenSetTrue_PersistsToSettings()
    {
        var (vm, svc, _) = Create();
        vm.AutoStart = true;
        Assert.True(svc.Current.AutoStart);
    }

    [Fact]
    public void AutoStart_WhenSetFalse_PersistsToSettings()
    {
        var (vm, svc, _) = Create(autoStartEnabled: true);
        vm.AutoStart = false;
        Assert.False(svc.Current.AutoStart);
    }

    [Fact]
    public void ToggleExpanded_SavesSettings()
    {
        var (vm, svc, _) = Create(startExpanded: false);
        int savesBefore = svc.SaveCount;

        vm.ToggleExpandedCommand.Execute(null);

        Assert.True(svc.SaveCount > savesBefore);
    }

    [Fact]
    public void SaveSettings_IncreasesSaveCount()
    {
        var (vm, svc, _) = Create();
        int before = svc.SaveCount;

        vm.SaveSettings();

        Assert.Equal(before + 1, svc.SaveCount);
    }

    // ── Preset / corner label localization (M13 fix regression) ──────────────

    [Fact]
    public void MenuPresetLabels_AllNonEmpty_InEnglish()
    {
        var (vm, _, _) = Create();
        Assert.NotEmpty(vm.MenuPresetDefaultLabel);
        Assert.NotEmpty(vm.MenuPresetOceanLabel);
        Assert.NotEmpty(vm.MenuPresetForestLabel);
        Assert.NotEmpty(vm.MenuPresetSunsetLabel);
        Assert.NotEmpty(vm.MenuPresetMonoLabel);
    }

    [Fact]
    public void MenuCornerLabels_AllNonEmpty_InEnglish()
    {
        var (vm, _, _) = Create();
        Assert.NotEmpty(vm.MenuCornerRoundedLabel);
        Assert.NotEmpty(vm.MenuCornerSharpLabel);
    }

    [Fact]
    public void MenuPresetLabels_ChangeWhenLanguageSwitched()
    {
        var (vm, _, _) = Create();
        var enDefault = vm.MenuPresetDefaultLabel;

        vm.SetLanguageNeCommand.Execute(null);

        Assert.NotEqual(enDefault, vm.MenuPresetDefaultLabel);
    }

    // ── Immediate save on key preference changes (M13 fix regression) ─────────

    [Fact]
    public void Language_Change_SavesImmediately()
    {
        var (vm, svc, _) = Create();
        int before = svc.SaveCount;
        vm.Language = "ne";
        Assert.True(svc.SaveCount > before);
    }

    [Fact]
    public void Theme_Change_SavesImmediately()
    {
        var (vm, svc, _) = Create();
        int before = svc.SaveCount;
        vm.SetThemeDarkCommand.Execute(null);
        Assert.True(svc.SaveCount > before);
    }

    [Fact]
    public void BackgroundPreset_Change_SavesImmediately()
    {
        var (vm, svc, _) = Create();
        int before = svc.SaveCount;
        vm.SetPresetOceanCommand.Execute(null);
        Assert.True(svc.SaveCount > before);
    }

    // ── Network VM wiring ─────────────────────────────────────────────────────

    [Fact]
    public void Network_PropertyNotNull_AfterConstruction()
    {
        var (vm, _, _) = Create();
        Assert.NotNull(vm.Network);
    }

    [Fact]
    public void Network_IsNetworkToolsViewModel()
    {
        var (vm, _, _) = Create();
        Assert.IsType<NetworkToolsViewModel>(vm.Network);
    }

    [Fact]
    public void Network_Labels_NonEmpty_InEnglish()
    {
        var (vm, _, _) = Create();
        Assert.NotEmpty(vm.Network.ModeMyIpLabel);
        Assert.NotEmpty(vm.Network.ColIpLabel);
        Assert.NotEmpty(vm.Network.ColStatusLabel);
    }

    [Fact]
    public void Network_Labels_UpdateOnLanguageChange()
    {
        var (vm, _, _) = Create();
        var enLabel = vm.Network.ModeMyIpLabel;

        vm.SetLanguageNeCommand.Execute(null);

        Assert.NotEqual(enLabel, vm.Network.ModeMyIpLabel);
    }

    [Fact]
    public void TabNetworkLabel_NonEmpty_InEnglish()
    {
        var (vm, _, _) = Create();
        Assert.NotEmpty(vm.TabNetworkLabel);
        Assert.NotEqual("[tab.network]", vm.TabNetworkLabel);
    }

    [Fact]
    public void TabNetworkLabel_ChangesOnLanguageSwitch()
    {
        var (vm, _, _) = Create();
        var enLabel = vm.TabNetworkLabel;

        vm.SetLanguageNeCommand.Execute(null);

        Assert.NotEqual(enLabel, vm.TabNetworkLabel);
    }

    // ── Tab index mapping (must be maintained when tabs are reordered) ────────

    [Fact]
    public void OpenSettingsCommand_SetsTabIndexTo8()
    {
        // Settings tab is at index 8 (Home=0, Date=1, Unit=2, Text=3, Bank=4, Network=5, More=6, About=7, Settings=8)
        var (vm, _, _) = Create();
        vm.ToggleExpandedCommand.Execute(null); // expand first

        vm.OpenSettingsCommand.Execute(null);

        Assert.Equal(8, vm.SelectedTabIndex);
    }

    [Fact]
    public void OpenUnitAreaCommand_SetsTabIndexTo2()
    {
        var (vm, _, _) = Create();
        vm.ToggleExpandedCommand.Execute(null);

        vm.OpenUnitAreaCommand.Execute(null);

        Assert.Equal(2, vm.SelectedTabIndex);
    }

    [Fact]
    public void OpenToolsConvertCommand_SetsTabIndexTo1()
    {
        var (vm, _, _) = Create();
        vm.ToggleExpandedCommand.Execute(null);

        vm.OpenToolsConvertCommand.Execute(null);

        Assert.Equal(1, vm.SelectedTabIndex);
    }

    // ── OpenAboutCommand tab index ───────────────────────────────────────────

    [Fact]
    public void OpenAboutCommand_SetsTabIndexTo7()
    {
        var (vm, _, _) = Create();
        vm.ToggleExpandedCommand.Execute(null);

        vm.OpenAboutCommand.Execute(null);

        Assert.Equal(7, vm.SelectedTabIndex);
    }

    // ── Multiple rapid toggles ───────────────────────────────────────────────

    [Fact]
    public void RapidToggle_TenTimes_EndsCollapsed()
    {
        var (vm, _, _) = Create();
        for (int i = 0; i < 10; i++)
            vm.ToggleExpandedCommand.Execute(null);
        Assert.False(vm.IsExpanded);
    }

    [Fact]
    public void RapidToggle_ElevenTimes_EndsExpanded()
    {
        var (vm, _, _) = Create();
        for (int i = 0; i < 11; i++)
            vm.ToggleExpandedCommand.Execute(null);
        Assert.True(vm.IsExpanded);
    }

    // ── Expanded presets: Aurora, Cherry, Midnight, Slate, Ember ─────────────

    [Theory]
    [InlineData("Aurora")]
    [InlineData("Cherry")]
    [InlineData("Midnight")]
    [InlineData("Slate")]
    [InlineData("Ember")]
    public void BackgroundPreset_NewPresets_SetCorrectly(string preset)
    {
        var (vm, svc, ts) = Create();
        vm.BackgroundPreset = preset;
        Assert.Equal(preset, vm.BackgroundPreset);
        Assert.Equal(preset, svc.Current.BackgroundPreset);
        Assert.Equal(preset, ts.CurrentPreset);
    }

    // ── Size persistence after expand/collapse cycle ─────────────────────────

    [Fact]
    public void UpdateSize_SurvivesExpandCollapseCycle()
    {
        var (vm, svc, _) = Create();
        vm.ToggleExpandedCommand.Execute(null); // expand
        vm.UpdateSize(700, 500);
        vm.ToggleExpandedCommand.Execute(null); // collapse
        vm.ToggleExpandedCommand.Execute(null); // expand again
        Assert.Equal(500, vm.WindowHeight);
        Assert.Equal(700, svc.Current.ExpandedWidth);
    }

    // ── PropertyChanged notifications ────────────────────────────────────────

    [Fact]
    public void IsExpanded_RaisesPropertyChanged()
    {
        var (vm, _, _) = Create();
        var raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.IsExpanded)) raised = true; };
        vm.ToggleExpandedCommand.Execute(null);
        Assert.True(raised);
    }

    [Fact]
    public void WindowHeight_HasExpandedValue_AfterExpand()
    {
        var (vm, svc, _) = Create();
        vm.ToggleExpandedCommand.Execute(null);
        Assert.Equal(svc.Current.ExpandedHeight, vm.WindowHeight);
    }

    // ── Calendar sub-VM ──────────────────────────────────────────────────────

    [Fact]
    public void Calendar_NotNull_AfterConstruction()
    {
        var (vm, _, _) = Create();
        Assert.NotNull(vm.Calendar);
    }

    // ── TextTools sub-VM ─────────────────────────────────────────────────────

    [Fact]
    public void TextTools_NotNull_AfterConstruction()
    {
        var (vm, _, _) = Create();
        Assert.NotNull(vm.TextTools);
    }

    // ── Settings sub-VM ──────────────────────────────────────────────────────

    [Fact]
    public void Settings_NotNull_AfterConstruction()
    {
        var (vm, _, _) = Create();
        Assert.NotNull(vm.Settings);
    }

    // ── Banking sub-VM ───────────────────────────────────────────────────────

    [Fact]
    public void Banking_NotNull_AfterConstruction()
    {
        var (vm, _, _) = Create();
        Assert.NotNull(vm.Banking);
    }

    // ── About sub-VM ─────────────────────────────────────────────────────────

    [Fact]
    public void About_NotNull_AfterConstruction()
    {
        var (vm, _, _) = Create();
        Assert.NotNull(vm.About);
    }

    // ── MiniBar sub-VM ───────────────────────────────────────────────────────

    [Fact]
    public void MiniBar_NotNull_AfterConstruction()
    {
        var (vm, _, _) = Create();
        Assert.NotNull(vm.MiniBar);
    }

    // ── Tab labels ───────────────────────────────────────────────────────────

    [Fact]
    public void AllTabLabels_NonEmpty()
    {
        var (vm, _, _) = Create();
        Assert.NotEmpty(vm.TabHomeLabel);
        Assert.NotEmpty(vm.TabDateLabel);
        Assert.NotEmpty(vm.TabTextLabel);
        Assert.NotEmpty(vm.TabBankLabel);
        Assert.NotEmpty(vm.TabNetworkLabel);
    }

    // ── Copy commands exist ──────────────────────────────────────────────────

    [Fact]
    public void CopyCommands_AllExist()
    {
        var (vm, _, _) = Create();
        Assert.NotNull(vm.CopyTodayBsShortCommand);
        Assert.NotNull(vm.CopyTodayBsLongCommand);
        Assert.NotNull(vm.CopyTodayAdShortCommand);
        Assert.NotNull(vm.CopyTodayAdLongCommand);
    }

    // ── In-memory tab persistence across expand/collapse ────────────────────

    [Fact]
    public void ToggleExpanded_RestoresLastTab_OnReexpand()
    {
        var (vm, _, _) = Create();
        vm.ToggleExpandedCommand.Execute(null); // expand
        vm.SelectedTabIndex = 3; // switch to Banking
        vm.ToggleExpandedCommand.Execute(null); // collapse
        vm.ToggleExpandedCommand.Execute(null); // re-expand
        Assert.Equal(3, vm.SelectedTabIndex);
    }

    [Fact]
    public void ToggleExpanded_FirstExpand_OpensLastTabFromSettings()
    {
        var (vm, svc, _) = Create();
        vm.ToggleExpandedCommand.Execute(null); // first expand
        Assert.Equal(svc.Current.LastExpandedTab, vm.SelectedTabIndex);
    }

    [Fact]
    public void ToggleExpanded_MultipleCollapseExpand_RemembersLastTab()
    {
        var (vm, _, _) = Create();
        vm.ToggleExpandedCommand.Execute(null); // expand
        vm.SelectedTabIndex = 5; // Network
        vm.ToggleExpandedCommand.Execute(null); // collapse
        vm.ToggleExpandedCommand.Execute(null); // re-expand
        Assert.Equal(5, vm.SelectedTabIndex);

        vm.SelectedTabIndex = 1; // Date
        vm.ToggleExpandedCommand.Execute(null); // collapse
        vm.ToggleExpandedCommand.Execute(null); // re-expand
        Assert.Equal(1, vm.SelectedTabIndex);
    }
}
