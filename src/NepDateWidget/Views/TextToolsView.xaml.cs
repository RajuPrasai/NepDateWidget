using NepDateWidget.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NepDateWidget.Views;

public partial class TextToolsView : UserControl
{
    public TextToolsView()
    {
        InitializeComponent();

        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue && DataContext is TextToolsViewModel vm)
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => FocusModeInput(vm.ActiveMode)));
        };

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is TextToolsViewModel oldVm)
                oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is TextToolsViewModel vm)
            {
                vm.PropertyChanged += OnVmPropertyChanged;
                vm.RequestSaveFilePath = ShowSaveFileDialog;
                FocusModeInput(vm.ActiveMode);
            }
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TextToolsViewModel.ActiveMode) && sender is TextToolsViewModel vm)
            Dispatcher.BeginInvoke(() => FocusModeInput(vm.ActiveMode));
    }

    private void FocusModeInput(int mode)
    {
        var input = mode switch
        {
            1 => WordInputBox as UIElement,
            2 => UnicodeInputBox,
            3 => ScriptInputBox,
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

    // Enter in Unicode input triggers conversion in the selected direction
    private void UnicodeInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None
            && DataContext is TextToolsViewModel vm)
        {
            // Live conversion already handles this; Enter is a no-op now
            e.Handled = true;
        }
    }

    // Enter in Script input prevents newline; live binding handles the conversion.
    private void ScriptInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            e.Handled = true;
    }

    private void BrowseUnicodeFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select a file to convert",
            Filter = "Supported files|*.txt;*.docx|Text files (*.txt)|*.txt|Word documents (*.docx)|*.docx"
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) == true && DataContext is TextToolsViewModel vm)
            vm.SetUnicodeFilePath(dlg.FileName);
    }

    private void BrowseScriptFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select a file to convert",
            Filter = "Supported files|*.txt;*.docx|Text files (*.txt)|*.txt|Word documents (*.docx)|*.docx"
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) == true && DataContext is TextToolsViewModel vm)
            vm.SetScriptFilePath(dlg.FileName);
    }

    private string? ShowSaveFileDialog(string defaultPath)
    {
        var ext = System.IO.Path.GetExtension(defaultPath);
        var filter = ext?.ToLowerInvariant() switch
        {
            ".docx" => "Word documents (*.docx)|*.docx|All files (*.*)|*.*",
            ".txt"  => "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            _       => "All files (*.*)|*.*",
        };
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "Save converted file",
            Filter           = filter,
            FileName         = System.IO.Path.GetFileName(defaultPath),
            InitialDirectory = System.IO.Path.GetDirectoryName(defaultPath) ?? string.Empty,
            OverwritePrompt  = true,
        };
        return dlg.ShowDialog(Window.GetWindow(this)) == true ? dlg.FileName : null;
    }
}
