using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DistroNexus.Desktop.Converters;

/// <summary>
/// Converts a WSL instance state to a color brush.
/// </summary>
public class InstanceStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string state)
            return System.Windows.Media.Brushes.Gray;

        return state.ToLowerInvariant() switch
        {
            "running" => System.Windows.Media.Brushes.Green,
            "stopped" => System.Windows.Media.Brushes.Orange,
            "installing" => System.Windows.Media.Brushes.Blue,
            _ => System.Windows.Media.Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
