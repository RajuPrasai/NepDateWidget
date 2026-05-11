using NepDateWidget.Models;
using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Direct unit tests for SettingsValidator rules.
/// No disk I/O - validator operates purely on in-memory objects.
/// </summary>
public class SettingsValidatorTests
{
    private static WidgetSettings Valid() => new WidgetSettings();

    // ── String enum validation ────────────────────────────────────────────────

    [Theory]
    [InlineData("en")]
    [InlineData("ne")]
    [InlineData("EN")]   // case-insensitive
    public void Language_ValidValue_IsPreserved(string lang)
    {
        var s = Valid(); s.Language = lang;
        SettingsValidator.Validate(s);
        Assert.Equal(lang, s.Language, ignoreCase: true);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("fr")]
    [InlineData("klingon")]
    public void Language_InvalidValue_FallsBackToEn(string? lang)
    {
        var s = Valid(); s.Language = lang!;
        SettingsValidator.Validate(s);
        Assert.Equal("en", s.Language);
    }

    [Theory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public void Theme_ValidValue_IsPreserved(string theme)
    {
        var s = Valid(); s.Theme = theme;
        SettingsValidator.Validate(s);
        Assert.Equal(theme, s.Theme, ignoreCase: true);
    }

    [Theory]
    [InlineData("Neon")]
    [InlineData("")]
    [InlineData(null)]
    public void Theme_InvalidValue_FallsBackToLight(string? theme)
    {
        var s = Valid(); s.Theme = theme!;
        SettingsValidator.Validate(s);
        Assert.Equal("Light", s.Theme);
    }

    [Theory]
    [InlineData("ADtoBS")]
    [InlineData("BStoAD")]
    public void ConverterDirection_ValidValue_IsPreserved(string dir)
    {
        var s = Valid(); s.ConverterDefaultDirection = dir;
        SettingsValidator.Validate(s);
        Assert.Equal(dir, s.ConverterDefaultDirection, ignoreCase: true);
    }

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
    public void BackgroundPreset_ValidValue_IsPreserved(string preset)
    {
        var s = Valid(); s.BackgroundPreset = preset;
        SettingsValidator.Validate(s);
        Assert.Equal(preset, s.BackgroundPreset, ignoreCase: true);
    }

    [Theory]
    [InlineData("Neon")]
    [InlineData("")]
    [InlineData(null)]
    public void BackgroundPreset_InvalidValue_FallsBackToForest(string? preset)
    {
        var s = Valid(); s.BackgroundPreset = preset!;
        SettingsValidator.Validate(s);
        Assert.Equal("Forest", s.BackgroundPreset);
    }

    [Theory]
    [InlineData("12h")]
    [InlineData("24h")]
    public void ClockFormat_ValidValue_IsPreserved(string fmt)
    {
        var s = Valid(); s.ClockFormat = fmt;
        SettingsValidator.Validate(s);
        Assert.Equal(fmt, s.ClockFormat, ignoreCase: true);
    }

    [Theory]
    [InlineData("6h")]
    [InlineData("")]
    [InlineData(null)]
    public void ClockFormat_InvalidValue_FallsBackTo12h(string? fmt)
    {
        var s = Valid(); s.ClockFormat = fmt!;
        SettingsValidator.Validate(s);
        Assert.Equal("12h", s.ClockFormat);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(101)]
    [InlineData(-1)]
    public void LogMaxSizeMb_OutOfRange_ClampsTo10(int val)
    {
        var s = Valid(); s.LogMaxSizeMb = val;
        SettingsValidator.Validate(s);
        Assert.Equal(10, s.LogMaxSizeMb);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void LogMaxSizeMb_ValidValue_IsPreserved(int val)
    {
        var s = Valid(); s.LogMaxSizeMb = val;
        SettingsValidator.Validate(s);
        Assert.Equal(val, s.LogMaxSizeMb);
    }

    // ── Dimension clamping ────────────────────────────────────────────────────

    [Fact]
    public void ExpandedHeight_BelowMin_ClampedTo497()
    {
        var s = Valid(); s.ExpandedHeight = 50;
        SettingsValidator.Validate(s);
        Assert.Equal(497.33333, s.ExpandedHeight);
    }

    // ── Null-safety ───────────────────────────────────────────────────────────

    [Fact]
    public void HighlightedDays_Null_ReplacedWithEmptyList()
    {
        var s = Valid(); s.HighlightedDays = null!;
        SettingsValidator.Validate(s);
        Assert.NotNull(s.HighlightedDays);
    }

    // ── Valid defaults pass without mutation ──────────────────────────────────

    [Fact]
    public void DefaultSettings_PassValidationUnchanged()
    {
        var s = new WidgetSettings();
        SettingsValidator.Validate(s);

        Assert.Equal("en",          s.Language);
        Assert.Equal("Light",       s.Theme);
        Assert.Equal("Rounded",     s.CornerStyle);
        Assert.Equal("BStoAD",      s.ConverterDefaultDirection);
        Assert.Equal(840,           s.ExpandedWidth);
        Assert.Equal(750,           s.ExpandedHeight);
        Assert.Equal(6,             s.RunBoxHotkeyModifiers);   // Ctrl+Shift
        Assert.Equal(0x20,          s.RunBoxHotkeyKey);         // VK_SPACE
        Assert.True(s.AutoCheckForUpdates);
    }

    // ── SchemaVersion ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void SchemaVersion_BelowOne_ClampsToCurrentVersion(int version)
    {
        var s = Valid(); s.SchemaVersion = version;
        SettingsValidator.Validate(s);
        Assert.Equal(SettingsValidator.CurrentSchemaVersion, s.SchemaVersion);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(99)]
    public void SchemaVersion_OneOrAbove_Preserved(int version)
    {
        var s = Valid(); s.SchemaVersion = version;
        SettingsValidator.Validate(s);
        Assert.Equal(version, s.SchemaVersion);
    }

    // ── ExpandedWidth clamping ────────────────────────────────────────────────

    [Fact]
    public void ExpandedWidth_BelowMin_ClampedTo560()
    {
        var s = Valid(); s.ExpandedWidth = 100;
        SettingsValidator.Validate(s);
        Assert.Equal(560, s.ExpandedWidth);
    }

    [Fact]
    public void ExpandedWidth_AboveMax_ClampedTo3840()
    {
        var s = Valid(); s.ExpandedWidth = 5000;
        SettingsValidator.Validate(s);
        Assert.Equal(3840, s.ExpandedWidth);
    }

    [Fact]
    public void ExpandedWidth_NaN_FallsBackToDefault()
    {
        var s = Valid(); s.ExpandedWidth = double.NaN;
        SettingsValidator.Validate(s);
        Assert.Equal(600, s.ExpandedWidth);
    }

    [Fact]
    public void ExpandedWidth_PositiveInfinity_FallsBackToDefault()
    {
        var s = Valid(); s.ExpandedWidth = double.PositiveInfinity;
        SettingsValidator.Validate(s);
        Assert.Equal(600, s.ExpandedWidth);
    }

    [Fact]
    public void ExpandedWidth_NegativeInfinity_FallsBackToDefault()
    {
        var s = Valid(); s.ExpandedWidth = double.NegativeInfinity;
        SettingsValidator.Validate(s);
        Assert.Equal(600, s.ExpandedWidth);
    }

    [Fact]
    public void ExpandedWidth_AtMin_Preserved()
    {
        var s = Valid(); s.ExpandedWidth = 560;
        SettingsValidator.Validate(s);
        Assert.Equal(560, s.ExpandedWidth);
    }

    [Fact]
    public void ExpandedWidth_AtMax_Preserved()
    {
        var s = Valid(); s.ExpandedWidth = 3840;
        SettingsValidator.Validate(s);
        Assert.Equal(3840, s.ExpandedWidth);
    }

    // ── ExpandedHeight clamping ──────────────────────────────────────────────

    [Fact]
    public void ExpandedHeight_AboveMax_ClampedTo3840()
    {
        var s = Valid(); s.ExpandedHeight = 5000;
        SettingsValidator.Validate(s);
        Assert.Equal(3840, s.ExpandedHeight);
    }

    [Fact]
    public void ExpandedHeight_NaN_FallsBackToDefault()
    {
        var s = Valid(); s.ExpandedHeight = double.NaN;
        SettingsValidator.Validate(s);
        Assert.Equal(497.33333, s.ExpandedHeight);
    }

    [Fact]
    public void ExpandedHeight_Negative_ClampedToMin()
    {
        var s = Valid(); s.ExpandedHeight = -100;
        SettingsValidator.Validate(s);
        Assert.Equal(497.33333, s.ExpandedHeight);
    }

    // ── RunBoxHotkey bounds ──────────────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    [InlineData(100)]
    public void RunBoxHotkeyModifiers_OutOfRange_FallsBackTo6(int mods)
    {
        var s = Valid(); s.RunBoxHotkeyModifiers = mods;
        SettingsValidator.Validate(s);
        Assert.Equal(6, s.RunBoxHotkeyModifiers);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(15)]
    public void RunBoxHotkeyModifiers_ValidRange_Preserved(int mods)
    {
        var s = Valid(); s.RunBoxHotkeyModifiers = mods;
        SettingsValidator.Validate(s);
        Assert.Equal(mods, s.RunBoxHotkeyModifiers);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(255)]
    [InlineData(1000)]
    public void RunBoxHotkeyKey_OutOfRange_FallsBackTo0x20(int key)
    {
        var s = Valid(); s.RunBoxHotkeyKey = key;
        SettingsValidator.Validate(s);
        Assert.Equal(0x20, s.RunBoxHotkeyKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(0x20)]
    [InlineData(254)]
    public void RunBoxHotkeyKey_ValidRange_Preserved(int key)
    {
        var s = Valid(); s.RunBoxHotkeyKey = key;
        SettingsValidator.Validate(s);
        Assert.Equal(key, s.RunBoxHotkeyKey);
    }

    // ── ConverterDefaultDirection ────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("XYtoZ")]
    public void ConverterDirection_InvalidValue_FallsBackToADtoBS(string? dir)
    {
        var s = Valid(); s.ConverterDefaultDirection = dir!;
        SettingsValidator.Validate(s);
        Assert.Equal("ADtoBS", s.ConverterDefaultDirection);
    }

    // ── CornerStyle ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Beveled")]
    [InlineData("")]
    [InlineData(null)]
    public void CornerStyle_InvalidValue_FallsBackToRounded(string? cs)
    {
        var s = Valid(); s.CornerStyle = cs!;
        SettingsValidator.Validate(s);
        Assert.Equal("Rounded", s.CornerStyle);
    }

    // ── Multiple validations are idempotent ──────────────────────────────────

    [Fact]
    public void Validate_CalledTwice_SameResult()
    {
        var s = new WidgetSettings
        {
            Language = "invalid",
            Theme = "",
            ExpandedWidth = -1,
            ExpandedHeight = double.NaN,
            LogMaxSizeMb = 999,
            RunBoxHotkeyModifiers = -5,
            RunBoxHotkeyKey = 999,
            HighlightedDays = null!,
            SchemaVersion = -1
        };

        SettingsValidator.Validate(s);

        var lang1 = s.Language;
        var theme1 = s.Theme;
        var w1 = s.ExpandedWidth;
        var h1 = s.ExpandedHeight;
        var log1 = s.LogMaxSizeMb;
        var mod1 = s.RunBoxHotkeyModifiers;
        var key1 = s.RunBoxHotkeyKey;
        var schema1 = s.SchemaVersion;

        SettingsValidator.Validate(s);

        Assert.Equal(lang1, s.Language);
        Assert.Equal(theme1, s.Theme);
        Assert.Equal(w1, s.ExpandedWidth);
        Assert.Equal(h1, s.ExpandedHeight);
        Assert.Equal(log1, s.LogMaxSizeMb);
        Assert.Equal(mod1, s.RunBoxHotkeyModifiers);
        Assert.Equal(key1, s.RunBoxHotkeyKey);
        Assert.Equal(schema1, s.SchemaVersion);
    }

    // ── All fields corrupt at once ───────────────────────────────────────────

    [Fact]
    public void AllFieldsCorrupt_ValidatorRepairsEverything()
    {
        var s = new WidgetSettings
        {
            SchemaVersion = -10,
            Language = "klingon",
            Theme = "Rainbow",
            BackgroundPreset = "Magenta",
            CornerStyle = "Beveled",
            ClockFormat = "6h",
            ConverterDefaultDirection = "XYtoZ",
            ExpandedWidth = double.NaN,
            ExpandedHeight = double.NegativeInfinity,
            LogMaxSizeMb = 200,
            RunBoxHotkeyModifiers = -99,
            RunBoxHotkeyKey = 999,
            HighlightedDays = null!
        };

        SettingsValidator.Validate(s);

        Assert.Equal(SettingsValidator.CurrentSchemaVersion, s.SchemaVersion);
        Assert.Equal("en", s.Language);
        Assert.Equal("Light", s.Theme);
        Assert.Equal("Forest", s.BackgroundPreset);
        Assert.Equal("Rounded", s.CornerStyle);
        Assert.Equal("12h", s.ClockFormat);
        Assert.Equal("ADtoBS", s.ConverterDefaultDirection);
        Assert.Equal(600, s.ExpandedWidth);
        Assert.Equal(497.33333, s.ExpandedHeight);
        Assert.Equal(10, s.LogMaxSizeMb);
        Assert.Equal(6, s.RunBoxHotkeyModifiers);
        Assert.Equal(0x20, s.RunBoxHotkeyKey);
        Assert.NotNull(s.HighlightedDays);
    }

    // ── NotificationDurationSeconds ──────────────────────────────────────────

    [Theory]
    [InlineData(4)]
    [InlineData(61)]
    [InlineData(-1)]
    [InlineData(0)]
    public void NotificationDuration_OutOfRange_ClampsTo10(int val)
    {
        var s = Valid(); s.NotificationDurationSeconds = val;
        SettingsValidator.Validate(s);
        Assert.Equal(10, s.NotificationDurationSeconds);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void NotificationDuration_ValidValue_Preserved(int val)
    {
        var s = Valid(); s.NotificationDurationSeconds = val;
        SettingsValidator.Validate(s);
        Assert.Equal(val, s.NotificationDurationSeconds);
    }

    // ── LastExpandedTab ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    [InlineData(100)]
    public void LastExpandedTab_OutOfRange_ClampsTo0(int val)
    {
        var s = Valid(); s.LastExpandedTab = val;
        SettingsValidator.Validate(s);
        Assert.Equal(0, s.LastExpandedTab);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    public void LastExpandedTab_ValidValue_Preserved(int val)
    {
        var s = Valid(); s.LastExpandedTab = val;
        SettingsValidator.Validate(s);
        Assert.Equal(val, s.LastExpandedTab);
    }

    // ── New settings in defaults check ────────────────────────────────────────

    [Fact]
    public void DefaultSettings_NewSettingsPassValidation()
    {
        var s = new WidgetSettings();
        SettingsValidator.Validate(s);

        Assert.Equal(10, s.NotificationDurationSeconds);
        Assert.True(s.NotificationSound);
        Assert.False(s.ShowSecondsInClock);
        Assert.False(s.ShowFiscalYear);
        Assert.Equal(0, s.LastExpandedTab);
        Assert.True(s.HideOnFullscreen);
    }
}
