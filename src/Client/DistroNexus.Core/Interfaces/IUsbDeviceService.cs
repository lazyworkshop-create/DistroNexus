using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface IUsbIpdAdapter
{
    Task<UsbIpdStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UsbDeviceInfo>> ListAsync(UsbIpdStatus status, CancellationToken cancellationToken = default);
    Task<UsbDeviceActionResult> ExecuteUnelevatedAsync(UsbDeviceAction action, UsbBusId busId, string? distributionName, CancellationToken cancellationToken = default);
    Task<UsbAttachmentVerificationResult> VerifyAttachmentAsync(UsbDeviceInfo device, string distributionName, CancellationToken cancellationToken = default);
}

/// <summary>Minimal elevation boundary: bind and unbind only, no caller-provided executable or arguments.</summary>
public interface IUsbElevatedOperationBroker
{
    /// <summary>Executes a fixed allow-listed operation through the product-owned elevated helper.</summary>
    Task<UsbDeviceActionResult> ExecuteAsync(UsbElevatedOperationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Issues one-time grants shared by the desktop and its fixed elevated helper boundary.</summary>
public interface IUsbElevatedRequestIssuer
{
    UsbElevatedOperationRequest Issue(UsbDeviceActionPreview preview, string callerIdentity);
    bool IsCurrent(UsbElevatedOperationRequest request, string callerIdentity);
    bool Consume(UsbElevatedOperationRequest request, string callerIdentity);
}

public interface IUsbCallerIdentityProvider { string GetCallerIdentity(); }
public interface IUsbElevatedHelperLauncher
{
    Task<UsbDeviceActionResult> LaunchAsync(UsbElevatedOperationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Optional production protocol: the helper must obtain a one-time authorization over a pipe before acting.</summary>
public interface IUsbElevatedAuthenticatedHelperLauncher : IUsbElevatedHelperLauncher
{
    Task<UsbDeviceActionResult> LaunchAuthorizedAsync(UsbElevatedOperationRequest request, IUsbElevatedRequestIssuer issuer,
        string callerIdentity, CancellationToken cancellationToken = default);
}

public interface IUsbDeviceService
{
    Task<UsbIpdStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UsbDeviceInfo>> ListAsync(CancellationToken cancellationToken = default);
    Task<UsbDeviceActionPreview> PreviewAsync(UsbDeviceAction action, UsbBusId busId, string? distributionName = null, CancellationToken cancellationToken = default);
    /// <summary>Runs a confirmed operation and reports only product-defined phases; no tool output crosses this boundary.</summary>
    Task<UsbDeviceActionResult> ExecuteAsync(UsbDeviceActionPreview preview, IProgress<UsbOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IUsbDeviceChangeWatcher : IDisposable
{
    event EventHandler? DevicesChanged;
    void Start();
    void Stop();
}
