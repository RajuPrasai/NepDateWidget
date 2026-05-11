using NepDateWidget.Helpers;
using NepDateWidget.Services;
using System.Windows.Input;

namespace NepDateWidget.ViewModels;

/// <summary>
/// Root view model for the widget window.
/// Owns the expand/collapse state, window dimensions, always-on-top flag,
/// and coordinates position/size persistence back through ISettingsService.
/// Also owns child view models (MiniBarViewModel) and the active language.
/// </summary>
public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly IAutoStartService _autoStartService;
    private readonly ICalendarService _calendarService;

    // ── Expand/Collapse ──────────────────────────────────────────────────────

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        private set => SetProperty(ref _isExpanded, value);
    }

    // ── Window dimensions ────────────────────────────────────────────────────

    private double _windowWidth;
    public double WindowWidth
    {
        get => _windowWidth;
        private set => SetProperty(ref _windowWidth, value);
    }

    private double _windowHeight;
    public double WindowHeight
    {
        get => _windowHeight;
        private set => SetProperty(ref _windowHeight, value);
    }

    // ── Behaviour flags ──────────────────────────────────────────────────────

    private bool _alwaysOnTop;
    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set
        {
            if (SetProperty(ref _alwaysOnTop, value))
            {
                _settingsService.Current.AlwaysOnTop = value;
                _settingsService.Save();
            }
        }
    }

    private bool _expandedPinned;
    public bool ExpandedPinned
    {
        get => _expandedPinned;
        set => SetProperty(ref _expandedPinned, value);
    }

    private string _language = "en";
    public string Language
    {
        get => _language;
        set
        {
            if (SetProperty(ref _language, value))
            {
                Log.Action($"setting: lang {_language}");
                _settingsService.Current.Language = value;
                _localizationService.SetLanguage(value);
                MiniBar.OnLanguageChanged();
                Calendar.OnLanguageChanged();
                Settings.OnLanguageChanged();
                Unit.OnLanguageChanged();
                Network.OnLanguageChanged();
                Banking.OnLanguageChanged();
                TextTools.OnLanguageChanged();
                RunBox.OnLanguageChanged();
                About.OnLanguageChanged();
                More.OnLanguageChanged();
                OnPropertyChanged(nameof(IsLanguageEn));
                OnPropertyChanged(nameof(IsLanguageNe));
                RefreshMenuLabels();
                _settingsService.Save();
            }
        }
    }

    public bool IsLanguageEn => string.Equals(_language, "en", StringComparison.OrdinalIgnoreCase);
    public bool IsLanguageNe => string.Equals(_language, "ne", StringComparison.OrdinalIgnoreCase);

    // ── Copy-today labels (fetched live so they're always today's date) ────────
    public string CopyTodayBsShortLabel => _calendarService.GetCurrentDateInfo().BsShortEn;
    public string CopyTodayBsLongLabel => _calendarService.GetCurrentDateInfo().BsLongEn;
    public string CopyTodayAdShortLabel => _calendarService.GetCurrentDateInfo().AdDate.ToString("yyyy-MM-dd");
    public string CopyTodayAdLongLabel => _calendarService.GetCurrentDateInfo().AdLong;
    public string MenuCopyTodayLabel { get; private set; } = string.Empty;

    // ── Context menu labels (localized) ──────────────────────────────────────
    // Language names are always shown in their own script - not translated.
    public string MenuLanguageLabel { get; private set; } = string.Empty;
    public string MenuLanguageEnLabel => "English";
    public string MenuLanguageNeLabel => "नेपाली";
    public string MenuAlwaysOnTopLabel { get; private set; } = string.Empty;
    public string MenuShowClockLabel { get; private set; } = string.Empty;
    public string MenuShowTimezoneLabel { get; private set; } = string.Empty;
    public string MenuThemeLabel { get; private set; } = string.Empty;
    public string MenuThemeDarkLabel { get; private set; } = string.Empty;
    public string MenuThemeLightLabel { get; private set; } = string.Empty;
    public string MenuBackgroundLabel { get; private set; } = string.Empty;
    public string MenuPresetDefaultLabel { get; private set; } = string.Empty;
    public string MenuPresetOceanLabel { get; private set; } = string.Empty;
    public string MenuPresetForestLabel { get; private set; } = string.Empty;
    public string MenuPresetSunsetLabel { get; private set; } = string.Empty;
    public string MenuPresetMonoLabel { get; private set; } = string.Empty;
    public string MenuCornerLabel { get; private set; } = string.Empty;
    public string MenuCornerRoundedLabel { get; private set; } = string.Empty;
    public string MenuCornerSharpLabel { get; private set; } = string.Empty;
    public string MenuAnimationLabel { get; private set; } = string.Empty; public string MenuAutoStartLabel { get; private set; } = string.Empty; public string MenuExitLabel { get; private set; } = string.Empty;
    public string MenuSettingsLabel { get; private set; } = string.Empty;
    public string MenuMoreLabel { get; private set; } = string.Empty;

    // ── Tab labels (localized) ───────────────────────────────────────────────
    public string TabHomeLabel    { get; private set; } = string.Empty;
    public string TabDateLabel     { get; private set; } = string.Empty;
    public string TabSettingsLabel { get; private set; } = string.Empty;
    public string TabUnitLabel     { get; private set; } = string.Empty;
    public string TabBankLabel     { get; private set; } = string.Empty;
    public string TabNetworkLabel  { get; private set; } = string.Empty;
    public string TabTextLabel     { get; private set; } = string.Empty;
    public string TabAboutLabel    { get; private set; } = string.Empty;
    public string TabMoreLabel     { get; private set; } = string.Empty;

    // ── Chrome tooltip labels (localized) ─────────────────────────────────────
    public string TooltipAbout    { get; private set; } = string.Empty;
    public string TooltipMinimize { get; private set; } = string.Empty;
    public string TooltipSettings { get; private set; } = string.Empty;
    // ── Selected tab index (for programmatic tab switching) ────────────────
    private int _selectedTabIndex;
    /// <summary>
    /// Index of the tab the user was on the last time the widget was expanded.
    /// Loaded from settings at startup so the widget always reopens on the
    /// user's last tab. Updated whenever the user switches tabs while expanded.
    /// </summary>
    private int _lastExpandedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                // Names must match TabItem order in MainWindow.xaml (ExpandedTabs).
                var name = value switch { 0 => "Calendar", 1 => "Tools", 2 => "Unit", 3 => "Text", 4 => "Banking", 5 => "Network", 6 => "More", 7 => "About", 8 => "Settings", _ => value.ToString() };
                Log.Action($"tab → {name}");
                if (value == 0)
                    Calendar.ClearMissedBadge();
                if (value == 6)
                {
                    More.RefreshNotes();
                    More.RefreshReminders();
                }

                // Track the last expanded tab so the widget always reopens here.
                // Only persist when expanded; programmatic tab changes during
                // construction/collapse should not overwrite the saved value.
                if (_isExpanded)
                {
                    _lastExpandedTabIndex = value;
                    _settingsService.Current.LastExpandedTab = value;
                    _settingsService.Save();
                }
            }
        }
    }
    // ── Theme / Preset / Corner ──────────────────────────────────────────

    private string _theme = "Dark";
    public string Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value))
            {
                _settingsService.Current.Theme = value;
                _themeService.Apply(value, _backgroundPreset);
                OnPropertyChanged(nameof(IsThemeDark));
                OnPropertyChanged(nameof(IsThemeLight));
                _settingsService.Save();
            }
        }
    }
    public bool IsThemeDark => string.Equals(_theme, "Dark", StringComparison.OrdinalIgnoreCase);
    public bool IsThemeLight => string.Equals(_theme, "Light", StringComparison.OrdinalIgnoreCase);

    private string _backgroundPreset = "Default";
    public string BackgroundPreset
    {
        get => _backgroundPreset;
        set
        {
            if (SetProperty(ref _backgroundPreset, value))
            {
                _settingsService.Current.BackgroundPreset = value;
                _themeService.Apply(_theme, value);
                NotifyPresetSelection();
                _settingsService.Save();
            }
        }
    }
    public bool IsPresetDefault => string.Equals(_backgroundPreset, "Default", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetOcean => string.Equals(_backgroundPreset, "Ocean", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetForest => string.Equals(_backgroundPreset, "Forest", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetSunset => string.Equals(_backgroundPreset, "Sunset", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetMonochrome => string.Equals(_backgroundPreset, "Monochrome", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetAurora => string.Equals(_backgroundPreset, "Aurora", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetCherry => string.Equals(_backgroundPreset, "Cherry", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetMidnight => string.Equals(_backgroundPreset, "Midnight", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetSlate => string.Equals(_backgroundPreset, "Slate", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetEmber => string.Equals(_backgroundPreset, "Ember", StringComparison.OrdinalIgnoreCase);

    private string _cornerStyle = "Rounded";
    public string CornerStyle
    {
        get => _cornerStyle;
        set
        {
            if (SetProperty(ref _cornerStyle, value))
            {
                _settingsService.Current.CornerStyle = value;
                OnPropertyChanged(nameof(CornerRadiusValue));
                OnPropertyChanged(nameof(IsCornerRounded));
                OnPropertyChanged(nameof(IsCornerSharp));
                _settingsService.Save();
            }
        }
    }
    /// <summary>WPF CornerRadius bound directly to WidgetBorder.CornerRadius.</summary>
    public double CornerRadiusValue => string.Equals(_cornerStyle, "Sharp", StringComparison.OrdinalIgnoreCase) ? 0 : 8;
    public bool IsCornerRounded => string.Equals(_cornerStyle, "Rounded", StringComparison.OrdinalIgnoreCase);
    public bool IsCornerSharp => string.Equals(_cornerStyle, "Sharp", StringComparison.OrdinalIgnoreCase);

    private bool _animationEnabled = true;
    public bool AnimationEnabled
    {
        get => _animationEnabled;
        set
        {
            if (SetProperty(ref _animationEnabled, value))
            {
                _settingsService.Current.AnimationEnabled = value;
                _settingsService.Save();
            }
        }
    }

    private bool _transparentWhenCollapsed;
    public bool TransparentWhenCollapsed
    {
        get => _transparentWhenCollapsed;
        set
        {
            if (SetProperty(ref _transparentWhenCollapsed, value))
            {
                _settingsService.Current.TransparentWhenCollapsed = value;
                OnPropertyChanged(nameof(IsCollapsedTransparent));
                _settingsService.Save();
            }
        }
    }

    /// <summary>True when transparent mode is on (pill stays transparent at all times).</summary>
    public bool IsCollapsedTransparent => _transparentWhenCollapsed;

    private bool _autoStart;
    public bool AutoStart
    {
        get => _autoStart;
        set
        {
            if (SetProperty(ref _autoStart, value))
            {
                _settingsService.Current.AutoStart = value;
                _autoStartService.SetEnabled(value);
                _settingsService.Save();
            }
        }
    }

    // ── Child view models ─────────────────────────────────────────────────────

    // ── Settings consumed by View layer ─────────────────────────────────────

    private bool _hideOnFullscreen = true;
    public bool HideOnFullscreen
    {
        get => _hideOnFullscreen;
        set => SetProperty(ref _hideOnFullscreen, value);
    }

    private bool _showSecondsInClock;
    public bool ShowSecondsInClock
    {
        get => _showSecondsInClock;
        set => SetProperty(ref _showSecondsInClock, value);
    }

    private bool _showFiscalYear = true;
    public bool ShowFiscalYear
    {
        get => _showFiscalYear;
        set => SetProperty(ref _showFiscalYear, value);
    }

    private int _notificationDurationSeconds = 10;
    public int NotificationDurationSeconds
    {
        get => _notificationDurationSeconds;
        set => SetProperty(ref _notificationDurationSeconds, value);
    }

    private bool _notificationSound = true;
    public bool NotificationSound
    {
        get => _notificationSound;
        set => SetProperty(ref _notificationSound, value);
    }

    public MiniBarViewModel MiniBar { get; }
    public CalendarViewModel Calendar { get; }
    public SettingsViewModel Settings { get; }
    public UnitViewModel Unit { get; }
    public NetworkToolsViewModel Network { get; }
    public BankingViewModel Banking { get; }
    public TextToolsViewModel TextTools { get; }
    public RunBoxViewModel RunBox { get; }
    public AboutViewModel About { get; }
    public MoreViewModel More { get; }

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand ToggleExpandedCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenAboutCommand { get; }
    public ICommand OpenMoreCommand { get; }
    public ICommand SetLanguageEnCommand { get; }
    public ICommand SetLanguageNeCommand { get; }
    public ICommand SetThemeDarkCommand { get; }
    public ICommand SetThemeLightCommand { get; }
    public ICommand SetPresetDefaultCommand { get; }
    public ICommand SetPresetOceanCommand { get; }
    public ICommand SetPresetForestCommand { get; }
    public ICommand SetPresetSunsetCommand { get; }
    public ICommand SetPresetMonoCommand { get; }
    public ICommand SetPresetAuroraCommand { get; }
    public ICommand SetPresetCherryCommand { get; }
    public ICommand SetPresetMidnightCommand { get; }
    public ICommand SetPresetSlateCommand { get; }
    public ICommand SetPresetEmberCommand { get; }
    public ICommand SetPresetCommand { get; }
    public ICommand SetCornerRoundedCommand { get; }
    public ICommand SetCornerSharpCommand { get; }

    // Copy-today commands
    public ICommand CopyTodayBsShortCommand { get; }
    public ICommand CopyTodayBsLongCommand { get; }
    public ICommand CopyTodayAdShortCommand { get; }
    public ICommand CopyTodayAdLongCommand { get; }

    // Tools shortcuts (context menu)
    public ICommand OpenToolsConvertCommand { get; }
    public ICommand OpenToolsDaysCommand { get; }
    public ICommand OpenToolsTimeCommand { get; }

    // Banking shortcuts (context menu)
    public ICommand OpenBankingInterestCommand { get; }
    public ICommand OpenBankingEmiCommand { get; }

    // Unit shortcuts (context menu)
    public ICommand OpenUnitAreaCommand { get; }
    public ICommand OpenUnitScriptCommand { get; }
    public ICommand OpenUnitWeightCommand { get; }

    // Network shortcuts (context menu)
    public ICommand OpenNetworkMyIpCommand    { get; }
    public ICommand OpenNetworkPingCommand    { get; }
    public ICommand OpenNetworkScanCommand    { get; }
    public ICommand OpenNetworkTraceCommand   { get; }
    public ICommand OpenNetworkWhoisCommand   { get; }
    public ICommand OpenNetworkDnsCommand     { get; }

    // Quick-toggle for the pin button in the tab strip
    public ICommand ToggleExpandedPinCommand { get; }

    // Text tools shortcuts (context menu)
    public ICommand OpenTextUnicodeCommand  { get; }
    public ICommand OpenTextWordCommand     { get; }
    public ICommand OpenTextPasswordCommand { get; }
    public ICommand OpenTextScriptCommand   { get; }

    // ── Context menu Tools labels ─────────────────────────────────────────
    public string MenuToolsConvertLabel { get; private set; } = string.Empty;
    public string MenuToolsDaysLabel { get; private set; } = string.Empty;
    public string MenuToolsTimeLabel { get; private set; } = string.Empty;
    public string MenuToolsSectionLabel { get; private set; } = string.Empty;

    // ── Context menu Banking labels ──────────────────────────────────
    public string MenuBankingSectionLabel { get; private set; } = string.Empty;
    public string MenuBankingInterestLabel { get; private set; } = string.Empty;
    public string MenuBankingEmiLabel { get; private set; } = string.Empty;

    // ── Context menu Unit labels ────────────────────────────────────
    public string MenuUnitSectionLabel { get; private set; } = string.Empty;
    public string MenuUnitAreaLabel { get; private set; } = string.Empty;
    public string MenuUnitScriptLabel { get; private set; } = string.Empty;
    public string MenuUnitWeightLabel { get; private set; } = string.Empty;

    // ── Context menu Network labels ──────────────────────────────────
    public string MenuNetworkSectionLabel { get; private set; } = string.Empty;
    public string MenuNetworkMyIpLabel    { get; private set; } = string.Empty;
    public string MenuNetworkPingLabel    { get; private set; } = string.Empty;
    public string MenuNetworkScanLabel    { get; private set; } = string.Empty;
    public string MenuNetworkTraceLabel   { get; private set; } = string.Empty;
    public string MenuNetworkWhoisLabel   { get; private set; } = string.Empty;
    public string MenuNetworkDnsLabel     { get; private set; } = string.Empty;

    // ── Context menu Text Tools labels ───────────────────────────────
    public string MenuTextSectionLabel  { get; private set; } = string.Empty;
    public string MenuTextUnicodeLabel  { get; private set; } = string.Empty;
    public string MenuTextWordLabel     { get; private set; } = string.Empty;
    public string MenuTextPasswordLabel { get; private set; } = string.Empty;    public string MenuTextScriptLabel   { get; private set; } = string.Empty;
    // ── Exit signal ──────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the user explicitly chooses to exit the application.
    /// The View subscribes and calls Window.Close() after setting its own allow-close flag.
    /// </summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Raised when the global hotkey is activated and the RunBox should receive focus.
    /// The View subscribes to focus the RunBox TextBox.
    /// </summary>
    public event EventHandler? RunBoxFocusRequested;

    /// <summary>
    /// Raised when the user navigates to a tab or mode while the shell is already expanded.
    /// The View subscribes to bring the shell window to the foreground.
    /// </summary>
    public event EventHandler? ShellBringToFrontRequested;

    // ── Construction ─────────────────────────────────────────────────────────

    public MainViewModel(
        ISettingsService settingsService,
        ICalendarService calendarService,
        ILocalizationService localizationService,
        IConversionService conversionService,
        IThemeService themeService,
        IAutoStartService autoStartService,
        IReminderService? reminderService = null,
        INotesService? notesService = null,
        IDocumentService? documentService = null,
        ISearchHistoryService? searchHistoryService = null,
        ISearchHistoryService? runHistoryService = null,
        IUpdateService? updateService = null,
        IHolidayLookupService? holidayLookupService = null,
        INepaliDateAdapter? adapter = null,
        IShortcutsService? shortcutsService = null,
        IAppStateService? appStateService = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _autoStartService = autoStartService ?? throw new ArgumentNullException(nameof(autoStartService));
        _calendarService = calendarService ?? throw new ArgumentNullException(nameof(calendarService));
        _ = conversionService ?? throw new ArgumentNullException(nameof(conversionService));

        _settingsService.Load();

        var s = _settingsService.Current;
        // Widget always starts in collapsed mode (desktop widget convention).
        // We deliberately do NOT mutate s.IsExpanded here: that would silently
        // overwrite a future hand-edit or migration value during Load().
        // The runtime state (_isExpanded) is the source of truth at startup;
        // s.IsExpanded is brought back in sync when the user toggles state.
        _isExpanded = false;
        _alwaysOnTop = s.AlwaysOnTop;
        _language = s.Language;
        _theme = s.Theme;
        _backgroundPreset = s.BackgroundPreset;
        _cornerStyle = s.CornerStyle;
        _animationEnabled = s.AnimationEnabled;
        _transparentWhenCollapsed = s.TransparentWhenCollapsed;
        _hideOnFullscreen = s.HideOnFullscreen;
        _lastExpandedTabIndex = s.LastExpandedTab;
        _showSecondsInClock = s.ShowSecondsInClock;
        _showFiscalYear = s.ShowFiscalYear;
        _notificationDurationSeconds = s.NotificationDurationSeconds;
        _notificationSound = s.NotificationSound;
        // On first launch, register auto-start automatically; the user can disable it in Settings.
        if (_settingsService.IsFirstLaunch && !_autoStartService.IsEnabled)
            _autoStartService.SetEnabled(true);

        // AutoStart: read live state from service (registry), not persisted bool
        _autoStart = _autoStartService.IsEnabled;
        s.AutoStart = _autoStart;   // keep settings in sync with actual state

        _localizationService.SetLanguage(_language);
        _themeService.Apply(_theme, _backgroundPreset);
        ApplyFont(s.FontFamily);

        MiniBar = new MiniBarViewModel(calendarService, localizationService,
                                       s.ShowTimezone, s.SelectedTimezoneId, s.ShowOffset,
                                       s.ShowDayOfWeek, s.ShowEnglishDate,
                                       s.ClockFormat, s.ShowSecondsInClock);
        Calendar = new CalendarViewModel(calendarService, localizationService, conversionService,
                                         s.HighlightedDays, s.ConverterDefaultDirection,
                                         s.ShowEnglishDayNumbers, s.HighlightSaturdays,
                                         s.HighlightSundays,
                                         selectedTimezoneId: s.SelectedTimezoneId,
                                         reminderService: reminderService,
                                         notesService: notesService,
                                         showTithi: s.ShowTithi,
                                         showEvents: s.ShowEvents,
                                         highlightPublicHolidays: s.HighlightPublicHolidays,
                                         showFiscalYear: s.ShowFiscalYear,
                                         showHolidayCountdown: s.ShowHolidayCountdown,
                                         holidayLookupService: holidayLookupService);

        // Apply user-chosen highlight color override after theme is set
        _themeService.OverrideHighlightColor(s.HighlightColor);

        Settings = new SettingsViewModel(settingsService, localizationService, themeService, autoStartService, updateService, appStateService);
        Unit = new UnitViewModel(localizationService);
        Network = new NetworkToolsViewModel(localizationService);
        Banking = new BankingViewModel(localizationService);
        TextTools = new TextToolsViewModel(localizationService);
        RunBox = new RunBoxViewModel(runHistoryService, localizationService, shortcutsService ?? ShortcutsService.CreateBuiltInOnly());
        About = new AboutViewModel(localizationService);
        More = new MoreViewModel(localizationService, notesService, reminderService, documentService, searchHistoryService, adapter: adapter);

        // When settings are applied from the Settings tab, sync live state
        Settings.SettingsApplied += (_, _) => SyncFromSettings();

        // Update calendar tab label when missed reminder count changes
        Calendar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CalendarViewModel.MissedReminderCount))
                RefreshCalendarTabLabel();
        };

        // Persist direction changes back to settings whenever the user toggles it
        Calendar.Converter.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConverterViewModel.IsAdToBs))
                _settingsService.Current.ConverterDefaultDirection =
                    Calendar.Converter.IsAdToBs ? "ADtoBS" : "BStoAD";
        };

        ToggleExpandedCommand = new RelayCommand(ToggleExpanded);
        ExitCommand = new RelayCommand(RequestExit);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        OpenAboutCommand    = new RelayCommand(OpenAbout);
        OpenMoreCommand     = new RelayCommand(OpenMore);
        ToggleExpandedPinCommand = new RelayCommand(() => ExpandedPinned = !ExpandedPinned);
        SetLanguageEnCommand = new RelayCommand(() => Language = "en");
        SetLanguageNeCommand = new RelayCommand(() => Language = "ne");
        SetThemeDarkCommand = new RelayCommand(() => Theme = "Dark");
        SetThemeLightCommand = new RelayCommand(() => Theme = "Light");
        SetPresetCommand = new RelayCommand<string>(s => { if (s is not null) BackgroundPreset = s; });
        SetPresetDefaultCommand = new RelayCommand(() => BackgroundPreset = "Default");
        SetPresetOceanCommand = new RelayCommand(() => BackgroundPreset = "Ocean");
        SetPresetForestCommand = new RelayCommand(() => BackgroundPreset = "Forest");
        SetPresetSunsetCommand = new RelayCommand(() => BackgroundPreset = "Sunset");
        SetPresetMonoCommand = new RelayCommand(() => BackgroundPreset = "Monochrome");
        SetPresetAuroraCommand = new RelayCommand(() => BackgroundPreset = "Aurora");
        SetPresetCherryCommand = new RelayCommand(() => BackgroundPreset = "Cherry");
        SetPresetMidnightCommand = new RelayCommand(() => BackgroundPreset = "Midnight");
        SetPresetSlateCommand = new RelayCommand(() => BackgroundPreset = "Slate");
        SetPresetEmberCommand = new RelayCommand(() => BackgroundPreset = "Ember");
        SetCornerRoundedCommand = new RelayCommand(() => CornerStyle = "Rounded");
        SetCornerSharpCommand = new RelayCommand(() => CornerStyle = "Sharp");

        CopyTodayBsShortCommand = new RelayCommand(() => System.Windows.Clipboard.SetText(CopyTodayBsShortLabel));
        CopyTodayBsLongCommand = new RelayCommand(() => System.Windows.Clipboard.SetText(CopyTodayBsLongLabel));
        CopyTodayAdShortCommand = new RelayCommand(() => System.Windows.Clipboard.SetText(CopyTodayAdShortLabel));
        CopyTodayAdLongCommand = new RelayCommand(() => System.Windows.Clipboard.SetText(CopyTodayAdLongLabel));

        OpenToolsConvertCommand = new RelayCommand(() => OpenToolsMode(0));
        OpenToolsDaysCommand    = new RelayCommand(() => OpenToolsMode(1));
        OpenToolsTimeCommand    = new RelayCommand(() => OpenToolsMode(2));

        OpenBankingInterestCommand = new RelayCommand(() => OpenBankingMode(0));
        OpenBankingEmiCommand      = new RelayCommand(() => OpenBankingMode(1));

        OpenUnitAreaCommand = new RelayCommand(() => OpenUnitMode(0));
        OpenUnitScriptCommand = new RelayCommand(() => OpenUnitMode(1));
        OpenUnitWeightCommand = new RelayCommand(() => OpenUnitMode(2));

        OpenNetworkMyIpCommand  = new RelayCommand(() => OpenNetworkMode(0));
        OpenNetworkPingCommand  = new RelayCommand(() => OpenNetworkMode(1));
        OpenNetworkScanCommand  = new RelayCommand(() => OpenNetworkMode(2));
        OpenNetworkTraceCommand = new RelayCommand(() => OpenNetworkMode(3));
        OpenNetworkWhoisCommand = new RelayCommand(() => OpenNetworkMode(4));
        OpenNetworkDnsCommand   = new RelayCommand(() => OpenNetworkMode(5));

        // TextToolsViewModel's ActiveMode follows the on-screen tab strip order in
        // TextToolsView.xaml: 0 = Password, 1 = Word, 2 = Unicode, 3 = Script.
        // The shortcut commands map the user's intent ("open Unicode") to the
        // matching internal mode index. Do NOT change without updating both sides.
        OpenTextUnicodeCommand  = new RelayCommand(() => OpenTextMode(2));
        OpenTextWordCommand     = new RelayCommand(() => OpenTextMode(1));
        OpenTextPasswordCommand = new RelayCommand(() => OpenTextMode(0));
        OpenTextScriptCommand   = new RelayCommand(() => OpenTextMode(3));

        RefreshMenuLabels();
    }

    // ── Mouse wheel (delegated from View when expanded) ───────────────────────

    /// <summary>
    /// Called by MainWindow.PreviewMouseWheel when the widget is expanded.
    /// Positive delta = scroll up = previous month; negative = next month.
    /// </summary>
    public void OnMouseWheel(int delta)
    {
        if (!_isExpanded) return;
        Calendar.NavigateMonths(delta > 0 ? -1 : +1);
    }

    /// <summary>
    /// Opens the Settings tab. Expands the widget if collapsed.
    /// </summary>
    private void OpenSettings()
    {
        if (!_isExpanded) ToggleExpanded();
        else ShellBringToFrontRequested?.Invoke(this, EventArgs.Empty);
        SelectedTabIndex = 8;
    }

    private void OpenAbout()
    {
        if (!_isExpanded) ToggleExpanded();
        else ShellBringToFrontRequested?.Invoke(this, EventArgs.Empty);
        SelectedTabIndex = 7;
    }

    private void OpenMore()
    {
        if (!_isExpanded) ToggleExpanded();
        else ShellBringToFrontRequested?.Invoke(this, EventArgs.Empty);
        SelectedTabIndex = 6;
    }

    private void OpenToolsMode(int mode)
    {
        if (!_isExpanded) ToggleExpanded();
        else ShellBringToFrontRequested?.Invoke(this, EventArgs.Empty);
        SelectedTabIndex = 1;
        Calendar.Converter.ActiveMode = mode;
    }

    /// <summary>
    /// Opens the Banking tab with a specific mode. Expands the widget if collapsed.
    /// Mode: 0 = Interest, 1 = EMI.
    /// </summary>
    private void OpenBankingMode(int mode)
    {
        if (!_isExpanded) ToggleExpanded();
        else ShellBringToFrontRequested?.Invoke(this, EventArgs.Empty);
        SelectedTabIndex = 4;
        Banking.ActiveMode = mode;
    }

    /// <summary>
    /// Opens the Unit tab with a specific mode. Expands the widget if collapsed.
    /// Mode: 0 = Area, 1 = Script, 2 = Weight.
    /// </summary>
    private void OpenUnitMode(int mode)
    {
        if (!_isExpanded) ToggleExpanded();
        else ShellBringToFrontRequested?.Invoke(this, EventArgs.Empty);
        SelectedTabIndex = 2;
        Unit.ActiveMode = mode;
    }

    /// <summary>
    /// Opens the Text Tools tab with a specific internal mode index. Expands the
    /// widget if collapsed. Internal indices follow <c>TextToolsView.xaml</c> tab
    /// strip order: 0 = Password, 1 = Word, 2 = Unicode, 3 = Script. Callers should
    /// use the <c>OpenTextXxxCommand</c> wrappers, not pass raw integers.
    /// </summary>
    private void OpenTextMode(int mode)
    {
        if (!_isExpanded) ToggleExpanded();
        else ShellBringToFrontRequested?.Invoke(this, EventArgs.Empty);
        SelectedTabIndex = 3;
        TextTools.ActiveMode = mode;
    }

    /// <summary>
    /// Opens the Network tab with a specific mode. Expands the widget if collapsed.
    /// Mode: 0 = My IP, 1 = Ping, 2 = Scan, 3 = Trace, 4 = Whois, 5 = DNS.
    /// </summary>
    private void OpenNetworkMode(int mode)
    {
        if (!_isExpanded) ToggleExpanded();
        else ShellBringToFrontRequested?.Invoke(this, EventArgs.Empty);
        SelectedTabIndex = 5;
        Network.ActiveMode = mode;
    }

    // ── Public helpers used by the View ──────────────────────────────────────

    /// <summary>
    /// Returns the startup window position after off-screen recovery.
    /// Must be called after the first Load() so settings are available.
    /// </summary>
    public (double Left, double Top) GetInitialPosition()
    {
        // Widget always starts collapsed with SizeToContent; use an estimate for the
        // off-screen recovery check (actual size is determined by layout after startup).
        const double collapsedEstimateW = 280;
        const double collapsedEstimateH = 44;

        if (_settingsService.IsFirstLaunch)
            return ScreenBoundsHelper.GetFirstRunPosition(collapsedEstimateW, collapsedEstimateH);

        var s = _settingsService.Current;
        return ScreenBoundsHelper.GetStartupPosition(
            s.WindowLeft, s.WindowTop, collapsedEstimateW, collapsedEstimateH);
    }

    /// <summary>Called by the View whenever the window is moved.</summary>
    public void UpdatePosition(double left, double top)
    {
        _settingsService.Current.WindowLeft = left;
        _settingsService.Current.WindowTop = top;
    }

    /// <summary>
    /// Persists current settings to disk. Called by the View after move/resize completes.
    /// </summary>
    public void SaveSettings()
    {
        _settingsService.Save();
    }

    /// <summary>Called by the View whenever the window is resized by the user.</summary>
    public void UpdateSize(double width, double height)
    {
        // Keep backing fields in sync without raising PropertyChanged
        // (the window itself is the source of truth during user resize).
        _windowWidth = width;
        _windowHeight = height;

        // Only persist expanded dimensions; collapsed size is driven by SizeToContent.
        var s = _settingsService.Current;
        if (_isExpanded)
        {
            s.ExpandedWidth = width;
            s.ExpandedHeight = height;
        }
    }

    // ── Private logic ─────────────────────────────────────────────────────────

    private void ToggleExpanded()
    {
        var s = _settingsService.Current;
        if (_isExpanded)
        {
            s.ExpandedWidth = _windowWidth;
            s.ExpandedHeight = _windowHeight;
            _lastExpandedTabIndex = _selectedTabIndex;
            s.LastExpandedTab = _selectedTabIndex;
        }
        else
        {
            // Pre-load expanded dimensions so the View can read them
            // synchronously when the IsExpanded PropertyChanged fires.
            _windowWidth = s.ExpandedWidth;
            _windowHeight = s.ExpandedHeight;
            // Restore the tab the user was last on. Default (0 = Calendar) on first launch.
            SelectedTabIndex = _lastExpandedTabIndex;
        }

        // Set backing field directly to control notification order.
        _isExpanded = !_isExpanded;
        s.IsExpanded = _isExpanded;
        Log.Action(_isExpanded ? "expand" : "collapse");

        // Fire IsExpanded FIRST so the View hides the HWND before any
        // visual state changes. IsCollapsedTransparent fires second,
        // updating XAML bindings while the HWND is hidden.
        OnPropertyChanged(nameof(IsExpanded));
        OnPropertyChanged(nameof(IsCollapsedTransparent));

        _settingsService.Save();
    }

    /// <summary>Maps a font display name to a WPF FontFamily and updates the global resource.</summary>
    private static void ApplyFont(string fontName)
    {
        if (System.Windows.Application.Current is null) return;

        System.Windows.Media.FontFamily ff;
        var embeddedBase = fontName switch
        {
            "Cascadia Code"   => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/Cascadia/",
            "Inter"           => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/Inter/",
            "JetBrains Mono"  => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/JetBrainsMono/",
            "Fira Code"       => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/FiraCode/",
            "Source Sans 3"   => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/SourceSans3/",
            "Source Code Pro" => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/SourceCodePro/",
            "IBM Plex Sans"   => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/IBMPlexSans/",
            "IBM Plex Mono"   => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/IBMPlexMono/",
            "Roboto"          => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/Roboto/",
            "Roboto Mono"     => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/RobotoMono/",
            "Noto Sans"       => "pack://application:,,,/NepDateWidget;component/Assets/Fonts/NotoSans/",
            _ => null
        };

        if (embeddedBase is not null)
            ff = new System.Windows.Media.FontFamily(new Uri(embeddedBase), "./#" + fontName);
        else
            // Fall back through Segoe UI then a generic sans for systems that
            // lack the requested family.
            ff = new System.Windows.Media.FontFamily(fontName + ", Segoe UI, Tahoma");

        System.Windows.Application.Current.Resources["WidgetFontFamily"] = ff;
    }

    /// <summary>
    /// Re-reads persisted settings into live properties after the Settings tab applies changes.
    /// Only non-restart properties are synced; language change triggers an app restart.
    /// </summary>
    private void SyncFromSettings()
    {
        var s = _settingsService.Current;
        Language = s.Language;
        AlwaysOnTop = s.AlwaysOnTop;
        Theme = s.Theme;
        BackgroundPreset = s.BackgroundPreset;
        CornerStyle = s.CornerStyle;
        AnimationEnabled = s.AnimationEnabled;
        AutoStart = s.AutoStart;
        TransparentWhenCollapsed = s.TransparentWhenCollapsed;
        ApplyFont(s.FontFamily);

        // Sync collapsed display toggles to mini bar
        MiniBar.ShowTimezone = s.ShowTimezone;
        MiniBar.ClockFormat = s.ClockFormat;
        MiniBar.SelectedTimezoneId = s.SelectedTimezoneId;
        MiniBar.ShowOffset = s.ShowOffset;
        MiniBar.ShowDayOfWeek = s.ShowDayOfWeek;
        MiniBar.ShowEnglishDate = s.ShowEnglishDate;

        // Sync calendar display settings
        Calendar.UpdateDisplaySettings(s.ShowEnglishDayNumbers, s.HighlightSaturdays, s.HighlightSundays,
            s.ShowTithi, s.ShowEvents, s.HighlightPublicHolidays);
        Calendar.ShowHolidayCountdown = s.ShowHolidayCountdown;
        _themeService.OverrideHighlightColor(s.HighlightColor);

        // Sync new settings
        HideOnFullscreen = s.HideOnFullscreen;
        ShowSecondsInClock = s.ShowSecondsInClock;
        ShowFiscalYear = s.ShowFiscalYear;
        NotificationDurationSeconds = s.NotificationDurationSeconds;
        NotificationSound = s.NotificationSound;

        // Sync show seconds to mini bar
        MiniBar.ShowSeconds = s.ShowSecondsInClock;

        // Sync show fiscal year to calendar
        Calendar.ShowFiscalYear = s.ShowFiscalYear;

        // Sync timezone to the Time converter so it reflects the latest setting
        Calendar.Converter.UpdateHomeTimezone(s.SelectedTimezoneId);
    }

    private void RequestExit()
    {
        _settingsService.Save();
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Called when the global hotkey fires. Expands the widget (if collapsed)
    /// and signals the View to focus the RunBox input.
    /// </summary>
    public void ActivateRunBox()
    {
        if (!_isExpanded)
            ToggleExpanded();
        // Switch to Calendar tab (index 0) so the RunBox at the bottom is visible
        // alongside the most useful default content.
        if (_selectedTabIndex == 8) // don't leave Settings tab showing
            SelectedTabIndex = 0;
        RunBoxFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Re-raises PropertyChanged for the live copy-today labels so WPF re-evaluates
    /// them when the context menu opens (in case the date changed past midnight).
    /// </summary>
    public void RefreshCopyLabels()
    {
        OnPropertyChanged(nameof(CopyTodayBsShortLabel));
        OnPropertyChanged(nameof(CopyTodayBsLongLabel));
        OnPropertyChanged(nameof(CopyTodayAdShortLabel));
        OnPropertyChanged(nameof(CopyTodayAdLongLabel));
    }

    private void RefreshMenuLabels()
    {
        MenuLanguageLabel = _localizationService.Get("menu.language");
        MenuAlwaysOnTopLabel = _localizationService.Get("menu.always_on_top");
        MenuShowClockLabel = _localizationService.Get("menu.show_clock");
        MenuShowTimezoneLabel = _localizationService.Get("menu.show_timezone");
        MenuThemeLabel = _localizationService.Get("menu.theme");
        MenuThemeDarkLabel = _localizationService.Get("menu.theme_dark");
        MenuThemeLightLabel = _localizationService.Get("menu.theme_light");
        MenuBackgroundLabel = _localizationService.Get("menu.background");
        MenuPresetDefaultLabel = _localizationService.Get("menu.preset_default");
        MenuPresetOceanLabel = _localizationService.Get("menu.preset_ocean");
        MenuPresetForestLabel = _localizationService.Get("menu.preset_forest");
        MenuPresetSunsetLabel = _localizationService.Get("menu.preset_sunset");
        MenuPresetMonoLabel = _localizationService.Get("menu.preset_monochrome");
        MenuCornerLabel = _localizationService.Get("menu.corner_style");
        MenuCornerRoundedLabel = _localizationService.Get("menu.corner_rounded");
        MenuCornerSharpLabel = _localizationService.Get("menu.corner_sharp");
        MenuAnimationLabel = _localizationService.Get("menu.animation");
        MenuAutoStartLabel = _localizationService.Get("menu.auto_start");
        MenuExitLabel = _localizationService.Get("menu.exit");
        MenuSettingsLabel = _localizationService.Get("tab.settings");
        MenuMoreLabel = _localizationService.Get("tab.more");
        MenuCopyTodayLabel = _localizationService.Get("menu.copy_today");
        RefreshCalendarTabLabel();
        TabDateLabel     = _localizationService.Get("tab.converter");
        TabSettingsLabel = _localizationService.Get("tab.settings");
        TabUnitLabel     = _localizationService.Get("tab.unit");
        TabBankLabel     = _localizationService.Get("tab.banking");
        TabNetworkLabel  = _localizationService.Get("tab.network");
        TabTextLabel     = _localizationService.Get("tab.text");
        TabAboutLabel    = _localizationService.Get("tab.about");
        TabMoreLabel     = _localizationService.Get("tab.more");
        MenuToolsConvertLabel = _localizationService.Get("menu.tools_convert");
        MenuToolsDaysLabel    = _localizationService.Get("menu.tools_days");
        MenuToolsTimeLabel    = _localizationService.Get("menu.tools_time");
        MenuToolsSectionLabel = _localizationService.Get("menu.tools_section");
        MenuBankingSectionLabel  = _localizationService.Get("menu.banking_section");
        MenuBankingInterestLabel = _localizationService.Get("menu.banking_interest");
        MenuBankingEmiLabel      = _localizationService.Get("menu.banking_emi");
        MenuUnitSectionLabel = _localizationService.Get("menu.unit_section");
        MenuUnitAreaLabel = _localizationService.Get("menu.unit_area");
        MenuUnitScriptLabel = _localizationService.Get("menu.unit_script");
        MenuUnitWeightLabel = _localizationService.Get("menu.unit_weight");
        MenuNetworkSectionLabel = _localizationService.Get("menu.network_section");
        MenuNetworkMyIpLabel    = _localizationService.Get("net.mode_myip");
        MenuNetworkPingLabel    = _localizationService.Get("net.mode_ping");
        MenuNetworkScanLabel    = _localizationService.Get("net.mode_scan");
        MenuNetworkTraceLabel   = _localizationService.Get("net.mode_trace");
        MenuNetworkWhoisLabel   = _localizationService.Get("net.mode_whois");
        MenuNetworkDnsLabel     = _localizationService.Get("net.mode_dns");
        MenuTextSectionLabel  = _localizationService.Get("menu.text_section");
        MenuTextUnicodeLabel  = _localizationService.Get("menu.text_unicode");
        MenuTextWordLabel     = _localizationService.Get("menu.text_word");
        MenuTextPasswordLabel = _localizationService.Get("menu.text_password");
        MenuTextScriptLabel   = _localizationService.Get("menu.text_script");

        TooltipAbout    = _localizationService.Get("tooltip.about");
        TooltipMinimize = _localizationService.Get("tooltip.minimize");
        TooltipSettings = _localizationService.Get("tooltip.settings");

        OnPropertyChanged(nameof(MenuLanguageLabel));
        OnPropertyChanged(nameof(MenuAlwaysOnTopLabel));
        OnPropertyChanged(nameof(MenuShowClockLabel));
        OnPropertyChanged(nameof(MenuShowTimezoneLabel));
        OnPropertyChanged(nameof(MenuThemeLabel));
        OnPropertyChanged(nameof(MenuThemeDarkLabel));
        OnPropertyChanged(nameof(MenuThemeLightLabel));
        OnPropertyChanged(nameof(MenuBackgroundLabel));
        OnPropertyChanged(nameof(MenuPresetDefaultLabel));
        OnPropertyChanged(nameof(MenuPresetOceanLabel));
        OnPropertyChanged(nameof(MenuPresetForestLabel));
        OnPropertyChanged(nameof(MenuPresetSunsetLabel));
        OnPropertyChanged(nameof(MenuPresetMonoLabel));
        OnPropertyChanged(nameof(MenuCornerLabel));
        OnPropertyChanged(nameof(MenuCornerRoundedLabel));
        OnPropertyChanged(nameof(MenuCornerSharpLabel));
        OnPropertyChanged(nameof(MenuAnimationLabel));
        OnPropertyChanged(nameof(MenuAutoStartLabel));
        OnPropertyChanged(nameof(MenuExitLabel));
        OnPropertyChanged(nameof(MenuSettingsLabel));
        OnPropertyChanged(nameof(MenuMoreLabel));
        OnPropertyChanged(nameof(MenuCopyTodayLabel));
        OnPropertyChanged(nameof(TabHomeLabel));
        OnPropertyChanged(nameof(TabDateLabel));
        OnPropertyChanged(nameof(TabSettingsLabel));
        OnPropertyChanged(nameof(TabUnitLabel));
        OnPropertyChanged(nameof(TabBankLabel));
        OnPropertyChanged(nameof(TabNetworkLabel));
        OnPropertyChanged(nameof(TabTextLabel));
        OnPropertyChanged(nameof(TabAboutLabel));
        OnPropertyChanged(nameof(TabMoreLabel));
        OnPropertyChanged(nameof(MenuToolsConvertLabel));
        OnPropertyChanged(nameof(MenuToolsDaysLabel));
        OnPropertyChanged(nameof(MenuToolsTimeLabel));
        OnPropertyChanged(nameof(MenuToolsSectionLabel));
        OnPropertyChanged(nameof(MenuBankingSectionLabel));
        OnPropertyChanged(nameof(MenuBankingInterestLabel));
        OnPropertyChanged(nameof(MenuBankingEmiLabel));
        OnPropertyChanged(nameof(MenuUnitSectionLabel));
        OnPropertyChanged(nameof(MenuUnitAreaLabel));
        OnPropertyChanged(nameof(MenuUnitScriptLabel));
        OnPropertyChanged(nameof(MenuUnitWeightLabel));
        OnPropertyChanged(nameof(MenuNetworkSectionLabel));
        OnPropertyChanged(nameof(MenuNetworkMyIpLabel));
        OnPropertyChanged(nameof(MenuNetworkPingLabel));
        OnPropertyChanged(nameof(MenuNetworkScanLabel));
        OnPropertyChanged(nameof(MenuNetworkTraceLabel));
        OnPropertyChanged(nameof(MenuNetworkWhoisLabel));
        OnPropertyChanged(nameof(MenuNetworkDnsLabel));
        OnPropertyChanged(nameof(MenuTextSectionLabel));
        OnPropertyChanged(nameof(MenuTextUnicodeLabel));
        OnPropertyChanged(nameof(MenuTextWordLabel));
        OnPropertyChanged(nameof(MenuTextPasswordLabel));
        OnPropertyChanged(nameof(MenuTextScriptLabel));
        OnPropertyChanged(nameof(TooltipAbout));
        OnPropertyChanged(nameof(TooltipMinimize));
        OnPropertyChanged(nameof(TooltipSettings));
    }

    private void NotifyPresetSelection()
    {
        OnPropertyChanged(nameof(IsPresetDefault));
        OnPropertyChanged(nameof(IsPresetOcean));
        OnPropertyChanged(nameof(IsPresetForest));
        OnPropertyChanged(nameof(IsPresetSunset));
        OnPropertyChanged(nameof(IsPresetMonochrome));
        OnPropertyChanged(nameof(IsPresetAurora));
        OnPropertyChanged(nameof(IsPresetCherry));
        OnPropertyChanged(nameof(IsPresetMidnight));
        OnPropertyChanged(nameof(IsPresetSlate));
        OnPropertyChanged(nameof(IsPresetEmber));
    }

    private void RefreshCalendarTabLabel()
    {
        int missed = Calendar.MissedReminderCount;
        if (missed > 0)
            TabHomeLabel = string.Format(_localizationService.Get("reminder.missed_badge"), missed);
        else
            TabHomeLabel = _localizationService.Get("tab.calendar");
        OnPropertyChanged(nameof(TabHomeLabel));
    }

    public void Dispose()
    {
        MiniBar.Dispose();
        Settings.Dispose();
    }
}
