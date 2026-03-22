using DistroNexus.Core.Exceptions;
using DistroNexus.Desktop.ViewModels;
using Xunit;

namespace DistroNexus.Tests.ViewModels;

public class MainViewModelErrorFormatTests
{
    [Fact]
    public void FormatAlertMessage_WithWslException_IncludesErrorCodePrefix()
    {
        var ex = new WslExportFailedException("Ubuntu", "disk full");
        var msg = MainViewModel.FormatAlertMessage(ex);
        Assert.StartsWith("[DN-", msg);
        Assert.Contains(ex.Message, msg);
    }

    [Fact]
    public void FormatAlertMessage_WithPlainException_HasNoPrefix()
    {
        var ex = new InvalidOperationException("plain error");
        var msg = MainViewModel.FormatAlertMessage(ex);
        Assert.DoesNotContain("[DN-", msg);
        Assert.Equal("plain error", msg);
    }
}
