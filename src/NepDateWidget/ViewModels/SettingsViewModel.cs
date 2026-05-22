using NepDateWidget.Helpers;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows.Input;
using System.Windows.Threading;

namespace NepDateWidget.ViewModels;

public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _loc;
    private readonly IThemeService _themeService;
    private readonly IAutoStartService _autoStartService;
    private readonly IAppStateService? _appStateService;

    // ── Bound properties (two-way) ───────────────────────────────────────────

    private string _language;
    public string Language
    {
        get => _language;
        set
        {
            if (SetProperty(ref _language, value))
            {
                Log.Action($"setting: language {value}");
                OnPropertyChanged(nameof(IsLanguageEn));
                OnPropertyChanged(nameof(IsLanguageNe));
                Apply();
            }
        }
    }
    public bool IsLanguageEn => string.Equals(_language, "en", StringComparison.OrdinalIgnoreCase);
    public bool IsLanguageNe => string.Equals(_language, "ne", StringComparison.OrdinalIgnoreCase);

    private string _theme;
    public string Theme
    {
        get => _theme;
        set
        {
            var old = _theme;
            if (SetProperty(ref _theme, value))
            {
                Log.Action($"setting: theme {old}→{value}");
                OnPropertyChanged(nameof(IsThemeDark));
                OnPropertyChanged(nameof(IsThemeLight));
                Apply();
            }
        }
    }
    public bool IsThemeDark => string.Equals(_theme, "Dark", StringComparison.OrdinalIgnoreCase);
    public bool IsThemeLight => string.Equals(_theme, "Light", StringComparison.OrdinalIgnoreCase);

    private string _backgroundPreset;
    public string BackgroundPreset
    {
        get => _backgroundPreset;
        set
        {
            var old = _backgroundPreset;
            if (SetProperty(ref _backgroundPreset, value))
            {
                Log.Action($"setting: bg {old}→{value}");
                NotifyPresetSelection();
                Apply();
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

    private string _cornerStyle;
    public string CornerStyle
    {
        get => _cornerStyle;
        set
        {
            if (SetProperty(ref _cornerStyle, value))
            {
                Log.Action($"setting: corner {value}");
                OnPropertyChanged(nameof(IsCornerRounded));
                OnPropertyChanged(nameof(IsCornerSharp));
                Apply();
            }
        }
    }
    public bool IsCornerRounded => string.Equals(_cornerStyle, "Rounded", StringComparison.OrdinalIgnoreCase);
    public bool IsCornerSharp => string.Equals(_cornerStyle, "Sharp", StringComparison.OrdinalIgnoreCase);

    // ── Font family ──────────────────────────────────────────────────────────

    /// <summary>
    /// Font display names available in the dropdown. All non-Microsoft entries
    /// are embedded as binary resources, so they are available regardless of
    /// the user's installed fonts. WPF rasterises text through GDI rather than
    /// DirectWrite, so the font's own hinting determines small-size sharpness;
    /// every entry below has either Microsoft-grade or screen-tuned hinting.
    /// </summary>
    public static IReadOnlyList<string> FontFamilyNames { get; } = new[]
    {
        // Windows system fonts - guaranteed on Windows 10+
        "Segoe UI",
        "Calibri",
        "Verdana",
        // Embedded sans - static weights
        "Inter",
        "Source Sans 3",
        "IBM Plex Sans",
        "Roboto",
        "Noto Sans",
        // Embedded monospace
        "Cascadia Code",
        // Embedded sans - variable + display
        "Poppins",
        "Lato",
        "Montserrat",
        "Open Sans",
        "Raleway",
        "Nunito",
        "Rubik",
        "DM Sans",
        "Work Sans",
        "Quicksand",
        "Imprima",
    };

    private string _fontFamily;
    public string FontFamily
    {
        get => _fontFamily;
        set
        {
            if (SetProperty(ref _fontFamily, value))
            {
                Log.Action($"setting: font {value}");
                Apply();
            }
        }
    }

    private bool _animationEnabled;
    public bool AnimationEnabled { get => _animationEnabled; set { if (SetProperty(ref _animationEnabled, value)) { Log.Action($"setting: animation {value}"); Apply(); } } }

    private bool _autoStart;
    public bool AutoStart { get => _autoStart; set { if (SetProperty(ref _autoStart, value)) { Log.Action($"setting: auto-start {value}"); Apply(); } } }

    private bool _transparentWhenCollapsed;
    public bool TransparentWhenCollapsed { get => _transparentWhenCollapsed; set { if (SetProperty(ref _transparentWhenCollapsed, value)) { Log.Action($"setting: transparent-collapsed {value}"); Apply(); } } }

    // ── Collapsed display toggles ────────────────────────────────────────────

    private bool _showTimezone;
    public bool ShowTimezone { get => _showTimezone; set { if (SetProperty(ref _showTimezone, value)) { Log.Action($"setting: show-timezone {value}"); Apply(); } } }

    private string _clockFormat = "12h";
    public string ClockFormat
    {
        get => _clockFormat;
        set
        {
            if (SetProperty(ref _clockFormat, value))
            {
                Log.Action($"setting: clock-format {value}");
                OnPropertyChanged(nameof(IsClockFormat12h));
                OnPropertyChanged(nameof(IsClockFormat24h));
                Apply();
            }
        }
    }
    public bool IsClockFormat12h => string.Equals(_clockFormat, "12h", StringComparison.OrdinalIgnoreCase);
    public bool IsClockFormat24h => string.Equals(_clockFormat, "24h", StringComparison.OrdinalIgnoreCase);

    private bool _showOffset;
    public bool ShowOffset { get => _showOffset; set { if (SetProperty(ref _showOffset, value)) { Log.Action($"setting: show-offset {value}"); Apply(); } } }

    private bool _showDayOfWeek;
    public bool ShowDayOfWeek { get => _showDayOfWeek; set { if (SetProperty(ref _showDayOfWeek, value)) { Log.Action($"setting: show-day-of-week {value}"); Apply(); } } }

    private bool _showEnglishDate;
    public bool ShowEnglishDate { get => _showEnglishDate; set { if (SetProperty(ref _showEnglishDate, value)) { Log.Action($"setting: show-english-date {value}"); Apply(); } } }

    private bool _showHolidayCountdown;
    public bool ShowHolidayCountdown { get => _showHolidayCountdown; set { if (SetProperty(ref _showHolidayCountdown, value)) { Log.Action($"setting: show-holiday-countdown {value}"); Apply(); } } }

    private bool _showDailyEventsNotification;
    public bool ShowDailyEventsNotification { get => _showDailyEventsNotification; set { if (SetProperty(ref _showDailyEventsNotification, value)) { Log.Action($"setting: show-daily-events-notification {value}"); Apply(); } } }

    // ── Calendar display toggles ─────────────────────────────────────────────

    private bool _showEnglishDayNumbers;
    public bool ShowEnglishDayNumbers { get => _showEnglishDayNumbers; set { if (SetProperty(ref _showEnglishDayNumbers, value)) { Log.Action($"setting: show-eng-day-nums {value}"); Apply(); } } }

    private bool _highlightSaturdays;
    public bool HighlightSaturdays { get => _highlightSaturdays; set { if (SetProperty(ref _highlightSaturdays, value)) { Log.Action($"setting: highlight-saturdays {value}"); Apply(); } } }

    private bool _highlightSundays;
    public bool HighlightSundays { get => _highlightSundays; set { if (SetProperty(ref _highlightSundays, value)) { Log.Action($"setting: highlight-sundays {value}"); Apply(); } } }

    private bool _showTithi;
    public bool ShowTithi { get => _showTithi; set { if (SetProperty(ref _showTithi, value)) { Log.Action($"setting: show-tithi {value}"); Apply(); } } }

    private bool _showEvents;
    public bool ShowEvents { get => _showEvents; set { if (SetProperty(ref _showEvents, value)) { Log.Action($"setting: show-events {value}"); Apply(); } } }

    private bool _highlightPublicHolidays;
    public bool HighlightPublicHolidays { get => _highlightPublicHolidays; set { if (SetProperty(ref _highlightPublicHolidays, value)) { Log.Action($"setting: highlight-holidays {value}"); Apply(); } } }

    private string _highlightColor = string.Empty;
    public string HighlightColor
    {
        get => _highlightColor;
        set
        {
            if (SetProperty(ref _highlightColor, value))
            {
                Log.Action($"setting: highlight-color {value}");
                NotifyHighlightColorSelection();
                Apply();
            }
        }
    }
    public bool IsHighlightColorDefault => string.IsNullOrEmpty(_highlightColor);
    public bool IsHighlightColorRed => _highlightColor == "#E53935";
    public bool IsHighlightColorOrange => _highlightColor == "#F4511E";
    public bool IsHighlightColorPink => _highlightColor == "#EC407A";
    public bool IsHighlightColorPurple => _highlightColor == "#AB47BC";
    public bool IsHighlightColorBlue => _highlightColor == "#1E88E5";
    public bool IsHighlightColorTeal => _highlightColor == "#26A69A";
    public bool IsHighlightColorGreen => _highlightColor == "#43A047";
    public bool IsHighlightColorYellow => _highlightColor == "#FDD835";
    public bool IsHighlightColorAmber => _highlightColor == "#FFB300";
    public bool IsHighlightColorCyan => _highlightColor == "#00ACC1";
    public bool IsHighlightColorIndigo => _highlightColor == "#3949AB";
    public bool IsHighlightColorBrown => _highlightColor == "#6D4C41";

    // ── Timezone selection ───────────────────────────────────────────────────

    public ObservableCollection<TimezoneItem> Timezones { get; } = new();

    private TimezoneItem? _selectedTimezone;
    public TimezoneItem? SelectedTimezone
    {
        get => _selectedTimezone;
        set { if (SetProperty(ref _selectedTimezone, value)) { Log.Action($"setting: timezone {value?.Id}"); Apply(); } }
    }

    // ── Labels ───────────────────────────────────────────────────────────────
    public string LanguageLabel { get; private set; } = string.Empty;
    public string ThemeLabel { get; private set; } = string.Empty;
    public string BackgroundLabel { get; private set; } = string.Empty;
    public string CornerStyleLabel { get; private set; } = string.Empty;
    public string TransparentWhenCollapsedLabel { get; private set; } = string.Empty;
    public string AnimationLabel { get; private set; } = string.Empty;
    public string AutoStartLabel { get; private set; } = string.Empty;
    public string CollapsedDisplayLabel { get; private set; } = string.Empty;
    public string ShowTimezoneLabel { get; private set; } = string.Empty;
    public string TimezoneLabel { get; private set; } = string.Empty;
    public string ShowOffsetLabel { get; private set; } = string.Empty;
    public string ShowDayLabel { get; private set; } = string.Empty;
    public string ShowEnglishLabel { get; private set; } = string.Empty;
    public string ShowHolidayCountdownLabel { get; private set; } = string.Empty;
    public string ShowDailyEventsNotificationLabel { get; private set; } = string.Empty;
    public string ClockFormatLabel { get; private set; } = string.Empty;

    // ── Section labels ────────────────────────────────────────────────────────
    public string AppearanceSectionLabel { get; private set; } = string.Empty;
    public string BehaviorSectionLabel { get; private set; } = string.Empty;
    public string CalendarSectionLabel { get; private set; } = string.Empty;
    public string ShowEngDayNumsLabel { get; private set; } = string.Empty;
    public string HighlightSaturdaysLabel { get; private set; } = string.Empty;
    public string HighlightSundaysLabel { get; private set; } = string.Empty;
    public string HighlightColorLabel { get; private set; } = string.Empty;
    public string ShowTithiLabel { get; private set; } = string.Empty;
    public string ShowEventsLabel { get; private set; } = string.Empty;
    public string HighlightPublicHolidaysLabel { get; private set; } = string.Empty;

    // ── Log size ─────────────────────────────────────────────────────────────
    private int _logMaxSizeMb = 10;
    public int LogMaxSizeMb
    {
        get => _logMaxSizeMb;
        set
        {
            if (SetProperty(ref _logMaxSizeMb, value))
            {
                OnPropertyChanged(nameof(LogMaxSizeMbDisplay));
                Log.Action($"setting: log-size {value}MB");
                Apply();
            }
        }
    }
    public string LogMaxSizeMbDisplay => $"{_logMaxSizeMb} MB";
    public string LogSizeLabel { get; private set; } = string.Empty;
    public string ResetToDefaultsLabel { get; private set; } = string.Empty;
    public string ExportBackupLabel { get; private set; } = string.Empty;
    public string ImportBackupLabel { get; private set; } = string.Empty;
    public string FontLabel { get; private set; } = string.Empty;

    // ── Hotkey configuration ────────────────────────────────────────────────

    private int _hotkeyModifiers;
    private int _hotkeyKey;

    private string _hotkeyDisplayText = string.Empty;
    public string HotkeyDisplayText
    {
        get => _hotkeyDisplayText;
        private set => SetProperty(ref _hotkeyDisplayText, value);
    }

    private string _hotkeyErrorText = string.Empty;
    public string HotkeyErrorText
    {
        get => _hotkeyErrorText;
        private set
        {
            if (SetProperty(ref _hotkeyErrorText, value))
            {
                OnPropertyChanged(nameof(HasHotkeyError));
            }
        }
    }

    public bool HasHotkeyError => !string.IsNullOrEmpty(_hotkeyErrorText);

    private bool _isRecordingHotkey;
    public bool IsRecordingHotkey
    {
        get => _isRecordingHotkey;
        set
        {
            if (SetProperty(ref _isRecordingHotkey, value))
            {
                HotkeyErrorText = string.Empty;
                if (value)
                {
                    HotkeyDisplayText = _loc.Get("settings.hotkey_record");
                }
                else
                {
                    UpdateHotkeyDisplayText();
                }
            }
        }
    }

    public string HotkeyLabel { get; private set; } = string.Empty;
    public string HotkeyClearLabel { get; private set; } = string.Empty;

    // ── Notification settings ────────────────────────────────────────────────

    private int _notificationDurationSeconds = 10;
    public int NotificationDurationSeconds
    {
        get => _notificationDurationSeconds;
        set
        {
            if (SetProperty(ref _notificationDurationSeconds, value))
            {
                OnPropertyChanged(nameof(NotificationDurationDisplay));
                Log.Action($"setting: notification-duration {value}s");
                Apply();
            }
        }
    }
    public string NotificationDurationDisplay => $"{_notificationDurationSeconds}s";
    public string NotificationDurationLabel { get; private set; } = string.Empty;
    public string NotificationSectionLabel { get; private set; } = string.Empty;

    private bool _notificationSound = true;
    public bool NotificationSound { get => _notificationSound; set { if (SetProperty(ref _notificationSound, value)) { Log.Action($"setting: notification-sound {value}"); Apply(); } } }
    public string NotificationSoundLabel { get; private set; } = string.Empty;
    public string TestNotificationLabel { get; private set; } = string.Empty;

    // ── Clock: show seconds ──────────────────────────────────────────────────

    private bool _showSecondsInClock;
    public bool ShowSecondsInClock { get => _showSecondsInClock; set { if (SetProperty(ref _showSecondsInClock, value)) { Log.Action($"setting: show-seconds {value}"); Apply(); } } }
    public string ShowSecondsLabel { get; private set; } = string.Empty;

    // ── Calendar: show fiscal year ───────────────────────────────────────────

    private bool _showFiscalYear = true;
    public bool ShowFiscalYear { get => _showFiscalYear; set { if (SetProperty(ref _showFiscalYear, value)) { Log.Action($"setting: show-fiscal-year {value}"); Apply(); } } }
    public string ShowFiscalYearLabel { get; private set; } = string.Empty;

    // ── Help badges ─────────────────────────────────────────────────────────

    private bool _showHelpBadges = true;
    public bool ShowHelpBadges { get => _showHelpBadges; set { if (SetProperty(ref _showHelpBadges, value)) { Log.Action($"setting: show-help-badges {value}"); Apply(); } } }
    public string ShowHelpBadgesLabel { get; private set; } = string.Empty;
    public string DataFilesSectionLabel { get; private set; } = string.Empty;

    private DispatcherTimer? _dataFileMessageTimer;
    private string _dataFileMessage = string.Empty;
    public string DataFileMessage
    {
        get => _dataFileMessage;
        private set
        {
            if (SetProperty(ref _dataFileMessage, value))
            {
                OnPropertyChanged(nameof(HasDataFileMessage));
            }
        }
    }
    public bool HasDataFileMessage => !string.IsNullOrEmpty(_dataFileMessage);

    public string ThemeDarkLabel { get; private set; } = string.Empty;
    public string ThemeLightLabel { get; private set; } = string.Empty;
    public string CornerRoundedLabel { get; private set; } = string.Empty;
    public string CornerSharpLabel { get; private set; } = string.Empty;
    public string PresetDefaultLabel { get; private set; } = string.Empty;
    public string PresetOceanLabel { get; private set; } = string.Empty;
    public string PresetForestLabel { get; private set; } = string.Empty;
    public string PresetSunsetLabel { get; private set; } = string.Empty;
    public string PresetMonoLabel { get; private set; } = string.Empty;
    public string PresetAuroraLabel { get; private set; } = string.Empty;
    public string PresetCherryLabel { get; private set; } = string.Empty;
    public string PresetMidnightLabel { get; private set; } = string.Empty;
    public string PresetSlateLabel { get; private set; } = string.Empty;
    public string PresetEmberLabel { get; private set; } = string.Empty;

    public ICommand ClearHotkeyCommand { get; private set; } = null!;

    // ── Commands ─────────────────────────────────────────────────────────────
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
    public ICommand SetCornerRoundedCommand { get; }
    public ICommand SetCornerSharpCommand { get; }
    public ICommand SetClockFormat12hCommand { get; }
    public ICommand SetClockFormat24hCommand { get; }
    public ICommand ResetToDefaultsCommand { get; }
    public ICommand SetHighlightColorDefaultCommand { get; }
    public ICommand SetHighlightColorRedCommand { get; }
    public ICommand SetHighlightColorOrangeCommand { get; }
    public ICommand SetHighlightColorPinkCommand { get; }
    public ICommand SetHighlightColorPurpleCommand { get; }
    public ICommand SetHighlightColorBlueCommand { get; }
    public ICommand SetHighlightColorTealCommand { get; }
    public ICommand SetHighlightColorGreenCommand { get; }
    public ICommand SetHighlightColorYellowCommand { get; }
    public ICommand SetHighlightColorAmberCommand { get; }
    public ICommand SetHighlightColorCyanCommand { get; }
    public ICommand SetHighlightColorIndigoCommand { get; }
    public ICommand SetHighlightColorBrownCommand { get; }
    public ICommand SetPresetCommand { get; }
    public ICommand SetHighlightColorCommand { get; }
    public ICommand TestNotificationCommand { get; }
    public ICommand OpenSettingsFileCommand { get; }
    public ICommand OpenShortcutsFileCommand { get; }
    public ICommand OpenRemindersFileCommand { get; }
    public ICommand OpenNotesFileCommand { get; }
    public ICommand OpenDocumentsFileCommand { get; }
    public ICommand OpenRunHistoryFileCommand { get; }
    public ICommand OpenScriptsFileCommand { get; }
    public ICommand OpenRuntimeFileCommand { get; }
    public ICommand OpenLocalizationFileCommand { get; }
    public ICommand OpenLogFileCommand { get; }
    public ICommand ExportBackupCommand { get; }
    public ICommand ImportBackupCommand { get; }

    // ── Callback to parent (MainViewModel) ───────────────────────────────────
    public event EventHandler? SettingsApplied;
    public event EventHandler? TestNotificationRequested;

    public SettingsViewModel(
        ISettingsService settingsService,
        ILocalizationService localizationService,
        IThemeService themeService,
        IAutoStartService autoStartService,
        IAppStateService? appStateService = null)
    {
        _settingsService = settingsService;
        _loc = localizationService;
        _themeService = themeService;
        _autoStartService = autoStartService;
        _appStateService = appStateService;

        var s = _settingsService.Current;
        _language = s.Language;
        _theme = s.Theme;
        _backgroundPreset = s.BackgroundPreset;
        _cornerStyle = s.CornerStyle;
        // Migrate retired font choices to the new default so the dropdown shows a valid selection.
        // Retired: Gilroy, General Sans, Segoe UI Variable, Consolas, Georgia, Arial, Trebuchet MS,
        // Palatino Linotype, JetBrains Mono, Fira Code, Source Code Pro, IBM Plex Mono, Roboto Mono.
        _fontFamily = FontFamilyNames.Contains(s.FontFamily) ? s.FontFamily : "Open Sans";
        _animationEnabled = s.AnimationEnabled;
        _autoStart = _autoStartService.IsEnabled;
        _transparentWhenCollapsed = s.TransparentWhenCollapsed;

        // Calendar display settings
        _showEnglishDayNumbers = s.ShowEnglishDayNumbers;
        _highlightSaturdays = s.HighlightSaturdays;
        _highlightSundays = s.HighlightSundays;
        _highlightColor = s.HighlightColor;
        _showTithi = s.ShowTithi;
        _showEvents = s.ShowEvents;
        _highlightPublicHolidays = s.HighlightPublicHolidays;

        // Log settings
        _logMaxSizeMb = s.LogMaxSizeMb;

        // Collapsed display toggles
        _showTimezone = s.ShowTimezone;
        _clockFormat = s.ClockFormat;
        _showOffset = s.ShowOffset;
        _showDayOfWeek = s.ShowDayOfWeek;
        _showEnglishDate = s.ShowEnglishDate;
        _showHolidayCountdown = s.ShowHolidayCountdown;
        _showDailyEventsNotification = s.ShowDailyEventsNotification;

        // Populate timezone list
        PopulateTimezones(s.SelectedTimezoneId);

        SetLanguageEnCommand = new RelayCommand(() => Language = "en");
        SetLanguageNeCommand = new RelayCommand(() => Language = "ne");
        SetThemeDarkCommand = new RelayCommand(() => Theme = "Dark");
        SetThemeLightCommand = new RelayCommand(() => Theme = "Light");
        SetPresetCommand = new RelayCommand<string>(s => { if (s is not null) { BackgroundPreset = s; } });
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
        SetClockFormat12hCommand = new RelayCommand(() => ClockFormat = "12h");
        SetClockFormat24hCommand = new RelayCommand(() => ClockFormat = "24h");
        ResetToDefaultsCommand = new RelayCommand(DoResetToDefaults);
        ClearHotkeyCommand = new RelayCommand(ClearHotkey);
        SetHighlightColorCommand = new RelayCommand<string>(s => HighlightColor = s ?? "");
        SetHighlightColorDefaultCommand = new RelayCommand(() => HighlightColor = "");
        SetHighlightColorRedCommand = new RelayCommand(() => HighlightColor = "#E53935");
        SetHighlightColorOrangeCommand = new RelayCommand(() => HighlightColor = "#F4511E");
        SetHighlightColorPinkCommand = new RelayCommand(() => HighlightColor = "#EC407A");
        SetHighlightColorPurpleCommand = new RelayCommand(() => HighlightColor = "#AB47BC");
        SetHighlightColorBlueCommand = new RelayCommand(() => HighlightColor = "#1E88E5");
        SetHighlightColorTealCommand = new RelayCommand(() => HighlightColor = "#26A69A");
        SetHighlightColorGreenCommand = new RelayCommand(() => HighlightColor = "#43A047");
        SetHighlightColorYellowCommand = new RelayCommand(() => HighlightColor = "#FDD835");
        SetHighlightColorAmberCommand = new RelayCommand(() => HighlightColor = "#FFB300");
        SetHighlightColorCyanCommand = new RelayCommand(() => HighlightColor = "#00ACC1");
        SetHighlightColorIndigoCommand = new RelayCommand(() => HighlightColor = "#3949AB");
        SetHighlightColorBrownCommand = new RelayCommand(() => HighlightColor = "#6D4C41");

        // Hotkey init
        _hotkeyModifiers = s.RunBoxHotkeyModifiers;
        _hotkeyKey = s.RunBoxHotkeyKey;
        UpdateHotkeyDisplayText();

        // New settings init
        _notificationDurationSeconds = s.NotificationDurationSeconds;
        _notificationSound = s.NotificationSound;
        _showSecondsInClock = s.ShowSecondsInClock;
        _showFiscalYear = s.ShowFiscalYear;
        _showHelpBadges = s.ShowHelpBadges;

        TestNotificationCommand = new RelayCommand(() => TestNotificationRequested?.Invoke(this, EventArgs.Empty));

        OpenSettingsFileCommand = new RelayCommand(() => OpenFile(AppPaths.SettingsPath));
        OpenShortcutsFileCommand = new RelayCommand(() => OpenFile(AppPaths.ShortcutsPath));
        OpenRemindersFileCommand = new RelayCommand(() => OpenFile(AppPaths.RemindersPath));
        OpenNotesFileCommand = new RelayCommand(() => OpenFile(AppPaths.NotesPath));
        OpenDocumentsFileCommand = new RelayCommand(() => OpenFile(AppPaths.DocumentsPath));
        OpenRunHistoryFileCommand = new RelayCommand(() => OpenFile(AppPaths.RunHistoryPath));
        OpenScriptsFileCommand = new RelayCommand(() => OpenFile(AppPaths.ScriptsPath));
        OpenRuntimeFileCommand = new RelayCommand(() => OpenFile(AppPaths.AppStatePath));
        OpenLocalizationFileCommand = new RelayCommand(() => OpenFile(AppPaths.LocalizationPath));
        OpenLogFileCommand = new RelayCommand(() => OpenFile(AppPaths.LogPath));
        ExportBackupCommand = new RelayCommand(ExportBackup);
        ImportBackupCommand = new RelayCommand(ImportBackup);

        RefreshLabels();
    }

    public void OnLanguageChanged()
    {
        var currentLang = _loc.CurrentLanguage;
        if (!string.Equals(_language, currentLang, StringComparison.OrdinalIgnoreCase))
        {
            _language = currentLang;
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(IsLanguageEn));
            OnPropertyChanged(nameof(IsLanguageNe));
        }
        RefreshLabels();
    }

    private void PopulateTimezones(string savedId)
    {
        Timezones.Clear();
        var localTz = TimeZoneInfo.Local;
        TimezoneItem? savedItem = null;

        foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        {
            // For the local timezone, append "(Local)" so it stands out without
            // being moved to the top and disrupting the UTC-offset sort order.
            bool isLocal = tz.Id == localTz.Id;
            string label = isLocal
                ? $"{tz.DisplayName} (Local)"
                : tz.DisplayName;
            var item = new TimezoneItem(tz.Id, label);
            Timezones.Add(item);

            if (string.IsNullOrEmpty(savedId) || string.Equals(savedId, localTz.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (isLocal)
                {
                    _selectedTimezone = item;
                }
            }
            else if (string.Equals(tz.Id, savedId, StringComparison.OrdinalIgnoreCase))
            {
                savedItem = item;
            }
        }

        if (savedItem is not null)
        {
            _selectedTimezone = savedItem;
        }

        _selectedTimezone ??= Timezones.FirstOrDefault();
    }

    private void Apply()
    {
        // Guard: don't run during construction (before timezones are populated)
        if (Timezones.Count == 0)
        {
            return;
        }

        var s = _settingsService.Current;

        s.Language = _language;
        s.Theme = _theme;
        s.BackgroundPreset = _backgroundPreset;
        s.CornerStyle = _cornerStyle;
        s.FontFamily = _fontFamily;
        s.AnimationEnabled = _animationEnabled;
        s.AutoStart = _autoStart;
        s.TransparentWhenCollapsed = _transparentWhenCollapsed;

        // Calendar display settings
        s.ShowEnglishDayNumbers = _showEnglishDayNumbers;
        s.HighlightSaturdays = _highlightSaturdays;
        s.HighlightSundays = _highlightSundays;
        s.HighlightColor = _highlightColor;
        s.ShowTithi = _showTithi;
        s.ShowEvents = _showEvents;
        s.HighlightPublicHolidays = _highlightPublicHolidays;
        _themeService.OverrideHighlightColor(_highlightColor);

        // Log settings
        s.LogMaxSizeMb = _logMaxSizeMb;
        Log.UpdateMaxSize(_logMaxSizeMb);

        // Collapsed display settings
        s.ShowTimezone = _showTimezone;
        s.ClockFormat = _clockFormat;
        s.SelectedTimezoneId = _selectedTimezone?.Id ?? string.Empty;
        s.ShowOffset = _showOffset;
        s.ShowDayOfWeek = _showDayOfWeek;
        s.ShowEnglishDate = _showEnglishDate;
        s.ShowHolidayCountdown = _showHolidayCountdown;
        s.ShowDailyEventsNotification = _showDailyEventsNotification;

        // Hotkey
        s.RunBoxHotkeyModifiers = _hotkeyModifiers;
        s.RunBoxHotkeyKey = _hotkeyKey;

        // New settings
        s.NotificationDurationSeconds = _notificationDurationSeconds;
        s.NotificationSound = _notificationSound;
        s.ShowSecondsInClock = _showSecondsInClock;
        s.ShowFiscalYear = _showFiscalYear;
        s.ShowHelpBadges = _showHelpBadges;

        _autoStartService.SetEnabled(_autoStart);
        _themeService.Apply(_theme, _backgroundPreset);
        _settingsService.Save();

        SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshLabels()
    {
        LanguageLabel = _loc.Get("settings.language");
        ThemeLabel = _loc.Get("settings.theme");
        BackgroundLabel = _loc.Get("settings.background");
        CornerStyleLabel = _loc.Get("settings.corner_style");
        TransparentWhenCollapsedLabel = _loc.Get("settings.transparent_collapsed");
        AnimationLabel = _loc.Get("settings.animation");
        AutoStartLabel = _loc.Get("settings.auto_start");
        CollapsedDisplayLabel = _loc.Get("settings.collapsed_display");
        ShowTimezoneLabel = _loc.Get("settings.show_timezone");
        TimezoneLabel = _loc.Get("settings.timezone");
        ShowOffsetLabel = _loc.Get("settings.show_offset");
        ShowDayLabel = _loc.Get("settings.show_day");
        ShowEnglishLabel = _loc.Get("settings.show_english");
        ShowHolidayCountdownLabel = _loc.Get("settings.show_holiday_countdown");
        ShowDailyEventsNotificationLabel = _loc.Get("settings.show_daily_events_notification");
        ClockFormatLabel = _loc.Get("settings.clock_format");
        CalendarSectionLabel = _loc.Get("settings.calendar");
        ShowEngDayNumsLabel = _loc.Get("settings.show_eng_day_nums");
        HighlightSaturdaysLabel = _loc.Get("settings.highlight_saturdays");
        HighlightSundaysLabel = _loc.Get("settings.highlight_sundays");
        HighlightColorLabel = _loc.Get("settings.highlight_color");
        ShowTithiLabel = _loc.Get("settings.show_tithi");
        ShowEventsLabel = _loc.Get("settings.show_events");
        HighlightPublicHolidaysLabel = _loc.Get("settings.highlight_holidays");
        AppearanceSectionLabel = _loc.Get("settings.appearance");
        BehaviorSectionLabel = _loc.Get("settings.behavior");

        LogSizeLabel = _loc.Get("settings.log_size");
        ResetToDefaultsLabel = _loc.Get("settings.reset_defaults");
        ExportBackupLabel = _loc.Get("settings.data_export");
        ImportBackupLabel = _loc.Get("settings.data_import");
        FontLabel = _loc.Get("settings.font");
        HotkeyLabel = _loc.Get("settings.hotkey");
        HotkeyClearLabel = _loc.Get("settings.hotkey_clear");
        NotificationDurationLabel = _loc.Get("settings.notification_duration");
        NotificationSoundLabel = _loc.Get("settings.notification_sound");
        NotificationSectionLabel = _loc.Get("settings.notification");
        TestNotificationLabel = _loc.Get("settings.test_notification");
        ShowSecondsLabel = _loc.Get("settings.show_seconds");
        ShowFiscalYearLabel = _loc.Get("settings.show_fiscal_year");
        ShowHelpBadgesLabel = _loc.Get("settings.show_help_badges");
        DataFilesSectionLabel = _loc.Get("settings.data_files");

        ThemeDarkLabel = _loc.Get("settings.theme_dark");
        ThemeLightLabel = _loc.Get("settings.theme_light");
        CornerRoundedLabel = _loc.Get("settings.corner_rounded");
        CornerSharpLabel = _loc.Get("settings.corner_sharp");
        PresetDefaultLabel = _loc.Get("menu.preset_default");
        PresetOceanLabel = _loc.Get("menu.preset_ocean");
        PresetForestLabel = _loc.Get("menu.preset_forest");
        PresetSunsetLabel = _loc.Get("menu.preset_sunset");
        PresetMonoLabel = _loc.Get("menu.preset_monochrome");
        PresetAuroraLabel = _loc.Get("menu.preset_aurora");
        PresetCherryLabel = _loc.Get("menu.preset_cherry");
        PresetMidnightLabel = _loc.Get("menu.preset_midnight");
        PresetSlateLabel = _loc.Get("menu.preset_slate");
        PresetEmberLabel = _loc.Get("menu.preset_ember");

        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(ThemeLabel));
        OnPropertyChanged(nameof(BackgroundLabel));
        OnPropertyChanged(nameof(CornerStyleLabel));
        OnPropertyChanged(nameof(TransparentWhenCollapsedLabel));
        OnPropertyChanged(nameof(AnimationLabel));
        OnPropertyChanged(nameof(AutoStartLabel));
        OnPropertyChanged(nameof(CollapsedDisplayLabel));
        OnPropertyChanged(nameof(ShowTimezoneLabel));
        OnPropertyChanged(nameof(TimezoneLabel));
        OnPropertyChanged(nameof(ShowOffsetLabel));
        OnPropertyChanged(nameof(ShowDayLabel));
        OnPropertyChanged(nameof(ShowEnglishLabel));
        OnPropertyChanged(nameof(ShowHolidayCountdownLabel));
        OnPropertyChanged(nameof(ShowDailyEventsNotificationLabel));
        OnPropertyChanged(nameof(ClockFormatLabel));
        OnPropertyChanged(nameof(CalendarSectionLabel));
        OnPropertyChanged(nameof(ShowEngDayNumsLabel));
        OnPropertyChanged(nameof(HighlightSaturdaysLabel));
        OnPropertyChanged(nameof(HighlightSundaysLabel));
        OnPropertyChanged(nameof(HighlightColorLabel));
        OnPropertyChanged(nameof(ShowTithiLabel));
        OnPropertyChanged(nameof(ShowEventsLabel));
        OnPropertyChanged(nameof(HighlightPublicHolidaysLabel));
        OnPropertyChanged(nameof(AppearanceSectionLabel));
        OnPropertyChanged(nameof(BehaviorSectionLabel));
        OnPropertyChanged(nameof(LogSizeLabel));
        OnPropertyChanged(nameof(ResetToDefaultsLabel));
        OnPropertyChanged(nameof(ExportBackupLabel));
        OnPropertyChanged(nameof(ImportBackupLabel));
        OnPropertyChanged(nameof(FontLabel));
        OnPropertyChanged(nameof(HotkeyLabel));
        OnPropertyChanged(nameof(HotkeyClearLabel));
        OnPropertyChanged(nameof(NotificationDurationLabel));
        OnPropertyChanged(nameof(NotificationSoundLabel));
        OnPropertyChanged(nameof(NotificationSectionLabel));
        OnPropertyChanged(nameof(TestNotificationLabel));
        OnPropertyChanged(nameof(ShowSecondsLabel));
        OnPropertyChanged(nameof(ShowFiscalYearLabel));
        OnPropertyChanged(nameof(ShowHelpBadgesLabel));
        OnPropertyChanged(nameof(DataFilesSectionLabel));
        OnPropertyChanged(nameof(ThemeDarkLabel));
        OnPropertyChanged(nameof(ThemeLightLabel));
        OnPropertyChanged(nameof(CornerRoundedLabel));
        OnPropertyChanged(nameof(CornerSharpLabel));
        OnPropertyChanged(nameof(PresetDefaultLabel));
        OnPropertyChanged(nameof(PresetOceanLabel));
        OnPropertyChanged(nameof(PresetForestLabel));
        OnPropertyChanged(nameof(PresetSunsetLabel));
        OnPropertyChanged(nameof(PresetMonoLabel));
        OnPropertyChanged(nameof(PresetAuroraLabel));
        OnPropertyChanged(nameof(PresetCherryLabel));
        OnPropertyChanged(nameof(PresetMidnightLabel));
        OnPropertyChanged(nameof(PresetSlateLabel));
        OnPropertyChanged(nameof(PresetEmberLabel));
    }

    private static readonly HashSet<string> AllowedBackupEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        "config/settings.json",
        "config/localization.json",
        "config/shortcuts.json",
        "config/scripts.json",
        "data/notes.json",
        "data/reminders.json",
        "data/documents.json",
        "data/run-history.json",
        "runtime.json",
        "nepdate.log"
    };

    /// <summary>
    /// Validates a ZIP entry for import: the entry’s relative path must be on the
    /// allowlist and the resolved destination must remain inside
    /// <paramref name="dataDirectory"/> (path-traversal guard).
    /// Returns the full destination path when safe, or <c>null</c> when the entry
    /// should be skipped.
    /// </summary>
    internal static string? ResolveImportEntryPath(string entryName, string dataDirectory)
    {
        if (string.IsNullOrEmpty(entryName))
        {
            return null;
        }
        // Normalize to forward-slash form for allowlist lookup, then to OS separator for path ops.
        var forwardSlash = entryName.Replace('\\', '/');
        if (!AllowedBackupEntries.Contains(forwardSlash))
        {
            return null;
        }

        var relative = forwardSlash.Replace('/', Path.DirectorySeparatorChar);
        var dest = Path.GetFullPath(Path.Combine(dataDirectory, relative));
        var root = Path.GetFullPath(dataDirectory);
        if (!dest.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(dest, root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return dest;
    }

    private void ExportBackup()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = _loc.Get("settings.data_export"),
            Filter = "ZIP archive (*.zip)|*.zip",
            FileName = $"NepDateWidget-backup-{DateTime.Now:yyyy-MM-dd}",
            DefaultExt = ".zip"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var target = dialog.FileName;
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            using var archive = ZipFile.Open(target, ZipArchiveMode.Create);
            using (var w = new StreamWriter(archive.CreateEntry("nepdate-backup.manifest").Open(), System.Text.Encoding.UTF8))
            {
                w.Write("NepDateWidget");
            }

            string[] paths =
            [
                AppPaths.SettingsPath, AppPaths.RemindersPath, AppPaths.NotesPath,
                AppPaths.ShortcutsPath, AppPaths.DocumentsPath, AppPaths.RunHistoryPath,
                AppPaths.AppStatePath, AppPaths.ScriptsPath, AppPaths.LocalizationPath, AppPaths.LogPath
            ];
            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    archive.CreateEntryFromFile(p, Path.GetRelativePath(AppPaths.DataDirectory, p).Replace('\\', '/'));
                }
            }

            ShowDataFileMessage(_loc.Get("settings.data_export_ok"));
            Log.Action($"backup exported: {target}");
        }
        catch (Exception ex)
        {
            ShowDataFileMessage(_loc.Get("settings.data_export_failed"));
            Log.Error($"backup export failed: {ex.Message}");
        }
    }

    private void ImportBackup()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _loc.Get("settings.data_import"),
            Filter = "ZIP archive (*.zip)|*.zip"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            using var archive = ZipFile.OpenRead(dialog.FileName);
            var dataDir = AppPaths.DataDirectory;
            Directory.CreateDirectory(dataDir);

            var manifest = archive.GetEntry("nepdate-backup.manifest");
            if (manifest is null)
            {
                ShowDataFileMessage(_loc.Get("settings.data_import_invalid"));
                Log.Warn($"backup restore rejected (no manifest): {dialog.FileName}");
                return;
            }
            using (var sr = new StreamReader(manifest.Open(), System.Text.Encoding.UTF8))
            {
                if (sr.ReadToEnd() != "NepDateWidget")
                {
                    ShowDataFileMessage(_loc.Get("settings.data_import_invalid"));
                    Log.Warn($"backup restore rejected (invalid manifest): {dialog.FileName}");
                    return;
                }
            }

            foreach (var entry in archive.Entries)
            {
                // Skip directory entries
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                // Allowlist + path-traversal guard (entry.FullName is the relative path in the ZIP)
                var dest = ResolveImportEntryPath(entry.FullName, dataDir);
                if (dest is null)
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
            }
            ShowDataFileMessage(_loc.Get("settings.data_import_ok"));
            Log.Action($"backup restored: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            ShowDataFileMessage(_loc.Get("settings.data_import_failed"));
            Log.Error($"backup restore failed: {ex.Message}");
        }
    }

    private void OpenFile(string path)
    {
        EnsureDataFile(path);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn($"settings: failed to open '{path}': {ex.Message}");
        }
    }

    // Creates a data file with minimal valid content if it does not exist.
    // Called before opening a file so the editor always has something to show.
    // internal for unit tests
    internal static void EnsureDataFile(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var name = Path.GetFileName(path);
            string content;
            // JSON-object files (“{}”): notes.json is a string dict, settings.json and
            // runtime.json are plain objects, localization.json is a dict-of-dicts.
            if (name.Equals("notes.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("runtime.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("settings.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("localization.json", StringComparison.OrdinalIgnoreCase))
            {
                content = "{}";
            }
            else if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                content = "[]";
            }
            else
            {
                content = string.Empty;
            }

            AtomicFile.WriteAllText(path, content);
        }
        catch { /* best-effort - the editor will show an error if the file is still missing */ }
    }

    private void ShowDataFileMessage(string message)
    {
        if (_dataFileMessageTimer is null)
        {
            _dataFileMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _dataFileMessageTimer.Tick += (_, _) => { _dataFileMessageTimer.Stop(); DataFileMessage = string.Empty; };
        }
        _dataFileMessageTimer.Stop();
        DataFileMessage = message;
        _dataFileMessageTimer.Start();
    }

    private void DoResetToDefaults()
    {
        _settingsService.ResetToDefaults();
        var s = _settingsService.Current;

        _language = s.Language;
        _theme = s.Theme;
        _backgroundPreset = s.BackgroundPreset;
        _fontFamily = s.FontFamily;
        _cornerStyle = s.CornerStyle;
        _animationEnabled = s.AnimationEnabled;
        _autoStart = s.AutoStart;
        _transparentWhenCollapsed = s.TransparentWhenCollapsed;
        _showEnglishDayNumbers = s.ShowEnglishDayNumbers;
        _highlightSaturdays = s.HighlightSaturdays;
        _highlightSundays = s.HighlightSundays;
        _highlightColor = s.HighlightColor;
        _showTithi = s.ShowTithi;
        _showEvents = s.ShowEvents;
        _highlightPublicHolidays = s.HighlightPublicHolidays;
        _showTimezone = s.ShowTimezone;
        _clockFormat = s.ClockFormat;
        _showOffset = s.ShowOffset;
        _showDayOfWeek = s.ShowDayOfWeek;
        _showEnglishDate = s.ShowEnglishDate;
        _showHolidayCountdown = s.ShowHolidayCountdown;
        _showDailyEventsNotification = s.ShowDailyEventsNotification;
        _logMaxSizeMb = s.LogMaxSizeMb;
        _hotkeyModifiers = s.RunBoxHotkeyModifiers;
        _hotkeyKey = s.RunBoxHotkeyKey;
        UpdateHotkeyDisplayText();

        _notificationDurationSeconds = s.NotificationDurationSeconds;
        _notificationSound = s.NotificationSound;
        _showSecondsInClock = s.ShowSecondsInClock;
        _showFiscalYear = s.ShowFiscalYear;
        _showHelpBadges = s.ShowHelpBadges;

        PopulateTimezones(s.SelectedTimezoneId);

        OnPropertyChanged(nameof(Language)); OnPropertyChanged(nameof(IsLanguageEn)); OnPropertyChanged(nameof(IsLanguageNe));
        OnPropertyChanged(nameof(Theme)); OnPropertyChanged(nameof(IsThemeDark)); OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(BackgroundPreset));
        NotifyPresetSelection();
        OnPropertyChanged(nameof(CornerStyle)); OnPropertyChanged(nameof(IsCornerRounded)); OnPropertyChanged(nameof(IsCornerSharp));
        OnPropertyChanged(nameof(FontFamily));
        OnPropertyChanged(nameof(AnimationEnabled)); OnPropertyChanged(nameof(AutoStart));
        OnPropertyChanged(nameof(TransparentWhenCollapsed));
        OnPropertyChanged(nameof(ShowEnglishDayNumbers)); OnPropertyChanged(nameof(HighlightSaturdays));
        OnPropertyChanged(nameof(HighlightSundays)); OnPropertyChanged(nameof(ShowTithi)); OnPropertyChanged(nameof(ShowEvents)); OnPropertyChanged(nameof(HighlightPublicHolidays));
        OnPropertyChanged(nameof(HighlightColor));
        NotifyHighlightColorSelection();
        OnPropertyChanged(nameof(ShowTimezone)); OnPropertyChanged(nameof(ClockFormat)); OnPropertyChanged(nameof(IsClockFormat12h)); OnPropertyChanged(nameof(IsClockFormat24h));
        OnPropertyChanged(nameof(ShowOffset)); OnPropertyChanged(nameof(ShowDayOfWeek)); OnPropertyChanged(nameof(ShowEnglishDate)); OnPropertyChanged(nameof(ShowHolidayCountdown));
        OnPropertyChanged(nameof(ShowDailyEventsNotification));
        OnPropertyChanged(nameof(SelectedTimezone)); OnPropertyChanged(nameof(Timezones));
        OnPropertyChanged(nameof(LogMaxSizeMb)); OnPropertyChanged(nameof(LogMaxSizeMbDisplay));
        OnPropertyChanged(nameof(HotkeyDisplayText));
        OnPropertyChanged(nameof(NotificationDurationSeconds)); OnPropertyChanged(nameof(NotificationDurationDisplay));
        OnPropertyChanged(nameof(NotificationSound));
        OnPropertyChanged(nameof(ShowSecondsInClock)); OnPropertyChanged(nameof(ShowFiscalYear));
        OnPropertyChanged(nameof(ShowHelpBadges));

        Apply();
        Log.Action("settings: reset to defaults");
    }

    // ── Notification helpers ────────────────────────────────────────────────

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

    private void NotifyHighlightColorSelection()
    {
        OnPropertyChanged(nameof(IsHighlightColorDefault));
        OnPropertyChanged(nameof(IsHighlightColorRed));
        OnPropertyChanged(nameof(IsHighlightColorOrange));
        OnPropertyChanged(nameof(IsHighlightColorPink));
        OnPropertyChanged(nameof(IsHighlightColorPurple));
        OnPropertyChanged(nameof(IsHighlightColorBlue));
        OnPropertyChanged(nameof(IsHighlightColorTeal));
        OnPropertyChanged(nameof(IsHighlightColorGreen));
        OnPropertyChanged(nameof(IsHighlightColorYellow));
        OnPropertyChanged(nameof(IsHighlightColorAmber));
        OnPropertyChanged(nameof(IsHighlightColorCyan));
        OnPropertyChanged(nameof(IsHighlightColorIndigo));
        OnPropertyChanged(nameof(IsHighlightColorBrown));
    }

    // ── Hotkey helpers ───────────────────────────────────────────────────────

    // Win32 modifier flag constants
    private const int MOD_ALT = 0x0001;
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT = 0x0004;
    private const int MOD_WIN = 0x0008;

    public void TrySetHotkey(System.Windows.Input.ModifierKeys wpfMod, System.Windows.Input.Key wpfKey)
    {
        // Convert WPF modifiers to Win32 modifier flags
        int mod = 0;
        if (wpfMod.HasFlag(System.Windows.Input.ModifierKeys.Control))
        {
            mod |= MOD_CONTROL;
        }

        if (wpfMod.HasFlag(System.Windows.Input.ModifierKeys.Shift))
        {
            mod |= MOD_SHIFT;
        }

        if (wpfMod.HasFlag(System.Windows.Input.ModifierKeys.Alt))
        {
            mod |= MOD_ALT;
        }

        if (wpfMod.HasFlag(System.Windows.Input.ModifierKeys.Windows))
        {
            mod |= MOD_WIN;
        }

        // Must have at least one modifier
        if (mod == 0)
        {
            HotkeyErrorText = _loc.Get("settings.hotkey_reserved");
            UpdateHotkeyDisplayText();
            return;
        }

        // Convert WPF Key to Win32 virtual key code
        int vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(wpfKey);
        if (vk == 0)
        {
            UpdateHotkeyDisplayText();
            return;
        }

        // Block Windows-reserved shortcuts
        if (IsReservedHotkey(mod, vk))
        {
            HotkeyErrorText = _loc.Get("settings.hotkey_reserved");
            UpdateHotkeyDisplayText();
            return;
        }

        _hotkeyModifiers = mod;
        _hotkeyKey = vk;
        HotkeyErrorText = string.Empty;
        UpdateHotkeyDisplayText();
        Log.Action($"setting: hotkey mod=0x{mod:X2} key=0x{vk:X2}");
        Apply();
    }

    private void ClearHotkey()
    {
        _hotkeyModifiers = 0;
        _hotkeyKey = 0;
        HotkeyErrorText = string.Empty;
        UpdateHotkeyDisplayText();
        Log.Action("setting: hotkey cleared");
        Apply();
    }

    private void UpdateHotkeyDisplayText()
    {
        if (_hotkeyModifiers == 0 && _hotkeyKey == 0)
        {
            HotkeyDisplayText = string.Empty;
            return;
        }

        var parts = new List<string>(4);
        if ((_hotkeyModifiers & MOD_CONTROL) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((_hotkeyModifiers & MOD_ALT) != 0)
        {
            parts.Add("Alt");
        }

        if ((_hotkeyModifiers & MOD_SHIFT) != 0)
        {
            parts.Add("Shift");
        }

        if ((_hotkeyModifiers & MOD_WIN) != 0)
        {
            parts.Add("Win");
        }

        var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey(_hotkeyKey);
        parts.Add(key.ToString());

        HotkeyDisplayText = string.Join(" + ", parts);
    }

    private static bool IsReservedHotkey(int mod, int vk)
    {
        // Block all Win+* (OS reserves these)
        if ((mod & MOD_WIN) != 0)
        {
            return true;
        }

        // Ctrl+Alt+Del (vk 0x2E = Delete)
        if (mod == (MOD_CONTROL | MOD_ALT) && vk == 0x2E)
        {
            return true;
        }

        // Ctrl+Shift+Esc (Task Manager, vk 0x1B = Escape)
        if (mod == (MOD_CONTROL | MOD_SHIFT) && vk == 0x1B)
        {
            return true;
        }

        // Alt+F4 (vk 0x73)
        if (mod == MOD_ALT && vk == 0x73)
        {
            return true;
        }

        // Alt+Tab (vk 0x09)
        if (mod == MOD_ALT && vk == 0x09)
        {
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        _dataFileMessageTimer?.Stop();
        _dataFileMessageTimer = null;
    }
}

public sealed class TimezoneItem
{
    public string Id { get; }
    public string DisplayName { get; }
    public TimezoneItem(string id, string displayName) { Id = id; DisplayName = displayName; }
    public override string ToString() => DisplayName;

    /// <summary>
    /// Builds a display label using the system timezone display name, which is already
    /// in the standard professional format: "(UTC+05:45) Kathmandu".
    /// Pass displayNameOverride to append a suffix such as " (Local)".
    /// </summary>
    public static TimezoneItem FromTimeZoneInfo(TimeZoneInfo tz, string? displayNameOverride = null)
    {
        return new TimezoneItem(tz.Id, displayNameOverride ?? tz.DisplayName);
    }
}
