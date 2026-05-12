using Microsoft.Win32;
using NepDateWidget.Helpers;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
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
    private readonly DispatcherTimer _fullscreenTimer;
    private bool _hiddenForFullscreen;
    private bool _intentionalHide;
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
    private RunBoxSpotlightWindow? _spotlight;

    private readonly ISettingsService _settingsService;
    private readonly IAppStateService _appStateService;

    public MainWindow(MainViewModel viewModel, ISettingsService settingsService, IAppStateService appStateService)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _appStateService = appStateService ?? throw new ArgumentNullException(nameof(appStateService));

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.ExitRequested += ViewModel_ExitRequested;

        // RunBox focus request: open the spotlight (shell may or may not be visible).
        viewModel.RunBoxFocusRequested += (_, _) =>
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => OpenSpotlight());
        };

        // Bring the shell to the foreground when navigating to an already-open tab.
        viewModel.ShellBringToFrontRequested += (_, _) =>
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => _shell?.Activate());
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

        // Dedicated faster poll for fullscreen detection - catches browser F11
        // and other cases where the foreground window does not change.
        _fullscreenTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(600)
        };
        _fullscreenTimer.Tick += (_, _) => FullScreenCheck();
        _fullscreenTimer.Start();

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

        Win32Interop.SetWindowPos(_hwnd, Win32Interop.HWND_TOPMOST, 0, 0, 0, 0,
            Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE);

        EnsureShellOwner();

        HwndSource.FromHwnd(_hwnd)!.AddHook(WndProcHook);

        RegisterRunBoxHotkey();

        _initialized = true;

        // Persist the startup position immediately after layout so the resolved
        // first-run or recovered position survives the session even if the window
        // is never moved. Without this, OnLocationChanged is suppressed during
        // OnSourceInitialized (because _initialized is still false at that point),
        // and a fast VS stop / process kill before the 800ms debounce fires leaves
        // WindowLeft/WindowTop at their default (0,0) - placing the pill at the
        // wrong corner on the next launch.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            ViewModel.UpdatePosition(Left, Top);
            ViewModel.SaveSettings();
        }));
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

            _topmostTimer.Stop();
            _saveTimer.Stop();
            _fullscreenTimer.Stop();
            ViewModel.SaveSettings();

            // Tear down the shell and spotlight so we don't leak hidden top-level windows.
            if (_shell is not null)
            {
                try { _shell.ForceClose(); } catch { }
                _shell = null;
            }
            if (_spotlight is not null)
            {
                try { _spotlight.ForceClose(); } catch { }
                _spotlight = null;
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
            _shell ??= CreateShell();
            _shell.Show();
            _shell.Activate();

            if (ViewModel.AnimationEnabled)
                PlayPillBounce();
        }
        else
        {
            if (_shell is not null)
                _shell.AnimateAndHide();

            if (ViewModel.AnimationEnabled)
                PlayPillBounce();
            else
            {
                PillScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
                PillScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            }

            EnsureShellOwner();
            if (_hwnd != IntPtr.Zero)
                Win32Interop.SetWindowPos(_hwnd, Win32Interop.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOACTIVATE);
        }
    }

    /// <summary>
    /// Subtle bounce played on the pill whenever the widget is opened or closed.
    /// Quickly expands slightly then settles back, giving tactile feedback without
    /// a disruptive scale-down/scale-up transition.
    /// </summary>
    private void PlayPillBounce()
    {
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.06,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80)),
            new CubicEase { EasingMode = EasingMode.EaseOut }));
        anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0,
            KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280)),
            new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 }));
        PillScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, anim);
        PillScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, anim);
    }

    private ExpandedShellWindow CreateShell()
    {
        var shell = new ExpandedShellWindow(ViewModel, _settingsService);
        shell.SpotlightRequested += (_, _) => OpenSpotlight();
        return shell;
    }

    // ── Spotlight ─────────────────────────────────────────────────────────────

    private void OpenSpotlight()
    {
        // Already open: just re-activate.
        if (_spotlight is { IsVisible: true } existing && !existing.IsClosing)
        {
            existing.Activate();
            existing.FocusInput();
            return;
        }

        // Collapse the shell if it is open so it does not overlap the spotlight.
        if (ViewModel.IsExpanded)
            ViewModel.ToggleExpandedCommand.Execute(null);

        var monitor = GetTargetMonitor();

        if (_spotlight is null)
            _spotlight = CreateSpotlight();

        _spotlight.PrepareForShow();
        _spotlight.Show();
        _spotlight.PositionOnMonitor(monitor);
        _spotlight.Activate();
        _spotlight.FocusInput();
        _spotlight.PlayOpenAnimation();
    }

    private RunBoxSpotlightWindow CreateSpotlight()
    {
        return new RunBoxSpotlightWindow(ViewModel.RunBox, ViewModel);
    }

    private Win32Interop.MONITORINFO GetTargetMonitor()
    {
        // Use shell HWND when it has one; fall back to pill HWND.
        IntPtr targetHwnd = _hwnd;
        if (_shell is not null)
        {
            var shellInterop = new WindowInteropHelper(_shell);
            if (shellInterop.Handle != IntPtr.Zero)
                targetHwnd = shellInterop.Handle;
        }

        var hMon = Win32Interop.MonitorFromWindow(targetHwnd, Win32Interop.MONITOR_DEFAULTTONEAREST);
        var info = new Win32Interop.MONITORINFO { cbSize = Marshal.SizeOf<Win32Interop.MONITORINFO>() };
        Win32Interop.GetMonitorInfo(hMon, ref info);
        return info;
    }

    private static string FormatHotkey(int modifiers, int virtualKey)
    {
        var parts = new System.Collections.Generic.List<string>();
        if ((modifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((modifiers & 0x0004) != 0) parts.Add("Shift");
        if ((modifiers & 0x0001) != 0) parts.Add("Alt");
        if ((modifiers & 0x0008) != 0) parts.Add("Win");
        try
        {
            var key = KeyInterop.KeyFromVirtualKey(virtualKey);
            parts.Add(key == Key.None ? $"0x{virtualKey:X2}" : key.ToString());
        }
        catch
        {
            parts.Add($"0x{virtualKey:X2}");
        }
        return string.Join("+", parts);
    }

    private void ViewModel_ExitRequested(object? sender, EventArgs e)
    {
        _allowClose = true;
        Close();
    }

    // ── Win32 hook: hotkey + shell ownership recovery ────────────────────────

    private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Prevent Win+D and other system-initiated hides from removing the pill.
        // WPF-initiated hides (fullscreen detection) are allowed via _intentionalHide.
        if (msg == Win32Interop.WM_WINDOWPOSCHANGING && !_intentionalHide)
        {
            var wp = Marshal.PtrToStructure<Win32Interop.WINDOWPOS>(lParam);
            if ((wp.flags & Win32Interop.SWP_HIDEWINDOW) != 0)
            {
                wp.flags &= ~Win32Interop.SWP_HIDEWINDOW;
                Marshal.StructureToPtr(wp, lParam, true);
            }
            return IntPtr.Zero;
        }

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
                OpenSpotlight();
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
            ViewModel.RunBox.SetHotkeyHint(FormatHotkey(mod, key));
        }
        else
        {
            Log.Warn($"RunBox hotkey registration failed: mod=0x{mod:X2} key=0x{key:X2}");
        }
    }

    // ── Shell owner / topmost recovery ───────────────────────────────────────

    private void EnsureShellOwner()
    {
        if (_hwnd == IntPtr.Zero) return;

        IntPtr shellTray = Win32Interop.FindWindow("Shell_TrayWnd", null);
        if (shellTray == IntPtr.Zero) return;

        var interop = new WindowInteropHelper(this);
        if (interop.Owner != shellTray)
            interop.Owner = shellTray;
    }

    private void RelocateToTopmost()
    {
        if (_hwnd == IntPtr.Zero) return;

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

        // Only the pill needs explicit hide-for-fullscreen treatment.
        // The shell is a normal (non-topmost) window and goes behind fullscreen
        // apps naturally. The pill, however, is topmost and must be hidden explicitly
        // regardless of whether the shell is currently open.

        if (Win32Interop.IsForegroundFullscreen())
        {
            if (!_hiddenForFullscreen)
            {
                _hiddenForFullscreen = true;
                _intentionalHide = true;
                Hide();
                _intentionalHide = false;
            }
        }
        else if (_hiddenForFullscreen)
        {
            _hiddenForFullscreen = false;
            Show();
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
            if (ViewModel.IsExpanded && _shell is { IsVisible: true })
            {
                // Shell is open: bring to front if not foreground, collapse if already foreground
                var shellHwnd = new WindowInteropHelper(_shell).Handle;
                if (shellHwnd != IntPtr.Zero && Win32Interop.GetForegroundWindow() != shellHwnd)
                    _shell.Activate();
                else
                    ViewModel.ToggleExpandedCommand.Execute(null);
            }
            else
            {
                ViewModel.ToggleExpandedCommand.Execute(null);
            }
        }
        _hasDragged = false;
    }

    private void MiniBar_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        ViewModel.RefreshCopyLabels();
    }

    // ── Reminder integration─────────────────────────────────────────────────

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

        ViewModel.Settings.TestNotificationRequested += (_, _) => ShowTestNotification();
    }

    private void ShowTestNotification()
    {
        if (_localizationService is null) return;
        var header  = _localizationService.Get("settings.notification");
        var title   = _localizationService.Get("settings.test_notification");
        var body    = "This is a test reminder notification.";
        var dismiss = _localizationService.Get("reminder.dismiss");
        bool sound  = ViewModel.Settings.NotificationSound;
        int  dur    = ViewModel.Settings.NotificationDurationSeconds;
        _activeNotifications.RemoveAll(n => !n.IsLoaded);
        var popup = new NotificationPopup(header, title, body, dismiss, _activeNotifications.Count, sound, dur);
        popup.Closed += (s, _) => { if (s is NotificationPopup n) _activeNotifications.Remove(n); };
        _activeNotifications.Add(popup);
        popup.Show();
    }

    private void OnMoreNavigateToMore(int mode, string dateKey)
    {
        ViewModel.OpenMoreCommand.Execute(null);
        if (mode == 0)
        {
            ViewModel.More.OpenNoteForm(dateKey); // dateKey is "YYYY-MM-DD"
        }
        else
        {
            // dateKey is "YYYY-MM-DD"; OpenReminderForm wants y, m, d
            var parts = dateKey.Split('-');
            if (parts.Length == 3
                && int.TryParse(parts[0], out int y)
                && int.TryParse(parts[1], out int m)
                && int.TryParse(parts[2], out int d))
            {
                ViewModel.More.OpenReminderForm(y, m, d);
            }
        }
    }

    private void OnMoreEditReminder(string reminderId)
    {
        ViewModel.OpenMoreCommand.Execute(null);
        ViewModel.More.OpenEditReminderForm(reminderId);
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

        vm.NavigateToMoreRequested += OnMoreNavigateToMore;
        vm.EditReminderRequested   += OnMoreEditReminder;

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
    /// guard <see cref="DailyEventsAnnouncer.ShouldFire"/> plus the persisted
    /// <c>LastDailyEventsNotificationDate</c> in <c>AppState</c> (runtime.json) ensures it never fires
    /// twice for the same day even across restarts. Always best-effort: any
    /// failure (out-of-range BS date, state save error, popup error) is
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
                    todayAd, _appStateService.Current.LastDailyEventsNotificationDate,
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
            _appStateService.Current.LastDailyEventsNotificationDate = Helpers.DailyEventsAnnouncer.ToIsoDate(todayAd);
            _appStateService.Save();

            Log.Action($"daily events notification: shown {events!.Length} event(s) for {_appStateService.Current.LastDailyEventsNotificationDate}");
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
