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
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
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
        Assert.Equal("Default", vm.BackgroundPreset);
        Assert.Equal("Rounded", vm.CornerStyle);
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
        Assert.Equal("Default", vm.BackgroundPreset);
        Assert.Equal("Rounded", vm.CornerStyle);
        Assert.Equal("en", vm.Language);
    }

    [Fact]
    public void ResetToDefaultsCommand_AutoStart_ReadsFromSettingsNotOsState()
    {
        // Regression: _autoStart was read from _autoStartService.IsEnabled (OS state)
        // rather than from the default settings value. If the OS has AutoStart disabled,
        // reset should still restore the default (true), not preserve the disabled state.
        var svc = new FakeSettingsService();
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        var auto = new FakeAutoStartService(initialState: false); // OS has it disabled
        var vm = new SettingsViewModel(svc, loc, new FakeThemeService(), auto);

        vm.ResetToDefaultsCommand.Execute(null);

        // After reset the VM should reflect the default (true), not the OS state (false).
        Assert.True(vm.AutoStart);
        // And the autostart service should have been re-enabled.
        Assert.True(auto.IsEnabled);
    }

    [Fact]
    public void ResetToDefaultsCommand_NotifiesCalendarDisplayProperties()
    {
        // Regression: ShowTithi, ShowEvents, HighlightPublicHolidays were reset in
        // the backing fields but OnPropertyChanged was never called for them.
        var (vm, _, _, _) = Create();
        vm.ShowTithi = false;
        vm.ShowEvents = false;
        vm.HighlightPublicHolidays = false;

        var notified = new HashSet<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is not null) notified.Add(e.PropertyName); };

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Contains(nameof(vm.ShowTithi), notified);
        Assert.Contains(nameof(vm.ShowEvents), notified);
        Assert.Contains(nameof(vm.HighlightPublicHolidays), notified);
        Assert.True(vm.ShowTithi);
        Assert.True(vm.ShowEvents);
        Assert.True(vm.HighlightPublicHolidays);
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
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
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

    // ── Export / Import backup commands (#11 / #12) ───────────────────────────

    [Fact]
    public void ExportBackupLabel_IsNonEmpty_InEnglish()
    {
        var (vm, _, _, _) = Create();
        Assert.NotEmpty(vm.ExportBackupLabel);
    }

    [Fact]
    public void ImportBackupLabel_IsNonEmpty_InEnglish()
    {
        var (vm, _, _, _) = Create();
        Assert.NotEmpty(vm.ImportBackupLabel);
    }

    [Fact]
    public void ExportBackupCommand_IsNotNull_AndCanExecute()
    {
        var (vm, _, _, _) = Create();
        Assert.NotNull(vm.ExportBackupCommand);
        Assert.True(vm.ExportBackupCommand.CanExecute(null));
    }

    [Fact]
    public void ImportBackupCommand_IsNotNull_AndCanExecute()
    {
        var (vm, _, _, _) = Create();
        Assert.NotNull(vm.ImportBackupCommand);
        Assert.True(vm.ImportBackupCommand.CanExecute(null));
    }

    [Fact]
    public void ExportBackupLabel_ChangesOnLanguageSwitch()
    {
        var svc = new FakeSettingsService();
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        var vm = new SettingsViewModel(svc, loc, new FakeThemeService(), new FakeAutoStartService());
        var en = vm.ExportBackupLabel;

        loc.SetLanguage("ne");
        vm.OnLanguageChanged();

        Assert.NotEqual(en, vm.ExportBackupLabel);
    }

    [Fact]
    public void ImportBackupLabel_ChangesOnLanguageSwitch()
    {
        var svc = new FakeSettingsService();
        var loc = new LocalizationService(TestPaths.DefaultLocalizationPath);
        loc.SetLanguage("en");
        var vm = new SettingsViewModel(svc, loc, new FakeThemeService(), new FakeAutoStartService());
        var en = vm.ImportBackupLabel;

        loc.SetLanguage("ne");
        vm.OnLanguageChanged();

        Assert.NotEqual(en, vm.ImportBackupLabel);
    }

    [Fact]
    public void Labels_AllNonEmpty_IncludesBackupLabels()
    {
        var (vm, _, _, _) = Create();
        Assert.NotEmpty(vm.ExportBackupLabel);
        Assert.NotEmpty(vm.ImportBackupLabel);
        Assert.NotEmpty(vm.AppearanceSectionLabel);
        Assert.NotEmpty(vm.BehaviorSectionLabel);
        Assert.NotEmpty(vm.CalendarSectionLabel);
        Assert.NotEmpty(vm.LogSizeLabel);
        Assert.NotEmpty(vm.FontLabel);
        Assert.NotEmpty(vm.ResetToDefaultsLabel);
    }

    // ── Import backup path-traversal guard (#12) ──────────────────────────────

    [Fact]
    public void ResolveImportEntryPath_KnownFile_ReturnsPathInsideDataDir()
    {
        var dir  = Path.Combine(Path.GetTempPath(), $"bktest_{Guid.NewGuid():N}");
        var dest = SettingsViewModel.ResolveImportEntryPath("config/settings.json", dir);
        Assert.NotNull(dest);
        Assert.StartsWith(Path.GetFullPath(dir), dest, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("settings.json", dest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveImportEntryPath_UnknownFile_ReturnsNull()
    {
        var dir  = Path.Combine(Path.GetTempPath(), $"bktest_{Guid.NewGuid():N}");
        var dest = SettingsViewModel.ResolveImportEntryPath("evil.exe", dir);
        Assert.Null(dest);
    }

    [Fact]
    public void ResolveImportEntryPath_PathTraversal_ReturnsNull()
    {
        // An attacker could craft a ZIP entry named "../settings.json" to escape the
        // target directory. The guard must reject it.
        var dir  = Path.Combine(Path.GetTempPath(), $"bktest_{Guid.NewGuid():N}");
        var dest = SettingsViewModel.ResolveImportEntryPath("../settings.json", dir);
        Assert.Null(dest);
    }

    [Fact]
    public void ResolveImportEntryPath_AbsolutePath_ReturnsNull()
    {
        // A ZIP entry whose name is an absolute path must be rejected.
        var dir  = Path.Combine(Path.GetTempPath(), $"bktest_{Guid.NewGuid():N}");
        var dest = SettingsViewModel.ResolveImportEntryPath(@"C:\Windows\System32\evil.dll", dir);
        Assert.Null(dest);
    }

    [Fact]
    public void ResolveImportEntryPath_EmptyName_ReturnsNull()
    {
        var dir  = Path.Combine(Path.GetTempPath(), $"bktest_{Guid.NewGuid():N}");
        Assert.Null(SettingsViewModel.ResolveImportEntryPath("", dir));
    }

    [Fact]
    public void ResolveImportEntryPath_AllAllowedFiles_ReturnValidPaths()
    {
        // Every entry in the allowlist must resolve to a path under the data directory.
        var dir = Path.Combine(Path.GetTempPath(), $"bktest_{Guid.NewGuid():N}");
        string[] allowed =
        [
            "config/settings.json",
            "config/localization.json",
            "config/shortcuts.json",
            "config/scripts.json",
            "data/notes.json",
            "data/reminders.json",
            "data/documents.json",
            "data/run-history.json",
            "runtime.json",
            "nepdate.log"
        ];
        foreach (var name in allowed)
        {
            var dest = SettingsViewModel.ResolveImportEntryPath(name, dir);
            Assert.NotNull(dest);
            Assert.True(
                dest!.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase),
                $"Path for '{name}' escapes data directory: {dest}");
        }
    }

    [Fact]
    public void ResolveImportEntryPath_CaseInsensitiveName_Allowed()
    {
        // The allowlist uses OrdinalIgnoreCase - "config/Settings.JSON" is valid.
        var dir  = Path.Combine(Path.GetTempPath(), $"bktest_{Guid.NewGuid():N}");
        var dest = SettingsViewModel.ResolveImportEntryPath("config/Settings.JSON", dir);
        Assert.NotNull(dest);
    }

    [Fact]
    public void ResolveImportEntryPath_FlatFilenameNotOnAllowlist_ReturnsNull()
    {
        // Old-style flat backups (just filename, no subdir) must be rejected.
        var dir  = Path.Combine(Path.GetTempPath(), $"bktest_{Guid.NewGuid():N}");
        Assert.Null(SettingsViewModel.ResolveImportEntryPath("settings.json", dir));
    }

    [Fact]
    public void ResolveImportEntryPath_BackslashSeparator_IsAccepted()
    {
        // Windows-style backslash in entry name should be normalized and still resolve.
        var dir  = Path.Combine(Path.GetTempPath(), $"bktest_{Guid.NewGuid():N}");
        var dest = SettingsViewModel.ResolveImportEntryPath("config\\settings.json", dir);
        Assert.NotNull(dest);
    }

    // ── EnsureDataFile seed content ───────────────────────────────────────────

    [Theory]
    [InlineData("notes.json",        "{}")]  // string-keyed dictionary
    [InlineData("runtime.json",      "{}")]  // plain object
    [InlineData("settings.json",     "{}")]  // plain object
    [InlineData("localization.json", "{}")]  // dict-of-dicts - must NOT be []
    [InlineData("reminders.json",    "[]")]
    [InlineData("documents.json",    "[]")]
    [InlineData("shortcuts.json",    "[]")]
    [InlineData("scripts.json",      "[]")]
    [InlineData("run-history.json",  "[]")]
    public void EnsureDataFile_CreatesCorrectSeedContent(string filename, string expected)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ensure_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, filename);
            SettingsViewModel.EnsureDataFile(path);
            Assert.True(File.Exists(path));
            Assert.Equal(expected, File.ReadAllText(path, System.Text.Encoding.UTF8).Trim());
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void EnsureDataFile_LogFile_CreatesEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ensure_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "nepdate.log");
            SettingsViewModel.EnsureDataFile(path);
            Assert.True(File.Exists(path));
            Assert.Equal(string.Empty, File.ReadAllText(path));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void EnsureDataFile_ExistingFile_IsNotOverwritten()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ensure_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "sentinel");
            SettingsViewModel.EnsureDataFile(path);
            Assert.Equal("sentinel", File.ReadAllText(path));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void EnsureDataFile_MissingDirectory_IsCreated()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ensure_{Guid.NewGuid():N}", "nested");
        try
        {
            var path = Path.Combine(dir, "notes.json");
            SettingsViewModel.EnsureDataFile(path);
            Assert.True(File.Exists(path));
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(dir)!, true); } catch { }
        }
    }

    // ── New settings properties persistence ───────────────────────────────────

    [Fact]
    public void ShowSecondsInClock_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowSecondsInClock = true;
        Assert.True(svc.Current.ShowSecondsInClock);
        vm.ShowSecondsInClock = false;
        Assert.False(svc.Current.ShowSecondsInClock);
    }

    [Fact]
    public void ShowFiscalYear_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowFiscalYear = false;
        Assert.False(svc.Current.ShowFiscalYear);
    }

    [Fact]
    public void ShowHelpBadges_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowHelpBadges = false;
        Assert.False(svc.Current.ShowHelpBadges);
    }

    [Fact]
    public void NotificationDurationSeconds_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.NotificationDurationSeconds = 30;
        Assert.Equal(30, svc.Current.NotificationDurationSeconds);
    }

    [Fact]
    public void NotificationDurationDisplay_FormattedCorrectly()
    {
        var (vm, _, _, _) = Create();
        vm.NotificationDurationSeconds = 15;
        Assert.Equal("15s", vm.NotificationDurationDisplay);
    }

    [Fact]
    public void NotificationSound_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.NotificationSound = false;
        Assert.False(svc.Current.NotificationSound);
    }

    [Fact]
    public void ShowDailyEventsNotification_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowDailyEventsNotification = true;
        Assert.True(svc.Current.ShowDailyEventsNotification);
    }

    [Fact]
    public void HighlightSundays_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.HighlightSundays = false;
        Assert.False(svc.Current.HighlightSundays);
    }

    [Fact]
    public void ShowTithi_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowTithi = true;
        Assert.True(svc.Current.ShowTithi);
    }

    [Fact]
    public void ShowEvents_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowEvents = false;
        Assert.False(svc.Current.ShowEvents);
    }

    [Fact]
    public void HighlightPublicHolidays_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.HighlightPublicHolidays = false;
        Assert.False(svc.Current.HighlightPublicHolidays);
    }

    [Fact]
    public void ShowHolidayCountdown_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.ShowHolidayCountdown = false;
        Assert.False(svc.Current.ShowHolidayCountdown);
    }

    [Fact]
    public void HighlightColor_PersistsToSettings()
    {
        var (vm, svc, _, _) = Create();
        vm.HighlightColor = "#E53935";
        Assert.Equal("#E53935", svc.Current.HighlightColor);
        Assert.True(vm.IsHighlightColorRed);
        Assert.False(vm.IsHighlightColorDefault);
    }

    [Fact]
    public void HighlightColor_SetEmpty_IsDefault()
    {
        var (vm, _, _, _) = Create();
        vm.HighlightColor = "#E53935";
        vm.SetHighlightColorDefaultCommand.Execute(null);
        Assert.True(vm.IsHighlightColorDefault);
        Assert.Equal(string.Empty, vm.HighlightColor);
    }

    // ── Labels completeness (extended) ────────────────────────────────────────

    [Fact]
    public void Labels_AllNonEmpty_Extended()
    {
        // Covers the label properties that Labels_AllNonEmpty_InEnglish omits.
        var (vm, _, _, _) = Create();
        Assert.NotEmpty(vm.ShowSecondsLabel);
        Assert.NotEmpty(vm.ShowFiscalYearLabel);
        Assert.NotEmpty(vm.ShowHelpBadgesLabel);
        Assert.NotEmpty(vm.DataFilesSectionLabel);
        Assert.NotEmpty(vm.NotificationSoundLabel);
        Assert.NotEmpty(vm.NotificationSectionLabel);
        Assert.NotEmpty(vm.TestNotificationLabel);
        Assert.NotEmpty(vm.HotkeyLabel);
        Assert.NotEmpty(vm.HotkeyClearLabel);
        Assert.NotEmpty(vm.ShowHolidayCountdownLabel);
        Assert.NotEmpty(vm.ShowDailyEventsNotificationLabel);
        Assert.NotEmpty(vm.ThemeDarkLabel);
        Assert.NotEmpty(vm.ThemeLightLabel);
        Assert.NotEmpty(vm.CornerRoundedLabel);
        Assert.NotEmpty(vm.CornerSharpLabel);
        Assert.NotEmpty(vm.PresetDefaultLabel);
        Assert.NotEmpty(vm.PresetOceanLabel);
        Assert.NotEmpty(vm.PresetForestLabel);
        Assert.NotEmpty(vm.PresetSunsetLabel);
        Assert.NotEmpty(vm.PresetMonoLabel);
        Assert.NotEmpty(vm.PresetAuroraLabel);
        Assert.NotEmpty(vm.PresetCherryLabel);
        Assert.NotEmpty(vm.PresetMidnightLabel);
        Assert.NotEmpty(vm.PresetSlateLabel);
        Assert.NotEmpty(vm.PresetEmberLabel);
    }

    // ── ResetToDefaults - new/specific properties ────────────────────────────

    [Fact]
    public void ResetToDefaultsCommand_RestoresHighlightColorToOrange()
    {
        // The factory default is #F4511E (orange), not empty string.
        // IsHighlightColorOrange must be true and IsHighlightColorDefault false.
        var (vm, _, _, _) = Create();
        vm.HighlightColor = "#E53935";  // change to red

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal("#F4511E", vm.HighlightColor);
        Assert.True(vm.IsHighlightColorOrange);
        Assert.False(vm.IsHighlightColorDefault);
        Assert.False(vm.IsHighlightColorRed);
    }

    [Fact]
    public void ResetToDefaultsCommand_RestoresClockFormatAndDerivedBooleans()
    {
        var (vm, _, _, _) = Create();
        vm.ClockFormat = "24h";

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal("12h", vm.ClockFormat);
        Assert.True(vm.IsClockFormat12h);
        Assert.False(vm.IsClockFormat24h);
    }

    [Fact]
    public void ResetToDefaultsCommand_RestoresShowSecondsShowFiscalYearShowHelpBadges()
    {
        var (vm, _, _, _) = Create();
        vm.ShowSecondsInClock = true;
        vm.ShowFiscalYear     = false;
        vm.ShowHelpBadges     = false;

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.False(vm.ShowSecondsInClock);
        Assert.True(vm.ShowFiscalYear);
        Assert.True(vm.ShowHelpBadges);
    }

    [Fact]
    public void ResetToDefaultsCommand_RestoresNotificationSettings()
    {
        var (vm, _, _, _) = Create();
        vm.NotificationDurationSeconds = 60;
        vm.NotificationSound           = false;

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal(10,  vm.NotificationDurationSeconds);
        Assert.Equal("10s", vm.NotificationDurationDisplay);
        Assert.True(vm.NotificationSound);
    }

    [Fact]
    public void ResetToDefaultsCommand_RestoresLogMaxSize()
    {
        var (vm, _, _, _) = Create();
        vm.LogMaxSizeMb = 100;

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Equal(10,     vm.LogMaxSizeMb);
        Assert.Equal("10 MB", vm.LogMaxSizeMbDisplay);
    }

    [Fact]
    public void ResetToDefaultsCommand_NotifiesShowSecondsShowFiscalYearShowHelpBadges()
    {
        var (vm, _, _, _) = Create();
        vm.ShowSecondsInClock = true;
        vm.ShowFiscalYear     = false;
        vm.ShowHelpBadges     = false;

        var notified = new HashSet<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is not null) notified.Add(e.PropertyName); };

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Contains(nameof(vm.ShowSecondsInClock), notified);
        Assert.Contains(nameof(vm.ShowFiscalYear),     notified);
        Assert.Contains(nameof(vm.ShowHelpBadges),     notified);
    }

    [Fact]
    public void ResetToDefaultsCommand_NotifiesDisplayProperties()
    {
        // LogMaxSizeMbDisplay and NotificationDurationDisplay are derived strings.
        // They must be explicitly notified because their backing fields are set
        // directly (no through the public property setter which would call OnPropertyChanged).
        var (vm, _, _, _) = Create();
        vm.LogMaxSizeMb            = 50;
        vm.NotificationDurationSeconds = 30;

        var notified = new HashSet<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is not null) notified.Add(e.PropertyName); };

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Contains(nameof(vm.LogMaxSizeMbDisplay),         notified);
        Assert.Contains(nameof(vm.NotificationDurationDisplay), notified);
    }

    [Fact]
    public void ResetToDefaultsCommand_FiresSettingsApplied()
    {
        var (vm, _, _, _) = Create();
        bool raised = false;
        vm.SettingsApplied += (_, _) => raised = true;

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void ResetToDefaultsCommand_NotifiesHighlightColorAndAllDerivatives()
    {
        var (vm, _, _, _) = Create();
        vm.HighlightColor = "#E53935";

        var notified = new HashSet<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is not null) notified.Add(e.PropertyName); };

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Contains(nameof(vm.HighlightColor),          notified);
        Assert.Contains(nameof(vm.IsHighlightColorDefault), notified);
        Assert.Contains(nameof(vm.IsHighlightColorRed),     notified);
        Assert.Contains(nameof(vm.IsHighlightColorOrange),  notified);
    }

    [Fact]
    public void ResetToDefaultsCommand_NotifiesAllPresetDerivatives()
    {
        var (vm, _, _, _) = Create();
        vm.BackgroundPreset = "Ocean";

        var notified = new HashSet<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is not null) notified.Add(e.PropertyName); };

        vm.ResetToDefaultsCommand.Execute(null);

        Assert.Contains(nameof(vm.IsPresetDefault),    notified);
        Assert.Contains(nameof(vm.IsPresetOcean),      notified);
        Assert.Contains(nameof(vm.IsPresetForest),     notified);
        Assert.Contains(nameof(vm.IsPresetSunset),     notified);
        Assert.Contains(nameof(vm.IsPresetMonochrome), notified);
        Assert.Contains(nameof(vm.IsPresetAurora),     notified);
        Assert.Contains(nameof(vm.IsPresetCherry),     notified);
        Assert.Contains(nameof(vm.IsPresetMidnight),   notified);
        Assert.Contains(nameof(vm.IsPresetSlate),      notified);
        Assert.Contains(nameof(vm.IsPresetEmber),      notified);
    }

    // ── Highlight color - full command + exclusivity coverage ────────────────

    [Theory]
    [InlineData("",        true,  false, false, false, false, false, false, false, false, false, false, false, false)]
    [InlineData("#E53935", false, true,  false, false, false, false, false, false, false, false, false, false, false)]
    [InlineData("#F4511E", false, false, true,  false, false, false, false, false, false, false, false, false, false)]
    [InlineData("#EC407A", false, false, false, true,  false, false, false, false, false, false, false, false, false)]
    [InlineData("#AB47BC", false, false, false, false, true,  false, false, false, false, false, false, false, false)]
    [InlineData("#1E88E5", false, false, false, false, false, true,  false, false, false, false, false, false, false)]
    [InlineData("#26A69A", false, false, false, false, false, false, true,  false, false, false, false, false, false)]
    [InlineData("#43A047", false, false, false, false, false, false, false, true,  false, false, false, false, false)]
    [InlineData("#FDD835", false, false, false, false, false, false, false, false, true,  false, false, false, false)]
    [InlineData("#FFB300", false, false, false, false, false, false, false, false, false, true,  false, false, false)]
    [InlineData("#00ACC1", false, false, false, false, false, false, false, false, false, false, true,  false, false)]
    [InlineData("#3949AB", false, false, false, false, false, false, false, false, false, false, false, true,  false)]
    [InlineData("#6D4C41", false, false, false, false, false, false, false, false, false, false, false, false, true )]
    public void HighlightColor_AllColors_AreExclusive(
        string hex,
        bool isDefault, bool isRed,   bool isOrange, bool isPink,   bool isPurple,
        bool isBlue,    bool isTeal,  bool isGreen,  bool isYellow, bool isAmber,
        bool isCyan,    bool isIndigo, bool isBrown)
    {
        var (vm, _, _, _) = Create();
        vm.HighlightColor = hex;

        Assert.Equal(isDefault, vm.IsHighlightColorDefault);
        Assert.Equal(isRed,     vm.IsHighlightColorRed);
        Assert.Equal(isOrange,  vm.IsHighlightColorOrange);
        Assert.Equal(isPink,    vm.IsHighlightColorPink);
        Assert.Equal(isPurple,  vm.IsHighlightColorPurple);
        Assert.Equal(isBlue,    vm.IsHighlightColorBlue);
        Assert.Equal(isTeal,    vm.IsHighlightColorTeal);
        Assert.Equal(isGreen,   vm.IsHighlightColorGreen);
        Assert.Equal(isYellow,  vm.IsHighlightColorYellow);
        Assert.Equal(isAmber,   vm.IsHighlightColorAmber);
        Assert.Equal(isCyan,    vm.IsHighlightColorCyan);
        Assert.Equal(isIndigo,  vm.IsHighlightColorIndigo);
        Assert.Equal(isBrown,   vm.IsHighlightColorBrown);
    }

    [Fact]
    public void HighlightColor_Change_NotifiesAllThirteenDerivatives()
    {
        var (vm, _, _, _) = Create();
        var notified = new HashSet<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is not null) notified.Add(e.PropertyName); };

        vm.HighlightColor = "#E53935";

        Assert.Contains(nameof(vm.IsHighlightColorDefault), notified);
        Assert.Contains(nameof(vm.IsHighlightColorRed),     notified);
        Assert.Contains(nameof(vm.IsHighlightColorOrange),  notified);
        Assert.Contains(nameof(vm.IsHighlightColorPink),    notified);
        Assert.Contains(nameof(vm.IsHighlightColorPurple),  notified);
        Assert.Contains(nameof(vm.IsHighlightColorBlue),    notified);
        Assert.Contains(nameof(vm.IsHighlightColorTeal),    notified);
        Assert.Contains(nameof(vm.IsHighlightColorGreen),   notified);
        Assert.Contains(nameof(vm.IsHighlightColorYellow),  notified);
        Assert.Contains(nameof(vm.IsHighlightColorAmber),   notified);
        Assert.Contains(nameof(vm.IsHighlightColorCyan),    notified);
        Assert.Contains(nameof(vm.IsHighlightColorIndigo),  notified);
        Assert.Contains(nameof(vm.IsHighlightColorBrown),   notified);
    }

    [Fact]
    public void HighlightColor_SameValue_DoesNotRaisePropertyChanged()
    {
        // SetProperty equality guard must prevent spurious notifications and Apply() calls.
        var (vm, svc, _, _) = Create();
        vm.HighlightColor = "#E53935";

        int saveBefore = svc.SaveCount;
        var notified   = new List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is not null) notified.Add(e.PropertyName); };

        vm.HighlightColor = "#E53935";  // same value

        Assert.DoesNotContain(nameof(vm.HighlightColor), notified);
        Assert.Equal(saveBefore, svc.SaveCount);
    }

    [Theory]
    [InlineData("SetHighlightColorDefaultCommand", "")]
    [InlineData("SetHighlightColorRedCommand",     "#E53935")]
    [InlineData("SetHighlightColorOrangeCommand",  "#F4511E")]
    [InlineData("SetHighlightColorPinkCommand",    "#EC407A")]
    [InlineData("SetHighlightColorPurpleCommand",  "#AB47BC")]
    [InlineData("SetHighlightColorBlueCommand",    "#1E88E5")]
    [InlineData("SetHighlightColorTealCommand",    "#26A69A")]
    [InlineData("SetHighlightColorGreenCommand",   "#43A047")]
    [InlineData("SetHighlightColorYellowCommand",  "#FDD835")]
    [InlineData("SetHighlightColorAmberCommand",   "#FFB300")]
    [InlineData("SetHighlightColorCyanCommand",    "#00ACC1")]
    [InlineData("SetHighlightColorIndigoCommand",  "#3949AB")]
    [InlineData("SetHighlightColorBrownCommand",   "#6D4C41")]
    public void HighlightColor_AllColorCommands_PersistCorrectHex(string commandName, string expectedHex)
    {
        var (vm, svc, _, _) = Create();
        var cmd = (System.Windows.Input.ICommand)typeof(SettingsViewModel)
            .GetProperty(commandName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!
            .GetValue(vm)!;

        cmd.Execute(null);

        Assert.Equal(expectedHex, vm.HighlightColor);
        Assert.Equal(expectedHex, svc.Current.HighlightColor);
    }

    // ── BackgroundPreset notifications ────────────────────────────────────────

    [Fact]
    public void BackgroundPreset_Change_NotifiesAllTenPresetDerivatives()
    {
        var (vm, _, _, _) = Create();
        var notified = new HashSet<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is not null) notified.Add(e.PropertyName); };

        vm.BackgroundPreset = "Ocean";

        Assert.Contains(nameof(vm.IsPresetDefault),    notified);
        Assert.Contains(nameof(vm.IsPresetOcean),      notified);
        Assert.Contains(nameof(vm.IsPresetForest),     notified);
        Assert.Contains(nameof(vm.IsPresetSunset),     notified);
        Assert.Contains(nameof(vm.IsPresetMonochrome), notified);
        Assert.Contains(nameof(vm.IsPresetAurora),     notified);
        Assert.Contains(nameof(vm.IsPresetCherry),     notified);
        Assert.Contains(nameof(vm.IsPresetMidnight),   notified);
        Assert.Contains(nameof(vm.IsPresetSlate),      notified);
        Assert.Contains(nameof(vm.IsPresetEmber),      notified);
    }

    [Fact]
    public void BackgroundPreset_SameValue_DoesNotRaisePropertyChanged()
    {
        var (vm, svc, _, _) = Create();
        vm.BackgroundPreset = "Ocean";

        int saveBefore = svc.SaveCount;
        var notified   = new List<string>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName is not null) notified.Add(e.PropertyName); };

        vm.BackgroundPreset = "Ocean";  // same value

        Assert.DoesNotContain(nameof(vm.BackgroundPreset), notified);
        Assert.Equal(saveBefore, svc.SaveCount);
    }

    // ── AnimationEnabled toggle ───────────────────────────────────────────────

    [Fact]
    public void AnimationEnabled_ToggleFalseThenTrue_BothPersistAndFireSettingsApplied()
    {
        var (vm, svc, _, _) = Create();
        var events = new List<bool>();
        vm.SettingsApplied += (_, _) => events.Add(vm.AnimationEnabled);

        vm.AnimationEnabled = false;
        vm.AnimationEnabled = true;

        Assert.True(svc.Current.AnimationEnabled);
        Assert.Equal(2, events.Count);
        Assert.False(events[0]);
        Assert.True(events[1]);
    }

    // ── Display string edge values ────────────────────────────────────────────

    [Theory]
    [InlineData(5,  "5s")]
    [InlineData(10, "10s")]
    [InlineData(60, "60s")]
    public void NotificationDurationDisplay_AtAllBoundaries(int seconds, string expected)
    {
        var (vm, _, _, _) = Create();
        vm.NotificationDurationSeconds = seconds;
        Assert.Equal(expected, vm.NotificationDurationDisplay);
    }

    [Theory]
    [InlineData(5,   "5 MB")]
    [InlineData(10,  "10 MB")]
    [InlineData(100, "100 MB")]
    public void LogMaxSizeMbDisplay_AtAllBoundaries(int mb, string expected)
    {
        var (vm, _, _, _) = Create();
        vm.LogMaxSizeMb = mb;
        Assert.Equal(expected, vm.LogMaxSizeMbDisplay);
    }

    // ── IsRecordingHotkey side-effects ────────────────────────────────────────

    [Fact]
    public void IsRecordingHotkey_SetTrue_ClearsHotkeyErrorText()
    {
        // HotkeyErrorText must be cleared whenever recording starts so stale
        // error messages from a previous failed attempt do not remain visible.
        var (vm, _, _, _) = Create();
        vm.TrySetHotkey(System.Windows.Input.ModifierKeys.None, System.Windows.Input.Key.Space);
        // drive an error: try to clear then set (no modifiers → should produce an error or at minimum test state)
        vm.IsRecordingHotkey = true;
        Assert.Equal(string.Empty, vm.HotkeyErrorText);
        Assert.False(vm.HasHotkeyError);
    }

    [Fact]
    public void IsRecordingHotkey_SetFalse_RestoresDisplayText()
    {
        var (vm, _, _, _) = Create();
        string textBeforeRecording = vm.HotkeyDisplayText;
        vm.IsRecordingHotkey = true;
        // While recording, display text should change to the "press keys" prompt.
        Assert.NotEqual(textBeforeRecording, vm.HotkeyDisplayText);

        vm.IsRecordingHotkey = false;
        // After cancelling, display should revert to the current hotkey.
        Assert.Equal(textBeforeRecording, vm.HotkeyDisplayText);
    }
}
