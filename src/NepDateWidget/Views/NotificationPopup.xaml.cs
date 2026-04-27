using NepDateWidget.Models;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NepDateWidget.Views;

public partial class NotificationPopup : Window
{
    private readonly DispatcherTimer _autoDismissTimer;
    private readonly TimeSpan _autoDismissDuration;

    /// <summary>
    /// Raised when the user clicks the notification body to navigate to the reminder.
    /// Parameter: reminder ID.
    /// </summary>
    public event Action<string>? NavigateRequested;

    public string ReminderId { get; }

    public NotificationPopup(ReminderEntry reminder, string headerLabel, string dismissLabel,
                              int stackIndex, bool playSound = true, int durationSeconds = 10)
    {
        InitializeComponent();

        _autoDismissDuration = TimeSpan.FromSeconds(Math.Clamp(durationSeconds, 5, 60));

        ReminderId = reminder.Id;

        HeaderText.Text = headerLabel;
        TitleText.Text = reminder.Title;
        TimeText.Text = FormatTime12(reminder.Time);
        NotesText.Text = reminder.Notes;
        DismissButton.Content = dismissLabel;

        if (string.IsNullOrWhiteSpace(reminder.Notes))
            NotesText.Visibility = Visibility.Collapsed;

        PositionOnScreen(stackIndex);

        _autoDismissTimer = new DispatcherTimer { Interval = _autoDismissDuration };
        _autoDismissTimer.Tick += (_, _) => Close();
        _autoDismissTimer.Start();

        Loaded += (_, _) =>
        {
            StartCountdownAnimation();
            // Subtle fade-in
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            BeginAnimation(OpacityProperty, fadeIn);
        };

        if (playSound)
        {
            try { System.Media.SystemSounds.Exclamation.Play(); }
            catch { /* sound not available */ }
        }
    }

    /// <summary>
    /// Generic-content overload used by the daily events announcer. Reuses the
    /// reminder notification chrome (same colours, animation, dismiss button,
    /// auto-dismiss countdown) but takes free-form header/title/body text and
    /// is not tied to a <see cref="ReminderEntry"/>. The body is rendered
    /// where the reminder notes normally appear, so multi-line bullet lists
    /// look identical to a reminder note.
    /// </summary>
    public NotificationPopup(string headerLabel, string title, string body,
                              string dismissLabel, int stackIndex,
                              bool playSound = false, int durationSeconds = 10)
    {
        InitializeComponent();

        _autoDismissDuration = TimeSpan.FromSeconds(Math.Clamp(durationSeconds, 5, 60));

        ReminderId = string.Empty;

        HeaderText.Text = headerLabel;
        TitleText.Text  = title;
        TimeText.Visibility = Visibility.Collapsed;   // no time row for daily events
        NotesText.Text  = body;
        DismissButton.Content = dismissLabel;

        // Allow more vertical room than a reminder note so multiple events
        // remain readable instead of being clipped to a single visible line.
        NotesText.MaxHeight = 140;

        if (string.IsNullOrWhiteSpace(body))
            NotesText.Visibility = Visibility.Collapsed;

        PositionOnScreen(stackIndex);

        _autoDismissTimer = new DispatcherTimer { Interval = _autoDismissDuration };
        _autoDismissTimer.Tick += (_, _) => Close();
        _autoDismissTimer.Start();

        Loaded += (_, _) =>
        {
            StartCountdownAnimation();
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            BeginAnimation(OpacityProperty, fadeIn);
        };

        if (playSound)
        {
            try { System.Media.SystemSounds.Exclamation.Play(); }
            catch { /* sound not available */ }
        }
    }

    private void StartCountdownAnimation()
    {
        // Use the countdown bar's parent (the track border) width, not window width,
        // to avoid overshoot from drop shadow extending the window bounds
        var parent = CountdownBar.Parent as FrameworkElement;
        double barWidth = parent?.ActualWidth ?? ActualWidth;
        CountdownBar.Width = barWidth;
        var animation = new DoubleAnimation
        {
            From = barWidth,
            To = 0,
            Duration = new Duration(_autoDismissDuration),
        };
        CountdownBar.BeginAnimation(WidthProperty, animation);
    }

    private void PositionOnScreen(int stackIndex)
    {
        var workArea = SystemParameters.WorkArea;
        Loaded += (_, _) =>
        {
            Left = workArea.Right - ActualWidth - 12;
            double top = workArea.Bottom - (ActualHeight + 8) * (stackIndex + 1);
            // Clamp to screen top so notifications don't go off-screen
            Top = Math.Max(workArea.Top, top);
        };
    }

    private void Body_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Walk up from the original source to see if the click was inside the dismiss button
        var source = e.OriginalSource as DependencyObject;
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button btn && btn.Name == "DismissButton")
                return;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        NavigateRequested?.Invoke(ReminderId);
        _autoDismissTimer.Stop();
        Close();
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        _autoDismissTimer.Stop();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoDismissTimer.Stop();
        base.OnClosed(e);
    }

    private static string FormatTime12(string hhmm)
    {
        if (!TimeSpan.TryParse(hhmm, out var ts))
            return hhmm;

        int h = ts.Hours;
        int m = ts.Minutes;
        string period = h < 12 ? "AM" : "PM";
        int h12 = h % 12;
        if (h12 == 0) h12 = 12;
        return $"{h12}:{m:D2} {period}";
    }
}
