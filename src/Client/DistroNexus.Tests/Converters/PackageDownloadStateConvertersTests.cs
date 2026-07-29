using System.Globalization;
using System.Windows;
using DistroNexus.Desktop.Converters;

namespace DistroNexus.Tests.Converters;

public sealed class PackageDownloadStateConvertersTests
{
    [Theory]
    [InlineData("Running", Visibility.Visible)]
    [InlineData("Queued", Visibility.Collapsed)]
    public void ProgressVisibility_UsesPublicPackageJobStates(string state, Visibility expected) =>
        Assert.Equal(expected, new DownloadingToVisibilityConverter().Convert(state, typeof(Visibility), null!, CultureInfo.InvariantCulture));

    [Theory]
    [InlineData("Queued", Visibility.Visible)]
    [InlineData("Running", Visibility.Visible)]
    [InlineData("Completed", Visibility.Collapsed)]
    public void CancelVisibility_UsesPublicPackageJobStates(string state, Visibility expected) =>
        Assert.Equal(expected, new DownloadStatusToActionVisibilityConverter().Convert(state, typeof(Visibility), null!, CultureInfo.InvariantCulture));

    [Theory]
    [InlineData("Failed", Visibility.Visible)]
    [InlineData("Cancelled", Visibility.Visible)]
    [InlineData("Interrupted", Visibility.Visible)]
    [InlineData("Completed", Visibility.Collapsed)]
    public void RetryVisibility_UsesPublicPackageJobStates(string state, Visibility expected) =>
        Assert.Equal(expected, new DownloadStatusToRetryVisibilityConverter().Convert(state, typeof(Visibility), null!, CultureInfo.InvariantCulture));
}
