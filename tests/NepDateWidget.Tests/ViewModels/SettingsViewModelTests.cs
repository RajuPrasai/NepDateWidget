using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

public class SettingsViewModelTests
{
    private sealed class FakeSettingsService : ISettingsService
    {
        public WidgetSettings Current { get; private set; } = new();
        public bool IsFirstLaunch => false;
        public int SaveCount { get; private set; }
        public void Load() { }
        public void Save() { SaveCount++; }
        public void ResetToDefaults() { Current = new WidgetSettings(); }
    }

    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentTheme { get; private set; } = "Dark";
        public string CurrentPreset { get; private set; } = "Default";
        public int ApplyCount { get; private set; }
        public void Apply(string theme, string preset)
        {
            CurrentTheme = theme;
            CurrentPreset = preset;
            ApplyCount++;
        }
        public void OverrideHighlightColor(string colorHex) { }
    }

    private static (SettingsViewModel vm, FakeSettingsService svc, FakeThemeService theme, FakeAutoStartService auto) Create()
    {
        var svc = new FakeSettingsService();
        var loc = new LocalizationService();
        var theme = new FakeThemeService();
        var auto = new FakeAutoStartService();
        var vm = new SettingsViewModel(svc, loc, theme, auto);
        return (vm, svc, theme, auto);
    }

    // ── Construction defaults ────────────────────────────────────────────────

    [Fact]
    public void Constructor_LoadsFromSettings()
    {
        var (vm, _, _, _) = Create();
        Assert.Equal("en", vm.Language);
        Assert.Equal("Light", vm.Theme);
        Assert.Equal("Forest", vm.BackgroundPreset);
        Assert.Equal("Rounded", vm.CornerStyle);
        Assert.True(vm.AlwaysOnTop);
        Assert.True(vm.AnimationEnabled);
        Assert.True(vm.TransparentWhenCollapsed);
    }

    // ── Language ─────────────────────────────────────────────────────────────

    [Fact]
    public void SetLanguageEnCommand_SetsLanguageEn()
    {
        var (vm, _, _, _) = Create();
        vm.SetLanguageNeCommand.Execute(null);
        vm.SetLanguageEnCommand.Execute(null);
        Assert.Equal("en", vm.Language);
        Assert.True(vm.IsLanguageEn);
        Assert.False(vm.IsLanguageNe);
    }

    [Fact]
    public void SetLanguageNeCommand_SetsLanguageNe()
    {
        var (vm, _, _, _) = Create();
        vm.SetLanguageNeCommand.Execute(null);
        Assert.Equal("ne", vm.Language);
        Assert.True(vm.IsLanguageNe);
        Assert.False(vm.IsLanguageEn);
    }

    [Fact]
    public void Language_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.Language = "ne";
        Assert.Equal("ne", svc.Current.Language);
    }

    // ── Theme ────────────────────────────────────────────────────────────────

    [Fact]
    public void SetThemeDarkCommand_SetsThemeDark()
    {
        var (vm, _, _, _) = Create();
        vm.SetThemeDarkCommand.Execute(null);
        Assert.Equal("Dark", vm.Theme);
        Assert.True(vm.IsThemeDark);
        Assert.False(vm.IsThemeLight);
    }

    [Fact]
    public void SetThemeLightCommand_SetsThemeLight()
    {
        var (vm, _, _, _) = Create();
        vm.SetThemeDarkCommand.Execute(null);
        vm.SetThemeLightCommand.Execute(null);
        Assert.Equal("Light", vm.Theme);
        Assert.True(vm.IsThemeLight);
        Assert.False(vm.IsThemeDark);
    }

    [Fact]
    public void Theme_CallsThemeServiceApply()
    {
        var (vm, _, theme, _) = Create();
        int before = theme.ApplyCount;
        vm.Theme = "Dark";
        Assert.True(theme.ApplyCount > before);
    }

    // ── Background presets ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Default")]
    [InlineData("Ocean")]
    [InlineData("Forest")]
    [InlineData("Sunset")]
    [InlineData("Monochrome")]
    [InlineData("Aurora")]
    [InlineData("Cherry")]
    [InlineData("Midnight")]
    [InlineData("Slate")]
    [InlineData("Ember")]
    public void BackgroundPreset_AllPresets_AreExclusive(string preset)
    {
        var (vm, _, _, _) = Create();
        vm.BackgroundPreset = preset;
        Assert.Equal(preset == "Default", vm.IsPresetDefault);
        Assert.Equal(preset == "Ocean", vm.IsPresetOcean);
        Assert.Equal(preset == "Forest", vm.IsPresetForest);
        Assert.Equal(preset == "Sunset", vm.IsPresetSunset);
        Assert.Equal(preset == "Monochrome", vm.IsPresetMonochrome);
        Assert.Equal(preset == "Aurora", vm.IsPresetAurora);
        Assert.Equal(preset == "Cherry", vm.IsPresetCherry);
        Assert.Equal(preset == "Midnight", vm.IsPresetMidnight);
        Assert.Equal(preset == "Slate", vm.IsPresetSlate);
        Assert.Equal(preset == "Ember", vm.IsPresetEmber);
    }

    [Fact]
    public void BackgroundPreset_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.BackgroundPreset = "Ocean";
        Assert.Equal("Ocean", svc.Current.BackgroundPreset);
    }

    // ── Corner style ─────────────────────────────────────────────────────────

    [Fact]
    public void SetCornerRoundedCommand_SetsRounded()
    {
        var (vm, _, _, _) = Create();
        vm.SetCornerSharpCommand.Execute(null);
        vm.SetCornerRoundedCommand.Execute(null);
        Assert.Equal("Rounded", vm.CornerStyle);
        Assert.True(vm.IsCornerRounded);
        Assert.False(vm.IsCornerSharp);
    }

    [Fact]
    public void SetCornerSharpCommand_SetsSharp()
    {
        var (vm, _, _, _) = Create();
        vm.SetCornerSharpCommand.Execute(null);
        Assert.Equal("Sharp", vm.CornerStyle);
        Assert.True(vm.IsCornerSharp);
        Assert.False(vm.IsCornerRounded);
    }

    // ── Clock format ─────────────────────────────────────────────────────────

    [Fact]
    public void SetClockFormat12hCommand_Sets12h()
    {
        var (vm, _, _, _) = Create();
        vm.SetClockFormat24hCommand.Execute(null);
        vm.SetClockFormat12hCommand.Execute(null);
        Assert.Equal("12h", vm.ClockFormat);
        Assert.True(vm.IsClockFormat12h);
        Assert.False(vm.IsClockFormat24h);
    }

    [Fact]
    public void SetClockFormat24hCommand_Sets24h()
    {
        var (vm, _, _, _) = Create();
        vm.SetClockFormat24hCommand.Execute(null);
        Assert.Equal("24h", vm.ClockFormat);
        Assert.True(vm.IsClockFormat24h);
        Assert.False(vm.IsClockFormat12h);
    }

    // ── Boolean toggles ──────────────────────────────────────────────────────

    [Fact]
    public void AlwaysOnTop_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.AlwaysOnTop = false;
        Assert.False(svc.Current.AlwaysOnTop);
        vm.AlwaysOnTop = true;
        Assert.True(svc.Current.AlwaysOnTop);
    }

    [Fact]
    public void AnimationEnabled_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.AnimationEnabled = false;
        Assert.False(svc.Current.AnimationEnabled);
    }

    [Fact]
    public void TransparentWhenCollapsed_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.TransparentWhenCollapsed = false;
        Assert.False(svc.Current.TransparentWhenCollapsed);
    }

    [Fact]
    public void ShowTimezone_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowTimezone = false;
        Assert.False(svc.Current.ShowTimezone);
    }

    [Fact]
    public void ShowDayOfWeek_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowDayOfWeek = false;
        Assert.False(svc.Current.ShowDayOfWeek);
    }

    [Fact]
    public void ShowEnglishDate_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowEnglishDate = false;
        Assert.False(svc.Current.ShowEnglishDate);
    }

    [Fact]
    public void ShowOffset_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowOffset = true;
        Assert.True(svc.Current.ShowOffset);
    }

    [Fact]
    public void ShowEnglishDayNumbers_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowEnglishDayNumbers = false;
        Assert.False(svc.Current.ShowEnglishDayNumbers);
    }

    [Fact]
    public void HighlightSaturdays_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.HighlightSaturdays = false;
        Assert.False(svc.Current.HighlightSaturdays);
    }

    // ── Log size ─────────────────────────────────────────────────────────────

    [Fact]
    public void LogMaxSizeMb_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.LogMaxSizeMb = 50;
        Assert.Equal(50, svc.Current.LogMaxSizeMb);
    }

    [Fact]
    public void LogMaxSizeMbDisplay_FormattedCorrectly()
    {
        var (vm, _, _, _) = Create();
        vm.LogMaxSizeMb = 25;
        Assert.Equal("25 MB", vm.LogMaxSizeMbDisplay);
    }

    // ── Font family ──────────────────────────────────────────────────────────

    [Fact]
    public void FontFamilyNames_ContainsExpectedFonts()
    {
        Assert.Contains("Segoe UI", SettingsViewModel.FontFamilyNames);
        Assert.Contains("Inter", SettingsViewModel.FontFamilyNames);
        Assert.Contains("Cascadia Code", SettingsViewModel.FontFamilyNames);
    }

    [Fact]
    public void FontFamily_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.FontFamily = "Cascadia Code";
        Assert.Equal("Cascadia Code", svc.Current.FontFamily);
    }

    // ── AutoStart ────────────────────────────────────────────────────────────

    [Fact]
    public void AutoStart_CallsAutoStartService()
    {
        var (vm, _, _, auto) = Create();
        vm.AutoStart = true;
        Assert.True(auto.IsEnabled);
        Assert.Equal(1, auto.SetEnabledCount);
    }

    // ── Save on every property change ────────────────────────────────────────

    [Fact]
    public void EachPropertyChange_SavesSettings()
    {
        var (vm, svc, _, _) = Create();
        int before = svc.SaveCount;

        vm.Language = "ne";
        Assert.True(svc.SaveCount > before, "Language change should save");

        before = svc.SaveCount;
        vm.Theme = "Dark";
        Assert.True(svc.SaveCount > before, "Theme change should save");

        before = svc.SaveCount;
        vm.BackgroundPreset = "Ocean";
        Assert.True(svc.SaveCount > before, "Preset change should save");

        before = svc.SaveCount;
        vm.CornerStyle = "Sharp";
        Assert.True(svc.SaveCount > before, "CornerStyle change should save");

        before = svc.SaveCount;
        vm.AlwaysOnTop = !vm.AlwaysOnTop;
        Assert.True(svc.SaveCount > before, "AlwaysOnTop change should save");
    }

    // ── ResetToDefaults ──────────────────────────────────────────────────────

    [Fact]
    public void ResetToDefaultsCommand_RestoresDefaults()
    {
        var (vm, _, _, _) = Create();
        vm.Theme = "Dark";
        vm.BackgroundPreset = "Ocean";
        vm.CornerStyle = "Sharp";

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal("Light", vm.Theme);
        Assert.Equal("Forest", vm.BackgroundPreset);
        Assert.Equal("Rounded", vm.CornerStyle);
        Assert.Equal("en", vm.Language);
    }

    // ── Labels ───────────────────────────────────────────────────────────────

    [Fact]
    public void Labels_AllNonEmpty_InEnglish()
    {
        var (vm, _, _, _) = Create();
        Assert.NotEmpty(vm.LanguageLabel);
        Assert.NotEmpty(vm.ThemeLabel);
        Assert.NotEmpty(vm.BackgroundLabel);
        Assert.NotEmpty(vm.CornerStyleLabel);
        Assert.NotEmpty(vm.AlwaysOnTopLabel);
        Assert.NotEmpty(vm.AnimationLabel);
        Assert.NotEmpty(vm.AutoStartLabel);
        Assert.NotEmpty(vm.TransparentWhenCollapsedLabel);
        Assert.NotEmpty(vm.CollapsedDisplayLabel);
        Assert.NotEmpty(vm.ShowTimezoneLabel);
        Assert.NotEmpty(vm.TimezoneLabel);
        Assert.NotEmpty(vm.ShowOffsetLabel);
        Assert.NotEmpty(vm.ShowDayLabel);
        Assert.NotEmpty(vm.ShowEnglishLabel);
        Assert.NotEmpty(vm.ClockFormatLabel);
        Assert.NotEmpty(vm.AppearanceSectionLabel);
        Assert.NotEmpty(vm.BehaviorSectionLabel);
        Assert.NotEmpty(vm.CalendarSectionLabel);
        Assert.NotEmpty(vm.LogSizeLabel);
        Assert.NotEmpty(vm.FontLabel);
        Assert.NotEmpty(vm.ResetToDefaultsLabel);
    }

    [Fact]
    public void OnLanguageChanged_UpdatesLabels()
    {
        var svc = new FakeSettingsService();
        var loc = new LocalizationService();
        loc.SetLanguage("en");
        var vm = new SettingsViewModel(svc, loc, new FakeThemeService(), new FakeAutoStartService());
        var enTheme = vm.ThemeLabel;

        loc.SetLanguage("ne");
        vm.OnLanguageChanged();

        Assert.NotEqual(enTheme, vm.ThemeLabel);
    }

    // ── SettingsApplied event ────────────────────────────────────────────────

    [Fact]
    public void PropertyChange_RaisesSettingsApplied()
    {
        var (vm, _, _, _) = Create();
        bool raised = false;
        vm.SettingsApplied += (_, _) => raised = true;

        vm.Theme = "Dark";

        Assert.True(raised);
    }

    // ── Timezone selection ───────────────────────────────────────────────────

    [Fact]
    public void Timezones_Populated_OnConstruction()
    {
        var (vm, _, _, _) = Create();
        Assert.NotEmpty(vm.Timezones);
    }

    [Fact]
    public void SelectedTimezone_NotNull_AfterConstruction()
    {
        var (vm, _, _, _) = Create();
        Assert.NotNull(vm.SelectedTimezone);
    }

    // ── Hotkey ───────────────────────────────────────────────────────────────

    [Fact]
    public void HotkeyDisplayText_NonEmpty_WithDefaults()
    {
        var (vm, _, _, _) = Create();
        Assert.NotEmpty(vm.HotkeyDisplayText);
    }

    [Fact]
    public void ClearHotkeyCommand_ClearsHotkey()
    {
        var (vm, svc, _, _) = Create();
        vm.ClearHotkeyCommand.Execute(null);
        Assert.Equal(0, svc.Current.RunBoxHotkeyModifiers);
        Assert.Equal(0, svc.Current.RunBoxHotkeyKey);
    }

    [Fact]
    public void IsRecordingHotkey_DefaultFalse()
    {
        var (vm, _, _, _) = Create();
        Assert.False(vm.IsRecordingHotkey);
    }

    [Fact]
    public void HasHotkeyError_DefaultFalse()
    {
        var (vm, _, _, _) = Create();
        Assert.False(vm.HasHotkeyError);
    }

    // ── PropertyChanged for SetProperty-backed fields ────────────────────────

    [Fact]
    public void Language_RaisesPropertyChanged()
    {
        var (vm, _, _, _) = Create();
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.Language = "ne";

        Assert.Contains(nameof(vm.Language), changed);
        Assert.Contains(nameof(vm.IsLanguageEn), changed);
        Assert.Contains(nameof(vm.IsLanguageNe), changed);
    }

    [Fact]
    public void Theme_RaisesPropertyChanged()
    {
        var (vm, _, _, _) = Create();
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.Theme = "Dark";

        Assert.Contains(nameof(vm.Theme), changed);
        Assert.Contains(nameof(vm.IsThemeDark), changed);
        Assert.Contains(nameof(vm.IsThemeLight), changed);
    }

    [Fact]
    public void NoChange_DoesNotRaisePropertyChanged()
    {
        var (vm, _, _, _) = Create();
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.Language = vm.Language; // same value

        Assert.DoesNotContain(nameof(vm.Language), changed);
    }
}
