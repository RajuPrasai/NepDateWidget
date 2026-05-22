namespace NepDateWidget.Models;

/// <summary>
/// A user-defined RunBox shortcut entry, persisted in shortcuts.json.
/// Each entry maps a keyword prefix to a URL template.
///
/// File location: same AppData folder as settings.json (AppPaths.ShortcutsPath).
///
/// Format rules:
///   - key:      non-empty, letters and digits only (e.g. "my", "shop2")
///   - url:      must contain exactly one {query} placeholder (e.g. "https://example.com/search?q={query}")
///               also supports {year} for the current year (same as built-in "ek")
///   - name:     display name shown in the "Search {name}..." hint (required for non-disabled entries)
///   - disabled: set to true to suppress a built-in shortcut without providing a replacement
///
/// Example shortcuts.json:
/// [
///   { "key": "shop", "url": "https://example.com/search?q={query}", "name": "My Shop" },
///   { "key": "fb",   "disabled": true }
/// ]
/// </summary>
public sealed class UserShortcut
{
    public string Key { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Name { get; set; }
    public bool Disabled { get; set; }
}
