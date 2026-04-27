using NepDateWidget.Models;

namespace NepDateWidget.Services;

/// <summary>
/// Defines all operations for loading, saving, and resetting user settings.
/// Implementations must not throw for corrupted or missing files - they must recover to defaults.
/// </summary>
public interface ISettingsService
{
    /// <summary>Returns the current in-memory settings. Never null.</summary>
    WidgetSettings Current { get; }

    /// <summary>
    /// True when <see cref="Load"/> found no settings file on disk (i.e. first ever launch).
    /// Remains false for all subsequent loads.
    /// </summary>
    bool IsFirstLaunch { get; }

    /// <summary>
    /// Loads settings from disk. If the file is missing or corrupted, returns defaults.
    /// </summary>
    void Load();

    /// <summary>
    /// Persists current settings to disk. 
    /// </summary>
    void Save();

    /// <summary>
    /// Resets all settings to factory defaults and saves immediately.
    /// </summary>
    void ResetToDefaults();
}
