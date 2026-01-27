using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DistroNexus.Desktop.Converters;

/// <summary>
/// Converts a boolean value to a Visibility value.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool boolValue)
            return Visibility.Collapsed;

        var inverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) ?? false;

        if (inverse)
            boolValue = !boolValue;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Visibility visibility)
            return false;

        var inverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) ?? false;
        var result = visibility == Visibility.Visible;

        return inverse ? !result : result;
    }
}
