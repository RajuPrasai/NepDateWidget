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

    public CalendarView()
    {
        InitializeComponent();
        DaysGrid.RenderTransform = new TranslateTransform();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Forwards user month selection to the VM. The binding is OneWay to prevent
    /// WPF from writing -1 back when ItemsSource is swapped (language change).
    /// </summary>
    private void MonthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0 && DataContext is CalendarViewModel vm)
            vm.SelectedMonthIndex = cb.SelectedIndex;
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
        if (DataContext is not CalendarViewModel vm) return;
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
            doNavigate();
            return;
        }

        var transform = (TranslateTransform)DaysGrid.RenderTransform;

        // Cancel any in-flight opacity animation so rapid navigation doesn't leave opacity stuck at 0.
        // BeginAnimation(prop, null) removes the active clock and restores the local value (defaults to 1).
        DaysGrid.BeginAnimation(OpacityProperty, null);

        // direction: -1 = prev (slide right), 0 = today, +1 = next (slide left)
        double outX = direction == 0 ? 0 : (direction > 0 ? -SlidePixels : SlidePixels);
        double inX = direction == 0 ? 0 : (direction > 0 ? SlidePixels : -SlidePixels);

        // Animate out
        var opacityOut = new DoubleAnimation(1, 0, SlideOut) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var translateOut = new DoubleAnimation(0, outX, SlideOut) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

        opacityOut.Completed += (_, _) =>
        {
            // Refresh grid data - wrapped in try/catch so any exception does NOT prevent
            // the animate-in phase from starting (which would leave DaysGrid invisible).
            try { doNavigate(); }
            catch { /* navigation error - still animate in so the grid stays visible */ }

            // Reset transform to incoming position, then animate in regardless
            transform.X = inX;

            var opacityIn = new DoubleAnimation(0, 1, SlideIn) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            var translateIn = new DoubleAnimation(inX, 0, SlideIn) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

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
            vm.SelectedYearIndex = cb.SelectedIndex;
    }
}
