using System.Management;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Reflection;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public sealed class UsbDeviceService : IUsbDeviceService
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(2);
    private readonly IUsbIpdAdapter _adapter;
    private readonly IUsbElevatedOperationBroker _elevated;
    private readonly IUsbElevatedRequestIssuer _issuer;
    private readonly IUsbCallerIdentityProvider _caller;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<Guid, UsbDeviceActionPreview> _previews = [];
    public UsbDeviceService(IUsbIpdAdapter adapter, IUsbElevatedOperationBroker elevated,
        IUsbElevatedRequestIssuer? issuer = null, IUsbCallerIdentityProvider? caller = null, TimeProvider? time = null)
    { (_adapter, _elevated, _issuer, _caller, _time) = (adapter, elevated, issuer ?? new UsbElevatedRequestIssuer(), caller ?? new WindowsUsbCallerIdentityProvider(), time ?? TimeProvider.System); }
    public Task<UsbIpdStatus> GetStatusAsync(CancellationToken cancellationToken = default) => _adapter.GetStatusAsync(cancellationToken);
    public async Task<IReadOnlyList<UsbDeviceInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var status = await _adapter.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return status.IsInstalled ? await _adapter.ListAsync(status, cancellationToken).ConfigureAwait(false) : [];
    }
    public async Task<UsbDeviceActionPreview> PreviewAsync(UsbDeviceAction action, UsbBusId busId, string? distributionName = null, CancellationToken cancellationToken = default)
    {
        if (action == UsbDeviceAction.Attach && !SafeDistribution(distributionName)) throw new ArgumentException("A valid distribution name is required for attach.", nameof(distributionName));
        if (action != UsbDeviceAction.Attach && distributionName is not null) throw new ArgumentException("Only attach accepts a distribution.", nameof(distributionName));
        var status = await _adapter.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        EnsureMutable(status);
        var device = (await _adapter.ListAsync(status, cancellationToken).ConfigureAwait(false)).FirstOrDefault(x => x.BusId == busId);
        if (device is null || device.Availability == UsbDeviceAvailability.NotConnected) throw new InvalidOperationException("DN-8008: The selected USB device is no longer connected.");
        EnsureLegalTransition(action, device);
        var privileged = action is UsbDeviceAction.Bind or UsbDeviceAction.Unbind;
        var warnings = new List<string> { "USB/IP attachment is visible to the running WSL 2 VM and is not isolated to one distribution." };
        if (device.IsStorageClass) warnings.Add("Storage devices can be modified by Linux software. Confirm that no Windows application is using the device.");
        var preview = new UsbDeviceActionPreview(Guid.NewGuid(), action, busId, device.HardwareId, distributionName, privileged, true,
            [$"{action} USB device {busId.Value}" + (distributionName is null ? "." : $" for WSL distribution {distributionName}."), privileged ? "This operation requires an explicit elevated helper." : "The operation is sent through usbipd using structured arguments."], warnings);
        preview = preview with { ExpiresAt = _time.GetUtcNow().Add(PreviewLifetime) };
        _previews[preview.Token] = preview;
        return preview;
    }
    public async Task<UsbDeviceActionResult> ExecuteAsync(UsbDeviceActionPreview preview, IProgress<UsbOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new("Usb.Phase.Validating", 10));
        if (!_previews.TryRemove(preview.Token, out var expected) || expected != preview || expected.ExpiresAt <= _time.GetUtcNow())
            return Failure("Usb.PreviewRequired", "DN-8009", "Generate and explicitly confirm a current USB operation preview.");
        var status = await _adapter.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.IsInstalled) return Failure("Usb.Unavailable", "DN-8006", "usbipd-win is unavailable.");
        if (!status.IsServiceRunning) return Failure("Usb.ServiceStopped", "DN-8012", "The usbipd service is not running.");
        if (!status.SupportsMutation) return Failure("Usb.UnsupportedVersion", "DN-8007", "The detected usbipd version is not approved for mutation.");
        progress?.Report(new("Usb.Phase.Revalidating", 30));
        var current = (await _adapter.ListAsync(status, cancellationToken).ConfigureAwait(false)).FirstOrDefault(x => x.BusId == preview.BusId);
        if (current is null || current.Availability == UsbDeviceAvailability.NotConnected) return Failure("Usb.StaleBusId", "DN-8008", "Refresh the device list and select the connected device again.");
        if (!string.Equals(current.HardwareId, preview.HardwareId, StringComparison.OrdinalIgnoreCase))
            return Failure("Usb.HardwareChanged", "DN-8014", "A different USB device now owns this bus ID. Refresh and review a new preview.", current);
        if (!IsLegalTransition(preview.Action, current)) return Failure("Usb.StateChanged", "DN-8014", "The USB device state changed after confirmation. Refresh and review a new preview.", current);
        progress?.Report(new(preview.RequiresElevation ? "Usb.Phase.RequestingElevation" : "Usb.Phase.Executing", 55));
        var result = preview.RequiresElevation
            ? await _elevated.ExecuteAsync(_issuer.Issue(preview, _caller.GetCallerIdentity()), cancellationToken).ConfigureAwait(false)
            : await _adapter.ExecuteUnelevatedAsync(preview.Action, preview.BusId, preview.DistributionName, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded) return result.Diagnostic is null ? result with { Diagnostic = new UsbDiagnostic("DN-8011", result.Guidance ?? "USB operation failed.") } : result;
        progress?.Report(new("Usb.Phase.Refreshing", 80));
        var refreshed = (await _adapter.ListAsync(status, cancellationToken).ConfigureAwait(false)).FirstOrDefault(x => x.BusId == preview.BusId);
        var completed = result with { Device = refreshed ?? result.Device };
        if (completed.Succeeded && preview.Action == UsbDeviceAction.Attach && refreshed is not null && preview.DistributionName is not null)
        {
            var verification = await _adapter.VerifyAttachmentAsync(refreshed, preview.DistributionName, cancellationToken).ConfigureAwait(false);
            progress?.Report(new("Usb.Phase.Verifying", 90));
            completed = verification.Outcome == UsbAttachmentVerification.Present ? completed : completed with { Guidance = verification.Detail };
        }
        progress?.Report(new("Usb.Phase.Completed", 100));
        return completed;
    }
    private static UsbDeviceActionResult Failure(string outcome, string code, string message, UsbDeviceInfo? device = null) =>
        new(false, outcome, device, $"{code}: {message}", new UsbDiagnostic(code, message));
    private static void EnsureMutable(UsbIpdStatus status)
    {
        if (!status.IsInstalled) throw new InvalidOperationException("DN-8006: usbipd-win is unavailable.");
        if (!status.IsServiceRunning) throw new InvalidOperationException("DN-8012: The usbipd service is not running.");
        if (!status.SupportsMutation) throw new InvalidOperationException("DN-8007: The detected usbipd version is not approved for mutation.");
    }
    private static bool SafeDistribution(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value.IndexOfAny(['\r', '\n', '\0']) < 0;
    private static void EnsureLegalTransition(UsbDeviceAction action, UsbDeviceInfo device)
    {
        if (!IsLegalTransition(action, device))
            throw new InvalidOperationException("DN-8014: This USB operation is not legal for the device's current state. Refresh the device list.");
    }
    private static bool IsLegalTransition(UsbDeviceAction action, UsbDeviceInfo device) => action switch
    {
        UsbDeviceAction.Bind => device.Availability == UsbDeviceAvailability.Available && !device.IsShared && !device.IsAttached,
        UsbDeviceAction.Unbind => device.Availability == UsbDeviceAvailability.Shared && device.IsShared && !device.IsAttached,
        UsbDeviceAction.Attach => device.Availability == UsbDeviceAvailability.Shared && device.IsShared && !device.IsAttached,
        UsbDeviceAction.Detach => device.Availability == UsbDeviceAvailability.Attached && device.IsAttached,
        _ => false
    };
}

