using NepDateWidget.Helpers;
using NepDateWidget.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NepDateWidget.Services;

/// <summary>
/// Loads and saves <see cref="AppState"/> from/to <c>runtime.json</c>.
/// Corrupted or missing files fall back to a default state (never crash).
/// Writes are atomic via <see cref="AtomicFile"/>.
/// </summary>
public sealed class AppStateService : IAppStateService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;
    private AppState _current = new();

    public AppStateService(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));
        _path = path;
    }

    public AppState Current => _current;

    public void Load()
    {
        if (!File.Exists(_path))
        {
            _current = new();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            _current = JsonSerializer.Deserialize<AppState>(json, SerializerOptions) ?? new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Warn($"runtime.json: load failed ({ex.GetType().Name}); defaults used.");
            _current = new();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_current, SerializerOptions);
            if (!AtomicFile.WriteAllText(_path, json))
                Log.Warn("runtime.json: AtomicFile.WriteAllText returned false");
        }
        catch (Exception ex)
        {
            Log.Warn($"runtime.json: save failed: {ex.Message}");
        }
    }
}
