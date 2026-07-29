using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.Converters;

/// <summary>
/// Converts download status to color.
/// </summary>
public class DownloadStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DownloadStatus status)
        {
            return status switch
            {
                DownloadStatus.Downloading => new SolidColorBrush(Colors.DodgerBlue),
                DownloadStatus.Completed => new SolidColorBrush(Colors.Green),
                DownloadStatus.Failed => new SolidColorBrush(Colors.Red),
                DownloadStatus.Cancelled => new SolidColorBrush(Colors.Gray),
                DownloadStatus.Pending => new SolidColorBrush(Colors.Orange),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts download status to visibility for downloading state.
/// </summary>
public class DownloadingToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DownloadStatus.Downloading or "Running" ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts download status to visibility for failed state.
/// </summary>
public class FailedToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DownloadStatus status)
        {
            return status == DownloadStatus.Failed ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts count to visibility (visible when > 0).
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to visibility with inverse parameter option.
/// </summary>
public class BoolToVisibilityWithInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            bool inverse = parameter?.ToString() == "Inverse";
            return boolValue ^ inverse ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool inverse = parameter?.ToString() == "Inverse";
            return (visibility == Visibility.Visible) ^ inverse;
        }
        return false;
    }
}

/// <summary>
/// Converts download status to visibility for actions (cancel button).
/// </summary>
public class DownloadStatusToActionVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DownloadStatus.Pending or DownloadStatus.Downloading or "Queued" or "Running" ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts download status to visibility for retry button.
/// </summary>
public class DownloadStatusToRetryVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DownloadStatus.Failed or "Failed" or "Cancelled" or "Interrupted" ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
