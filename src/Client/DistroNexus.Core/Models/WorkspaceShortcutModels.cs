namespace DistroNexus.Core.Models;

/// <summary>Closed request for a desktop shortcut to a persisted workspace.</summary>
public sealed record WorkspaceShortcutRequest(Guid WorkspaceId);

/// <summary>Bounded public result; it intentionally never exposes a filesystem path.</summary>
public sealed record WorkspaceShortcutResult(string OutcomeCode);
