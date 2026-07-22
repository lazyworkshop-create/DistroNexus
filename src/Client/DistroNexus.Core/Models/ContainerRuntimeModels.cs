namespace DistroNexus.Core.Models;
public static class VersionSafety
{
    public static string? Normalize(string? value)
    {
        var candidate = value?.Trim();
        return candidate is { Length: > 0 and <= 64 } && System.Text.RegularExpressions.Regex.IsMatch(candidate, "^[0-9A-Za-z.+_-]+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant) ? candidate : null;
    }
}
public static class RuntimeEndpointSafety
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment)) return null;
        if (endpoint.Scheme == "unix" && endpoint.IsAbsoluteUri && endpoint.AbsolutePath.StartsWith("/", StringComparison.Ordinal))
            return endpoint.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
        if (endpoint.Scheme is "tcp" or "http" or "https" && endpoint.IsLoopback && endpoint.Port is > 0 and <= 65535)
            return endpoint.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
        return null;
    }
}
public enum ContainerRuntimeKind { DockerDesktop, PodmanWsl, PodmanDesktop }
public enum ContainerRuntimeAvailability { Available, Unavailable, Degraded }
public sealed record ContainerRuntimeStatus
{
    public ContainerRuntimeStatus(ContainerRuntimeKind kind, ContainerRuntimeAvailability availability, string? version, string? endpoint, string serviceState, string health, string detail)
    {
        Kind = kind;
        Availability = availability;
        Version = VersionSafety.Normalize(version);
        Endpoint = RuntimeEndpointSafety.Normalize(endpoint);
        ServiceState = serviceState;
        Health = health;
        Detail = detail;
    }
    public ContainerRuntimeKind Kind { get; init; }
    public ContainerRuntimeAvailability Availability { get; init; }
    public string? Version { get; init; }
    public string? Endpoint { get; init { field = RuntimeEndpointSafety.Normalize(value); } }
    public string ServiceState { get; init; }
    public string Health { get; init; }
    public string Detail { get; init; }
}
public sealed record ContainerSummary(string Id, string Name, string Image, string State, string? Ports);
public sealed record ImageSummary(string Id, string Repository, string Tag, string? Size);
public sealed record ComposeProjectSummary(string Name, string Status, int ServiceCount);
public sealed record ContainerRuntimeSnapshot(IReadOnlyList<ContainerRuntimeStatus> Runtimes, IReadOnlyDictionary<ContainerRuntimeKind, IReadOnlyList<ContainerSummary>> Containers, IReadOnlyDictionary<ContainerRuntimeKind, IReadOnlyList<ImageSummary>> Images, IReadOnlyDictionary<ContainerRuntimeKind, IReadOnlyList<ComposeProjectSummary>> Projects, IReadOnlyDictionary<ContainerRuntimeKind, string> Failures);
public enum PodmanUserUnit { Service, Socket }
/// <summary>Locale-neutral preview text. Hosts render the code using their own resources.</summary>
public sealed record PodmanPreviewMessage(string Code, IReadOnlyList<string> Parameters)
{
    // Compatibility helper for callers that only classify the stable effect identifier.
    public bool StartsWith(string value, StringComparison comparison)
    {
        var compatibilityText = Code == "PodmanConnectionChange" && Parameters.Count >= 3
            ? $"{Parameters[0]} Podman connection '{Parameters[1]}' for {Parameters[^1]}."
            : string.Join(" ", Parameters);
        return Code.StartsWith(value, comparison) || Parameters.Any(p => p.StartsWith(value, comparison)) || compatibilityText.StartsWith(value, comparison);
    }
}
public sealed record PodmanServicePreview(SystemdOperationPreview SystemdPreview, PodmanUserUnit Unit, SystemdAction Action, IReadOnlyList<PodmanPreviewMessage>? Effects = null, IReadOnlyList<PodmanPreviewMessage>? Preconditions = null);
/// <summary>Only local Unix sockets or loopback TCP endpoints are accepted; credentials are never persisted by DistroNexus.</summary>
public sealed record PodmanConnectionRequest(string Name, Uri Endpoint)
{
    public string SafeEndpoint => Endpoint.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 63 || !System.Text.RegularExpressions.Regex.IsMatch(Name, "^[A-Za-z0-9][A-Za-z0-9_.-]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) throw new ArgumentException("Connection name is invalid.", nameof(Name));
        if (!string.IsNullOrEmpty(Endpoint.UserInfo) || !string.IsNullOrEmpty(Endpoint.Query) || !string.IsNullOrEmpty(Endpoint.Fragment)) throw new ArgumentException("Podman connection endpoints cannot contain credentials, query values, or fragments.", nameof(Endpoint));
        if (Endpoint.Scheme == "unix" && Endpoint.AbsolutePath.StartsWith("/run/user/", StringComparison.Ordinal) && Endpoint.AbsolutePath.EndsWith("/podman/podman.sock", StringComparison.Ordinal)) return;
        if (Endpoint.Scheme is "tcp" or "http" && Endpoint.IsLoopback && Endpoint.Port is > 0 and <= 65535) return;
        throw new ArgumentException("Only a local Podman Unix socket or loopback TCP endpoint is permitted.", nameof(Endpoint));
    }
}
public sealed record PodmanConnectionPreview(string InstanceName, PodmanConnectionRequest Request, string Operation, string? ExistingEndpoint, IReadOnlyList<PodmanPreviewMessage> Effects, IReadOnlyList<PodmanPreviewMessage> Preconditions, string Token);
public sealed record PodmanConnectionResult(bool Succeeded, string OutcomeCode, string? Endpoint = null, string? Guidance = null);