public sealed class UsbIpdAdapter(IProcessRunner runner) : IUsbIpdAdapter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
    public async Task<UsbIpdStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var version = await runner.RunAsync(new ProcessRequest("usbipd.exe", ["--version"], Timeout, 4096, 4096), cancellationToken).ConfigureAwait(false);
        if (version.Failure != ProcessFailureKind.None || version.ExitCode != 0) return new(false, false, null, false, "Usb.Unavailable");
        var service = await runner.RunAsync(new ProcessRequest("sc.exe", ["query", "usbipd"], Timeout, 4096, 4096), cancellationToken).ConfigureAwait(false);
        var running = service.ExitCode == 0 && service.StandardOutput.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
        var parsed = ParseVersion(version.StandardOutput);
        var profile = SelectParserProfile(parsed);
        // The service state is independent evidence. Keep it available for guidance even if the CLI
        // prints an unknown version, while still refusing every mutation without an approved parser.
        if (parsed is null) return new(true, running, null, false, running ? "Usb.VersionMalformed" : "Usb.ServiceStopped", profile, TrimDiagnostic(version.StandardOutput));
        return new(true, running, parsed, profile is "json-v4" or "json-v5", running ? "Usb.Available" : "Usb.ServiceStopped", profile, TrimDiagnostic(version.StandardOutput));
    }
    public async Task<IReadOnlyList<UsbDeviceInfo>> ListAsync(UsbIpdStatus status, CancellationToken cancellationToken = default)
    {
        if (status.ParserProfile is "json-v4" or "json-v5")
        {
            var jsonResult = await runner.RunAsync(new ProcessRequest("usbipd.exe", ["list", "--json"], Timeout, 256 * 1024, 64 * 1024), cancellationToken).ConfigureAwait(false);
            if (jsonResult.ExitCode == 0 && !jsonResult.OutputTruncated && TryParseJson(jsonResult.StandardOutput, status.Version!.Major, out var parsed)) return parsed;
        }
        var result = await runner.RunAsync(new ProcessRequest("usbipd.exe", ["list"], Timeout, 256 * 1024, 64 * 1024), cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 && !result.OutputTruncated ? ParseTable(result.StandardOutput) : [];
    }
    public async Task<UsbDeviceActionResult> ExecuteUnelevatedAsync(UsbDeviceAction action, UsbBusId busId, string? distributionName, CancellationToken cancellationToken = default)
    {
        if (action is UsbDeviceAction.Bind or UsbDeviceAction.Unbind) return new(false, "Usb.ElevationRequired", null, "DN-8010: Bind and unbind require the limited elevated helper.");
        var args = action == UsbDeviceAction.Attach ? new List<string> { "attach", "--wsl", "--busid", busId.Value, "--distribution", distributionName! } : new List<string> { "detach", "--busid", busId.Value };
        var result = await runner.RunAsync(new ProcessRequest("usbipd.exe", args, Timeout, 16 * 1024, 16 * 1024), cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 ? new(true, "Usb.Succeeded") : new(false, "Usb.OperationFailed", null, "DN-8011: usbipd rejected the requested device operation.");
    }
    public async Task<UsbAttachmentVerificationResult> VerifyAttachmentAsync(UsbDeviceInfo device, string distributionName, CancellationToken cancellationToken = default)
    {
        var result = await runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", distributionName, "--exec", "lsusb"], Timeout, 64 * 1024, 16 * 1024), cancellationToken).ConfigureAwait(false);
        if (result.Failure != ProcessFailureKind.None || result.ExitCode != 0)
            return new(UsbAttachmentVerification.ToolUnavailable, "USB attachment completed, but lsusb is unavailable in the selected distribution. Install it manually if verification is needed; DistroNexus does not install Linux packages or udev rules.");
        var present = !string.IsNullOrWhiteSpace(device.HardwareId) && result.StandardOutput.Contains(device.HardwareId, StringComparison.OrdinalIgnoreCase);
        return present ? new(UsbAttachmentVerification.Present, "Attachment verified by lsusb.") : new(UsbAttachmentVerification.NotPresent, "usbipd reported success, but lsusb did not yet show the device. Refresh or check Linux udev permissions.");
    }
    public static Version? ParseVersion(string? text)
    {
        // Process output is bounded by the caller, but preserve that boundary for direct callers and fixtures too.
        if (string.IsNullOrEmpty(text) || text.Length > 4096) return null;
        var match = Regex.Match(text, @"(?<![\d.])(?<version>\d{1,10}\.\d{1,10}(?:\.\d{1,10})?)(?![\d.])", RegexOptions.CultureInvariant);
        return match.Success && Version.TryParse(match.Groups["version"].Value, out var version) ? version : null;
    }
    public static string SelectParserProfile(Version? version) => version?.Major switch { 4 => "json-v4", 5 => "json-v5", null => "table-unknown", _ => "table-unsupported" };
    private static string TrimDiagnostic(string value) => value.Length <= 512 ? value : value[..512];
    public static IReadOnlyList<UsbDeviceInfo> ParseTable(string text)
    {
        var list = new List<UsbDeviceInfo>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line, @"^\s*(?<bus>[0-9A-Fa-f]{1,3}-[0-9A-Fa-f]{1,3})\s+(?<vid>[0-9A-Fa-f]{4}:[0-9A-Fa-f]{4})\s+(?<desc>.+?)\s{2,}(?<state>Not shared|Shared|Attached|Unknown)\s*$", RegexOptions.CultureInvariant);
            if (!match.Success) continue;
            try { list.Add(Device(new UsbBusId(match.Groups["bus"].Value), match.Groups["vid"].Value, match.Groups["desc"].Value.Trim(), match.Groups["state"].Value, null)); } catch (ArgumentException) { }
        }
        return list;
    }
    public static bool TryParseJson(string text, out IReadOnlyList<UsbDeviceInfo> devices) => TryParseJson(text, 4, out devices);
    public static bool TryParseJson(string text, int major, out IReadOnlyList<UsbDeviceInfo> devices)
    {
        devices = [];
        try
        {
            using var doc = JsonDocument.Parse(text); var root = doc.RootElement;
            var rows = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray() : root.TryGetProperty("devices", out var d) && d.ValueKind == JsonValueKind.Array ? d.EnumerateArray() : default;
            if (rows.Equals(default(JsonElement.ArrayEnumerator))) return false;
            var list = new List<UsbDeviceInfo>();
            foreach (var row in rows)
            {
                // Each approved major has a fixed producer shape.  Do not combine aliases: accepting
                // a mixed or changed shape could turn untrusted output into a mutatable device.
                var bus = major == 4 ? S(row, "busId") : major == 5 ? S(row, "bus_id") : null;
                var id = major == 4 ? S(row, "hardwareId") : major == 5 ? S(row, "vidPid") : null;
                var desc = major == 4 ? S(row, "description") : major == 5 ? S(row, "device") : null;
                var state = major == 4 ? S(row, "state") : major == 5 ? S(row, "status") : null;
                if (bus is null || id is null || !IsHardwareId(id) || desc is null || state is null) return false;
                try { list.Add(Device(new UsbBusId(bus), id, desc, state, S(row, "client") ?? S(row, "distribution"))); }
                catch (ArgumentException) { devices = []; return false; }
            }
            devices = list; return true;
        }
        catch (JsonException) { return false; }
    }
    private static string? S(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    public static bool IsHardwareId(string? value) => value is not null && Regex.IsMatch(value, "^[0-9A-Fa-f]{4}:[0-9A-Fa-f]{4}$", RegexOptions.CultureInvariant);
    private static UsbDeviceInfo Device(UsbBusId bus, string id, string desc, string state, string? distribution)
    {
        if (!TryParseState(state, out var availability, out var shared, out var attached))
            throw new ArgumentException("USB device state is not a supported exact value.", nameof(state));
        return new(bus, id, desc, availability, shared, attached, IsConservativelyStorageClass(desc), distribution, GuidanceFor(desc, id));
    }
    private static bool TryParseState(string value, out UsbDeviceAvailability availability, out bool shared, out bool attached)
    {
        switch (value)
        {
            case "Not shared": availability = UsbDeviceAvailability.Available; shared = false; attached = false; return true;
            case "Shared": availability = UsbDeviceAvailability.Shared; shared = true; attached = false; return true;
            case "Attached": availability = UsbDeviceAvailability.Attached; shared = true; attached = true; return true;
            case "Unknown": availability = UsbDeviceAvailability.Unknown; shared = false; attached = false; return true;
            default: availability = UsbDeviceAvailability.Unknown; shared = false; attached = false; return false;
        }
    }
    private static string? GuidanceFor(string description, string hardwareId)
    {
        var source = description + " " + hardwareId;
        if (source.Contains("arduino", StringComparison.OrdinalIgnoreCase)) return "Devices_GuidanceArduino";
        if (source.Contains("android", StringComparison.OrdinalIgnoreCase) || source.Contains("adb", StringComparison.OrdinalIgnoreCase)) return "Devices_GuidanceAndroid";
        if (source.Contains("smart card", StringComparison.OrdinalIgnoreCase) || source.Contains("smartcard", StringComparison.OrdinalIgnoreCase)) return "Devices_GuidanceSmartcard";
        if (source.Contains("serial", StringComparison.OrdinalIgnoreCase) || source.Contains("uart", StringComparison.OrdinalIgnoreCase) || source.Contains("ftdi", StringComparison.OrdinalIgnoreCase)) return "Devices_GuidanceSerial";
        if (IsConservativelyStorageClass(description)) return "Devices_GuidanceStorage";
        return null;
    }
    private static bool IsConservativelyStorageClass(string description)
    {
        var text = description.AsSpan();
        return text.Contains("mass storage", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("usb storage", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("flash drive", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("thumb drive", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("external disk", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Issues bounded grants. The registry is deliberately local, short-lived, and one-shot.</summary>
public sealed class UsbElevatedRequestIssuer(TimeProvider? timeProvider = null) : IUsbElevatedRequestIssuer
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<Guid, UsbElevatedOperationRequest> _issued = [];
    private readonly string _issuer = Guid.NewGuid().ToString("N");
    public UsbElevatedOperationRequest Issue(UsbDeviceActionPreview preview, string callerIdentity)
    {
        if (preview.Action is not (UsbDeviceAction.Bind or UsbDeviceAction.Unbind) || preview.ExpiresAt is null || preview.ExpiresAt <= _time.GetUtcNow())
            throw new InvalidOperationException("DN-8010: Only an unexpired bind or unbind preview can be elevated.");
        var request = new UsbElevatedOperationRequest(preview.Token, preview.Action, preview.BusId, preview.HardwareId, _time.GetUtcNow(), preview.ExpiresAt.Value, _issuer, callerIdentity);
        _issued[request.PreviewToken] = request;
        return request;
    }
    public bool Consume(UsbElevatedOperationRequest request, string callerIdentity)
    {
        if (!IsCurrent(request, callerIdentity)) return false;
        return _issued.TryRemove(request.PreviewToken, out var expected) && expected == request;
    }
    public bool IsCurrent(UsbElevatedOperationRequest request, string callerIdentity) =>
        request.Action is UsbDeviceAction.Bind or UsbDeviceAction.Unbind && request.ExpiresAt > _time.GetUtcNow() &&
        string.Equals(request.CallerIdentity, callerIdentity, StringComparison.Ordinal) &&
        _issued.TryGetValue(request.PreviewToken, out var expected) && expected == request;
}

public sealed class WindowsUsbCallerIdentityProvider : IUsbCallerIdentityProvider
{
    public string GetCallerIdentity()
    {
        try { return WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName; }
        catch (PlatformNotSupportedException) { return Environment.UserName; }
    }
}

/// <summary>
/// Fixed production boundary for the product-owned elevated helper. No executable path, command string, or arguments
/// are supplied by the caller. A missing or unsigned helper fails closed before any host mutation is attempted.
/// </summary>
public sealed class SignedUsbElevatedOperationBroker(IUsbElevatedRequestIssuer issuer, IUsbCallerIdentityProvider caller, IUsbElevatedHelperLauncher launcher) : IUsbElevatedOperationBroker
{
    public SignedUsbElevatedOperationBroker() : this(new UsbElevatedRequestIssuer(), new WindowsUsbCallerIdentityProvider(), new SignedUsbElevatedHelperLauncher(new ProcessRunner())) { }
    public Task<UsbDeviceActionResult> ExecuteAsync(UsbElevatedOperationRequest request, CancellationToken cancellationToken = default)
    {
        if (launcher is IUsbElevatedAuthenticatedHelperLauncher authenticated)
        {
            if (!issuer.IsCurrent(request, caller.GetCallerIdentity()))
                return Task.FromResult(new UsbDeviceActionResult(false, "Usb.AllowListRejected", null, "DN-8010: The elevated USB helper permits only a current, unconsumed bind or unbind preview from this caller."));
            return authenticated.LaunchAuthorizedAsync(request, issuer, caller.GetCallerIdentity(), cancellationToken);
        }
        if (!issuer.Consume(request, caller.GetCallerIdentity()))
            return Task.FromResult(new UsbDeviceActionResult(false, "Usb.AllowListRejected", null, "DN-8010: The elevated USB helper permits only a current, unconsumed bind or unbind preview from this caller."));
        return launcher.LaunchAsync(request, cancellationToken);
    }
}

/// <summary>Launcher for the fixed, packaged helper protocol. Signature validation is delegated to Windows before launch.</summary>
public sealed class SignedUsbElevatedHelperLauncher(IProcessRunner runner) : IUsbElevatedAuthenticatedHelperLauncher
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
    private const string ExpectedProductName = "DistroNexus.UsbElevatedHelper";
    private const string ExpectedPublisherSubject = "CN=DistroNexus";
    private static readonly string ExpectedPublisherThumbprint = typeof(SignedUsbElevatedHelperLauncher).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(x => x.Key == "DistroNexus.UsbElevatedHelperPublisherThumbprint")?.Value ?? string.Empty;
    public async Task<UsbDeviceActionResult> LaunchAsync(UsbElevatedOperationRequest request, CancellationToken cancellationToken = default)
        => new(false, "Usb.HelperProtocolRequired", null, "DN-8013: The elevated helper requires one-time pipe authorization.");

    public async Task<UsbDeviceActionResult> LaunchAuthorizedAsync(UsbElevatedOperationRequest request, IUsbElevatedRequestIssuer issuer,
        string callerIdentity, CancellationToken cancellationToken = default)
    {
        var helper = Path.Combine(AppContext.BaseDirectory, "DistroNexus.UsbElevatedHelper.exe");
        if (!File.Exists(helper)) return new(false, "Usb.ElevatedHelperUnavailable", null, "DN-8013: The signed DistroNexus elevated helper is not installed.");
        if (string.IsNullOrWhiteSpace(ExpectedPublisherThumbprint)) return new(false, "Usb.ElevatedHelperIdentityUnpinned", null, "DN-8013: This build does not contain the pinned DistroNexus helper certificate identity.");
        var signature = await runner.RunAsync(new ProcessRequest("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", "$s=Get-AuthenticodeSignature -LiteralPath $args[0];$v=(Get-Item -LiteralPath $args[0]).VersionInfo;if($s.Status -eq 'Valid' -and $s.SignerCertificate.Subject -eq $args[1] -and $s.SignerCertificate.Thumbprint -eq $args[2] -and $v.ProductName -eq $args[3]){exit 0};exit 1", helper, ExpectedPublisherSubject, ExpectedPublisherThumbprint, ExpectedProductName], TimeSpan.FromSeconds(20), 1024, 1024), cancellationToken).ConfigureAwait(false);
        if (signature.ExitCode != 0 || signature.Failure != ProcessFailureKind.None) return new(false, "Usb.ElevatedHelperUnsigned", null, "DN-8013: The DistroNexus USB helper signature could not be verified.");
        var pipeName = "DistroNexus.Usb." + Guid.NewGuid().ToString("N");
        var clientNonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        NamedPipeServerStream pipe;
        try { pipe = CreateAuthorizationPipe(pipeName, callerIdentity); }
        catch (Exception) { return new(false, "Usb.HelperProtocolUnavailable", null, "DN-8013: The elevated helper authorization channel could not be secured.", new UsbDiagnostic("DN-8013", "The elevated helper authorization channel could not be secured.")); }
        await using (pipe)
        {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new UsbElevatedHelperLaunchEnvelope(3, pipeName, request, clientNonce, Environment.ProcessId))));
        try
        {
            var start = new ProcessStartInfo(helper) { UseShellExecute = true, Verb = "runas" };
            start.ArgumentList.Add("--usb-operation");
            start.ArgumentList.Add(payload);
            using var process = Process.Start(start);
            if (process is null) return new(false, "Usb.ElevatedHelperUnavailable", null, "DN-8013: The signed DistroNexus elevated helper could not be started.");
            var authorization = AuthorizeHelperAsync(pipe, request, issuer, callerIdentity, process.Id, clientNonce, cancellationToken);
            var authorized = await authorization.ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (!authorized) return new(false, "Usb.AllowListRejected", null, "DN-8010: The helper authorization was rejected or replayed.");
            return process.ExitCode == 0 ? new(true, "Usb.Succeeded") : new(false, "Usb.OperationFailed", null, "DN-8011: The signed USB helper rejected the operation.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new(false, "Usb.ElevationDeclined", null, "DN-8010: Administrator approval was declined for the USB operation.");
        }
        }
    }

    /// <summary>Allows exactly the initiating account. Elevation retains the user's SID; allowing the
    /// Administrators group would let another administrator race the one-shot server.</summary>
    private static NamedPipeServerStream CreateAuthorizationPipe(string pipeName, string callerIdentity)
    {
        var caller = new SecurityIdentifier(callerIdentity);
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(caller, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous, 0, 0, security);
    }

    private static async Task<bool> AuthorizeHelperAsync(NamedPipeServerStream pipe, UsbElevatedOperationRequest expected,
        IUsbElevatedRequestIssuer issuer, string callerIdentity, int helperProcessId, string clientNonce, CancellationToken cancellationToken)
    {
        try
        {
            await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            if (!IsConnectedPeerAuthorized(pipe, callerIdentity)) return false;
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            var hello = JsonSerializer.Deserialize<UsbElevatedHelperHello>(await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty);
            if (!IsHelperHelloAuthorized(hello, helperProcessId, clientNonce)) return false;
            var serverNonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await writer.WriteLineAsync(JsonSerializer.Serialize(new UsbElevatedHelperChallenge(serverNonce))).ConfigureAwait(false);
            var proof = JsonSerializer.Deserialize<UsbElevatedHelperProof>(await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty);
            var accepted = proof is not null && proof.ProcessId == helperProcessId && proof.PreviewToken == expected.PreviewToken &&
                CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(proof.ClientNonce ?? string.Empty), Encoding.UTF8.GetBytes(clientNonce)) &&
                CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(proof.ServerNonce ?? string.Empty), Encoding.UTF8.GetBytes(serverNonce)) && issuer.Consume(expected, callerIdentity);
            await writer.WriteLineAsync(accepted ? "authorized" : "rejected").ConfigureAwait(false);
            return accepted;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { return false; }
        catch { return false; }
    }

    public static bool IsPeerSidAuthorizedForGrant(string? peerSid, string callerIdentity, bool peerIsAdministrator) =>
        !string.IsNullOrWhiteSpace(peerSid) &&
        string.Equals(peerSid, callerIdentity, StringComparison.OrdinalIgnoreCase);

    /// <summary>Pure validation seam: a hostile same-user client cannot claim the UAC-launched helper
    /// because its process identifier and fresh launch nonce must both match.</summary>
    public static bool IsHelperHelloAuthorized(UsbElevatedHelperHello? hello, int helperProcessId, string clientNonce) =>
        hello is { ProtocolVersion: 3 } && hello.ProcessId == helperProcessId &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hello.ClientNonce ?? string.Empty), Encoding.UTF8.GetBytes(clientNonce));

    private static bool IsConnectedPeerAuthorized(NamedPipeServerStream pipe, string callerIdentity)
    {
        try
        {
            string? peerSid = null;
            var administrator = false;
            pipe.RunAsClient(() =>
            {
                using var identity = WindowsIdentity.GetCurrent();
                peerSid = identity.User?.Value;
                administrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            });
            return IsPeerSidAuthorizedForGrant(peerSid, callerIdentity, administrator);
        }
        catch { return false; }
    }
}

/// <summary>Optional Windows device-notification bridge. It is active only while the Devices page is visible.</summary>
public sealed class UsbDeviceChangeWatcher : IUsbDeviceChangeWatcher
{
    private readonly System.Timers.Timer _debounce = new(500) { AutoReset = false };
    private ManagementEventWatcher? _watcher;
    private bool _disposed;
    public event EventHandler? DevicesChanged;
    public UsbDeviceChangeWatcher() => _debounce.Elapsed += (_, _) => DevicesChanged?.Invoke(this, EventArgs.Empty);
    public void Start()
    {
        if (_disposed) return;
        if (_watcher is not null) return;
        try
        {
            _watcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM __InstanceOperationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_PnPEntity'"));
            _watcher.EventArrived += (_, _) => { _debounce.Stop(); _debounce.Start(); };
            _watcher.Start();
        }
        catch { _watcher?.Dispose(); _watcher = null; }
    }
    public void Stop() { if (_disposed) return; _debounce.Stop(); if (_watcher is null) return; try { _watcher.Stop(); } catch { } _watcher.Dispose(); _watcher = null; }
    public void Dispose() { if (_disposed) return; Stop(); _disposed = true; _debounce.Dispose(); }
}
