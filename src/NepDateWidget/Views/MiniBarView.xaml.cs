using System.Windows;
using System.Windows.Controls;

namespace NepDateWidget.Views;

/// <summary>
/// Code-behind for the collapsed mini bar. No logic - all state lives in MiniBarViewModel.
/// </summary>
public partial class MiniBarView : UserControl
{
    public static readonly DependencyProperty UseTransparentStylingProperty =
        DependencyProperty.Register(
            nameof(UseTransparentStyling),
            typeof(bool),
            typeof(MiniBarView),
            new PropertyMetadata(true));

    public bool UseTransparentStyling
    {
        get => (bool)GetValue(UseTransparentStylingProperty);
        set => SetValue(UseTransparentStylingProperty, value);
    }

    public MiniBarView()
    {
        InitializeComponent();
    }
}
