using DistroNexus.Core.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DistroNexus.Desktop.Converters;

/// <summary>
/// Converts DownloadStatus to Visibility for UI elements.
/// </summary>
public class DownloadStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DownloadStatus status || parameter is not string targetStatus)
            return Visibility.Collapsed;

        if (Enum.TryParse<DownloadStatus>(targetStatus, out var target))
        {
            return status == target ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
