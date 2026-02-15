using System.Security.Cryptography;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for downloading files from remote sources.
/// </summary>
public class DownloadService : IDownloadService
{
    private readonly ILogger<DownloadService> _logger;
    private readonly HttpClient _httpClient;

    public DownloadService(ILogger<DownloadService> logger, HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc/>
    public async Task<bool> DownloadFileAsync(string url, string destination, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url));

        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentNullException(nameof(destination));

        try
        {
            _logger.LogInformation("Downloading file from {Url} to {Destination}", url, destination);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var canReportProgress = totalBytes != -1 && progress != null;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (canReportProgress)
                {
                    var progressPercentage = (double)totalBytesRead / totalBytes * 100;
                    progress!.Report(progressPercentage);
                }
            }

            _logger.LogInformation("File downloaded successfully: {Destination}", destination);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from {Url}", url);
            
            // Clean up partial download
            if (File.Exists(destination))
            {
                try
                {
                    File.Delete(destination);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to delete partial download file");
                }
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyChecksumAsync(string filePath, string expectedHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));

        if (string.IsNullOrWhiteSpace(expectedHash))
            throw new ArgumentNullException(nameof(expectedHash));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        try
        {
            _logger.LogInformation("Verifying checksum for {FilePath}", filePath);

            using var sha256 = SHA256.Create();
            await using var stream = File.OpenRead(filePath);
            
            var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            var normalizedExpectedHash = expectedHash.Replace("-", "").ToLowerInvariant();

            var isValid = actualHash.Equals(normalizedExpectedHash, StringComparison.OrdinalIgnoreCase);

            if (isValid)
            {
                _logger.LogInformation("Checksum verification successful");
            }
            else
            {
                _logger.LogWarning("Checksum verification failed. Expected: {Expected}, Actual: {Actual}", 
                    normalizedExpectedHash, actualHash);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify checksum for {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<long> GetRemoteFileSizeAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return response.Content.Headers.ContentLength ?? -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get remote file size for {Url}", url);
            return -1;
        }
    }
}
