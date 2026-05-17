using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NepDateWidget.Converters;

/// <summary>
/// Converts bool to Visibility.
/// Set Invert="True" to get the inverse mapping (false → Visible, true → Collapsed).
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>When true, false maps to Visible and true maps to Collapsed.</summary>
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool bv && bv;
        if (Invert)
        {
            b = !b;
        }

        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is Visibility v && v == Visibility.Visible;
        if (Invert)
        {
            b = !b;
        }

        return b;
    }
}
