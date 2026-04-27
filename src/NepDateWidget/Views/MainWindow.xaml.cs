using Microsoft.Win32;
using NepDateWidget.Helpers;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace NepDateWidget.Views;

/// <summary>
/// The persistent mini pill. Owns application lifetime, Win32 desktop anchoring
/// (Shell_TrayWnd ownership for Win+D survival), fullscreen detection, the global
/// RunBox hotkey, and the lazy lifecycle of <see cref="ExpandedShellWindow"/>.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private IntPtr _hwnd;

    // ── Drag-vs-click ────────────────────────────────────────────────────────
    private Point _mouseDownPos;
    private bool _hasDragged;

    // ── Lifetime ─────────────────────────────────────────────────────────────
    private bool _allowClose;
    private bool _initialized;
    private readonly DispatcherTimer _saveTimer;

    // ── Fullscreen + topmost recovery ────────────────────────────────────────
    private readonly DispatcherTimer _topmostTimer;
    private bool _hiddenForFullscreen;
    private IntPtr _winEventHook;
    private Win32Interop.WinEventDelegate? _winEventProc;

    // ── Reminders ────────────────────────────────────────────────────────────
    private IReminderService? _reminderService;
    private ILocalizationService? _localizationService;
    private INepaliDateAdapter? _nepaliDateAdapter;
    private INotesService? _notesService;
    private readonly List<NotificationPopup> _activeNotifications = new();
    private ReminderPopup? _currentReminderPopup;
    private DayInfoPopup? _currentDayInfoPopup;

    // ── Hotkey ───────────────────────────────────────────────────────────────
    private const int HOTKEY_ID_RUNBOX = 0x7001;
    private bool _hotkeyRegistered;
    private const int SaveDebounceMs = 800;

    // ── Shell window (lazy) ──────────────────────────────────────────────────
    private ExpandedShellWindow? _shell;

    private readonly ISettingsService _settingsService;

    public MainWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.ExitRequested += ViewModel_ExitRequested;

        // RunBox focus request: route to the shell when it is open, else open the shell.
        viewModel.RunBoxFocusRequested += (_, _) =>
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (_shell is { IsVisible: true } shell)
                    shell.FocusRunBox();
            });
        };

        // Re-register hotkey when settings change.
        viewModel.Settings.SettingsApplied += (_, _) =>
        {
            if (_hwnd != IntPtr.Zero)
                RegisterRunBoxHotkey();
        };

        if (Application.Current is not null)
        {
            Application.Current.SessionEnding += (_, _) =>
            {
                ViewModel.SaveSettings();
                _allowClose = true;
            };
        }

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SaveDebounceMs) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            ViewModel.SaveSettings();
        };

        _topmostTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _topmostTimer.Tick += (_, _) =>
        {
            EnsureShellOwner();
            FullScreenCheck();
            RelocateToTopmost();
        };
        _topmostTimer.Start();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnSystemTimeChanged;

        _winEventProc = OnForegroundChanged;
        _winEventHook = Win32Interop.SetWinEventHook(
            Win32Interop.EVENT_SYSTEM_FOREGROUND, Win32Interop.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc,
            0, 0, Win32Interop.WINEVENT_OUTOFCONTEXT | Win32Interop.WINEVENT_SKIPOWNPROCESS);
    }

    // ── Window lifetime ──────────────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        // WS_EX_TOOLWINDOW: hide the pill from Alt+Tab. The shell is a regular
        // window that DOES appear in Alt+Tab.
        var exStyle = Win32Interop.GetWindowLong(_hwnd, Win32Interop.GWL_EXSTYLE);
        Win32Interop.SetWindowLong(_hwnd, Win32Interop.GWL_EXSTYLE, exStyle | Win32Interop.WS_EX_TOOLWINDOW);

        var style = Win32Interop.GetWindowLong(_hwnd, Win32Interop.GWL_STYLE);
        Win32Interop.SetWindowLong(_hwnd, Win32Interop.GWL_STYLE,
            style & ~(Win32Interop.WS_MAXIMIZEBOX | Win32Interop.WS_MINIMIZEBOX));

        var (left, top) = ViewModel.GetInitialPosition();
        Left = left;
        Top = top;

        if (ViewModel.AlwaysOnTop)
            Win32Interop.SetWindowPos(_hwnd, Win32Interop.HWND_TOPMOST, 0, 0, 0, 0,
                Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE);

        EnsureShellOwner();

        HwndSource.FromHwnd(_hwnd)!.AddHook(WndProcHook);

        RegisterRunBoxHotkey();

        _initialized = true;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
        {
            if (_hotkeyRegistered && _hwnd != IntPtr.Zero)
            {
                Win32Interop.UnregisterHotKey(_hwnd, HOTKEY_ID_RUNBOX);
                _hotkeyRegistered = false;
            }

            if (_winEventHook != IntPtr.Zero)
            {
                Win32Interop.UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }

            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.TimeChanged -= OnSystemTimeChanged;

            _saveTimer.Stop();
            ViewModel.SaveSettings();

            // Tear down the shell so we don't leak a hidden top-level window.
            if (_shell is not null)
            {
                try { _shell.ForceClose(); } catch { }
                _shell = null;
            }

            base.OnClosing(e);
            return;
        }

        // The pill itself never gets a real close request (no titlebar X). If
        // somehow asked to close (Alt+F4), swallow it.
        e.Cancel = true;
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (!_initialized) return;
        ViewModel.UpdatePosition(Left, Top);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    // ── ViewModel sync ───────────────────────────────────────────────────────

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsExpanded):
                OnExpandStateChanged();
                break;

            case nameof(MainViewModel.AlwaysOnTop):
                if (_hwnd == IntPtr.Zero) break;
                if (ViewModel.AlwaysOnTop)
                {
                    EnsureShellOwner();
                    Win32Interop.SetWindowPos(_hwnd, Win32Interop.HWND_TOPMOST, 0, 0, 0, 0,
                        Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE);
                }
                else
                {
                    new WindowInteropHelper(this).Owner = IntPtr.Zero;
                    Win32Interop.SetWindowPos(_hwnd, Win32Interop.HWND_NOTOPMOST, 0, 0, 0, 0,
                        Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE);
                }
                break;
        }
    }

    /// <summary>
    /// Show or hide the shell when the IsExpanded flag flips. Only one window is
    /// visible at a time: the pill is hidden while the shell is open.
    /// </summary>
    private void OnExpandStateChanged()
    {
        if (ViewModel.IsExpanded)
        {
            // Lazy creation: the shell is allocated on first expand and reused thereafter.
            _shell ??= CreateShell();
            _shell.Show();
            _shell.Activate();
            // The mini bar stays visible while the shell is open so the user can
            // still see the live clock / date and use it to collapse back.
        }
        else
        {
            if (_shell is not null)
                _shell.Hide();

            // Re-assert topmost / shell owner so Win+D still works after a shell session.
            EnsureShellOwner();
            if (ViewModel.AlwaysOnTop && _hwnd != IntPtr.Zero)
                Win32Interop.SetWindowPos(_hwnd, Win32Interop.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE);
        }
    }

    private ExpandedShellWindow CreateShell()
    {
        var shell = new ExpandedShellWindow(ViewModel, _settingsService);
        // Detach the shell from the pill as Owner: an owned window inherits hide/show
        // with the owner, which we do not want here. The shell stands on its own.
        return shell;
    }

    private void ViewModel_ExitRequested(object? sender, EventArgs e)
    {
        _allowClose = true;
        Close();
    }

    // ── Win32 hook: hotkey + shell ownership recovery ────────────────────────

    private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Interop.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_RUNBOX)
        {
            handled = true;
            Dispatcher.BeginInvoke(() =>
            {
                if (_hiddenForFullscreen)
                {
                    _hiddenForFullscreen = false;
                    Show();
                }

                // Hotkey always activates the run box: open the shell if needed,
                // then focus the input. ActivateRunBox sets IsExpanded=true which
                // triggers OnExpandStateChanged → CreateShell → Show.
                ViewModel.ActivateRunBox();

                Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
                {
                    _shell?.FocusRunBox();
                });
            });
            return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }

    // ── Hotkey registration ──────────────────────────────────────────────────

    private void RegisterRunBoxHotkey()
    {
        if (_hwnd == IntPtr.Zero) return;

        if (_hotkeyRegistered)
        {
            Win32Interop.UnregisterHotKey(_hwnd, HOTKEY_ID_RUNBOX);
            _hotkeyRegistered = false;
        }

        var mod = _settingsService.Current.RunBoxHotkeyModifiers;
        var key = _settingsService.Current.RunBoxHotkeyKey;

        if (mod == 0 && key == 0)
            return;

        if (Win32Interop.RegisterHotKey(_hwnd, HOTKEY_ID_RUNBOX, (uint)mod, (uint)key))
        {
            _hotkeyRegistered = true;
            Log.Info($"RunBox hotkey registered: mod=0x{mod:X2} key=0x{key:X2}");
        }
        else
        {
            Log.Warn($"RunBox hotkey registration failed: mod=0x{mod:X2} key=0x{key:X2}");
        }
    }

    // ── Shell owner / topmost recovery ───────────────────────────────────────

    private void EnsureShellOwner()
    {
        if (!ViewModel.AlwaysOnTop || _hwnd == IntPtr.Zero) return;

        IntPtr shellTray = Win32Interop.FindWindow("Shell_TrayWnd", null);
        if (shellTray == IntPtr.Zero) return;

        var interop = new WindowInteropHelper(this);
        if (interop.Owner != shellTray)
            interop.Owner = shellTray;
    }

    private void RelocateToTopmost()
    {
        if (!ViewModel.AlwaysOnTop || _hwnd == IntPtr.Zero) return;

        IntPtr shellTray = Win32Interop.FindWindow("Shell_TrayWnd", null);
        if (shellTray == IntPtr.Zero) return;

        for (IntPtr h = Win32Interop.GetWindow(_hwnd, Win32Interop.GW_HWNDPREV);
             h != IntPtr.Zero;
             h = Win32Interop.GetWindow(h, Win32Interop.GW_HWNDPREV))
        {
            if (h == shellTray)
            {
                Win32Interop.SetWindowPos(_hwnd, Win32Interop.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_ASYNCWINDOWPOS);
                break;
            }
        }
    }

    // ── Fullscreen detection ─────────────────────────────────────────────────

    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        Dispatcher.BeginInvoke(() =>
        {
            FullScreenCheck();
            RelocateToTopmost();
        });
    }

    private void FullScreenCheck()
    {
        if (_hwnd == IntPtr.Zero) return;
        if (!ViewModel.HideOnFullscreen) return;

        // The shell is a normal opaque window; the OS already manages it. Only
        // the pill needs the hide-for-fullscreen treatment and only when visible.
        if (ViewModel.IsExpanded) return;

        if (Win32Interop.IsForegroundFullscreen())
        {
            if (!_hiddenForFullscreen)
            {
                _hiddenForFullscreen = true;
                Hide();
            }
        }
        else if (_hiddenForFullscreen)
        {
            _hiddenForFullscreen = false;
            Show();
            if (ViewModel.AlwaysOnTop)
                Win32Interop.SetWindowPos(_hwnd, Win32Interop.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE);
        }
    }

    // ── System events ────────────────────────────────────────────────────────

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ViewModel.MiniBar.Refresh();
                ViewModel.Calendar.OnLanguageChanged();
                Log.Info("Refreshed after power resume");
            });
        }
    }

    private void OnSystemTimeChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ViewModel.MiniBar.Refresh();
            ViewModel.Calendar.OnLanguageChanged();
            Log.Info("Refreshed after system time change");
        });
    }

    // ── Drag-vs-click on the pill ────────────────────────────────────────────

    private void WidgetBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownPos = e.GetPosition(this);
        _hasDragged = false;
    }

    private void WidgetBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _hasDragged)
            return;

        if (Mouse.Captured != null && !ReferenceEquals(Mouse.Captured, sender))
            return;

        var pos = e.GetPosition(this);
        var dx = Math.Abs(pos.X - _mouseDownPos.X);
        var dy = Math.Abs(pos.Y - _mouseDownPos.Y);

        if (dx > SystemParameters.MinimumHorizontalDragDistance ||
            dy > SystemParameters.MinimumVerticalDragDistance)
        {
            _hasDragged = true;
            try
            {
                DragMove();
                ViewModel.UpdatePosition(Left, Top);
            }
            catch (InvalidOperationException)
            {
                _hasDragged = false;
            }
        }
    }

    private void WidgetBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_hasDragged)
        {
            ViewModel.ToggleExpandedCommand.Execute(null);
        }
        _hasDragged = false;
    }

    private void MiniBar_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        ViewModel.RefreshCopyLabels();
    }

    // ── Reminder integration ─────────────────────────────────────────────────

    public void SetupReminders(IReminderService reminderService, ILocalizationService localizationService, INepaliDateAdapter adapter, INotesService? notesService = null)
    {
        _reminderService = reminderService;
        _localizationService = localizationService;
        _nepaliDateAdapter = adapter;
        _notesService = notesService;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) => CheckAndFireReminders();
        timer.Start();

        Dispatcher.BeginInvoke(DispatcherPriority.Background, CheckAndFireReminders);

        // Daily events announcer: fires at most once per AD day, on first
        // launch only. Deferred to Background priority so it never delays the
        // first paint of the widget pill.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, MaybeShowDailyEventsNotification);

        ViewModel.More.EditReminderRequested += OnMoreEditReminderRequested;
    }

    private void OnMoreEditReminderRequested(string reminderId)
    {
        if (_reminderService is null || _localizationService is null || _nepaliDateAdapter is null) return;
        var entry = _reminderService.GetAll().FirstOrDefault(r => r.Id == reminderId);
        if (entry is null) return;
        var parsed = Models.ReminderEntry.ParseDate(entry.BsDate);
        if (parsed is null) return;
        var (y, m, d) = parsed.Value;
        OpenReminderPopup(y, m, d);
        if (_currentReminderPopup?.DataContext is ReminderViewModel rvm)
            rvm.EditCommand.Execute(reminderId);
    }

    /// <summary>
    /// Returns the window that should serve as the visual anchor for child popups
    /// (reminder, day-info). When the shell is open, popups should track the shell;
    /// otherwise they track the pill.
    /// </summary>
    private Window AnchorWindow => _shell is { IsVisible: true } ? _shell : (Window)this;

    public void OpenReminderPopup(int bsYear, int bsMonth, int bsDay)
    {
        if (_reminderService is null || _localizationService is null || _nepaliDateAdapter is null) return;

        if (_currentReminderPopup is { IsLoaded: true } existing)
        {
            var existingVm = existing.DataContext as ReminderViewModel;
            if (existingVm is { IsEditing: true, IsDirty: true })
            {
                existingVm.ShowDiscardBanner = true;
                try { existing.Activate(); } catch { }
                return;
            }
            try { existing.Close(); } catch { }
        }

        var vm = new ReminderViewModel(_reminderService, _localizationService, _nepaliDateAdapter, bsYear, bsMonth, bsDay);
        var popup = new ReminderPopup(vm);

        var anchor = AnchorWindow;
        popup.WindowStartupLocation = WindowStartupLocation.Manual;
        popup.Left = anchor.Left + (anchor.ActualWidth - 300) / 2;
        popup.Top  = anchor.Top  + 40;
        popup.Topmost = true;
        _currentReminderPopup = popup;
        popup.Closed += (_, _) =>
        {
            _currentReminderPopup = null;
            ViewModel.Calendar.OnLanguageChanged();
        };
        popup.Show();
    }

    public void OpenDayInfoPopup(int bsYear, int bsMonth, int bsDay)
    {
        if (_localizationService is null || _nepaliDateAdapter is null) return;

        if (_currentDayInfoPopup is { IsLoaded: true } existing)
        {
            try { existing.Close(); } catch { }
        }

        var vm = new DayInfoViewModel(
            bsYear, bsMonth, bsDay,
            _settingsService, _nepaliDateAdapter, _localizationService, _reminderService, _notesService);

        var popup = new DayInfoPopup(vm);
        var anchor = AnchorWindow;
        popup.WindowStartupLocation = WindowStartupLocation.Manual;
        popup.Left = anchor.Left + (anchor.ActualWidth - 300) / 2;
        popup.Top  = anchor.Top  + 40;
        popup.Topmost = true;
        _currentDayInfoPopup = popup;

        vm.AddReminderRequested += (y, m, d) =>
        {
            OpenReminderPopup(y, m, d);
        };

        popup.Closed += (_, _) =>
        {
            _currentDayInfoPopup = null;
            ViewModel.Calendar.OnLanguageChanged();
        };

        popup.Show();
    }

    private void CheckAndFireReminders()
    {
        if (_reminderService is null || _localizationService is null) return;

        var fired = _reminderService.CheckAndFireDueReminders(DateTime.UtcNow);
        if (fired.Count == 0) return;

        _activeNotifications.RemoveAll(n => !n.IsLoaded);

        var headerLabel = _localizationService.Get("reminder.notification");
        var dismissLabel = _localizationService.Get("reminder.dismiss");

        int shown = 0;
        foreach (var reminder in fired)
        {
            if (_activeNotifications.Count >= 5) break;

            bool playSound = shown == 0 && ViewModel.NotificationSound;
            var notification = new NotificationPopup(reminder, headerLabel, dismissLabel,
                _activeNotifications.Count, playSound, ViewModel.NotificationDurationSeconds);
            notification.Closed += (s, _) =>
            {
                var n = (NotificationPopup)s!;
                n.NavigateRequested -= OnNotificationNavigateRequested;
                _activeNotifications.Remove(n);
            };
            notification.NavigateRequested += OnNotificationNavigateRequested;
            _activeNotifications.Add(notification);
            notification.Show();
            shown++;
        }

        if (shown > 0)
            Log.Action($"fired {shown} reminder notification(s)");
    }

    /// <summary>
    /// Once per AD day, on first widget launch of that day, surfaces a
    /// notification listing today's calendar events (excluding tithis). The
    /// guard <see cref="DailyEventsAnnouncer.ShouldFire"/> + the persisted
    /// <c>LastDailyEventsNotificationDate</c> setting ensures it never fires
    /// twice for the same day even across restarts. Always best-effort: any
    /// failure (out-of-range BS date, settings save error, popup error) is
    /// logged and swallowed so it cannot break startup.
    /// </summary>
    private void MaybeShowDailyEventsNotification()
    {
        if (_localizationService is null || _nepaliDateAdapter is null) return;

        try
        {
            var settings = _settingsService.Current;
            if (!settings.ShowDailyEventsNotification) return;

            var todayAd = _nepaliDateAdapter.GetTodayAd().Date;
            var (bsY, bsM, bsD) = _nepaliDateAdapter.GetTodayBs();

            // Pull today's metadata. EventsEn/EventsNp are already separate
            // from tithi by the adapter contract, so no extra filtering needed.
            var info = _nepaliDateAdapter.GetCalendarInfo(bsY, bsM, bsD);
            bool isNepali = string.Equals(_localizationService.CurrentLanguage, "ne", StringComparison.OrdinalIgnoreCase);
            var events = isNepali ? info.EventsNp : info.EventsEn;

            if (!Helpers.DailyEventsAnnouncer.ShouldFire(
                    todayAd, settings.LastDailyEventsNotificationDate,
                    settings.ShowDailyEventsNotification, events?.Length ?? 0))
                return;

            var headerLabel  = _localizationService.Get("daily_events.header");
            var titleLabel   = _localizationService.Get("daily_events.title");
            var dismissLabel = _localizationService.Get("reminder.dismiss");
            var body         = Helpers.DailyEventsAnnouncer.FormatBody(events!);

            _activeNotifications.RemoveAll(n => !n.IsLoaded);

            var popup = new NotificationPopup(
                headerLabel, titleLabel, body, dismissLabel,
                _activeNotifications.Count,
                playSound: settings.NotificationSound,
                durationSeconds: settings.NotificationDurationSeconds);

            popup.Closed += (s, _) =>
            {
                if (s is NotificationPopup n) _activeNotifications.Remove(n);
            };
            _activeNotifications.Add(popup);
            popup.Show();

            // Persist last-shown immediately so a crash before the next save
            // still prevents a double notification later in the day.
            settings.LastDailyEventsNotificationDate = Helpers.DailyEventsAnnouncer.ToIsoDate(todayAd);
            _settingsService.Save();

            Log.Action($"daily events notification: shown {events!.Length} event(s) for {settings.LastDailyEventsNotificationDate}");
        }
        catch (Exception ex)
        {
            Log.Error("daily events notification failed", ex);
        }
    }

    private void OnNotificationNavigateRequested(string reminderId)
    {
        if (!ViewModel.IsExpanded)
            ViewModel.ToggleExpandedCommand.Execute(null);
        ViewModel.SelectedTabIndex = 6;
        ViewModel.More.NavigateToReminder(reminderId);

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            (_shell as Window ?? this).Activate();
        });
    }
}
