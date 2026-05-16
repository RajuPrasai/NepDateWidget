using NepDateWidget.ViewModels;
using System.ComponentModel;
using System.Windows.Controls;

namespace NepDateWidget.Views;

public partial class MoreView : UserControl
{
    private MoreViewModel? _vm;

    public MoreView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = e.NewValue as MoreViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        };

        Unloaded += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MoreViewModel.IsNoteFormOpen) && _vm?.IsNoteFormOpen == true)
            Dispatcher.BeginInvoke(() => NoteFormFirstInput.Focus());
        else if (e.PropertyName == nameof(MoreViewModel.IsReminderFormOpen) && _vm?.IsReminderFormOpen == true)
            Dispatcher.BeginInvoke(() => ReminderFormFirstInput.Focus());
    }

    private void SearchBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MoreViewModel vm)
            vm.DocSearchGotFocusCommand.Execute(null);
    }

    // LostFocus fires before a Popup child can receive the click.
    // A short delay lets the suggestion command register first.
    private async void SearchBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        await System.Threading.Tasks.Task.Delay(150);
        if (DataContext is MoreViewModel vm)
            vm.DocSearchLostFocusCommand.Execute(null);
    }
}
