using NepDateWidget.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NepDateWidget.Views;

public partial class NetworkToolsView : UserControl
{
    public NetworkToolsView()
    {
        InitializeComponent();

        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue && DataContext is NetworkToolsViewModel vm)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => FocusModeInput(vm.ActiveMode)));
            }
        };

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is NetworkToolsViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnVmPropertyChanged;
            }

            if (e.NewValue is NetworkToolsViewModel vm)
            {
                vm.PropertyChanged += OnVmPropertyChanged;
            }
        };

        Unloaded += (_, _) =>
        {
            if (DataContext is NetworkToolsViewModel vm)
            {
                vm.PropertyChanged -= OnVmPropertyChanged;
            }
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NetworkToolsViewModel.ActiveMode) && sender is NetworkToolsViewModel vm)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                new Action(() => FocusModeInput(vm.ActiveMode)));
        }
    }

    private void FocusModeInput(int mode)
    {
        UIElement? input = mode switch
        {
            1 => PingHostBox,
            3 => TraceHostBox,
            4 => WhoisBox,
            5 => DnsHostBox,
            _ => null,
        };
        input?.Focus();
    }

    private void OutputBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.SelectAll();
        }
    }

    private void OutputBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox tb && !tb.IsKeyboardFocused)
        {
            e.Handled = true;
            tb.Focus();
        }
    }

    private void PingHost_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None
            && DataContext is NetworkToolsViewModel vm
            && vm.PingCommand.CanExecute(null))
        {
            vm.PingCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void TraceHost_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None
            && DataContext is NetworkToolsViewModel vm
            && vm.TraceCommand.CanExecute(null))
        {
            vm.TraceCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void WhoisDomain_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None
            && DataContext is NetworkToolsViewModel vm
            && vm.WhoisCommand.CanExecute(null))
        {
            vm.WhoisCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void DnsHost_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None
            && DataContext is NetworkToolsViewModel vm
            && vm.DnsCommand.CanExecute(null))
        {
            vm.DnsCommand.Execute(null);
            e.Handled = true;
        }
    }
}
