namespace DistroNexus.Core.Models;

public sealed record ProductLogRevealTarget(Uri? RevealUri, string OutcomeCode);
public sealed record ExternalLaunchTarget(Uri Uri, string OutcomeCode);
