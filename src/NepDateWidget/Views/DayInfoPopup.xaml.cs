using NepDateWidget.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace NepDateWidget.Views;

public partial class DayInfoPopup : Window
{
    private readonly DayInfoViewModel _vm;
    private bool _isClosing;

    /// <summary>True when the popup closed because focus went to an external app.</summary>
    public bool ClosedByDeactivation { get; private set; }

    public DayInfoPopup(DayInfoViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;

        // Allow drag to reposition
        MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { } };

        _vm.RequestClose += () =>
        {
            _isClosing = true;
            try { Close(); } catch { }
        };

        Deactivated += OnDeactivated;

        Loaded += (_, _) => { };
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_isClosing) return;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () =>
        {
            if (_isClosing) return;

            ClosedByDeactivation = true;
            _isClosing = true;
            Deactivated -= OnDeactivated;
            try { Close(); } catch { }
        });
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
        base.OnKeyDown(e);
    }
}
