using NepDateWidget.Helpers;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;
using NepDateWidget.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NepDateWidget;

public partial class App : Application
{
    private static Mutex? _instanceMutex;
    private ShortcutsService? _shortcutsService;
    private ScriptService?    _scriptService;
    private AppStateService? _appStateService;
    private NotesService? _notesService;
    private ReminderService? _reminderService;

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
            name: Helpers.AppEnvironment.SingleInstanceMutexName,
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
                 $" channel={(Helpers.AppEnvironment.IsPackaged ? "store" : "dev")}" +
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

        _shortcutsService = new ShortcutsService(Helpers.AppPaths.ShortcutsPath, Helpers.AppPaths.DefaultShortcutsPath);
        _shortcutsService.Load();

        _scriptService = new ScriptService(Helpers.AppPaths.ScriptsPath, Helpers.AppPaths.DefaultScriptsPath);
        _scriptService.Load();

        var mainViewModel = new MainViewModel(settingsService, calendarService, localizationService, conversionService, themeService, autoStartService, reminderService: _reminderService, notesService: _notesService, documentService: documentService, runHistoryService: runHistoryService, holidayLookupService: new HolidayLookupService(nepDateAdapter), adapter: nepDateAdapter, shortcutsService: _shortcutsService, appStateService: _appStateService!, scriptService: _scriptService);
        var mainWindow = new MainWindow(mainViewModel, settingsService, _appStateService!);
        mainWindow.SetupReminders(_reminderService, localizationService, nepDateAdapter, _notesService);

        mainWindow.Show();
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
                        goto skipIni; // content identical - skip the write entirely
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

