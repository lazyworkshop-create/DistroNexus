using System.Globalization;
using System.Text.RegularExpressions;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

public static partial class WslConfigurationSchema
{
    public static IReadOnlyList<WslSettingDefinition> Global { get; } =
    [
        D("memory", ConfigurationValueType.MemorySize), D("processors", ConfigurationValueType.Integer, min: 1),
        D("swap", ConfigurationValueType.MemorySize), D("swapFile", ConfigurationValueType.Path),
        D("pageReporting", ConfigurationValueType.Boolean), D("localhostForwarding", ConfigurationValueType.Boolean),
        D("networkingMode", ConfigurationValueType.Enum, ["nat", "mirrored", "virtioproxy", "none"]),
        D("dnsTunneling", ConfigurationValueType.Boolean, capability: "wsl.config.dnsTunneling"),
        D("firewall", ConfigurationValueType.Boolean, capability: "wsl.config.firewall"),
        D("autoProxy", ConfigurationValueType.Boolean, capability: "wsl.config.autoProxy"),
        D("hostAddressLoopback", ConfigurationValueType.Boolean, capability: "wsl.config.hostAddressLoopback"),
        D("ignoredPorts", ConfigurationValueType.PortList, capability: "wsl.config.ignoredPorts"),
        D("bestEffortDnsParsing", ConfigurationValueType.Boolean, capability: "wsl.config.bestEffortDnsParsing"),
        D("initialAutoProxyTimeout", ConfigurationValueType.Integer, min: 0, capability: "wsl.config.proxyTimeout"),
        D("kernel", ConfigurationValueType.Path), D("kernelCommandLine", ConfigurationValueType.String),
        D("nestedVirtualization", ConfigurationValueType.Boolean),
        D("autoMemoryReclaim", ConfigurationValueType.Enum, ["disabled", "gradual", "dropcache"], "experimental.autoMemoryReclaim", true, "experimental"),
        D("sparseVhd", ConfigurationValueType.Boolean, capability: "experimental.sparseVhd", experimental: true, section: "experimental")
    ];

    public static IReadOnlyList<WslSettingDefinition> Distribution { get; } =
    [
        X("boot", "command", ConfigurationValueType.String, RestartScope.Instance),
        X("boot", "systemd", ConfigurationValueType.Boolean, RestartScope.Instance),
        X("user", "default", ConfigurationValueType.String, RestartScope.Instance),
        X("automount", "enabled", ConfigurationValueType.Boolean, RestartScope.Instance),
        X("automount", "root", ConfigurationValueType.Path, RestartScope.Instance),
        X("automount", "options", ConfigurationValueType.String, RestartScope.Instance),
        X("automount", "mountFsTab", ConfigurationValueType.Boolean, RestartScope.Instance),
        X("automount", "metadata", ConfigurationValueType.Boolean, RestartScope.Instance),
        X("automount", "umask", ConfigurationValueType.String, RestartScope.Instance),
        X("automount", "case", ConfigurationValueType.Enum, RestartScope.Instance, ["off", "dir", "force"]),
        X("interop", "enabled", ConfigurationValueType.Boolean, RestartScope.Instance),
        X("interop", "appendWindowsPath", ConfigurationValueType.Boolean, RestartScope.Instance),
        X("network", "hostname", ConfigurationValueType.String, RestartScope.Instance),
        X("network", "generateHosts", ConfigurationValueType.Boolean, RestartScope.Instance),
        X("network", "generateResolvConf", ConfigurationValueType.Boolean, RestartScope.Instance)
    ];

