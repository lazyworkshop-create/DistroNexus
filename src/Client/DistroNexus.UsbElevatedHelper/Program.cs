using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;

namespace DistroNexus.UsbElevatedHelper;

/// <summary>Single-use, fixed-operation elevated entry point. It never accepts a command line or executable from its caller.</summary>
internal static class Program
{
    private static readonly TimeSpan PipeTimeout = TimeSpan.FromSeconds(20);

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--usb-operation") return 2;
        UsbElevatedHelperLaunchEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize<UsbElevatedHelperLaunchEnvelope>(Encoding.UTF8.GetString(Convert.FromBase64String(args[1]))); }
        catch { return 2; }
        if (!IsValid(envelope)) return 2;
        using var cancellation = new CancellationTokenSource(PipeTimeout);
        if (!await ObtainAuthorizationAsync(envelope!, cancellation.Token).ConfigureAwait(false)) return 3;
        return await RunUsbIpdAsync(envelope!.Request, cancellation.Token).ConfigureAwait(false) ? 0 : 4;
    }

    private static readonly string ExpectedPublisherThumbprint = typeof(TrustedUsbIpdExecutable).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(x => x.Key == "DistroNexus.UsbElevatedHelperPublisherThumbprint")?.Value ?? string.Empty;

    private static bool IsValid(UsbElevatedHelperLaunchEnvelope? value) => value is { ProtocolVersion: 3 } &&
        value.PipeName.StartsWith("DistroNexus.Usb.", StringComparison.Ordinal) && value.PipeName.Length <= 64 &&
        value.Request.Action is UsbDeviceAction.Bind or UsbDeviceAction.Unbind &&
        UsbIpdAdapter.IsHardwareId(value.Request.HardwareId) &&
        value.Request.ExpiresAt > DateTimeOffset.UtcNow && value.Request.IssuedAt <= value.Request.ExpiresAt &&
        !string.IsNullOrWhiteSpace(value.Request.CallerIdentity) && !string.IsNullOrWhiteSpace(value.Request.IssuerId) &&
        value.ClientNonce.Length is >= 32 and <= 256 && value.DesktopProcessId > 0;

    private static async Task<bool> ObtainAuthorizationAsync(UsbElevatedHelperLaunchEnvelope envelope, CancellationToken cancellationToken)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(".", envelope.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
            // The pipe name and launch envelope can be observed by a same-user attacker. Bind the
            // connected server to the actual signed desktop process before it can issue a challenge.
            if (!IsConnectedDesktopServerAuthorized(pipe, envelope.DesktopProcessId)) return false;
            using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new UsbElevatedHelperHello(3, Environment.ProcessId, envelope.ClientNonce))).ConfigureAwait(false);
            var challenge = JsonSerializer.Deserialize<UsbElevatedHelperChallenge>(await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty);
            if (challenge is null || string.IsNullOrWhiteSpace(challenge.ServerNonce) || challenge.ServerNonce.Length > 256) return false;
            await writer.WriteLineAsync(JsonSerializer.Serialize(new UsbElevatedHelperProof(envelope.ClientNonce, challenge.ServerNonce, envelope.Request.PreviewToken, Environment.ProcessId))).ConfigureAwait(false);
            return string.Equals(await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false), "authorized", StringComparison.Ordinal);
        }
        catch { return false; }
    }

    internal static bool IsDesktopServerAuthorized(uint reportedServerProcessId, int expectedDesktopProcessId, Func<int, bool> isTrustedDesktopProcess) =>
        reportedServerProcessId != 0 && reportedServerProcessId == (uint)expectedDesktopProcessId && isTrustedDesktopProcess(expectedDesktopProcessId);

    private static bool IsConnectedDesktopServerAuthorized(NamedPipeClientStream pipe, int expectedDesktopProcessId)
    {
        try
        {
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var serverProcessId)) return false;
            return IsDesktopServerAuthorized(serverProcessId, expectedDesktopProcessId, IsTrustedDesktopProcess);
        }
        catch { return false; }
    }

    private static bool IsTrustedDesktopProcess(int processId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ExpectedPublisherThumbprint)) return false;
            using var process = Process.GetProcessById(processId);
            var image = process.MainModule?.FileName;
            return !string.IsNullOrWhiteSpace(image) &&
                string.Equals(Path.GetFileName(image), "DistroNexus.Desktop.exe", StringComparison.OrdinalIgnoreCase) &&
                AuthenticodeTrust.IsTrustedProduct(image, "DistroNexus.Desktop", ExpectedPublisherThumbprint);
        }
        catch (Exception) { return false; }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(IntPtr pipe, out uint serverProcessId);

    private static async Task<bool> RunUsbIpdAsync(UsbElevatedOperationRequest request, CancellationToken cancellationToken)
    {
        var command = request.Action == UsbDeviceAction.Bind ? "bind" : "unbind";
        var executable = TrustedUsbIpdExecutable.Resolve();
        if (executable is null) return false;
        if (!await IsExpectedDeviceCurrentAsync(executable, request, cancellationToken).ConfigureAwait(false)) return false;
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(command);
        start.ArgumentList.Add("--busid");
        start.ArgumentList.Add(request.BusId.Value);
        using var process = Process.Start(start);
        if (process is null) return false;
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode == 0;
    }

    /// <summary>Closes the UAC-time race: never mutate a reused bus ID without a fresh identity and state match.</summary>
    private static async Task<bool> IsExpectedDeviceCurrentAsync(string executable, UsbElevatedOperationRequest request, CancellationToken cancellationToken)
    {
        var major = await GetApprovedMajorAsync(executable, cancellationToken).ConfigureAwait(false);
        if (major is not (4 or 5)) return false;
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("list");
        start.ArgumentList.Add("--json");
        using var process = Process.Start(start);
        if (process is null) return false;
        var output = await ReadBoundedAsync(process.StandardOutput, cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0 || output is null) return false;
        return MatchesExpectedDeviceJson(output, request, major.Value);
    }

    internal static bool MatchesExpectedDeviceJson(string output, UsbElevatedOperationRequest request, int major)
    {
        if (major is not (4 or 5) || !UsbIpdAdapter.IsHardwareId(request.HardwareId)) return false;
        try
        {
            using var document = JsonDocument.Parse(output);
            var rows = document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.EnumerateArray() :
                document.RootElement.TryGetProperty("devices", out var devices) && devices.ValueKind == JsonValueKind.Array ? devices.EnumerateArray() : default;
            if (rows.Equals(default(JsonElement.ArrayEnumerator))) return false;
            foreach (var row in rows)
            {
                // Match only the fixed JSON shape for the executable major.  A changed producer
                // must fail closed here because this check occurs immediately before elevation.
                var bus = major == 4 ? StringValue(row, "busId") : StringValue(row, "bus_id");
                var hardware = major == 4 ? StringValue(row, "hardwareId") : StringValue(row, "vidPid");
                var state = major == 4 ? StringValue(row, "state") : StringValue(row, "status");
                var expectedState = request.Action == UsbDeviceAction.Bind ? "Not shared" : "Shared";
                if (string.Equals(bus, request.BusId.Value, StringComparison.OrdinalIgnoreCase) &&
                    UsbIpdAdapter.IsHardwareId(hardware) &&
                    string.Equals(hardware, request.HardwareId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(state, expectedState, StringComparison.Ordinal)) return true;
            }
        }
        catch (JsonException) { }
        return false;
    }

    private static async Task<int?> GetApprovedMajorAsync(string executable, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("--version");
        using var process = Process.Start(start);
        if (process is null) return null;
        var output = await ReadBoundedAsync(process.StandardOutput, cancellationToken, 4096).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var major = process.ExitCode == 0 ? UsbIpdAdapter.ParseVersion(output ?? string.Empty)?.Major : null;
        return major is 4 or 5 ? major : null;
    }

    private static string? StringValue(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static async Task<string?> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken, int maximum = 256 * 1024)
    {
        var buffer = new char[4096];
        var builder = new StringBuilder();
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0) return builder.ToString();
            if (builder.Length + count > maximum) return null;
            builder.Append(buffer, 0, count);
        }
    }
}
