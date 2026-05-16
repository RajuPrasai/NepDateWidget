using NepDateWidget.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NepDateWidget.Views;

public partial class UnitView : UserControl
{
    public UnitView()
    {
        InitializeComponent();

        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue && DataContext is UnitViewModel vm)
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => FocusModeInput(vm.ActiveMode)));
        };

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is UnitViewModel oldVm)
                oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is UnitViewModel vm)
                vm.PropertyChanged += OnVmPropertyChanged;
        };

        Unloaded += (_, _) =>
        {
            if (DataContext is UnitViewModel vm)
                vm.PropertyChanged -= OnVmPropertyChanged;
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UnitViewModel.ActiveMode) && sender is UnitViewModel vm)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                new Action(() => FocusModeInput(vm.ActiveMode)));
    }

    private void FocusModeInput(int mode)
    {
        UIElement? input = mode switch
        {
            0 => AreaInputBox,
            2 => WeightInputBox,
            _ => null,
        };
        input?.Focus();
    }

    private void OutputBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox tb) tb.SelectAll();
    }

    private void OutputBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox tb && !tb.IsKeyboardFocused)
        {
            e.Handled = true;
            tb.Focus();
        }
    }
}
