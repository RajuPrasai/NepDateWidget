using NepDateWidget.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace NepDateWidget.Views;

public partial class CompressionView : UserControl
{
    // A real click always produces a MouseLeftButtonDown on the drop zone before
    // MouseLeftButtonUp. A drag-drop only produces MouseLeftButtonUp (mouse was
    // pressed on the drag source, not here), so we can distinguish the two.
    private bool _clickStartedHere;

    public CompressionView()
    {
        InitializeComponent();
        DragEnter += OnDragEnter;
        Drop += OnDrop;
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
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
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0)
        {
            return;
        }

        _clickStartedHere = false; // drop, not a click - clear any stale state

        if (DataContext is CompressionViewModel vm)
        {
            vm.AddFiles(files);
        }

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
        if (!wasClick)
        {
            return;
        }

        if (DataContext is CompressionViewModel vm)
        {
            vm.LoadFilesCommand.Execute(null);
        }
    }
}
