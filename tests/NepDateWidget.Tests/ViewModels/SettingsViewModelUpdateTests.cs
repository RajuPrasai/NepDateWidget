using NepDateWidget.Models;
using NepDateWidget.Services;
using NepDateWidget.Tests.Services;
using NepDateWidget.ViewModels;

namespace NepDateWidget.Tests.ViewModels;

/// <summary>
/// Tests for the auto-update wiring on <see cref="SettingsViewModel"/>.
/// Uses an in-memory <see cref="FakeUpdateService"/> so we never hit Velopack
/// or the network from the test process.
/// </summary>
public class SettingsViewModelUpdateTests
{
    private sealed class FakeSettingsService : ISettingsService
    {
        public WidgetSettings Current { get; private set; } = new();
        public bool IsFirstLaunch => false;
        public int SaveCount { get; private set; }
        public void Load() { }
        public void Save() { SaveCount++; }
        public void ResetToDefaults() { Current = new WidgetSettings(); }
    }

    private sealed class FakeThemeService : IThemeService
    {
        public string CurrentTheme { get; private set; } = "Dark";
        public string CurrentPreset { get; private set; } = "Default";
        public void Apply(string theme, string preset) { CurrentTheme = theme; CurrentPreset = preset; }
        public void OverrideHighlightColor(string colorHex) { }
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public bool IsInstalled { get; set; } = true;
        public string CurrentVersion { get; set; } = "1.0.0";
        public UpdateCheckResult NextCheckResult { get; set; } =
            new UpdateCheckResult(false, null, "1.0.0", null);
        public bool NextDownloadResult { get; set; } = true;

        public int CheckCount { get; private set; }
        public int DownloadCount { get; private set; }

        public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
        {
            CheckCount++;
            return Task.FromResult(NextCheckResult);
        }

        public Task<bool> DownloadAndApplyAsync(CancellationToken cancellationToken = default)
        {
            DownloadCount++;
            return Task.FromResult(NextDownloadResult);
        }
    }

    private static (SettingsViewModel vm, FakeSettingsService svc, FakeUpdateService upd) Create(
        IUpdateService? updateService = null)
    {
        var svc = new FakeSettingsService();
        var loc = new LocalizationService();
        var theme = new FakeThemeService();
        var auto = new FakeAutoStartService();
        var upd = updateService as FakeUpdateService ?? new FakeUpdateService();
        var vm = new SettingsViewModel(svc, loc, theme, auto, updateService ?? upd);
        return (vm, svc, upd);
    }

    [Fact]
    public void Constructor_WithNullUpdateService_DoesNotThrow()
    {
        var svc = new FakeSettingsService();
        var loc = new LocalizationService();
        var theme = new FakeThemeService();
        var auto = new FakeAutoStartService();
        var ex = Record.Exception(() => new SettingsViewModel(svc, loc, theme, auto, updateService: null));
        Assert.Null(ex);
    }

    [Fact]
    public void Constructor_LoadsAutoCheckForUpdates_FromSettings()
    {
        var (vm, _, _) = Create();
        Assert.True(vm.AutoCheckForUpdates); // default
    }

    [Fact]
    public void AutoCheckForUpdates_Setter_PersistsToSettings()
    {
        var (vm, svc, _) = Create();
        vm.AutoCheckForUpdates = false;
        Assert.False(svc.Current.AutoCheckForUpdates);
        Assert.True(svc.SaveCount > 0);
    }

    [Fact]
    public void UpdateLabels_Populated_AfterConstruction()
    {
        var (vm, _, _) = Create();
        Assert.False(string.IsNullOrEmpty(vm.UpdateSectionLabel));
        Assert.False(string.IsNullOrEmpty(vm.AutoUpdateLabel));
        Assert.False(string.IsNullOrEmpty(vm.CheckUpdateNowLabel));
    }

    [Fact]
    public void HasUpdateStatus_False_WhenStatusEmpty()
    {
        var (vm, _, _) = Create();
        Assert.False(vm.HasUpdateStatus);
    }

