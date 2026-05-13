using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Unit tests for ThemeService state tracking.
/// WPF resource side-effects are skipped in non-WPF test context
/// (Application.Current is null, which ThemeService guards against).
/// </summary>
public class ThemeServiceTests
{
    // ── Defaults ──────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultTheme_IsDark()
    {
        var svc = new ThemeService();
        Assert.Equal("Dark", svc.CurrentTheme);
    }

    [Fact]
    public void DefaultPreset_IsDefault()
    {
        var svc = new ThemeService();
        Assert.Equal("Default", svc.CurrentPreset);
    }

    // ── Apply - theme ─────────────────────────────────────────────────────────

    [Fact]
    public void Apply_Dark_SetsCurrentThemeDark()
    {
        var svc = new ThemeService();
        svc.Apply("Dark", "Default");
        Assert.Equal("Dark", svc.CurrentTheme);
    }

    [Fact]
    public void Apply_Light_SetsCurrentThemeLight()
    {
        var svc = new ThemeService();
        svc.Apply("Light", "Default");
        Assert.Equal("Light", svc.CurrentTheme);
    }

    [Fact]
    public void Apply_UnknownTheme_FallsBackToDark()
    {
        var svc = new ThemeService();
        svc.Apply("Rainbow", "Default");
        Assert.Equal("Dark", svc.CurrentTheme);
    }

    // ── Apply - preset ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Default"), InlineData("Ocean"), InlineData("Forest"),
     InlineData("Sunset"), InlineData("Monochrome"),
     InlineData("Aurora"), InlineData("Cherry"), InlineData("Midnight"),
     InlineData("Slate"), InlineData("Ember")]
    public void Apply_AllPresets_Dark_DoesNotThrow(string preset)
    {
        var svc = new ThemeService();
        var ex  = Record.Exception(() => svc.Apply("Dark",  preset));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("Default"), InlineData("Ocean"), InlineData("Forest"),
     InlineData("Sunset"), InlineData("Monochrome"),
     InlineData("Aurora"), InlineData("Cherry"), InlineData("Midnight"),
     InlineData("Slate"), InlineData("Ember")]
    public void Apply_AllPresets_Light_DoesNotThrow(string preset)
    {
        var svc = new ThemeService();
        var ex  = Record.Exception(() => svc.Apply("Light", preset));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("Default"), InlineData("Ocean"), InlineData("Forest"),
     InlineData("Sunset"), InlineData("Monochrome"),
     InlineData("Aurora"), InlineData("Cherry"), InlineData("Midnight"),
     InlineData("Slate"), InlineData("Ember")]
    public void Apply_Dark_SetsCurrentPreset(string preset)
    {
        var svc = new ThemeService();
        svc.Apply("Dark", preset);
        Assert.Equal(preset, svc.CurrentPreset);
    }

    [Theory]
    [InlineData("Default"), InlineData("Ocean"), InlineData("Forest"),
     InlineData("Sunset"), InlineData("Monochrome"),
     InlineData("Aurora"), InlineData("Cherry"), InlineData("Midnight"),
     InlineData("Slate"), InlineData("Ember")]
    public void Apply_Light_SetsCurrentPreset(string preset)
    {
        var svc = new ThemeService();
        svc.Apply("Light", preset);
        Assert.Equal(preset, svc.CurrentPreset);
    }

    [Fact]
    public void Apply_UnknownPreset_FallsBackToDefault()
    {
        var svc = new ThemeService();
        svc.Apply("Dark", "Magenta");
        Assert.Equal("Default", svc.CurrentPreset);
    }

    // ── Apply - case insensitivity ────────────────────────────────────────────

    [Fact]
    public void Apply_ThemeCaseInsensitive()
    {
        var svc = new ThemeService();
        svc.Apply("light", "default");
        Assert.Equal("Light", svc.CurrentTheme);
    }

    [Fact]
    public void Apply_PresetCaseInsensitive()
    {
        var svc = new ThemeService();
        svc.Apply("Dark", "ocean");
        Assert.Equal("Ocean", svc.CurrentPreset);
    }

    // ── Apply - idempotent ────────────────────────────────────────────────────

    [Fact]
    public void Apply_SameThemeTwice_DoesNotThrow()
    {
        var svc = new ThemeService();
        svc.Apply("Dark", "Default");
        var ex = Record.Exception(() => svc.Apply("Dark", "Default"));
        Assert.Null(ex);
    }

    // ── All 20 theme+preset combinations ─────────────────────────────────────

    [Theory]
    [InlineData("Dark", "Aurora")]
    [InlineData("Light", "Aurora")]
    [InlineData("Dark", "Cherry")]
    [InlineData("Light", "Cherry")]
    [InlineData("Dark", "Midnight")]
    [InlineData("Light", "Midnight")]
    [InlineData("Dark", "Slate")]
    [InlineData("Light", "Slate")]
    [InlineData("Dark", "Ember")]
    [InlineData("Light", "Ember")]
    public void Apply_NewPresets_DoNotThrow(string theme, string preset)
    {
        var svc = new ThemeService();
        var ex = Record.Exception(() => svc.Apply(theme, preset));
        Assert.Null(ex);
        Assert.Equal(theme, svc.CurrentTheme);
        Assert.Equal(preset, svc.CurrentPreset);
    }

    // ── Sequential preset switches ───────────────────────────────────────────

    [Fact]
    public void Apply_SwitchingPresets_Rapidly_DoesNotThrow()
    {
        var svc = new ThemeService();
        var presets = new[] { "Default", "Ocean", "Forest", "Sunset", "Monochrome",
                              "Aurora", "Cherry", "Midnight", "Slate", "Ember" };
        foreach (var p in presets)
        {
            svc.Apply("Dark", p);
            svc.Apply("Light", p);
        }
        Assert.Equal("Light", svc.CurrentTheme);
        Assert.Equal("Ember", svc.CurrentPreset);
    }

    // ── Theme switch preserves preset ────────────────────────────────────────

    [Fact]
    public void Apply_SwitchTheme_PreservesPreset()
    {
        var svc = new ThemeService();
        svc.Apply("Dark", "Ocean");
        Assert.Equal("Ocean", svc.CurrentPreset);

        svc.Apply("Light", "Ocean");
        Assert.Equal("Ocean", svc.CurrentPreset);
        Assert.Equal("Light", svc.CurrentTheme);
    }
}
