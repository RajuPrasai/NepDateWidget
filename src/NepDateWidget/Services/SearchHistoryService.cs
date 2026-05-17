using NepDateWidget.Helpers;
using System.IO;
using System.Text.Json;

namespace NepDateWidget.Services;

public sealed class SearchHistoryService : ISearchHistoryService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly int _maxEntries;
    private readonly string? _defaultFilePath;
    private List<string> _history = new();

    public SearchHistoryService(string filePath, int maxEntries = 100, string? defaultFilePath = null)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _maxEntries = maxEntries > 0 ? maxEntries : 100;
        _defaultFilePath = defaultFilePath;
    }

    public IReadOnlyList<string> GetMatching(string prefix, int max = 10)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return _history.Take(max).ToList();
        }

        return _history
            .Where(h => h.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .ToList();
    }

    public void Record(string term)
    {
        term = term.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return;
        }

        // Remove existing entry (dedup), add to front
        _history.RemoveAll(h => string.Equals(h, term, StringComparison.OrdinalIgnoreCase));
        _history.Insert(0, term);

        // Cap at configured limit
        if (_history.Count > _maxEntries)
        {
            _history.RemoveRange(_maxEntries, _history.Count - _maxEntries);
        }

        Save();
    }

    public void Remove(string term)
    {
        term = term.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return;
        }

        int removed = _history.RemoveAll(h => string.Equals(h, term, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            Save();
        }
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            if (_defaultFilePath is not null)
            {
                SeedFromDefaults();
            }

            Save();
            return;
        }
        try
        {
            var json = File.ReadAllText(_filePath);
            _history = JsonSerializer.Deserialize<List<string>>(json, Options) ?? new();
        }
        catch (Exception ex)
        {
            Log.Error("Failed to load search history", ex);
            _history = new();
        }
        MergeNewDefaults();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history, Options);
            if (!AtomicFile.WriteAllText(_filePath, json))
            {
                Log.Error("Failed to save search history (atomic write returned false)");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Failed to save search history", ex);
        }
    }

    /// <summary>
    /// Loads the default run-history entries from the given file path.
    /// Returns an empty list if the file is absent or the JSON is malformed.
    /// </summary>
    internal static IReadOnlyList<string> LoadDefaultEntries(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Array.Empty<string>();
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<string>>(json, Options) ?? new();
        }
        catch (Exception ex)
        {
            Log.Warn($"SearchHistoryService: failed to load defaults from '{filePath}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private void SeedFromDefaults()
    {
        var defaults = LoadDefaultEntries(_defaultFilePath!);
        _history = defaults.Take(_maxEntries).ToList();
    }

    /// <summary>
    /// Appends to the history any entry from the default file that is not already
    /// present (case-insensitive). Writes to disk if new entries were added.
    /// Runs at every launch on existing files so updates automatically seed new commands.
    /// </summary>
    private void MergeNewDefaults()
    {
        if (_defaultFilePath is null)
        {
            return;
        }

        var defaults = LoadDefaultEntries(_defaultFilePath);
        if (defaults.Count == 0)
        {
            return;
        }

        var existingSet = new HashSet<string>(_history, StringComparer.OrdinalIgnoreCase);
        var toAdd = defaults.Where(d => !existingSet.Contains(d)).ToList();
        if (toAdd.Count == 0)
        {
            return;
        }

        _history.AddRange(toAdd);

        // Cap at max entries - newly added defaults have lowest priority so they get dropped first.
        if (_history.Count > _maxEntries)
        {
            _history.RemoveRange(_maxEntries, _history.Count - _maxEntries);
        }

        Save();
    }
}
