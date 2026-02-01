using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DistroNexus.Desktop.Converters;

/// <summary>
/// Converts cached and downloading states to visibility.
/// Shows element only when cached is true AND downloading is false.
/// </summary>
public class CachedNotDownloadingVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2)
        {
            // Show when: cached AND NOT downloading
            bool isCached = values[0] is bool cached && cached;
            bool isDownloading = values[1] is bool downloading && downloading;
            
            if (isCached && !isDownloading)
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
