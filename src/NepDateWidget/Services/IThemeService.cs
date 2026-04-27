namespace NepDateWidget.Services;

/// <summary>
/// Applies a theme (dark/light) and background preset combination to the running
/// application by updating named resources at runtime.
/// </summary>
public interface IThemeService
{
    /// <summary>Active base theme: "Dark" or "Light".</summary>
    string CurrentTheme { get; }

    /// <summary>Active background preset name.</summary>
    string CurrentPreset { get; }

    /// <summary>
    /// Applies the given theme and preset immediately.
    /// Both parameters are validated; invalid values fall back to defaults.
    /// </summary>
    void Apply(string theme, string preset);

    /// <summary>
    /// Overrides WidgetDaySaturdayBrush with a custom color that persists through theme changes.
    /// Pass an empty string to clear the override and revert to the theme default.
    /// </summary>
    void OverrideHighlightColor(string colorHex);
}
