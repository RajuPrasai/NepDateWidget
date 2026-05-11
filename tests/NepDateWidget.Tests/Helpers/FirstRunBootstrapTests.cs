using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;

namespace NepDateWidget.Tests.Helpers;

/// <summary>
/// Behavioural contract for the first-run bootstrap helper. The helper owns a
/// small but important rule: enable Start with Windows the first time the app
/// boots on a fresh install, then never override the user's choice again.
/// </summary>
public class FirstRunBootstrapTests
{
    /// <summary>
    /// Minimal in-memory ISettingsService that tracks Save() calls. Mirrors
    /// the production service's IsFirstLaunch contract (true only when the
    /// settings file did not exist on disk before the current load).
    /// </summary>
    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly WidgetSettings _current = new();
        public WidgetSettings Current => _current;
        public bool IsFirstLaunch { get; set; }
        public int SaveCount { get; private set; }

        public void Load() { /* no-op */ }
        public void Save() => SaveCount++;
        public void ResetToDefaults() { /* no-op */ }
        public event EventHandler? SettingsChanged;
    }

    [Fact]
    public void IsDev_SkipsAllRegistryWork()
    {
        var settings = new FakeSettingsService { IsFirstLaunch = true };
        settings.Current.AutoStart = false;
        var auto = new FakeAutoStartService(initialState: false);

        var action = FirstRunBootstrap.ApplyAutoStart(settings, auto, isDev: true);

        Assert.Equal(FirstRunBootstrap.AutoStartAction.SkippedDevMode, action);
        Assert.Equal(0, auto.SetEnabledCount);
        Assert.Equal(0, settings.SaveCount);
        Assert.False(auto.IsEnabled);
    }

    [Fact]
    public void FirstLaunch_ForcesAutoStartOn_AndPersistsSetting()
    {
        var settings = new FakeSettingsService { IsFirstLaunch = true };
        // Even if the persisted default were false (e.g. a future change to
        // WidgetSettings), first-run should still force it on.
        settings.Current.AutoStart = false;
        var auto = new FakeAutoStartService(initialState: false);

        var action = FirstRunBootstrap.ApplyAutoStart(settings, auto, isDev: false);

        Assert.Equal(FirstRunBootstrap.AutoStartAction.EnabledOnFirstRun, action);
        Assert.True(auto.IsEnabled);
        Assert.True(settings.Current.AutoStart);
        Assert.Equal(1, auto.SetEnabledCount);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public void SubsequentLaunch_RegistryAlreadyMatches_DoesNothing()
    {
        var settings = new FakeSettingsService { IsFirstLaunch = false };
        settings.Current.AutoStart = true;
        var auto = new FakeAutoStartService(initialState: true);

        var action = FirstRunBootstrap.ApplyAutoStart(settings, auto, isDev: false);

        Assert.Equal(FirstRunBootstrap.AutoStartAction.AlreadyInSync, action);
        Assert.Equal(0, auto.SetEnabledCount);
        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public void SubsequentLaunch_RegistryStale_ReconcilesToSettings()
    {
        // Post-uninstall reinstall: settings says enabled but registry was wiped.
        var settings = new FakeSettingsService { IsFirstLaunch = false };
        settings.Current.AutoStart = true;
        var auto = new FakeAutoStartService(initialState: false);

        var action = FirstRunBootstrap.ApplyAutoStart(settings, auto, isDev: false);

        Assert.Equal(FirstRunBootstrap.AutoStartAction.ReconciledToSettings, action);
        Assert.True(auto.IsEnabled);
        Assert.Equal(1, auto.SetEnabledCount);
        // Reconcile must NOT bump SaveCount: the persisted value did not change.
        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public void SubsequentLaunch_UserDisabled_DoesNotReEnable()
    {
        // Critical regression guard: a returning user who toggled autostart
        // off must keep that choice. Previously a buggy bootstrap could see
        // settings=false, registry=anything and re-enable; we must not.
        var settings = new FakeSettingsService { IsFirstLaunch = false };
        settings.Current.AutoStart = false;
        var auto = new FakeAutoStartService(initialState: true); // stale enabled entry

        var action = FirstRunBootstrap.ApplyAutoStart(settings, auto, isDev: false);

        Assert.Equal(FirstRunBootstrap.AutoStartAction.ReconciledToSettings, action);
        Assert.False(auto.IsEnabled); // reconciled OFF, honouring the user's choice
        Assert.False(settings.Current.AutoStart);
        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public void FirstLaunch_RegistryAlreadyOn_StillFlowsThroughSetEnabled()
    {
        // Edge case: a partial install left an old registry entry but the
        // settings file was missing. We still want a full pass through
        // SetEnabled(true) so the entry gets rewritten with the current EXE
        // path and a Save() persists the (already-default) AutoStart=true.
        var settings = new FakeSettingsService { IsFirstLaunch = true };
        settings.Current.AutoStart = true;
        var auto = new FakeAutoStartService(initialState: true);

        var action = FirstRunBootstrap.ApplyAutoStart(settings, auto, isDev: false);

        Assert.Equal(FirstRunBootstrap.AutoStartAction.EnabledOnFirstRun, action);
        Assert.Equal(1, auto.SetEnabledCount);
        Assert.Equal(1, settings.SaveCount);
        Assert.True(auto.IsEnabled);
    }
}
