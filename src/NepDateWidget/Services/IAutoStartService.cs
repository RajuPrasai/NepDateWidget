namespace NepDateWidget.Services;

/// <summary>
/// Manages the Windows "Start with Windows" startup entry for the widget.
/// Velopack / portable channel: HKCU registry Run key.
/// MSIX / Store channel: Windows.ApplicationModel.StartupTask.
/// </summary>
public interface IAutoStartService
{
    /// <summary>Returns true if the startup registry entry currently exists.</summary>
    bool IsEnabled { get; }

    /// <summary>Creates or removes the startup entry to match <paramref name="enable"/>.</summary>
    void SetEnabled(bool enable);

    /// <summary>
    /// If autostart is enabled and the stored EXE path no longer matches the
    /// current EXE path (after a move or update), rewrite it. Best-effort.
    /// </summary>
    void RefreshIfStale();
}
