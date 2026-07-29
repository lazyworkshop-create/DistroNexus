namespace DistroNexus.Core.Models;

/// <summary>Sanitized review result for a credential mutation.</summary>
public sealed record CredentialOperationPreview(string PreviewToken, string InstanceName, DateTimeOffset ExpiresAt);
public sealed record CredentialOperationResult(bool Succeeded, string InstanceName, string OutcomeCode);

internal sealed record CredentialOperationGrant(string Sid, string InstanceName, string Username, string SecretEnvelope, string EnvelopeIdentity, string InstanceFingerprint, DateTimeOffset ExpiresAt);
