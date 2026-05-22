using NepDateWidget.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NepDateWidget.Views;

/// <summary>
/// Code-behind for the expanded calendar panel.
/// Handles slide animations on month navigation; all state lives in CalendarViewModel.
/// </summary>
public partial class CalendarView : UserControl
{
    private static readonly Duration SlideOut = new(TimeSpan.FromMilliseconds(80));
    private static readonly Duration SlideIn = new(TimeSpan.FromMilliseconds(80));
    private const double SlidePixels = 18;

    // Holds the doNavigate continuation from the most recently started animation.
    // When a second navigation starts before the first completes (rapid click),
    // BeginAnimation(null) cancels the in-flight clock without firing Completed,
    // orphaning the first doNavigate. Storing it here lets the incoming navigation
    // execute it synchronously before starting its own animation.
    private Action? _pendingNavContinuation;

    public CalendarView()
    {
        InitializeComponent();
        DaysGrid.RenderTransform = new TranslateTransform();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // The CalendarViewModel is a singleton owned by MainViewModel and outlives
        // this CalendarView. When ExpandedShellWindow is destroyed and recreated,
        // WPF may not fire DataContextChanged with the old value during teardown,
        // leaving stale subscriptions. Each subsequent expand would add another
        // subscriber, causing navigation to jump multiple months per click.
        // Unloaded fires reliably on window close and cleans up unconditionally.
        if (DataContext is CalendarViewModel vm)
        {
            vm.NavigationRequested -= OnNavigationRequested;
            vm.OpenDayInfoRequested -= OnOpenDayInfoRequested;
            DaysGrid.SizeChanged -= OnDaysGridSizeChanged;
        }
        _pendingNavContinuation = null;
    }

