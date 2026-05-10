using System.Windows;
using System.Windows.Media;

namespace NepDateWidget.Services;

/// <summary>
/// Applies theme (Dark/Light) × background preset combinations at runtime by
/// directly updating named <see cref="SolidColorBrush"/> entries in
/// <see cref="Application.Current.Resources"/>.
///
/// All 15 semantic widget brushes are recomputed from just three source values:
///   • BackgroundColor  - main surface
///   • ForegroundColor  - primary text
///   • AccentColor      - today highlight, active controls
///
/// The remaining brushes are derived algorithmically so every theme combination
/// stays consistent without a separate XAML file per combo.
/// </summary>
public sealed class ThemeService : IThemeService
{
    // ── Palette definitions ───────────────────────────────────────────────────

    private sealed record Palette(Color Background, Color Foreground, Color Accent);

    // Dark palettes
    private static readonly Dictionary<string, Palette> DarkPalettes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Default"] = new(C("#1E1E2E"), C("#CDD6F4"), C("#89B4FA")),
        ["Ocean"] = new(C("#0D1B2A"), C("#A8DADC"), C("#48CAE4")),
        ["Forest"] = new(C("#1A2F1E"), C("#B5E48C"), C("#74C69D")),
        ["Sunset"] = new(C("#2D1B1B"), C("#FAD2A0"), C("#F4A261")),
        ["Monochrome"] = new(C("#1A1A1A"), C("#E0E0E0"), C("#9E9E9E")),
        ["Aurora"] = new(C("#0D1117"), C("#C9D1D9"), C("#58A6FF")),
        ["Cherry"] = new(C("#1A0A0F"), C("#F7C5D0"), C("#F06292")),
        ["Midnight"] = new(C("#05060F"), C("#A0AEC0"), C("#7F9CF5")),
        ["Slate"] = new(C("#1C232E"), C("#CBD5E0"), C("#63B3ED")),
        ["Ember"] = new(C("#1C1208"), C("#F6E6C8"), C("#ED8936")),
    };

    // Light palettes
    private static readonly Dictionary<string, Palette> LightPalettes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Default"] = new(C("#FFFBFE"), C("#1C1B1F"), C("#6750A4")),
        ["Ocean"] = new(C("#E8F4F8"), C("#0D3E60"), C("#0077B6")),
        ["Forest"] = new(C("#EFF6EC"), C("#1A3E1C"), C("#2D6A4F")),
        ["Sunset"] = new(C("#FFF3E0"), C("#5C3317"), C("#E76F51")),
        ["Monochrome"] = new(C("#F5F5F5"), C("#212121"), C("#616161")),
        ["Aurora"] = new(C("#EBF4FF"), C("#1A365D"), C("#2B6CB0")),
        ["Cherry"] = new(C("#FFF0F3"), C("#6B1526"), C("#D53F8C")),
        ["Midnight"] = new(C("#EEF2FF"), C("#1E1B4B"), C("#4F46E5")),
        ["Slate"] = new(C("#F0F4F8"), C("#1A2332"), C("#3182CE")),
        ["Ember"] = new(C("#FFFAF0"), C("#7B341E"), C("#DD6B20")),
    };

    // ── State ─────────────────────────────────────────────────────────────────

    public string CurrentTheme { get; private set; } = "Dark";
    public string CurrentPreset { get; private set; } = "Default";

    private string _highlightColorOverride = string.Empty;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Apply(string theme, string preset)
    {
        bool isDark = !string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        CurrentTheme = isDark ? "Dark" : "Light";

        var palettes = isDark ? DarkPalettes : LightPalettes;

        // Find the canonical key (preserving original casing from the dictionary)
        string? canonicalKey = palettes.Keys.FirstOrDefault(k =>
            string.Equals(k, preset, StringComparison.OrdinalIgnoreCase));

        if (canonicalKey is null)
            canonicalKey = "Default";

        CurrentPreset = canonicalKey;
        ApplyPalette(isDark, palettes[canonicalKey]);

        // Re-apply user color override so theme changes don't revert it
        if (!string.IsNullOrEmpty(_highlightColorOverride))
            ApplyHighlightColorOverride(_highlightColorOverride);
    }

    public void OverrideHighlightColor(string colorHex)
    {
        _highlightColorOverride = colorHex ?? string.Empty;
        if (string.IsNullOrEmpty(_highlightColorOverride)) return;
        ApplyHighlightColorOverride(_highlightColorOverride);
    }

    private static void ApplyHighlightColorOverride(string colorHex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            SetBrush("WidgetDaySaturdayBrush", color);
        }
        catch (FormatException) { /* ignore invalid hex values */ }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void ApplyPalette(bool isDark, Palette p)
    {
        var bg = p.Background;
        var fg = p.Foreground;
        var acc = p.Accent;

        // Derived colours computed algorithmically
        Color border = Blend(bg, fg, 0.20f);
        Color hover = Blend(bg, fg, 0.10f);
        Color headerHover = hover;
        Color input = Blend(bg, fg, 0.08f);
        Color inputBdr = Blend(bg, fg, 0.25f);
        Color divider = Blend(bg, fg, 0.18f);

        // Saturday / holiday
        Color holiday = isDark ? C("#F38BA8") : C("#C62828");
        // Success (converter result)
        Color success = isDark ? C("#A6E3A1") : C("#2E7D32");
        // Error
        Color error = holiday;

        // Today cell text (must be readable on the accent background)
        Color todayText = IsLight(acc) ? C("#1C1C1C") : C("#FFFFFF");

        // Padding cells dimmed
        Color padding = Blend(bg, fg, 0.30f);

        SetBrush("WidgetBackgroundBrush", bg);
        SetBrush("WidgetForegroundBrush", fg);
        SetBrush("WidgetBorderBrush", border);
        SetBrush("WidgetAccentBrush", acc);
        SetBrush("WidgetHolidayBrush", holiday);
        SetBrush("WidgetHoverBrush", hover);
        SetBrush("WidgetCalHeaderHoverBrush", headerHover);
        SetBrush("WidgetDayTodayBrush", acc);
        SetBrush("WidgetDayTodayTextBrush", todayText);
        SetBrush("WidgetDaySaturdayBrush", holiday);
        SetBrush("WidgetDayHighlightedBrush", success);
        SetBrush("WidgetDayWeekendTintBrush", Color.FromArgb(0x10, holiday.R, holiday.G, holiday.B));
        SetBrush("WidgetDayPaddingBrush", Colors.Transparent);
        SetBrush("WidgetDayPaddingTextBrush", padding);
        SetBrush("WidgetInputBrush", input);
        SetBrush("WidgetInputBorderBrush", inputBdr);
        SetBrush("WidgetSuccessBrush", success);
        SetBrush("WidgetErrorBrush", error);
        SetBrush("WidgetDividerBrush", divider);

        // Muted foreground - toolbar icons, secondary text
        Color muted = Blend(bg, fg, 0.40f);
        SetBrush("WidgetMutedForegroundBrush", muted);

        // Calendar grid lines - very subtle, just enough to separate cells
        Color gridLine = Blend(bg, fg, 0.08f);
        SetBrush("WidgetGridLineBrush", gridLine);
    }

    /// <summary>
    /// Updates an existing <see cref="SolidColorBrush"/> in Application resources
    /// so all <c>DynamicResource</c> bindings refresh without a full resource swap.
    /// Falls back to adding the key if it does not exist yet (handles tests where
    /// no Application is running).
    /// </summary>
    private static void SetBrush(string key, Color color)
    {
        if (Application.Current is null)
            return;   // unit-test context - skip WPF resource update

        // Always replace with a new mutable brush so that DynamicResource bindings
        // re-evaluate via ResourceDictionary.Changed notification.
        // Mutating an existing brush's Color property does NOT trigger that notification,
        // meaning DynamicResource-bound elements would not update immediately.
        Application.Current.Resources[key] = new SolidColorBrush(color);
    }

    /// <summary>Linearly blends <paramref name="a"/> toward <paramref name="b"/> by <paramref name="t"/> (0-1).</summary>
    private static Color Blend(Color a, Color b, float t)
    {
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    /// <summary>Returns true if the colour is perceptually light (luminance > 0.5).</summary>
    private static bool IsLight(Color c)
    {
        // Relative luminance (sRGB approximation)
        double r = c.R / 255.0;
        double g = c.G / 255.0;
        double b = c.B / 255.0;
        double luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return luminance > 0.65;
    }

    /// <summary>Parses a hex color string like "#1E1E2E" or "#ABC".</summary>
    private static Color C(string hex)
        => (Color)ColorConverter.ConvertFromString(hex);
}
