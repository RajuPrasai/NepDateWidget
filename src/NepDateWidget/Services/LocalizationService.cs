namespace NepDateWidget.Services;

/// <summary>
/// Localization service backed by an embedded JSON resource (<c>Resources/strings.json</c>).
/// The JSON maps each key to a per-language dictionary: <c>{ "key": { "en": "...", "ne": "..." } }</c>.
/// Keys are stable, descriptive, and dot-separated by section.
///
/// Adding a new language:
///   1. Add the language entries to <c>strings.json</c>.
///   2. Call SetLanguage("xx") — everything updates automatically.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _strings;
    private string _language = "en";

    public LocalizationService()
    {
        var assembly = typeof(LocalizationService).Assembly;
        using var stream = assembly.GetManifestResourceStream("NepDateWidget.Resources.strings.json")
            ?? throw new InvalidOperationException("Embedded resource 'NepDateWidget.Resources.strings.json' not found. Build the project before running.");
        using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
        var json = reader.ReadToEnd();
        _strings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json)
            ?? throw new InvalidOperationException("Failed to deserialize localization strings from embedded resource.");
    }

    // ── ILocalizationService ──────────────────────────────────────────────────

    public string CurrentLanguage => _language;

    public string Get(string key)
    {
        if (key is null)
            return "[]";

        if (_strings.TryGetValue(key, out var langs))
        {
            // Try active language first, then English as fallback
            if (langs.TryGetValue(_language, out var text) && !string.IsNullOrEmpty(text))
                return text;
            if (langs.TryGetValue("en", out var fallback) && !string.IsNullOrEmpty(fallback))
                return fallback;
        }

        // Key not found - return the key itself so missing strings are obvious in testing
        return $"[{key}]";
    }

    public void SetLanguage(string languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
            _language = languageCode.ToLowerInvariant();
    }
}
