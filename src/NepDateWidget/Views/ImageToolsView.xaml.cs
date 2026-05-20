using NepDateWidget.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace NepDateWidget.Views;

public partial class ImageToolsView : UserControl
{
    // A real click produces MouseLeftButtonDown on the drop zone before MouseLeftButtonUp.
    // A drag-drop only produces MouseLeftButtonUp (mouse pressed on drag source, not here).
    // Tracking down here avoids the event-ordering race that a boolean flag on Drop would have.
    private bool _clickStartedHere;

    public ImageToolsView()
    {
        InitializeComponent();
        DragEnter += OnDragEnter;
        Drop += OnDrop;
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0) return;

        _clickStartedHere = false;

        if (DataContext is ImageToolsViewModel vm)
            vm.AddFiles(files);

        e.Handled = true;
    }

    private void DropZone_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _clickStartedHere = true;
    }

    private void DropZone_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        bool wasClick = _clickStartedHere;
        _clickStartedHere = false;
        if (!wasClick) return;

        if (DataContext is ImageToolsViewModel vm)
            vm.LoadFilesCommand.Execute(null);
    }
}
