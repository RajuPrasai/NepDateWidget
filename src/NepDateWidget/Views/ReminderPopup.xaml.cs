using NepDateWidget.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace NepDateWidget.Views;

public partial class ReminderPopup : Window
{
    private readonly ReminderViewModel _vm;
    private bool _isClosing;

    /// <summary>True when the popup closed because focus went to an external app.</summary>
    public bool ClosedByDeactivation { get; private set; }

    public ReminderPopup(ReminderViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;
        MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { } };

        Loaded += (_, _) =>
        {
            if (_vm.IsEditing)
                TitleInput.Focus();
        };

        _vm.RequestClose += () =>
        {
            _isClosing = true;
            try { Close(); } catch { }
        };

        Deactivated += OnDeactivated;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_isClosing) return;

        // If editing with unsaved changes, show the discard banner and re-activate
        if (_vm.IsEditing && _vm.IsDirty)
        {
            _vm.ShowDiscardBanner = true;
            _isClosing = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isClosing = false;
                try { Activate(); } catch { }
            }));
            return;
        }

        // Defer so transient focus changes (context menus, etc.) settle first.
        // Only close when focus went to an external app, not back to the widget.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () =>
        {
            if (_isClosing) return;
            if (NepDateWidget.Helpers.WindowHelpers.IsAnyAppWindowActive(this)) return;

            ClosedByDeactivation = true;
            _isClosing = true;
            Deactivated -= OnDeactivated;
            try { Close(); } catch { }
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _isClosing = true;
        Deactivated -= OnDeactivated;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _isClosing = true;
            Deactivated -= OnDeactivated;
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _vm.IsEditing && !_vm.ShowDiscardBanner)
        {
            // Don't intercept Enter in multiline TextBox (Notes field)
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox tb && tb.AcceptsReturn)
                return;
            _vm.SaveCommand.Execute(null);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}
