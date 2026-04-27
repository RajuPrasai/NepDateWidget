using System.Globalization;
using System.Windows.Data;

namespace NepDateWidget.Converters;

/// <summary>
/// Multi-value converter for the password strength progress bar.
/// Inputs: [0] percent (0–100 double), [1] total available width (double).
/// Returns: (percent / 100) * totalWidth, clamped to [0, totalWidth].
/// </summary>
public sealed class StrengthBarWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0.0;
        if (values[0] is not double percent) return 0.0;
        if (values[1] is not double total)   return 0.0;
        if (total <= 0) return 0.0;
        return Math.Max(0, Math.Min(total, percent / 100.0 * total));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