    /// <summary>
    /// Forwards user month selection to the VM. The binding is OneWay to prevent
    /// WPF from writing -1 back when ItemsSource is swapped (language change).
    /// </summary>
    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0 && DataContext is CalendarViewModel vm)
        {
            vm.SelectedMonthIndex = cb.SelectedIndex;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CalendarViewModel old)
        {
            old.NavigationRequested -= OnNavigationRequested;
            old.OpenDayInfoRequested -= OnOpenDayInfoRequested;
            DaysGrid.SizeChanged -= OnDaysGridSizeChanged;
        }

        if (e.NewValue is CalendarViewModel vm)
        {
            vm.NavigationRequested += OnNavigationRequested;
            vm.OpenDayInfoRequested += OnOpenDayInfoRequested;
            DaysGrid.SizeChanged += OnDaysGridSizeChanged;
        }
    }

    private void OnDaysGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is not CalendarViewModel vm)
        {
            return;
        }

        vm.UpdateCellLayout(DaysGrid.ActualHeight / 6.0, DaysGrid.ActualWidth / 7.0);
    }

    private void OnOpenDayInfoRequested(int bsYear, int bsMonth, int bsDay)
    {
        // Look up MainWindow via the application window list rather than walking
        // up the visual tree: when this view is hosted inside ExpandedShellWindow,
        // Window.GetWindow(this) returns the shell, not the popup orchestrator.
        var mainWindow = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
        mainWindow?.OpenDayInfoPopup(bsYear, bsMonth, bsDay);
    }

    private void OnNavigationRequested(int direction, Action doNavigate)
    {
        bool animEnabled = AnimationEnabled;

        if (!animEnabled || !IsLoaded)
        {
            // Drain any pending cancelled continuation first so no navigation is silently dropped.
            if (_pendingNavContinuation is not null)
            {
                var pending = _pendingNavContinuation;
                _pendingNavContinuation = null;
                try { pending(); }
                catch { }
            }
            doNavigate();
            return;
        }

        var transform = (TranslateTransform)DaysGrid.RenderTransform;

        // If a previous animation was cancelled (BeginAnimation(null) does not fire Completed),
        // its doNavigate would be silently orphaned. Execute it now so rapid clicks don't drop
        // intermediate navigations.
        if (_pendingNavContinuation is not null)
        {
            var pending = _pendingNavContinuation;
            _pendingNavContinuation = null;
            try { pending(); }
            catch { }
        }

        // Cancel in-flight animations on both properties. After BeginAnimation(prop, null):
        // opacity reverts to 1.0 (default), X reverts to 0 (default - no local value is ever
        // set on TranslateTransform.X). The new animations start from these resting values
        // without a visible stutter.
        DaysGrid.BeginAnimation(OpacityProperty, null);
        transform.BeginAnimation(TranslateTransform.XProperty, null);

        // direction: -1 = prev (slide right), 0 = today, +1 = next (slide left)
        double outX = direction == 0 ? 0 : (direction > 0 ? -SlidePixels : SlidePixels);
        double inX  = direction == 0 ? 0 : (direction > 0 ?  SlidePixels : -SlidePixels);

        // No From specified - both animations start from the current (post-cancel) value:
        // opacity 1.0, X 0. This avoids a stutter jump when a previous translate held a
        // non-zero position via HoldEnd which was cleared by the null cancel above.
        var opacityOut  = new DoubleAnimation(0, SlideOut) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var translateOut = new DoubleAnimation(outX, SlideOut) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

        // Store so a subsequent rapid navigation can rescue and execute this continuation
        // synchronously before starting its own animation. The ReferenceEquals check guards
        // the edge case where Completed fires at the same moment as the next navigation.
        _pendingNavContinuation = doNavigate;
        var captured = doNavigate;

        opacityOut.Completed += (_, _) =>
        {
            // Only execute if this is still the most recent pending navigation.
            // A rapid subsequent click would have already executed it synchronously above
            // and replaced _pendingNavContinuation with its own continuation.
            if (!ReferenceEquals(_pendingNavContinuation, captured)) return;
            _pendingNavContinuation = null;

            // Refresh grid data - wrapped in try/catch so any exception does NOT prevent
            // the animate-in phase from starting (which would leave DaysGrid invisible).
            try { captured(); }
            catch { /* navigation error - still animate in so the grid stays visible */ }

            // translateIn uses an explicit From=inX so no local-value assignment is needed.
            // FillBehavior.Stop ensures the clock is removed after completion, leaving X at
            // the default (0) rather than holding a non-zero value that would stutter on the
            // next BeginAnimation(null) cancel.
            var opacityIn  = new DoubleAnimation(0, 1, SlideIn) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            var translateIn = new DoubleAnimation(inX, 0, SlideIn) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };

            DaysGrid.BeginAnimation(OpacityProperty, opacityIn);
            transform.BeginAnimation(TranslateTransform.XProperty, translateIn);
        };

        DaysGrid.BeginAnimation(OpacityProperty, opacityOut);
        transform.BeginAnimation(TranslateTransform.XProperty, translateOut);
    }

    /// <summary>Reads AnimationEnabled from the root MainViewModel (safe even if null).</summary>
    private bool AnimationEnabled
    {
        get
        {
            var w = Window.GetWindow(this);
            return w?.DataContext is MainViewModel mv && mv.AnimationEnabled;
        }
    }

    /// <summary>
    /// Forwards user year selection to the VM. OneWay binding prevents WPF
    /// from writing -1 back when ItemsSource is swapped (language change).
    /// </summary>
    private void YearCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0 && DataContext is CalendarViewModel vm)
        {
            vm.SelectedYearIndex = cb.SelectedIndex;
        }
    }

    /// <summary>
    /// Fires on the DaysGrid before any current-month cell's ContextMenu is shown.
    /// Calls EnsureCopyOptionsBuilt() so DateFormatter.Build() runs at open-time,
    /// not during every navigation. If the build yields no options, the menu is suppressed.
    /// </summary>
    private void DaysGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (e.Source is Border border && border.DataContext is CalendarDayViewModel vm)
        {
            vm.EnsureCopyOptionsBuilt();
            if (!vm.HasCopyOptions)
                e.Handled = true;
        }
    }
}
