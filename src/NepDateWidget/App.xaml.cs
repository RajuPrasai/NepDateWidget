using NepDateWidget.Helpers;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;
using NepDateWidget.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Velopack;

namespace NepDateWidget;

public partial class App : Application
{
    private static Mutex? _instanceMutex;
    private ShortcutsService? _shortcutsService;
    private ScriptService?    _scriptService;
    private AppStateService? _appStateService;
    private NotesService? _notesService;
    private ReminderService? _reminderService;

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
        // ── Global TextBox caret-to-end on focus ────────────────────────────
        EventManager.RegisterClassHandler(
            typeof(TextBox),
            UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler((s, _) =>
            {
                if (s is TextBox tb && !tb.IsReadOnly)
                    tb.CaretIndex = tb.Text.Length;
            }));

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
            // Logging unavailable - nothing more we can do; continue startup.
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
        var localizationService = new LocalizationService(Helpers.AppPaths.LocalizationPath, Helpers.AppPaths.DefaultLocalizationPath);
        localizationService.Load();
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
        _reminderService = new ReminderService(Helpers.AppPaths.RemindersPath, nepDateAdapter);
        _reminderService.Load();

        // Notes service: notes.json in the resolved data folder.
        _notesService = new NotesService(Helpers.AppPaths.NotesPath);
        _notesService.Load();

        // Runtime state (last update check, last daily events notification).
        _appStateService = new AppStateService(Helpers.AppPaths.AppStatePath);
        _appStateService.Load();

        // Documents service: documents.json in the resolved data folder.
        var documentService = new DocumentService(Helpers.AppPaths.DocumentsPath);
        documentService.Load();

        // Pin the managed documents folder to Windows Quick Access so users can
        // reach it quickly from any file-upload dialog.
        PinDocumentsFolderToQuickAccess();

        // Run box command history
        var runHistoryService = new SearchHistoryService(Helpers.AppPaths.RunHistoryPath, maxEntries: 500, defaultFilePath: Helpers.AppPaths.DefaultRunHistoryPath);
        runHistoryService.Load();

        var updateService = new VelopackUpdateService();

        _shortcutsService = new ShortcutsService(Helpers.AppPaths.ShortcutsPath, Helpers.AppPaths.DefaultShortcutsPath);
        _shortcutsService.Load();

        _scriptService = new ScriptService(Helpers.AppPaths.ScriptsPath, Helpers.AppPaths.DefaultScriptsPath);
        _scriptService.Load();

        var mainViewModel = new MainViewModel(settingsService, calendarService, localizationService, conversionService, themeService, autoStartService, reminderService: _reminderService, notesService: _notesService, documentService: documentService, runHistoryService: runHistoryService, updateService: updateService, holidayLookupService: new HolidayLookupService(nepDateAdapter), adapter: nepDateAdapter, shortcutsService: _shortcutsService, appStateService: _appStateService!, scriptService: _scriptService);
        var mainWindow = new MainWindow(mainViewModel, settingsService, _appStateService!);
        mainWindow.SetupReminders(_reminderService, localizationService, nepDateAdapter, _notesService);

        mainWindow.Show();

        // Background update check: respects user opt-in and a 24h throttle.
        if (settingsService.Current.AutoCheckForUpdates && updateService.IsInstalled)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var last = _appStateService.Current.LastUpdateCheckUtc;
                    if (last is not null && (DateTime.UtcNow - last.Value).TotalHours < 24)
                        return;

                    var result = await updateService.CheckAsync().ConfigureAwait(false);

                    // AppState is mutated and saved on the UI thread to avoid
                    // racing the user toggling other settings concurrently.
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _appStateService.Current.LastUpdateCheckUtc = DateTime.UtcNow;
                        _appStateService.Save();
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

    private static void PinDocumentsFolderToQuickAccess()
    {
        try
        {
            var folder = Helpers.AppPaths.DocumentsFilesDirectory;
            System.IO.Directory.CreateDirectory(folder);

            // Write desktop.ini so Explorer shows the widget icon on the folder.
            var exePath = Environment.ProcessPath
                       ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath is not null)
            {
                var iniPath = System.IO.Path.Combine(folder, "desktop.ini");
                var iniContent = $"[.ShellClassInfo]\r\nIconResource={exePath},0\r\n";

                // If desktop.ini already exists with Hidden|System, writing to it will
                // throw UnauthorizedAccessException. Clear the attributes first.
                if (System.IO.File.Exists(iniPath))
                {
                    var existing = System.IO.File.ReadAllText(iniPath);
                    if (existing == iniContent)
                        goto skipIni; // content identical — skip the write entirely
                    System.IO.File.SetAttributes(iniPath, System.IO.FileAttributes.Normal);
                }

                System.IO.File.WriteAllText(iniPath, iniContent);
                System.IO.File.SetAttributes(iniPath,
                    System.IO.FileAttributes.Hidden | System.IO.FileAttributes.System);
                System.IO.File.SetAttributes(folder,
                    System.IO.File.GetAttributes(folder) | System.IO.FileAttributes.ReadOnly);
            }

            skipIni:
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
            shell.Namespace(folder).Self.InvokeVerb("pintohome");
            Log.Info($"Documents folder pinned to Quick Access: {folder}");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to pin documents folder to Quick Access", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("App closed");
        _shortcutsService?.Dispose();
        _scriptService?.Dispose();
        _notesService?.Dispose();
        _reminderService?.Dispose();
        Log.Shutdown();
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

