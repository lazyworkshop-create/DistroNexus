using System.Net;
using System.Net.Http;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

public class DownloadServiceTests
{
    private readonly Mock<ILogger<DownloadService>> _logger = new();

    [Fact]
    public async Task DownloadFileAsync_ShouldReportIncreasingBytesAndTotalBytes()
    {
        var data = new byte[32 * 1024];
        new Random(42).NextBytes(data);

        using var handler = new StubHttpMessageHandler(_ =>
        {
            var content = new ByteArrayContent(data);
            content.Headers.ContentLength = data.Length;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        using var httpClient = new HttpClient(handler);
        var service = new DownloadService(_logger.Object, httpClient);

        var reports = new List<(long BytesRead, long TotalBytes)>();
        var progress = new SynchronousProgress(r => reports.Add(r));

        var tempDir = Path.Combine(Path.GetTempPath(), "DistroNexus.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var destination = Path.Combine(tempDir, "sample.bin");

        try
        {
            var success = await service.DownloadFileAsync("https://example.com/sample.bin", destination, progress);

            Assert.True(success);
            Assert.True(File.Exists(destination));
            Assert.Equal(data.Length, new FileInfo(destination).Length);
            Assert.NotEmpty(reports);
            Assert.All(reports, r => Assert.Equal(data.Length, r.TotalBytes));

            var ordered = reports.Select(r => r.BytesRead).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                Assert.True(ordered[index] >= ordered[index - 1]);
            }

            Assert.Equal(data.Length, ordered[^1]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task DownloadFileAsync_WhenContentLengthMissing_ShouldReportUnknownTotal()
    {
        var data = new byte[8 * 1024];
        new Random(7).NextBytes(data);

        using var handler = new StubHttpMessageHandler(_ =>
        {
            var content = new UnknownLengthContent(data);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        using var httpClient = new HttpClient(handler);
        var service = new DownloadService(_logger.Object, httpClient);

        var reports = new List<(long BytesRead, long TotalBytes)>();
        var progress = new SynchronousProgress(r => reports.Add(r));

        var tempDir = Path.Combine(Path.GetTempPath(), "DistroNexus.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var destination = Path.Combine(tempDir, "unknown-length.bin");

        try
        {
            var success = await service.DownloadFileAsync("https://example.com/unknown-length.bin", destination, progress);

            Assert.True(success);
            Assert.True(File.Exists(destination));
            Assert.Equal(data.Length, new FileInfo(destination).Length);
            Assert.NotEmpty(reports);
            Assert.All(reports, r => Assert.Equal(-1, r.TotalBytes));
            Assert.Equal(data.Length, reports[^1].BytesRead);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task DownloadFileAsync_WithEmptyFile_ShouldSucceedAndCreateZeroByteFile()
    {
        using var handler = new StubHttpMessageHandler(_ =>
        {
            var content = new ByteArrayContent(Array.Empty<byte>());
            content.Headers.ContentLength = 0;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        using var httpClient = new HttpClient(handler);
        var service = new DownloadService(_logger.Object, httpClient);

        var reports = new List<(long BytesRead, long TotalBytes)>();
        var progress = new SynchronousProgress(r => reports.Add(r));

        var tempDir = Path.Combine(Path.GetTempPath(), "DistroNexus.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var destination = Path.Combine(tempDir, "empty.bin");

        try
        {
            var success = await service.DownloadFileAsync("https://example.com/empty.bin", destination, progress);

            Assert.True(success);
            Assert.True(File.Exists(destination));
            Assert.Equal(0, new FileInfo(destination).Length);
            Assert.Empty(reports);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return stream.WriteAsync(bytes, 0, bytes.Length);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }

    private sealed class SynchronousProgress(Action<(long BytesRead, long TotalBytes)> onReport) : IProgress<(long BytesRead, long TotalBytes)>
    {
        public void Report((long BytesRead, long TotalBytes) value)
        {
            onReport(value);
        }
    }
}
