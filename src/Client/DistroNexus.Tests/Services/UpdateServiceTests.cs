using System.Net;
using System.Net.Http;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

public class UpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_WhenStoreComplianceModeEnabled_ReturnsNullWithoutNetworkCall()
    {
        var handler = new ThrowingHttpMessageHandler();
        using var client = new HttpClient(handler);

        var logger = new Mock<ILogger<UpdateService>>();
        var complianceModeService = new Mock<IStoreComplianceModeService>();
        complianceModeService.Setup(service => service.IsStoreComplianceModeEnabled()).Returns(true);

        var updateService = new UpdateService(client, logger.Object, complianceModeService.Object);

        var result = await updateService.CheckForUpdatesAsync();

        Assert.Null(result);
        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenStoreComplianceModeDisabled_PerformsNetworkCall()
    {
        var responseJson = "{\"tag_name\":\"v2.1.1\",\"body\":\"release\",\"html_url\":\"https://example.com/release\",\"published_at\":\"2026-02-24T00:00:00Z\",\"prerelease\":false,\"assets\":[]}";
        var handler = new StaticResponseHttpMessageHandler(HttpStatusCode.OK, responseJson);
        using var client = new HttpClient(handler);

        var logger = new Mock<ILogger<UpdateService>>();
        var complianceModeService = new Mock<IStoreComplianceModeService>();
        complianceModeService.Setup(service => service.IsStoreComplianceModeEnabled()).Returns(false);

        var updateService = new UpdateService(client, logger.Object, complianceModeService.Object);

        _ = await updateService.CheckForUpdatesAsync();

        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_PrereleaseUsesReleaseFeedSelectsNewestAndUsesSemVerOrdering()
    {
        var releases = """
            [{"tag_name":"v2.0.0-alpha.2","body":"older","html_url":"https://example.test/older","published_at":"2026-02-24T00:00:00Z","prerelease":true,"draft":false,"assets":[]},
             {"tag_name":"v2.0.0-alpha.10","body":"newer","html_url":"https://example.test/newer","published_at":"2026-02-25T00:00:00Z","prerelease":true,"draft":false,"assets":[]}]
            """;
        var handler = new StaticResponseHttpMessageHandler(HttpStatusCode.OK, releases);
        using var client = new HttpClient(handler);
        var complianceModeService = new Mock<IStoreComplianceModeService>();
        complianceModeService.Setup(service => service.IsStoreComplianceModeEnabled()).Returns(false);
        var service = new TestableUpdateService(client, Mock.Of<ILogger<UpdateService>>(), complianceModeService.Object, "2.0.0-alpha.2");

        var update = await service.CheckForUpdatesAsync(includePrerelease: true);

        Assert.Equal("https://api.github.com/repos/LazyWorkshopCreate/DistroNexus/releases", handler.RequestUri!.ToString());
        Assert.NotNull(update);
        Assert.Equal("2.0.0-alpha.10", update.LatestVersion);
        Assert.True(update.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_StableUsesLatestReleaseEndpoint()
    {
        var handler = new StaticResponseHttpMessageHandler(HttpStatusCode.OK, "{\"tag_name\":\"v2.0.0\",\"body\":\"release\",\"html_url\":\"https://example.test/release\",\"published_at\":\"2026-02-24T00:00:00Z\",\"prerelease\":false,\"assets\":[]}");
        using var client = new HttpClient(handler);
        var complianceModeService = new Mock<IStoreComplianceModeService>();
        complianceModeService.Setup(service => service.IsStoreComplianceModeEnabled()).Returns(false);
        var service = new TestableUpdateService(client, Mock.Of<ILogger<UpdateService>>(), complianceModeService.Object, "1.0.0");

        _ = await service.CheckForUpdatesAsync(includePrerelease: false);

        Assert.Equal("https://api.github.com/repos/LazyWorkshopCreate/DistroNexus/releases/latest", handler.RequestUri!.ToString());
    }

    private sealed class TestableUpdateService(HttpClient client, ILogger<UpdateService> logger, IStoreComplianceModeService complianceModeService, string currentVersion)
        : UpdateService(client, logger, complianceModeService)
    {
        public override string GetCurrentVersion() => currentVersion;
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            RequestUri = request.RequestUri;
            throw new InvalidOperationException("HTTP should not be called in Store compliance mode.");
        }
    }

    private sealed class StaticResponseHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public StaticResponseHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public bool WasCalled { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            RequestUri = request.RequestUri;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody)
            };

            return Task.FromResult(response);
        }
    }
}
