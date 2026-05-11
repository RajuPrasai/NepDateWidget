using NepDateWidget.Helpers;
using NepDateWidget.Services;
using NepDateWidget.ViewModels;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NepDateWidget.Views;

/// <summary>
/// The opaque, Alt+Tab-visible shell that hosts the calendar, tools, and settings tabs.
/// Created lazily on first expand by MainWindow. Closing the shell (X / Alt+F4)
/// collapses back to the mini pill rather than exiting the process. The shell never
/// holds the only reference to the application lifetime; the pill (MainWindow) is
/// always the lifetime owner.
/// </summary>
public partial class ExpandedShellWindow : Window
{
    private const double MinExpandedWidthDip  = 538;
    private const double MinExpandedHeightDip = 497;
    private const int SaveDebounceMs = 800;

    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _saveTimer;
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private IntPtr _hwnd;
    private bool _initialized;

    /// <summary>
    /// Raised when the shell wants the host (MainWindow) to open the spotlight.
    /// Fired by clicking the RunBox strip or via the FocusRunBox() hotkey path.
    /// </summary>
    public event EventHandler? SpotlightRequested;

    /// <summary>
    /// True when the shell is being closed by the application (collapse, exit) and
    /// the cancel-and-collapse logic in OnClosing should be bypassed.
    /// </summary>
    private bool _allowClose;

