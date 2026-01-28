using System;
using System.Globalization;
using System.Windows.Data;

namespace DistroNexus.Desktop.Converters;

/// <summary>
/// Formats file size in bytes to human-readable format (KB, MB, GB).
/// </summary>
public class FileSizeConverter : IValueConverter
{
    private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB" };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long size || size < 0)
            return "0 B";

        int magnitude = 0;
        double adjustedSize = size;

        while (adjustedSize >= 1024 && magnitude < SizeSuffixes.Length - 1)
        {
            adjustedSize /= 1024;
            magnitude++;
        }

        return $"{adjustedSize:0.##} {SizeSuffixes[magnitude]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
