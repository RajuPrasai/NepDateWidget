using NepDateWidget.Models;

namespace NepDateWidget.Services;

/// <summary>
/// Validates and repairs a <see cref="WidgetSettings"/> instance in-place.
/// All rules are stateless so this class is fully unit-testable without disk I/O.
/// Called by <see cref="SettingsService"/> after every Load and before every Save.
/// </summary>
public static class SettingsValidator
{
    // Allowed string enum values
    private static readonly IReadOnlySet<string> ValidLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "en", "ne" };
    private static readonly IReadOnlySet<string> ValidThemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Dark", "Light" };
    private static readonly IReadOnlySet<string> ValidCorners = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Rounded", "Sharp" };
    private static readonly IReadOnlySet<string> ValidClockFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "12h", "24h" };
    private static readonly IReadOnlySet<string> ValidPresets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Default", "Ocean", "Forest", "Sunset", "Monochrome",
        "Aurora",  "Cherry", "Midnight", "Slate", "Ember"
    };
    private static readonly IReadOnlySet<string> ValidFonts = new HashSet<string>(StringComparer.Ordinal)
    {
        "Segoe UI", "Calibri", "Verdana",
        "Inter", "Source Sans 3", "IBM Plex Sans", "Roboto", "Noto Sans",
        "Cascadia Code",
        "Poppins", "Lato", "Montserrat", "Open Sans", "Raleway", "Nunito",
        "Rubik", "DM Sans", "Work Sans", "Quicksand", "Imprima",
    };

    // Window size bounds
    private const double MinWindowDim = 100.0;
    private const double MaxWindowDim = 3840.0;
    public const int MaxTabIndex = 8;
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Validates all fields of <paramref name="s"/>, replacing any invalid value with
    /// the documented safe default.  Operates in-place; nothing is thrown.
    /// </summary>
    public static void Validate(WidgetSettings s)
    {
        // Schema version - values below 1 are corrupt; reset to current.
        // Values above CurrentSchemaVersion are from a future app version; preserve.
        if (s.SchemaVersion < 1)
            s.SchemaVersion = CurrentSchemaVersion;

        // Window position - allow any value including negatives (multi-monitor);
        // off-screen recovery is handled by ScreenBoundsHelper at startup.

        // Window dimensions
        s.ExpandedWidth  = Clamp(s.ExpandedWidth,  560,       MaxWindowDim, 600);
        s.ExpandedHeight = Clamp(s.ExpandedHeight, 497.33333, MaxWindowDim, 497.33333);

        // String enums
        s.Language = ValidOrDefault(s.Language, ValidLanguages, "en");
        s.Theme = ValidOrDefault(s.Theme, ValidThemes, "Light");
        s.BackgroundPreset = ValidOrDefault(s.BackgroundPreset, ValidPresets, "Default");
        s.CornerStyle = ValidOrDefault(s.CornerStyle, ValidCorners, "Rounded");
        s.ClockFormat = ValidOrDefault(s.ClockFormat, ValidClockFormats, "12h");
        s.FontFamily = ValidOrDefault(s.FontFamily, ValidFonts, "Open Sans");

        // Log size: clamp to supported range
        if (s.LogMaxSizeMb < 5 || s.LogMaxSizeMb > 100)
            s.LogMaxSizeMb = 10;

        // Hotkey modifiers: must be a valid combination (0 = disabled, or 1..15)
        if (s.RunBoxHotkeyModifiers < 0 || s.RunBoxHotkeyModifiers > 15)
            s.RunBoxHotkeyModifiers = 6; // Ctrl+Shift

        // Hotkey key: must be a valid virtual key code (0 = disabled, or 1..254)
        if (s.RunBoxHotkeyKey < 0 || s.RunBoxHotkeyKey > 254)
            s.RunBoxHotkeyKey = 0x20; // VK_SPACE

        // Notification duration: 5-60 seconds
        if (s.NotificationDurationSeconds < 5 || s.NotificationDurationSeconds > 60)
            s.NotificationDurationSeconds = 10;

        // Last expanded tab: 0-MaxTabIndex
        if (s.LastExpandedTab < 0 || s.LastExpandedTab > MaxTabIndex)
            s.LastExpandedTab = 0;

    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static double Clamp(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return fallback;
        return Math.Max(min, Math.Min(max, value));
    }

    private static string ValidOrDefault(string? value, IReadOnlySet<string> allowed, string fallback)
    {
        if (value is not null && allowed.Contains(value))
            return value;
        return fallback;
    }
}
