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

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody)
            };

            return Task.FromResult(response);
        }
    }
}
