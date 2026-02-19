namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides methods for downloading files.
/// </summary>
public interface IDownloadService
{
    /// <summary>
    /// Downloads a file from the specified URL to the destination path.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="destination">The destination file path.</param>
    /// <param name="progress">Progress reporter for the download (bytes read, total bytes).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the download was successful, otherwise false.</returns>
    Task<bool> DownloadFileAsync(string url, string destination, IProgress<(long BytesRead, long TotalBytes)>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the SHA256 checksum of a downloaded file.
    /// </summary>
    /// <param name="filePath">The path to the file to verify.</param>
    /// <param name="expectedHash">The expected SHA256 hash.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>True if the checksum matches, otherwise false.</returns>
    Task<bool> VerifyChecksumAsync(string filePath, string expectedHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the size of a remote file without downloading it.
    /// </summary>
    /// <param name="url">The URL of the file.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The file size in bytes, or -1 if unavailable.</returns>
    Task<long> GetRemoteFileSizeAsync(string url, CancellationToken cancellationToken = default);
}
