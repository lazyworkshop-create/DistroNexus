using System.Globalization;
using DistroNexus.Desktop.Converters;

namespace DistroNexus.Tests.Converters;

public class FileSizeFormatterTests
{
    private readonly FileSizeFormatter _converter = new();

    [Theory]
    [InlineData(1024L, "1 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(0L, "0 B")]
    public void Convert_ShouldFormatExpectedSize(long bytes, string expected)
    {
        var result = _converter.Convert(bytes, typeof(string), string.Empty, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }
}
