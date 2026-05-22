using NepDateWidget.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NepDateWidget.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? VM => DataContext as SettingsViewModel;

    public SettingsView()
    {
        InitializeComponent();
    }

    // Initialise all toggle thumb positions after the visual tree is ready.
    // The Storyboard was removed from the ControlTemplate so we must set the
    // initial X for every checked toggle on first load (and on every
    // expand-recreate, since ExpandedShellWindow is destroyed on collapse).
    private void OnSettingsViewLoaded(object sender, RoutedEventArgs e)
    {
        InitThumbPositions(this);
    }

    private static void InitThumbPositions(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is CheckBox cb)
            {
                cb.ApplyTemplate();
                if (cb.Template?.FindName("ThumbTranslate", cb) is TranslateTransform t)
                    t.X = cb.IsChecked == true ? 18 : 0;
            }
            InitThumbPositions(child);
        }
    }

    // Handles both Checked and Unchecked events bubbled from every CheckBox in
    // this view.  Animates or snaps the toggle thumb based on AnimationEnabled.
    private void OnSettingsCheckChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        cb.ApplyTemplate();
        if (cb.Template?.FindName("ThumbTranslate", cb) is not TranslateTransform thumb) return;

        double to = e.RoutedEvent == CheckBox.CheckedEvent ? 18.0 : 0.0;

        if (VM?.AnimationEnabled == true)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromSeconds(0.18))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            thumb.BeginAnimation(TranslateTransform.XProperty, anim);
        }
        else
        {
            // Clear any in-progress animation so the property setter takes effect.
            thumb.BeginAnimation(TranslateTransform.XProperty, null);
            thumb.X = to;
        }
    }

    private void HotkeyInput_GotFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        VM?.IsRecordingHotkey = true;
    }

    private void HotkeyInput_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        VM?.IsRecordingHotkey = false;
    }

    private void HotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (VM is null || !VM.IsRecordingHotkey)
        {
            return;
        }

        e.Handled = true;

        // Ignore standalone modifier key presses
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        // Escape cancels recording
        if (key == Key.Escape)
        {
            VM.IsRecordingHotkey = false;
            HotkeyInput.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
            return;
        }

        var modifiers = Keyboard.Modifiers;
        VM.TrySetHotkey(modifiers, key);
        VM.IsRecordingHotkey = false;
        HotkeyInput.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
    }
}
