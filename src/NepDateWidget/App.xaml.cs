using NepDateWidget.Helpers;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;
using NepDateWidget.Views;
using System.Windows;
using Velopack;

namespace NepDateWidget;

public partial class App : Application
{
    private static Mutex? _instanceMutex;

    /// <summary>
    /// Custom entry point. Runs Velopack's CLI hook (handles --veloapp-install,
    /// --veloapp-uninstall, etc.) before any WPF code starts. This is the
    /// supported pattern for WPF + Velopack.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        // Velopack hook MUST be the first call so installer / updater commands
        // are intercepted before the rest of the app loads.
        VelopackApp.Build().Run();

        // Process-wide handlers for non-UI exceptions. Registered before the
        // WPF application is constructed so background tasks that fault during
        // App() construction are still observed. Logging is best-effort: if
        // Log.Initialize has not run yet (very early failure) the calls are
        // no-ops and we still avoid an unhandled crash.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Log.Fatal($"AppDomain unhandled exception (terminating={e.IsTerminating})", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // ── Global crash handler ─────────────────────────────────────────────
        // Must be registered before any other code so startup exceptions are
        // caught and shown rather than killing the process silently.
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Fatal("Unhandled UI exception", ex.Exception);
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{ex.Exception.Message}",
                "Nepali Calendar Widget - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ex.Handled = true;
            Shutdown(1);
        };

        // ── Early logging bootstrap ─────────────────────────────────────────
        // Initialise the log file before single-instance check or settings load
        // so any failure in those steps is recorded. The cap defaults to 10 MB;
        // once settings load we update it via Log.UpdateMaxSize.
        try
        {
            Helpers.AppPaths.MigrateLegacyData();
            Log.Initialize(new LogService(Helpers.AppPaths.LogPath));
        }
        catch (Exception logEx)
        {
            // Logging unavailable — nothing more we can do; continue startup.
            System.Diagnostics.Debug.WriteLine($"Log init failed: {logEx}");
        }

        // ── Single-instance guard (skipped under dotnet watch) ──────────────
        bool isDev = Environment.GetEnvironmentVariable("DOTNET_WATCH") == "1";
        _instanceMutex = new Mutex(
            initiallyOwned: true,
            name: "NepDateWidget_SingleInstance_v1",
            createdNew: out bool isFirstInstance);

        if (!isDev && !isFirstInstance)
        {
            Log.Info("Second instance detected; exiting.");
            MessageBox.Show(
                "NepDate Widget is already running.",
                "NepDate Widget",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        // ── Compose and launch ───────────────────────────────────────────────
        var settingsService = new SettingsService();
        settingsService.Load();   // load early so log cap is available

        // Now that the user-configured log cap is known, push it down.
        Log.UpdateMaxSize(settingsService.Current.LogMaxSizeMb);
        Log.Info($"App started | v={System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}" +
                 $" theme={settingsService.Current.Theme}/{settingsService.Current.BackgroundPreset}" +
                 $" lang={settingsService.Current.Language}" +
                 $" portable={Helpers.AppPaths.IsPortable}" +
                 $" data={Helpers.AppPaths.DataDirectory}");

        var nepDateAdapter = new NepaliDateAdapter();
        var calendarService = new CalendarService(nepDateAdapter);
        var conversionService = new ConversionService(nepDateAdapter);
        var localizationService = new LocalizationService();
        var themeService = new ThemeService();
        var autoStartService = new AutoStartService();
        // After install / move, the registry value may still point at the old EXE path.
        autoStartService.RefreshIfStale();
        // First-run onboarding + reconciliation. See FirstRunBootstrap for the
        // full decision table; logged here so support can verify behaviour.
        var autoStartAction = Helpers.FirstRunBootstrap.ApplyAutoStart(
            settingsService, autoStartService, isDev);
        if (autoStartAction == Helpers.FirstRunBootstrap.AutoStartAction.EnabledOnFirstRun)
            Log.Info("First-run bootstrap: enabled Start with Windows.");
        else if (autoStartAction == Helpers.FirstRunBootstrap.AutoStartAction.ReconciledToSettings)
            Log.Info($"AutoStart reconciled to persisted setting: {settingsService.Current.AutoStart}.");

        // Reminder service: reminders.json in the resolved data folder.
        var reminderService = new ReminderService(Helpers.AppPaths.RemindersPath, nepDateAdapter);
        reminderService.Load();

        // Notes service: notes.json in the resolved data folder.
        var notesService = new NotesService(Helpers.AppPaths.NotesPath);
        notesService.Load();
        // Migrate notes from settings if any exist (one-time migration)
        if (settingsService.Current.DayNotes.Count > 0)
        {
            notesService.MigrateFromSettings(settingsService.Current.DayNotes);
            settingsService.Current.DayNotes.Clear();
            settingsService.Save();
        }

        var updateService = new VelopackUpdateService();

        var mainViewModel = new MainViewModel(settingsService, calendarService, localizationService, conversionService, themeService, autoStartService, reminderService: reminderService, notesService: notesService, updateService: updateService, holidayLookupService: new HolidayLookupService(nepDateAdapter));
        var mainWindow = new MainWindow(mainViewModel, settingsService);
        mainWindow.SetupReminders(reminderService, localizationService, nepDateAdapter, notesService);

        mainWindow.Show();

        // Background update check: respects user opt-in and a 24h throttle.
        if (settingsService.Current.AutoCheckForUpdates && updateService.IsInstalled)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var last = settingsService.Current.LastUpdateCheckUtc;
                    if (last is not null && (DateTime.UtcNow - last.Value).TotalHours < 24)
                        return;

                    var result = await updateService.CheckAsync().ConfigureAwait(false);

                    // Settings is mutated and saved on the UI thread to avoid
                    // racing the user toggling other settings concurrently.
                    await Dispatcher.InvokeAsync(() =>
                    {
                        settingsService.Current.LastUpdateCheckUtc = DateTime.UtcNow;
                        settingsService.Save();
                    });

                    if (result.UpdateAvailable)
                        Log.Info($"Update available: {result.AvailableVersion} (current {result.CurrentVersion})");
                }
                catch (Exception ex)
                {
                    Log.Warn($"Background update check failed: {ex.Message}");
                }
            });
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("App closed");
        try
        {
            if (_instanceMutex is not null)
            {
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
            }
        }
        catch (ApplicationException)
        {
            // Mutex was not owned by this thread - safe to ignore.
        }

        base.OnExit(e);
    }
}

