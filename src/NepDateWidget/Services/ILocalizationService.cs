namespace NepDateWidget.Services;

/// <summary>
/// Returns localized UI strings for the active language.
/// All user-visible text flows through this service - never hardcoded in views.
/// </summary>
public interface ILocalizationService
{
    /// <summary>The currently active language code ("en" or "ne").</summary>
    string CurrentLanguage { get; }

    /// <summary>
    /// Returns the localized string for the given key.
    /// Falls back to the English string if the key is missing in the active language.
    /// Never returns null or throws.
    /// </summary>
    string Get(string key);

    /// <summary>
    /// Changes the active language. All subsequent Get() calls reflect the new language.
    /// </summary>
    void SetLanguage(string languageCode);
}
