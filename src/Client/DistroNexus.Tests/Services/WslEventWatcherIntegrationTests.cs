using System.Linq;
using System.Reflection;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistroNexus.Tests.Services;

public class WslEventWatcherIntegrationTests
{
    [Fact]
    public void WslEventWatcher_CacheInvalidationRequested_FiresWhenRaised()
    {
        var mockLogger = new Mock<ILogger<WslEventWatcher>>();
        var watcher = new WslEventWatcher(mockLogger.Object);
        var raised = false;
        watcher.CacheInvalidationRequested += (s, e) => raised = true;

        // Act: trigger the event via the internal test hook
        typeof(WslEventWatcher)
            .GetMethod("FireCacheInvalidatedForTest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(watcher, null);

        Assert.True(raised, "CacheInvalidationRequested was not fired");
    }

    [Fact]
    public void WslEventWatcher_Has_WMI_Field()
    {
        var fields = typeof(WslEventWatcher)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        var hasWmiField = fields.Any(f => f.FieldType.FullName!.Contains("ManagementEventWatcher"));
        Assert.True(hasWmiField, "WslEventWatcher should use ManagementEventWatcher for WMI events");
    }
}
