namespace NepDateWidget.Services;

/// <summary>
/// Provides the merged set of RunBox prefix shortcuts (built-ins + user overrides from shortcuts.json).
/// Implementations must support hot-reload: raise <see cref="ShortcutsChanged"/> whenever the active
/// set changes so callers can update their state without an app restart.
/// </summary>
public interface IShortcutsService
{
    /// <summary>
    /// Merged prefix→URL dictionary. Built-in shortcuts merged with user shortcuts.json.
    /// User entries win on key collision. Disabled user entries remove the built-in.
    /// Values use {0} as the query placeholder (already normalized from {query}).
    /// May also contain {year} tokens handled by BuildPrefixUrl at call time.
    /// </summary>
    IReadOnlyDictionary<string, string> Prefixes { get; }

    /// <summary>
    /// Merged prefix→display-name dictionary, parallel to <see cref="Prefixes"/>.
    /// Used for the "Search {site}..." hint label.
    /// </summary>
    IReadOnlyDictionary<string, string> PrefixSiteNames { get; }

    /// <summary>
    /// Raised on the UI thread (via captured SynchronizationContext) when shortcuts.json
    /// is created, modified, or deleted. Callers should refresh their state on this event.
    /// </summary>
    event EventHandler? ShortcutsChanged;

    /// <summary>
    /// Loads shortcuts.json from disk (if it exists) and sets up the FileSystemWatcher
    /// for subsequent hot-reloads. Safe to call multiple times; subsequent calls re-load.
    /// </summary>
    void Load();
}
