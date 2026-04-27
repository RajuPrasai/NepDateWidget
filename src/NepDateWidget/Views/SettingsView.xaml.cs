using NepDateWidget.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace NepDateWidget.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? VM => DataContext as SettingsViewModel;

    public SettingsView()
    {
        InitializeComponent();
    }

    private void HotkeyInput_GotFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (VM is not null)
            VM.IsRecordingHotkey = true;
    }

    private void HotkeyInput_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (VM is not null)
            VM.IsRecordingHotkey = false;
    }

    private void HotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (VM is null || !VM.IsRecordingHotkey)
            return;

        e.Handled = true;

        // Ignore standalone modifier key presses
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

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