    public ExpandedShellWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SaveDebounceMs) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            ViewModel.SaveSettings();
        };

        IsVisibleChanged += (_, e) => { if (e.NewValue is true) PlayOpenAnimation(); };

        // React to the user toggling Rounded / Sharp at runtime.
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CornerStyle) ||
                e.PropertyName == nameof(MainViewModel.CornerRadiusValue))
            {
                ApplyCornerPreference();
            }
        };
    }

    // ── Window lifetime ──────────────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        ApplyMicaAndDarkTitleBar();
        ApplyCornerPreference();

        var s = _settingsService.Current;
        var workArea = SystemParameters.WorkArea;

        double width  = Math.Max(MinExpandedWidthDip,  s.ExpandedWidth  > 0 ? s.ExpandedWidth  : ViewModel.WindowWidth);
        double height = Math.Max(MinExpandedHeightDip, s.ExpandedHeight > 0 ? s.ExpandedHeight : ViewModel.WindowHeight);
        Width  = width;
        Height = height;

        if (s.ExpandedWindowLeft.HasValue && s.ExpandedWindowTop.HasValue)
        {
            Left = Math.Max(workArea.Left, Math.Min(workArea.Right  - Width,  s.ExpandedWindowLeft.Value));
            Top  = Math.Max(workArea.Top,  Math.Min(workArea.Bottom - Height, s.ExpandedWindowTop.Value));
        }
        else
        {
            // First open: anchor near the pill's saved position, expanding downward.
            double anchorLeft = _settingsService.Current.WindowLeft;
            double anchorTop  = _settingsService.Current.WindowTop;
            if (anchorLeft <= 0 && anchorTop <= 0)
            {
                anchorLeft = workArea.Right - Width - 24;
                anchorTop  = workArea.Top + 24;
            }

            if (anchorLeft + Width > workArea.Right)
                anchorLeft = workArea.Right - Width;
            if (anchorTop + Height > workArea.Bottom)
                anchorTop = workArea.Bottom - Height;
            Left = Math.Max(workArea.Left, anchorLeft);
            Top  = Math.Max(workArea.Top,  anchorTop);
        }

        HwndSource.FromHwnd(_hwnd)!.AddHook(WndProcHook);
        _initialized = true;
    }

    /// <summary>
    /// Applies Mica backdrop and dark titlebar on Windows 11 22000+.
    /// Returns silently on older OS versions; the solid theme background remains.
    /// </summary>
    private void ApplyMicaAndDarkTitleBar()
    {
        if (_hwnd == IntPtr.Zero) return;

        // Mica is supported from Windows 11 (build 22000) onwards.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        try
        {
            int useDark = 1;
            Win32Interop.DwmSetWindowAttribute(_hwnd, Win32Interop.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

            int backdrop = Win32Interop.DWMSBT_MAINWINDOW;
            Win32Interop.DwmSetWindowAttribute(_hwnd, Win32Interop.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
        catch
        {
            // Safe to swallow: Mica is a visual nicety, never a hard requirement.
        }
    }

    /// <summary>
    /// Applies DWM window corner preference (Win11 22000+). Square Win10 chrome is
    /// left untouched - the user setting only changes shape on supported builds.
    /// </summary>
    private void ApplyCornerPreference()
    {
        if (_hwnd == IntPtr.Zero) return;
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        int preference = ViewModel.CornerRadiusValue > 0
            ? Win32Interop.DWMWCP_ROUND
            : Win32Interop.DWMWCP_DONOTROUND;
        try
        {
            Win32Interop.DwmSetWindowAttribute(_hwnd, Win32Interop.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch
        {
            // Best-effort: corner preference is purely cosmetic.
        }
    }

    private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32Interop.WM_GETMINMAXINFO)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var info = Marshal.PtrToStructure<Win32Interop.MINMAXINFO>(lParam);
            int minW = (int)Math.Ceiling(MinExpandedWidthDip * dpi.DpiScaleX);
            int minH = (int)Math.Ceiling(MinExpandedHeightDip * dpi.DpiScaleY);
            if (info.ptMinTrackSize.x < minW) info.ptMinTrackSize.x = minW;
            if (info.ptMinTrackSize.y < minH) info.ptMinTrackSize.y = minH;
            Marshal.StructureToPtr(info, lParam, true);
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Closing the shell collapses it back to the mini pill rather than exiting.
    /// MainWindow can force a real close by setting <c>AllowClose</c> via <see cref="ForceClose"/>.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
        {
            _saveTimer.Stop();
            PersistSizeAndPosition();
            ViewModel.SaveSettings();
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (ViewModel?.IsExpanded == true)
            ViewModel.ToggleExpandedCommand.Execute(null);
    }

    /// <summary>
    /// MainWindow calls this on application exit so the shell tears down cleanly.
    /// </summary>
    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (!_initialized) return;
        PersistSizeAndPosition();
        RestartSaveTimer();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (!_initialized) return;
        PersistSizeAndPosition();
        RestartSaveTimer();
    }

    private void PersistSizeAndPosition()
    {
        var s = _settingsService.Current;
        s.ExpandedWindowLeft = Left;
        s.ExpandedWindowTop  = Top;
        double persistWidth  = ActualWidth;
        double persistHeight = ActualHeight;
        if (persistWidth  > 0) s.ExpandedWidth  = persistWidth;
        if (persistHeight > 0) s.ExpandedHeight = persistHeight;
        ViewModel.UpdateSize(persistWidth, persistHeight);
    }

    private void RestartSaveTimer()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    // ── Title bar ────────────────────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Single click: drag. Double click: do nothing (no maximize for widget shell).
        if (e.ClickCount == 2) { e.Handled = true; return; }
        try { DragMove(); } catch (InvalidOperationException) { /* button released early */ }
    }

    private void TitleBar_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Refresh dynamic copy-today labels just before the menu is shown.
        ViewModel.RefreshCopyLabels();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        // Reserved for future use; the current titlebar exposes only Close.
        if (ViewModel.IsExpanded)
            ViewModel.ToggleExpandedCommand.Execute(null);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // Collapse to pill rather than terminating the app.
        if (ViewModel.IsExpanded)
            ViewModel.ToggleExpandedCommand.Execute(null);
    }

    // ── Run box ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by MainWindow when the global hotkey fires while the shell is open.
    /// Signals the host to open the spotlight instead of focusing a local input.
    /// </summary>
    public void FocusRunBox()
    {
        SpotlightRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RunBoxStrip_Click(object sender, RoutedEventArgs e)
    {
        SpotlightRequested?.Invoke(this, EventArgs.Empty);
    }

    // ── Mouse wheel month nav (only on Calendar tab) ─────────────────────────

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (ExpandedTabs.SelectedIndex == 0 && !IsAnyComboBoxDropDownOpen())
        {
            ViewModel.OnMouseWheel(e.Delta);
            e.Handled = true;
        }
    }

    private static bool IsAnyComboBoxDropDownOpen()
    {
        if (Mouse.DirectlyOver is not DependencyObject hit)
            return false;
        var current = hit;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.Popup { IsOpen: true })
                return true;
            current = VisualTreeHelper.GetParent(current)
                      ?? (current is FrameworkElement fe ? fe.Parent : null);
        }
        return false;
    }

    // ── Click-outside-textbox: clear focus to hide caret ─────────────────────

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (Keyboard.FocusedElement is TextBox focused && e.OriginalSource is not TextBox)
        {
            focused.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
        }
    }

    // ── Escape: unfocus textbox → collapse ───────────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key != Key.Escape) return;

        var focused = Keyboard.FocusedElement;
        if (focused is TextBox or PasswordBox or System.Windows.Controls.Primitives.TextBoxBase)
        {
            ShellRoot.Focusable = true;
            ShellRoot.Focus();
            Keyboard.Focus(ShellRoot);
            e.Handled = true;
            return;
        }

        if (ViewModel.IsExpanded)
        {
            ViewModel.ToggleExpandedCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ── Open/close animations ────────────────────────────────────────────────

    private void PlayOpenAnimation()
    {
        if (!ViewModel.AnimationEnabled) return;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        // Fade in
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };
        ShellRoot.BeginAnimation(OpacityProperty, fadeIn);

        // Slide down subtly
        var translate = ShellRoot.RenderTransform as TranslateTransform ?? new TranslateTransform();
        ShellRoot.RenderTransform = translate;
        var slideDown = new DoubleAnimation(-8, 0, TimeSpan.FromMilliseconds(250)) { EasingFunction = ease };
        translate.BeginAnimation(TranslateTransform.YProperty, slideDown);
    }

    /// <summary>
    /// Plays a close animation then hides the window. If animations are disabled, hides immediately.
    /// </summary>
    public void AnimateAndHide()
    {
        if (!ViewModel.AnimationEnabled)
        {
            Hide();
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };
        fadeOut.Completed += (_, _) =>
        {
            Hide();
            // Reset for next show
            ShellRoot.Opacity = 1;
            var t = ShellRoot.RenderTransform as TranslateTransform;
            if (t is not null) t.Y = 0;
        };
        ShellRoot.BeginAnimation(OpacityProperty, fadeOut);

        var translate = ShellRoot.RenderTransform as TranslateTransform ?? new TranslateTransform();
        ShellRoot.RenderTransform = translate;
        var slideUp = new DoubleAnimation(0, -6, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease };
        translate.BeginAnimation(TranslateTransform.YProperty, slideUp);
    }
}
