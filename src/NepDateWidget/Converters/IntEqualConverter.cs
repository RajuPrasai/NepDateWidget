using System.Globalization;
using System.Windows.Data;

namespace NepDateWidget.Converters;

/// <summary>
/// Two-way converter that binds an int property to a RadioButton IsChecked.
/// ConverterParameter holds the target integer value (as string).
/// Returns true when the bound int equals the parameter, and sets the int
/// to the parameter value when the RadioButton is checked.
/// </summary>
public sealed class IntEqualConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intVal && parameter is string paramStr && int.TryParse(paramStr, out int target))
        {
            return intVal == target;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string paramStr && int.TryParse(paramStr, out int target))
        {
            return target;
        }

        return Binding.DoNothing;
    }
}
