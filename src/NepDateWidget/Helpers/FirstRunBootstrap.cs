using NepDateWidget.Services;

namespace NepDateWidget.Helpers;

/// <summary>
/// One-time onboarding step: when the settings file did not exist on disk
/// before this launch (i.e. <see cref="ISettingsService.IsFirstLaunch"/>),
/// force the Windows "Start with Windows" registry entry on so the widget
/// behaves like a system applet from day one. Idempotent: subsequent runs
/// fall back to plain user-toggle reconciliation.
///
/// Kept as a static helper rather than a service because it has no state and
/// runs exactly once per process during App startup. Returning the action
/// taken makes it trivially unit-testable without mocking App.xaml.cs.
/// </summary>
internal static class FirstRunBootstrap
{
    public enum AutoStartAction
    {
        SkippedDevMode,
        EnabledOnFirstRun,
        ReconciledToSettings,
        AlreadyInSync,
    }

    /// <summary>
    /// Applies the first-run autostart policy. Caller decides whether to log
    /// or persist; this helper performs the registry write and (on first run)
    /// flips the persisted setting.
    /// </summary>
    public static AutoStartAction ApplyAutoStart(
        ISettingsService settings,
        IAutoStartService autoStart,
        bool isDev)
    {
        if (isDev) return AutoStartAction.SkippedDevMode;

        if (settings.IsFirstLaunch)
        {
            // Force-enable. The user has never seen the app before; we want
            // it auto-launching tomorrow morning. They can opt out from
            // Settings if they prefer manual launches.
            settings.Current.AutoStart = true;
            autoStart.SetEnabled(true);
            settings.Save();
            return AutoStartAction.EnabledOnFirstRun;
        }

        // Subsequent run: reconcile registry to the persisted choice. Covers
        // post-uninstall reinstall (settings true, registry empty) and the
        // user toggling off (settings false, registry stale entry).
        if (autoStart.IsEnabled != settings.Current.AutoStart)
        {
            autoStart.SetEnabled(settings.Current.AutoStart);
            return AutoStartAction.ReconciledToSettings;
        }

        return AutoStartAction.AlreadyInSync;
    }
}
