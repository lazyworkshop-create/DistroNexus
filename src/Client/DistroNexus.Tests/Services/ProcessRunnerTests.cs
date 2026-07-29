using DistroNexus.Core.Models;
using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

[CollectionDefinition(nameof(ProcessRunnerTests), DisableParallelization = true)]
public sealed class ProcessRunnerTestsCollection { }

[Collection(nameof(ProcessRunnerTests))]
public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_CapturesExitAndBoundsOutput()
    {
        var request = new ProcessRequest("powershell.exe", ["-NoProfile", "-Command", "[Console]::Out.Write('abcdefghij')"], TimeSpan.FromSeconds(10), 5);
        var result = await new ProcessRunner().RunAsync(request);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("abcde", result.StandardOutput);
        Assert.True(result.OutputTruncated);
    }

    [Fact]
    public async Task RunAsync_TimesOutAndTerminatesProcess()
    {
        var request = new ProcessRequest("powershell.exe", ["-NoProfile", "-Command", "Start-Sleep -Seconds 10"], TimeSpan.FromMilliseconds(100));
        var result = await new ProcessRunner().RunAsync(request);
        Assert.True(result.TimedOut);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task RunAsync_CancellationIsDistinctFromTimeout()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var request = new ProcessRequest("powershell.exe", ["-NoProfile", "-Command", "Start-Sleep -Seconds 10"], TimeSpan.FromSeconds(10));
        var result = await new ProcessRunner().RunAsync(request, cancellation.Token);
        Assert.True(result.Cancelled);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_PreservesExactArgumentTokens()
    {
        var script = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1");
        try
        {
            await File.WriteAllTextAsync(script, "param($a,$b,$c) [Console]::Write(\"$a|$b|$c\")");
            var result = await new ProcessRunner().RunAsync(new ProcessRequest("powershell.exe",
                ["-NoProfile", "-File", script, "plain", "space value", "x;y"], TimeSpan.FromSeconds(10)));
            Assert.Equal("plain|space value|x;y", result.StandardOutput);
        }
        finally { File.Delete(script); }
    }

    [Fact]
    public async Task RunAsync_BoundsStandardError()
    {
        var result = await new ProcessRunner().RunAsync(new ProcessRequest("powershell.exe",
            ["-NoProfile", "-Command", "[Console]::Error.Write('abcdefghij')"], TimeSpan.FromSeconds(10), MaxStandardErrorBytes: 4));
        Assert.Equal("abcd", result.StandardError);
        Assert.True(result.OutputTruncated);
    }

    [Fact]
    public async Task RunAsync_ReturnsTypedStartupFailure()
    {
        var result = await new ProcessRunner().RunAsync(new ProcessRequest(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"), [], TimeSpan.FromSeconds(1)));
        Assert.Equal(ProcessFailureKind.StartFailed, result.Failure);
        Assert.Null(result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_DecodesSelectedUtf16Output()
    {
        var result = await new ProcessRunner().RunAsync(new ProcessRequest("powershell.exe",
            ["-NoProfile", "-Command", "[Console]::OutputEncoding=[Text.Encoding]::Unicode;[Console]::Write('héllo')"],
            TimeSpan.FromSeconds(10), OutputEncoding: ProcessOutputEncoding.Utf16LittleEndian));
        Assert.Equal("héllo", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_AppliesUtf16ByteLimitUsingSelectedEncoding()
    {
        var result = await new ProcessRunner().RunAsync(new ProcessRequest("powershell.exe",
            ["-NoProfile", "-Command", "[Console]::OutputEncoding=[Text.Encoding]::Unicode;[Console]::Write('abcd');[Console]::Error.Write('wxyz')"],
            TimeSpan.FromSeconds(10), MaxStandardOutputBytes: 4, MaxStandardErrorBytes: 6,
            OutputEncoding: ProcessOutputEncoding.Utf16LittleEndian));
        Assert.Equal("ab", result.StandardOutput);
        Assert.Equal("wxy", result.StandardError);
        Assert.True(result.OutputTruncated);
    }

    [Fact]
    public async Task RunAsync_ReplacesMalformedUtf8WithoutFailing()
    {
        var result = await new ProcessRunner().RunAsync(new ProcessRequest("powershell.exe",
            ["-NoProfile", "-Command", "$s=[Console]::OpenStandardOutput();$b=[byte[]](255);$s.Write($b,0,1)"], TimeSpan.FromSeconds(10)));
        Assert.Equal("�", result.StandardOutput);
        Assert.Equal(0, result.ExitCode);
    }
}
