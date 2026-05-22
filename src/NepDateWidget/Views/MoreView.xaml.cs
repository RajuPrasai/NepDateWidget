using NepDateWidget.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NepDateWidget.Views;

public partial class MoreView : UserControl
{
    private MoreViewModel? _vm;
    private bool _docClickStartedHere;

    public MoreView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            _vm?.PropertyChanged -= OnVmPropertyChanged;

            _vm = e.NewValue as MoreViewModel;
            _vm?.PropertyChanged += OnVmPropertyChanged;
        };

        Unloaded += (_, _) =>
        {
            _vm?.PropertyChanged -= OnVmPropertyChanged;
        };

        DragEnter += OnDragEnter;
        Drop += OnDrop;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MoreViewModel.IsNoteFormOpen) && _vm?.IsNoteFormOpen == true)
        {
            Dispatcher.BeginInvoke(() => NoteFormFirstInput.Focus());
        }
        else if (e.PropertyName == nameof(MoreViewModel.IsReminderFormOpen) && _vm?.IsReminderFormOpen == true)
        {
            Dispatcher.BeginInvoke(() => ReminderFormFirstInput.Focus());
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (DataContext is MoreViewModel vm && vm.IsDocFormOpen &&
            e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not MoreViewModel vm || !vm.IsDocFormOpen)
            return;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0)
            return;

        _docClickStartedHere = false;
        vm.DocEditFilePath = files[0];
        e.Handled = true;
    }

    private void DocDropZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // If the click came from a Button descendant (e.g. the clear button),
        // don't treat it as a click-to-browse gesture.
        var src = e.OriginalSource as DependencyObject;
        while (src != null && !ReferenceEquals(src, sender))
        {
            if (src is Button) return;
            src = System.Windows.Media.VisualTreeHelper.GetParent(src);
        }
        _docClickStartedHere = true;
    }

    private void DocDropZone_Click(object sender, MouseButtonEventArgs e)
    {
        bool wasClick = _docClickStartedHere;
        _docClickStartedHere = false;
        if (!wasClick)
            return;

        if (DataContext is MoreViewModel vm)
        {
            vm.BrowseDocumentFileCommand.Execute(null);
        }
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MoreViewModel vm)
        {
            vm.DocSearchGotFocusCommand.Execute(null);
        }
    }

    // LostFocus fires before a Popup child can receive the click.
    // A short delay lets the suggestion command register first.
    private async void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await System.Threading.Tasks.Task.Delay(150);
        if (DataContext is MoreViewModel vm)
        {
            vm.DocSearchLostFocusCommand.Execute(null);
        }
    }
}
