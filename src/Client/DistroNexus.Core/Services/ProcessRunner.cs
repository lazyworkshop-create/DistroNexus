using System.Diagnostics;
using System.Text;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.FileName)) throw new ArgumentException("Executable is required.", nameof(request));
        if (request.Timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.MaxStandardOutputBytes < 0 || request.MaxStandardErrorBytes < 0) throw new ArgumentOutOfRangeException(nameof(request));

        var startInfo = new ProcessStartInfo(request.FileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.StandardInput is not null,
            CreateNoWindow = true,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty
        };
        foreach (var argument in request.Arguments) startInfo.ArgumentList.Add(argument);

        var encoding = request.OutputEncoding == ProcessOutputEncoding.Utf8 ? new UTF8Encoding(false, false) : Encoding.Unicode;
        startInfo.StandardOutputEncoding = encoding;
        startInfo.StandardErrorEncoding = encoding;
        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        try { process.Start(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            stopwatch.Stop();
            return new ProcessResult(null, string.Empty, string.Empty, stopwatch.Elapsed, false, false, false, null,
                ProcessFailureKind.StartFailed, ex.Message);
        }
        var stdout = ReadBoundedAsync(process.StandardOutput, request.MaxStandardOutputBytes, encoding);
        var stderr = ReadBoundedAsync(process.StandardError, request.MaxStandardErrorBytes, encoding);
        if (request.StandardInput is not null)
        {
            await process.StandardInput.WriteAsync(request.StandardInput).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        using var timeout = new CancellationTokenSource(request.Timeout);
        using var completion = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var timedOut = false;
        var cancelled = false;
        try
        {
            await process.WaitForExitAsync(completion.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            cancelled = cancellationToken.IsCancellationRequested;
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        var outResult = await stdout.ConfigureAwait(false);
        var errResult = await stderr.ConfigureAwait(false);
        stopwatch.Stop();
        return new ProcessResult(process.HasExited ? process.ExitCode : null, outResult.Text, errResult.Text,
            stopwatch.Elapsed, timedOut, cancelled, outResult.Truncated || errResult.Truncated, process.Id);
    }

    private static async Task<(string Text, bool Truncated)> ReadBoundedAsync(StreamReader reader, int limit, Encoding encoding)
    {
        var buffer = new char[4096];
        var builder = new StringBuilder();
        var bytes = 0;
        var truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            for (var i = 0; i < count; i++)
            {
                var size = encoding.GetByteCount(buffer.AsSpan(i, 1));
                if (bytes + size <= limit) { builder.Append(buffer[i]); bytes += size; }
                else truncated = true;
            }
        }
        return (builder.ToString(), truncated);
    }
}
