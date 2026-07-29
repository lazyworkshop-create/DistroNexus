using System.Text.RegularExpressions;

namespace DistroNexus.Core.Models;

/// <summary>A validated usbipd bus identifier. It is never treated as command text.</summary>
public sealed record UsbBusId
{
    private static readonly Regex Pattern = new("^[0-9A-Fa-f]{1,3}-[0-9A-Fa-f]{1,3}$", RegexOptions.CultureInvariant);
    public string Value { get; }
    public UsbBusId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Pattern.IsMatch(value)) throw new ArgumentException("USB bus ID is invalid.", nameof(value));
        Value = value.ToUpperInvariant();
    }
    public override string ToString() => Value;
}

public enum UsbDeviceAvailability { Available, Shared, Attached, NotConnected, Unsupported, Unknown }
/// <summary>Closed, display-safe USB status returned by the broker-free module read contract.</summary>
public sealed record UsbStatusResult(bool IsInstalled, string ServiceState, string? Version, bool SupportsActions, string? Reason, string OutcomeCode);
/// <summary>Closed, display-safe USB device returned by the broker-free module read contract.</summary>
public sealed record UsbDeviceResult(string BusId, string Description, string Availability, bool SharedState, bool AttachedState,
    bool IsStorage, string? Distribution, string? Guidance);
public sealed record UsbDeviceListResult(IReadOnlyList<UsbDeviceResult> Devices, string OutcomeCode);
public enum UsbDeviceAction { Bind, Unbind, Attach, Detach }
public sealed record UsbDeviceInfo(UsbBusId BusId, string HardwareId, string Description, UsbDeviceAvailability Availability,
    bool IsShared, bool IsAttached, bool IsStorageClass, string? AttachedDistribution = null, string? GuidanceCode = null);
public sealed record UsbIpdStatus(bool IsInstalled, bool IsServiceRunning, Version? Version, bool SupportsMutation, string ReasonCode,
    string? ParserProfile = null, string? RawVersionDiagnostic = null);
/// <summary>The hardware identity is captured with the bus ID so a reused bus cannot satisfy a stale preview.</summary>
public sealed record UsbDeviceActionPreview(Guid Token, UsbDeviceAction Action, UsbBusId BusId, string HardwareId, string? DistributionName,
    bool RequiresElevation, bool RequiresConfirmation, IReadOnlyList<string> Effects, IReadOnlyList<string> Warnings,
    DateTimeOffset? ExpiresAt = null);
/// <summary>Opaque, short-lived request accepted only by a product-owned signed helper.</summary>
public sealed record UsbElevatedOperationRequest(Guid PreviewToken, UsbDeviceAction Action, UsbBusId BusId, string HardwareId, DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt, string IssuerId, string CallerIdentity);
/// <summary>Versioned, fixed-shape launch payload. It contains no executable path, command line, or arbitrary arguments.</summary>
public sealed record UsbElevatedHelperLaunchEnvelope(int ProtocolVersion, string PipeName, UsbElevatedOperationRequest Request,
    string ClientNonce, int DesktopProcessId);
/// <summary>Fixed mutual-authentication messages for the one-shot elevated pipe.</summary>
public sealed record UsbElevatedHelperHello(int ProtocolVersion, int ProcessId, string ClientNonce);
public sealed record UsbElevatedHelperChallenge(string ServerNonce);
public sealed record UsbElevatedHelperProof(string ClientNonce, string ServerNonce, Guid PreviewToken, int ProcessId);
/// <summary>Safe diagnostic data for UI and PowerShell. It deliberately excludes usbipd process output.</summary>
public sealed record UsbDiagnostic(string Code, string Message);
public sealed record UsbDeviceActionResult(bool Succeeded, string OutcomeCode, UsbDeviceInfo? Device = null, string? Guidance = null,
    UsbDiagnostic? Diagnostic = null);
/// <summary>Bounded product operation phase, suitable for cancellation/progress UI.</summary>
public sealed record UsbOperationProgress(string PhaseCode, int Percent);
public enum UsbAttachmentVerification { Present, ToolUnavailable, NotPresent, Failed }
public sealed record UsbAttachmentVerificationResult(UsbAttachmentVerification Outcome, string Detail);
