namespace DistroNexus.Core.Models;

public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    TimeSpan Timeout,
    int MaxStandardOutputBytes = 1024 * 1024,
    int MaxStandardErrorBytes = 256 * 1024,
    string? WorkingDirectory = null,
    ProcessOutputEncoding OutputEncoding = ProcessOutputEncoding.Utf8);

public enum ProcessOutputEncoding { Utf8, Utf16LittleEndian }
public enum ProcessFailureKind { None, StartFailed }

public sealed record ProcessResult(
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut,
    bool Cancelled,
    bool OutputTruncated,
    int? ProcessId,
    ProcessFailureKind Failure = ProcessFailureKind.None,
    string? FailureMessage = null);