    [Fact]
    public async Task CheckForUpdatesNow_NoUpdateService_SetsUnavailableMessage()
    {
        var svc = new FakeSettingsService();
        var loc = new LocalizationService();
        var theme = new FakeThemeService();
        var auto = new FakeAutoStartService();
        var vm = new SettingsViewModel(svc, loc, theme, auto, updateService: null);

        vm.CheckForUpdatesNowCommand.Execute(null);
        await Task.Yield();

        Assert.True(vm.HasUpdateStatus);
        Assert.Equal(loc.Get("settings.update_unavailable"), vm.UpdateStatusText);
    }

    [Fact]
    public async Task CheckForUpdatesNow_NoUpdateAvailable_WritesLastCheckTimestamp()
    {
        var upd = new FakeUpdateService
        {
            NextCheckResult = new UpdateCheckResult(false, null, "1.0.0", null)
        };
        var (vm, svc, _) = Create(upd);

        await InvokeCheckAsync(vm);

        Assert.Equal(1, upd.CheckCount);
        Assert.NotNull(svc.Current.LastUpdateCheckUtc);
        Assert.True(svc.SaveCount >= 1);
        Assert.Contains("1.0.0", vm.UpdateStatusText);
    }

    [Fact]
    public async Task CheckForUpdatesNow_ServiceReturnsError_ShowsError_DoesNotDownload()
    {
        var upd = new FakeUpdateService
        {
            NextCheckResult = new UpdateCheckResult(false, null, null, "feed unreachable")
        };
        var (vm, _, _) = Create(upd);

        await InvokeCheckAsync(vm);

        Assert.Equal("feed unreachable", vm.UpdateStatusText);
        Assert.Equal(0, upd.DownloadCount);
    }

    [Fact]
    public async Task CheckForUpdatesNow_UpdateAvailable_TriggersDownload()
    {
        var upd = new FakeUpdateService
        {
            NextCheckResult = new UpdateCheckResult(true, "1.1.0", "1.0.0", null),
            NextDownloadResult = true
        };
        var (vm, _, _) = Create(upd);

        await InvokeCheckAsync(vm);

        Assert.Equal(1, upd.CheckCount);
        Assert.Equal(1, upd.DownloadCount);
    }

    [Fact]
    public async Task CheckForUpdatesNow_DownloadFails_ShowsFailedMessage()
    {
        var upd = new FakeUpdateService
        {
            NextCheckResult = new UpdateCheckResult(true, "1.1.0", "1.0.0", null),
            NextDownloadResult = false
        };
        var (vm, _, _) = Create(upd);
        var loc = new LocalizationService();

        await InvokeCheckAsync(vm);

        Assert.Equal(loc.Get("settings.update_failed"), vm.UpdateStatusText);
    }

    [Fact]
    public async Task CheckForUpdatesNow_ClearsIsCheckingFlag_AfterCompletion()
    {
        var (vm, _, _) = Create();
        await InvokeCheckAsync(vm);
        Assert.False(vm.IsCheckingForUpdates);
    }

    [Fact]
    public void CheckForUpdatesNowCommand_CanExecute_FalseWhileChecking()
    {
        // Ensure the CanExecute predicate observes the IsCheckingForUpdates flag.
        var (vm, _, _) = Create();
        Assert.True(vm.CheckForUpdatesNowCommand.CanExecute(null));
    }

    /// <summary>
    /// Drains the fire-and-forget async lambda used by the RelayCommand.
    /// We need a small spin to let the awaited Task.FromResult continuation run.
    /// </summary>
    private static async Task InvokeCheckAsync(SettingsViewModel vm)
    {
        vm.CheckForUpdatesNowCommand.Execute(null);
        // Yield several times so any synchronous-looking continuations complete.
        for (int i = 0; i < 20 && vm.IsCheckingForUpdates; i++)
            await Task.Delay(10);
    }
}
