using NepDateWidget.Helpers;
using System.IO;
using System.Text.Json;

namespace NepDateWidget.Services;

public sealed class SearchHistoryService : ISearchHistoryService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _filePath;
    private List<string> _history = new();

    public SearchHistoryService(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
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

        // Cap at 100 entries
        if (_history.Count > 100)
            _history.RemoveRange(100, _history.Count - 100);

        Save();
    }

    public void Load()
    {
        if (!File.Exists(_filePath)) return;
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
}
