namespace NepDateWidget.Models;

/// <summary>
/// Persisted user preferences. Schema version allows future migrations.
/// All properties must have safe defaults so the app starts cleanly even with no settings file.
/// </summary>
public sealed class WidgetSettings
{
    public int SchemaVersion { get; set; } = 1;

    // Window state. The collapsed pill auto-positions on first run via
    // ScreenBoundsHelper.GetFirstRunPosition; these defaults are only used if
    // a settings file from a previous version is missing the fields.
    public double WindowLeft { get; set; } = 0;
    public double WindowTop { get; set; } = 0;
    public double? ExpandedWindowLeft { get; set; }
    public double? ExpandedWindowTop  { get; set; }
    public double ExpandedWidth { get; set; } = 840;
    public double ExpandedHeight { get; set; } = 750;
    public bool IsExpanded { get; set; } = false;

    // Display preferences
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "Light";
    public string BackgroundPreset { get; set; } = "Forest";
    public string CornerStyle { get; set; } = "Rounded";
    public string FontFamily { get; set; } = "Segoe UI";

    // Behavior flags
    public bool AlwaysOnTop { get; set; } = true;
    public bool AutoStart { get; set; } = true;
    public bool AnimationEnabled { get; set; } = true;
    public bool TransparentWhenCollapsed { get; set; } = true;

    // Calendar display
    public bool ShowEnglishDayNumbers { get; set; } = true;
    public bool HighlightSaturdays { get; set; } = true;
    public bool HighlightSundays { get; set; } = true;
    /// <summary>User-chosen highlight color hex (e.g. "#E53935"). Empty means use theme default.</summary>
    public string HighlightColor { get; set; } = "#F4511E";
    public bool ShowTithi { get; set; } = true;
    public bool ShowEvents { get; set; } = true;
    public bool HighlightPublicHolidays { get; set; } = true;

    /// <summary>Per-day notes. Key is "YYYY-MM-DD" (BS date). Value is the note text.</summary>
    public Dictionary<string, string> DayNotes { get; set; } = new();

    // Collapsed mini-bar element visibility
    public bool ShowTimezone { get; set; } = true;
    public string ClockFormat { get; set; } = "12h";
    public string SelectedTimezoneId { get; set; } = "Nepal Standard Time";
    public bool ShowOffset { get; set; } = false;
    public bool ShowDayOfWeek { get; set; } = true;
    public bool ShowEnglishDate { get; set; } = true;
    /// <summary>
    /// Show a small "X days until {holiday}" line in the mini-bar (and the
    /// reused mini-bar inside the expanded title bar). Hover surfaces the
    /// next ~12 months of public holidays. Defaults to on.
    /// </summary>
    public bool ShowHolidayCountdown { get; set; } = true;

    // Calendar
    /// <summary>
    /// User-highlighted BS dates in "YYYY-MM-DD" format.
    /// Editing UI is deferred to a future milestone; the list is persisted from day one.
    /// </summary>
    public List<string> HighlightedDays { get; set; } = new();

    // Converter
    public string ConverterDefaultDirection { get; set; } = "BStoAD";

    // Logging
    /// <summary>Maximum size of nepdate.log before old entries are trimmed. Range: 5–100 MB.</summary>
    public int LogMaxSizeMb { get; set; } = 10;

    // RunBox
    public List<string> RunHistory { get; set; } =
    [
        "lusrmgr.msc",
        "excel",
        "cmd",
        "powershell",
        "wt",
        "explorer",
        "notepad",
        "pbrush",
        "winword",
        "shutdown -h",
        "control",
        "appwiz.cpl",
        "ncpa.cpl",
        "%temp%",
        "temp",
        "devmgmt.msc",
        "services.msc",
        "eventvwr",
        "regedit",
        "sysdm.cpl",
        "msconfig",
        "cleanmgr",
        "diskmgmt.msc",
        "resmon",
        "perfmon",
        "taskschd.msc",
        "wf.msc",
        "inetcpl.cpl",
        "optionalfeatures",
        "compmgmt.msc",
        "control printers",
        "control userpasswords2",
        "netplwiz",
        "dxdiag",
        "snippingtool",
        "osk",
        "magnify",
        "winver",
        "gpedit.msc",
        "secpol.msc",
        "fsmgmt.msc",
        "mmsys.cpl",
    ];

    // Global hotkey for RunBox (Win32 modifier flags and virtual key code)
    // Default: Ctrl+Shift+Space (MOD_CONTROL|MOD_SHIFT = 0x0006, VK_SPACE = 0x20)
    public int RunBoxHotkeyModifiers { get; set; } = 6;
    public int RunBoxHotkeyKey { get; set; } = 0x20;

    // Notification
    /// <summary>How many seconds a reminder notification stays on screen before auto-dismissing. Range: 5–60.</summary>
    public int NotificationDurationSeconds { get; set; } = 10;
    public bool NotificationSound { get; set; } = true;

    // Reminder
    // Check interval is fixed at 5 seconds (not user-configurable).

    // Clock
    public bool ShowSecondsInClock { get; set; } = false;

    // Calendar
    public bool ShowFiscalYear { get; set; } = false;

    /// <summary>
    /// When true, the widget shows a one-time notification on the first launch
    /// of each AD day listing today's calendar events (excluding tithis).
    /// Tracked via <see cref="LastDailyEventsNotificationDate"/>.
    /// </summary>
    public bool ShowDailyEventsNotification { get; set; } = true;

    /// <summary>
    /// AD date (yyyy-MM-dd) of the last day a daily events notification was
    /// shown. Empty when never shown. Used to enforce the once-per-day rule
    /// across app restarts.
    /// </summary>
    public string LastDailyEventsNotificationDate { get; set; } = string.Empty;

    // Expand behavior
    /// <summary>
    /// Tab index that was selected the last time the widget was expanded.
    /// Persisted so the widget always reopens on the user's last tab. Range: 0–8.
    /// </summary>
    public int LastExpandedTab { get; set; } = 0;

    // Fullscreen
    public bool HideOnFullscreen { get; set; } = true;

    /// <summary>
    /// Whether the app should periodically check for updates from the configured
    /// release feed (Velopack / GitHub Releases). User opt-in.
    /// </summary>
    public bool AutoCheckForUpdates { get; set; } = true;

    /// <summary>
    /// Last successful update check (UTC). Used to throttle background checks
    /// to once per UpdateCheckIntervalHours.
    /// </summary>
    public DateTime? LastUpdateCheckUtc { get; set; }
}
