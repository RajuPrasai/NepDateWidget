using NepDateWidget.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace NepDateWidget.Views;

public partial class HelpPopup : Window
{
    private bool _isClosing;

    public HelpPopup(string title, string desc, string[] howTo, string[] benefits, string howHeading, string whyHeading)
    {
        InitializeComponent();

        TitleText.Text   = title;
        DescText.Text    = desc;
        HowHeading.Text  = howHeading;
        WhyHeading.Text  = whyHeading;
        HowList.ItemsSource = howTo;
        WhyList.ItemsSource = benefits;

        MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { } };
        Deactivated += OnDeactivated;

        Loaded += (_, _) =>
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            BeginAnimation(OpacityProperty, anim);
        };
    }

    /// <summary>
    /// Factory: builds content from localization keys and shows the popup near the owner window.
    /// Key prefix example: "help.converter.convert"
    /// </summary>
    public static void ShowFor(string keyPrefix, ILocalizationService loc, Window owner)
    {
        var title    = loc.Get(keyPrefix + ".title");
        var desc     = loc.Get(keyPrefix + ".desc");
        var howTo    = new[] { loc.Get(keyPrefix + ".how1"), loc.Get(keyPrefix + ".how2"), loc.Get(keyPrefix + ".how3") };
        var benefits = new[] { loc.Get(keyPrefix + ".why1"), loc.Get(keyPrefix + ".why2"), loc.Get(keyPrefix + ".why3") };
        var howHead  = loc.Get("help.heading.howto");
        var whyHead  = loc.Get("help.heading.benefits");

        var popup = new HelpPopup(title, desc, howTo, benefits, howHead, whyHead)
        {
            Owner = owner
        };

        // Center near owner
        popup.WindowStartupLocation = WindowStartupLocation.Manual;
        popup.Loaded += (_, _) =>
        {
            double left = owner.Left + (owner.Width  - popup.ActualWidth)  / 2;
            double top  = owner.Top  + (owner.Height - popup.ActualHeight) / 2;
            popup.Left = Math.Max(0, left);
            popup.Top  = Math.Max(0, top);
        };

        popup.Show();
        popup.Activate();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => DoClose();

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_isClosing) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () =>
        {
            if (_isClosing) return;
            DoClose();
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DoClose();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void DoClose()
    {
        if (_isClosing) return;
        _isClosing = true;
        Deactivated -= OnDeactivated;
        try { Close(); } catch { }
    }
}
