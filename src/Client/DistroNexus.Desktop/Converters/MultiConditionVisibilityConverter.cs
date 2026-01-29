using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DistroNexus.Desktop.Converters;

/// <summary>
/// Converts multiple boolean conditions to visibility.
/// Shows element only when first condition is false AND second condition is false.
/// </summary>
public class MultiConditionVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2)
        {
            // Show Download button when: NOT cached AND NOT downloading
            bool isCached = values[0] is bool cached && cached;
            bool isDownloading = values[1] is bool downloading && downloading;
            
            if (!isCached && !isDownloading)
            {
                return Visibility.Visible;
            }
        }
        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
