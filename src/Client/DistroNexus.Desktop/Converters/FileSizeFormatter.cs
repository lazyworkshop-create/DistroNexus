using System.Globalization;
using System.Windows.Data;

namespace DistroNexus.Desktop.Converters;

/// <summary>
/// Converts a file size in bytes to a human-readable string.
/// </summary>
public class FileSizeFormatter : IValueConverter
{
    private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB" };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes)
            return "0 B";

        if (bytes == 0)
            return "0 B";

        var absBytes = Math.Abs(bytes);
        var place = System.Convert.ToInt32(Math.Floor(Math.Log(absBytes, 1024)));
        var num = Math.Round(absBytes / Math.Pow(1024, place), 1);

        return $"{(Math.Sign(bytes) * num):0.#} {SizeUnits[place]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
