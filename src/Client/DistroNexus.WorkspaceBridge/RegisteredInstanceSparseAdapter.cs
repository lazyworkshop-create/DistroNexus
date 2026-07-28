using Microsoft.Win32;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.WorkspaceBridge;

/// <summary>Bridge-local implementation of the deliberately narrow registered-instance sparse boundary.</summary>
public sealed class RegisteredInstanceSparseAdapter : IRegisteredInstanceSparseAdapter
{
    private readonly IProcessRunner _processes;
    private readonly Func<string, RegisteredInstanceSparseState?> _registeredState;
    public RegisteredInstanceSparseAdapter(IProcessRunner processes) : this(processes, ReadRegisteredState) { }
    public RegisteredInstanceSparseAdapter(IProcessRunner processes, Func<string, RegisteredInstanceSparseState?> registeredState)
    { _processes = processes; _registeredState = registeredState; }

    public Task<RegisteredInstanceSparseState?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['\r', '\n', '\0']) >= 0) return Task.FromResult<RegisteredInstanceSparseState?>(null);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_registeredState(name));
    }

    private static RegisteredInstanceSparseState? ReadRegisteredState(string name)
    {
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var lxss = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
        if (lxss is null) return null;
        foreach (var keyName in lxss.GetSubKeyNames())
        {
            using var key = lxss.OpenSubKey(keyName);
            if (key is null || !string.Equals(key.GetValue("DistributionName") as string, name, StringComparison.Ordinal)) continue;
            if (key.GetValue("Version") is not int version) return null;
            var sparse = key.GetValue("SparseVhd") is not null and not 0;
            return new(name, keyName, version, sparse);
        }
        return null;
    }

    public async Task<bool> SetSparseAsync(string registeredName, bool enabled, CancellationToken cancellationToken = default)
    {
        var value = enabled ? "true" : "false";
        var result = await _processes.RunAsync(new ProcessRequest("wsl.exe", ["--manage", registeredName, "--set-sparse", value], TimeSpan.FromSeconds(30)), cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 && !result.TimedOut && !result.Cancelled && result.Failure == ProcessFailureKind.None;
    }

}
