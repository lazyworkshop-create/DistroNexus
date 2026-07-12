using System.Text.RegularExpressions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Replays only a tiny, read-only template contract language.  Template scripts are
/// never replayed by a background health scan.</summary>
public sealed class TemplateRuntimePreflightEvaluator : ITemplateRuntimePreflightEvaluator
{
    private static readonly Regex Safe = new(@"^test\s+-[efdrx]\s+/[A-Za-z0-9._/+:-]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private readonly IProcessRunner _runner;
    public TemplateRuntimePreflightEvaluator(IProcessRunner runner) => _runner = runner;

    public async Task<IReadOnlyList<TemplateRuntimePreflightResult>> EvaluateAsync(TemplateApplicationRecord record, CancellationToken cancellationToken = default)
    {
        var contracts = record.DeclaredHealthSnapshot?.RuntimePreflightContracts ?? [];
        var observed = new List<TemplateRuntimePreflightResult>(contracts.Count);
        foreach (var contract in contracts)
        {
            if (!IsSafeCommand(contract.Command))
            {
                observed.Add(new TemplateRuntimePreflightResult(contract.Id, contract.Required, "unavailable", "The recorded preflight contract is not in the safe read-only health language."));
                continue;
            }
            var result = await _runner.RunAsync(new ProcessRequest("wsl.exe", ["--distribution", record.InstanceName, "--", "sh", "-lc", contract.Command], TimeSpan.FromSeconds(15), 1024, 1024), cancellationToken).ConfigureAwait(false);
            var state = result.ExitCode == 0 && !result.TimedOut && !result.Cancelled && result.Failure == ProcessFailureKind.None ? "healthy"
                : result.ExitCode is not null && !result.TimedOut && !result.Cancelled && result.Failure == ProcessFailureKind.None ? "failed" : "unavailable";
            observed.Add(new TemplateRuntimePreflightResult(contract.Id, contract.Required, state,
                state == "healthy" ? "Recorded read-only template preflight succeeded." : state == "failed" ? "Recorded template preflight did not pass." : "Recorded template preflight could not be observed."));
        }
        return observed;
    }

    public static bool IsSafeCommand(string? command) => !string.IsNullOrWhiteSpace(command) && Safe.IsMatch(command.Trim());
}

public sealed class NoLocalhostForwardingEndpointStrategy : ILocalhostForwardingEndpointStrategy
{
    public HealthTcpEndpoint? GetEndpoint(HealthCheckContext context, string networkingMode) => null;
}

/// <summary>Reads the opt-in Health Center forwarding endpoint from the application's typed
/// settings. Invalid values are intentionally surfaced as invalid endpoints rather than being
/// coerced to an arbitrary port.</summary>
public sealed class SettingsLocalhostForwardingEndpointStrategy : ILocalhostForwardingEndpointStrategy
{
    private readonly ISettingsService _settings;
    public SettingsLocalhostForwardingEndpointStrategy(ISettingsService settings) => _settings = settings;

    public HealthTcpEndpoint? GetEndpoint(HealthCheckContext context, string networkingMode)
    {
        var value = _settings.LoadSettings().LocalhostForwardingHealthEndpoint?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;

        // Accept only a strict host:port form. IPv6 must use brackets so the split cannot be
        // ambiguous. The adapter then enforces the loopback allow-list before connecting.
        if (value.StartsWith("[", StringComparison.Ordinal))
        {
            var closing = value.IndexOf(']');
            if (closing <= 1 || closing + 2 >= value.Length || value[closing + 1] != ':' || !int.TryParse(value[(closing + 2)..], out var ipv6Port))
                return new HealthTcpEndpoint("invalid", 0);
            return new HealthTcpEndpoint(value[1..closing], ipv6Port);
        }
        var separator = value.LastIndexOf(':');
        if (separator <= 0 || separator == value.Length - 1 || !int.TryParse(value[(separator + 1)..], out var port))
            return new HealthTcpEndpoint("invalid", 0);
        return new HealthTcpEndpoint(value[..separator], port);
    }
}
