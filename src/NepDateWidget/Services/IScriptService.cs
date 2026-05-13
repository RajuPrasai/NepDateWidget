using NepDateWidget.Models;

namespace NepDateWidget.Services;

/// <summary>
/// Provides access to user-defined script commands loaded from scripts.json.
/// The file is watched for hot-reload: <see cref="ScriptsChanged"/> fires on the UI
/// thread whenever the list changes.
/// </summary>
public interface IScriptService
{
    /// <summary>All loaded, valid script entries (empty name/path filtered out).</summary>
    IReadOnlyList<ScriptEntry> GetAll();

    /// <summary>Finds a script by name (case-insensitive). Returns null if not found.</summary>
    ScriptEntry? Find(string name);

    /// <summary>Loads scripts.json from disk and starts the file watcher.</summary>
    void Load();

    /// <summary>Raised on the UI thread when scripts.json is created, changed, or deleted.</summary>
    event EventHandler? ScriptsChanged;
}
