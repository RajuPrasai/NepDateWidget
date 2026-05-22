using NepDateWidget.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NepDateWidget.Views;

public partial class QrCodeView : UserControl
{
    public QrCodeView()
    {
        InitializeComponent();
    }

    private void OnDecodeDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DecodeDrop.Background = (Brush)FindResource("WidgetHoverBrush");
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDecodeDragLeave(object sender, DragEventArgs e)
    {
        DecodeDrop.Background = (Brush)FindResource("WidgetAccentBrushLight");
    }

    private void OnDecodeDrop(object sender, DragEventArgs e)
    {
        DecodeDrop.Background = (Brush)FindResource("WidgetAccentBrushLight");
        if (e.Data.GetDataPresent(DataFormats.FileDrop)
            && e.Data.GetData(DataFormats.FileDrop) is string[] files
            && files.Length > 0
            && DataContext is QrCodeViewModel vm)
        {
            vm.DecodeQrFromPath(files[0]);
        }
    }
}
