namespace NepDateWidget.Services;

/// <summary>
/// Loads and saves transient runtime state from/to <c>runtime.json</c>.
/// </summary>
public interface IAppStateService
{
    NepDateWidget.Models.AppState Current { get; }
    void Load();
    void Save();
}
