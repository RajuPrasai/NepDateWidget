using NepDateWidget.Helpers;
using NepDateWidget.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NepDateWidget.Views;

/// <summary>
/// Raycast-style floating spotlight. Appears centered on the current monitor with a
/// dark translucent card, search input, and an inline history list below. Opened by
/// the global hotkey or by clicking the RunBox strip in the expanded shell.
/// Closes on Escape (two-stage: first deselects history, then closes), on Enter
/// after execution, or when focus leaves the window.
/// </summary>
public partial class RunBoxSpotlightWindow : Window
{
    private readonly RunBoxViewModel _runBoxVm;
    private readonly MainViewModel _mainVm;
    private bool _isClosing;

    public bool IsClosing => _isClosing;

    public RunBoxSpotlightWindow(RunBoxViewModel runBoxVm, MainViewModel mainVm)
    {
        _runBoxVm = runBoxVm ?? throw new ArgumentNullException(nameof(runBoxVm));
        _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

        InitializeComponent();
        DataContext = runBoxVm;

        // Start off-screen so the first Show() does not flash at the default WPF position.
        Left = -5000;
        Top = -5000;

        _runBoxVm.CollapseRequested += OnCollapseRequested;
        _runBoxVm.FilteredHistory.CollectionChanged += (_, _) => UpdateListVisibility();
        _runBoxVm.ExecutedSuccessfully += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;
            Hide();
            // Do NOT reset _isClosing here - Deactivated fires on Hide() and dispatches
            // AnimateAndClose, which guards on _isClosing. PrepareForShow() resets it on
            // the next open cycle.
        });

        // Lose focus to another app/window → close spotlight (shell stays hidden).
        // Guard: if the mouse is still inside the window the deactivation came from clicking
        // an internal element (e.g. a history list item), not from switching to another app.
        // In that case SpotlightInput.Focus() in the click handler will reclaim focus
        // immediately, so we must not close.
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!IsMouseOver)
            {
                AnimateAndClose();
            }
        });
    }

    // ── Show / hide ──────────────────────────────────────────────────────────

    /// <summary>
    /// Prepares state for a new show cycle: ensures history is populated and
    /// resets any stale visual state from a previous close animation.
    /// </summary>
    public void PrepareForShow()
    {
        // Allow reopening even when called mid close-animation.
        _isClosing = false;

        // Clear held animation values so the local assignments below take effect immediately.
        SpotlightCard.BeginAnimation(OpacityProperty, null);
        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        // Pre-hide: window must be invisible when Show() is called so there is no flash
        // at the wrong position before PositionOnMonitor() repositions it.
        SpotlightCard.Opacity = 0;
        CardScale.ScaleX = 0.94;
        CardScale.ScaleY = 0.94;

        // Reset ViewModel state from the previous session.
        // Order: IsHistoryOpen must be false before RunText is set so the RunText
        // setter does not auto-select index 0 on an empty-text open.
        _runBoxVm.IsHistoryOpen = false;
        _runBoxVm.SelectedHistoryIndex = -1;
        _runBoxVm.ClearPrefix();
        _runBoxVm.RunText = string.Empty;  // triggers UpdateFilteredHistory → populates list
        UpdateListVisibility();
    }

    public void FocusInput()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            SpotlightInput.Focus();
            Keyboard.Focus(SpotlightInput);
        });
    }

    /// <summary>
    /// Positions the card centered horizontally and ~32 % from the top of the
    /// supplied monitor work area. Call this after Show() so the HWND exists and
    /// DPI is available.
    /// </summary>
    internal void PositionOnMonitor(Win32Interop.MONITORINFO monitor)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        double left = monitor.rcWork.Left / dpi.DpiScaleX;
        double top = monitor.rcWork.Top / dpi.DpiScaleY;
        double w = (monitor.rcWork.Right - monitor.rcWork.Left) / dpi.DpiScaleX;
        double h = (monitor.rcWork.Bottom - monitor.rcWork.Top) / dpi.DpiScaleY;

        Left = left + (w - Width) / 2;
        Top = top + h * 0.32;
    }

    // ── Animations ───────────────────────────────────────────────────────────

    public void PlayOpenAnimation()
    {
        // Initial state (Opacity=0, Scale=0.94) is already set by PrepareForShow().
        if (!_mainVm.AnimationEnabled)
        {
            CardScale.ScaleX = 1.0;
            CardScale.ScaleY = 1.0;
            SpotlightCard.Opacity = 1;
            return;
        }

        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.12 };
        var dur = TimeSpan.FromMilliseconds(180);

        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.94, 1.0, dur) { EasingFunction = ease });
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.94, 1.0, dur) { EasingFunction = ease });
        SpotlightCard.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
    }

    public void AnimateAndClose()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;

        if (!_mainVm.AnimationEnabled)
        {
            Hide();
            _isClosing = false;
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var dur = TimeSpan.FromMilliseconds(110);

        var scaleAnim = new DoubleAnimation(1.0, 0.95, dur) { EasingFunction = ease };
        scaleAnim.Completed += (_, _) =>
        {
            Hide();
            _isClosing = false;
        };

        CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        CardScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1.0, 0.95, dur) { EasingFunction = ease });
        SpotlightCard.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, 0, dur) { EasingFunction = ease });
    }

    /// <summary>
    /// Tears down the window cleanly on application exit.
    /// </summary>
    public void ForceClose()
    {
        _runBoxVm.CollapseRequested -= OnCollapseRequested;
        _isClosing = true; // prevent Deactivated from re-triggering
        Close();
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private void OnCollapseRequested(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => AnimateAndClose());
    }

    private void UpdateListVisibility()
    {
        bool has = _runBoxVm.FilteredHistory.Count > 0;
        HistoryList.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        SpotlightSeparator.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Keyboard ─────────────────────────────────────────────────────────────

    private void SpotlightInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                _runBoxVm.ExecuteCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Tab:
                if (_runBoxVm.FilteredHistory.Count > 0 && _runBoxVm.SelectedHistoryIndex >= 0)
                {
                    _runBoxVm.CommitSelection();
                    SpotlightInput.CaretIndex = SpotlightInput.Text.Length;
                    e.Handled = true;
                }
                break;

            case Key.Up:
                _runBoxVm.MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.Down:
                _runBoxVm.MoveSelection(1);
                e.Handled = true;
                break;

            case Key.Escape:
                _runBoxVm.HandleEscape();
                e.Handled = true;
                break;

            case Key.Back:
                if (SpotlightInput.Text.Length == 0 && _runBoxVm.HasActivePrefix)
                {
                    _runBoxVm.ClearPrefix();
                    e.Handled = true;
                }
                break;
        }
    }

    // ── History list interaction ──────────────────────────────────────────────

    private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is HistoryEntry item)
        {
            if (e.OriginalSource is DependencyObject src &&
                FindParent<Button>(src) is { Name: "RemoveBtn" })
            {
                e.Handled = true;
                _runBoxVm.RemoveHistoryItemCommand.Execute(item);
                SpotlightInput.Focus();
                return;
            }

            e.Handled = true;
            _runBoxVm.SelectHistoryItemCommand.Execute(item);
            SpotlightInput.Focus();
        }
    }

    private void HistoryRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is DependencyObject dep)
        {
            var item = FindParent<ListBoxItem>(dep);
            if (item?.DataContext is HistoryEntry text)
            {
                e.Handled = true;
                _runBoxVm.RemoveHistoryItemCommand.Execute(text);
                SpotlightInput.Focus();
            }
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current != null)
        {
            if (current is T found)
            {
                return found;
            }

            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
