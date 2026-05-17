using NepDateWidget.ViewModels;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace NepDateWidget.Views;

public partial class ConverterView : UserControl
{
    public ConverterView()
    {
        InitializeComponent();
        // When this view becomes visible (tab selected), focus the primary input.
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                    new Action(FocusActiveInput));
            }
        };

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ConverterViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnVmPropertyChanged;
            }

            if (e.NewValue is ConverterViewModel vm)
            {
                vm.PropertyChanged += OnVmPropertyChanged;
            }
        };

        Unloaded += (_, _) =>
        {
            if (DataContext is ConverterViewModel vm)
            {
                vm.PropertyChanged -= OnVmPropertyChanged;
            }
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConverterViewModel.ActiveMode))
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                new Action(FocusActiveInput));
        }
    }

    private void FocusActiveInput()
    {
        InputBox?.Focus();
    }

    // Select-all when a read-only output box receives keyboard focus (Tab / Shift+Tab).
    private void OutputBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    // Also select-all on mouse click so the user can just Ctrl+C immediately.
    private void OutputBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox tb && !tb.IsKeyboardFocused)
        {
            e.Handled = true;
            tb.Focus();
        }
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {

    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {

    }
}