    private static WslSettingDefinition D(string key, ConfigurationValueType type, string[]? allowed = null,
        string? capability = null, bool experimental = false, string section = "wsl2", int? min = null) =>
        new(section, key, type, RestartScope.Wsl, allowed?.ToHashSet(StringComparer.OrdinalIgnoreCase), min, null, capability, experimental);
    private static WslSettingDefinition X(string section, string key, ConfigurationValueType type, RestartScope scope,
        string[]? allowed = null) => new(section, key, type, scope, allowed?.ToHashSet(StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<ConfigurationDiagnostic> Validate(LosslessIniDocument document,
        IReadOnlyList<WslSettingDefinition> schema, IReadOnlySet<string>? capabilities = null)
    {
        var result = new List<ConfigurationDiagnostic>();
        foreach (var token in document.Tokens)
        {
            if (token.Kind == ConfigurationTokenKind.Malformed)
            { result.Add(new(token.Line, "config.malformed", "Expected a section, comment, or key=value record.")); continue; }
            if (token.Kind != ConfigurationTokenKind.KeyValue) continue;
            var definition = schema.LastOrDefault(d => Eq(d.Section, token.Section) && Eq(d.Key, token.Key));
            if (definition is null) continue;
            if (definition.RequiredCapability is not null && capabilities is not null && !capabilities.Contains(definition.RequiredCapability))
            { result.Add(new(token.Line, "config.unsupported", $"{token.Section}.{token.Key} is not supported by this WSL version.")); continue; }
            if (Eq(token.Section, "wsl2") && Eq(token.Key, "networkingMode") &&
                Eq(token.Value, "mirrored") && capabilities is not null && !capabilities.Contains("wsl.config.mirroredNetworking"))
            { result.Add(new(token.Line, "config.unsupported", "wsl2.networkingMode=mirrored is not supported by this WSL version.")); continue; }
            if (!Valid(token.Value ?? string.Empty, definition))
                result.Add(new(token.Line, "config.invalidValue", $"Invalid value for {token.Section}.{token.Key}."));
        }
        return result;
    }

    private static bool Valid(string value, WslSettingDefinition d) => d.Type switch
    {
        ConfigurationValueType.Boolean => bool.TryParse(value, out _),
        ConfigurationValueType.Integer => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var i) &&
            (d.Minimum is null || i >= d.Minimum) && (d.Maximum is null || i <= d.Maximum),
        ConfigurationValueType.MemorySize => MemoryRegex().IsMatch(value),
        ConfigurationValueType.Enum => d.AllowedValues?.Contains(value) == true,
        ConfigurationValueType.Path => value.Length > 0 && !value.Contains('\0') && !value.Contains('\n') && !value.Contains('\r'),
        ConfigurationValueType.PortList => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .All(p => int.TryParse(p, out var port) && port is >= 1 and <= 65535),
        _ => !value.Contains('\0') && !value.Contains('\n') && !value.Contains('\r')
    };
    private static bool Eq(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlySet<string> MapCapabilities(PlatformCapabilitySnapshot snapshot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappings = new Dictionary<string, CapabilityId>(StringComparer.OrdinalIgnoreCase)
        {
            ["wsl.config.mirroredNetworking"] = CapabilityId.MirroredNetworking,
            ["experimental.sparseVhd"] = CapabilityId.SparseVhd,
            ["wsl.config.dnsTunneling"] = CapabilityId.ConfigDnsTunneling,
            ["wsl.config.firewall"] = CapabilityId.ConfigFirewall,
            ["wsl.config.autoProxy"] = CapabilityId.ConfigAutoProxy,
            ["wsl.config.hostAddressLoopback"] = CapabilityId.ConfigHostAddressLoopback,
            ["wsl.config.ignoredPorts"] = CapabilityId.ConfigIgnoredPorts,
            ["wsl.config.bestEffortDnsParsing"] = CapabilityId.ConfigBestEffortDnsParsing,
            ["wsl.config.proxyTimeout"] = CapabilityId.ConfigProxyTimeout,
            ["experimental.autoMemoryReclaim"] = CapabilityId.ConfigAutoMemoryReclaim
        };
        foreach (var (name, id) in mappings)
            if (snapshot.Capabilities.TryGetValue(id, out var capability) && capability.IsSupported)
                result.Add(name);
        return result;
    }
    [GeneratedRegex(@"^(0|[1-9]\d*(?:KB|MB|GB|TB)?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MemoryRegex();
}
