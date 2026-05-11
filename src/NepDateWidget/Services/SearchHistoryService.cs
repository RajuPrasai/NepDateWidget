using NepDateWidget.Helpers;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace NepDateWidget.Services;

public sealed class SearchHistoryService : ISearchHistoryService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly int _maxEntries;
    private readonly string? _defaultResourceName;
    private List<string> _history = new();

    public SearchHistoryService(string filePath, int maxEntries = 100, string? defaultResourceName = null)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _maxEntries = maxEntries > 0 ? maxEntries : 100;
        _defaultResourceName = defaultResourceName;
    }

    public IReadOnlyList<string> GetMatching(string prefix, int max = 10)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return _history.Take(max).ToList();

        return _history
            .Where(h => h.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Take(max)
            .ToList();
    }

    public void Record(string term)
    {
        term = term.Trim();
        if (string.IsNullOrEmpty(term)) return;

        // Remove existing entry (dedup), add to front
        _history.RemoveAll(h => string.Equals(h, term, StringComparison.OrdinalIgnoreCase));
        _history.Insert(0, term);

        // Cap at configured limit
        if (_history.Count > _maxEntries)
            _history.RemoveRange(_maxEntries, _history.Count - _maxEntries);

        Save();
    }

    public void Remove(string term)
    {
        term = term.Trim();
        if (string.IsNullOrEmpty(term)) return;
        int removed = _history.RemoveAll(h => string.Equals(h, term, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) Save();
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            if (_defaultResourceName is not null) SeedFromDefaults();
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
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_history, Options);
            if (!AtomicFile.WriteAllText(_filePath, json))
                Log.Error("Failed to save search history (atomic write returned false)");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to save search history", ex);
        }
    }

    /// <summary>
    /// Loads the embedded default entries for the given resource name.
    /// Returns an empty list if the resource is absent or the JSON is malformed.
    /// </summary>
    internal static IReadOnlyList<string> LoadDefaultEntries(string resourceName)
    {
        try
        {
            var asm = typeof(SearchHistoryService).Assembly;
            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<List<string>>(json, Options) ?? new();
        }
        catch (Exception ex)
        {
            Log.Warn($"SearchHistoryService: failed to load defaults from '{resourceName}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private void SeedFromDefaults()
    {
        var defaults = LoadDefaultEntries(_defaultResourceName!);
        _history = defaults.Take(_maxEntries).ToList();
    }
}
