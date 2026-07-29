using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>Reusable presentation state for a discoverable capability-gated page, tab, or action.</summary>
public partial class CapabilityAvailabilityViewModel : ObservableObject
{
    private readonly Func<string, string> _localize;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isPendingRestart;
    [ObservableProperty] private string _currentState = string.Empty;
    [ObservableProperty] private string _desiredState = string.Empty;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _safeNextAction = string.Empty;
    [ObservableProperty] private DateTimeOffset _refreshedAt;

    public CapabilityAvailabilityViewModel(Func<string, string>? localize = null)
    {
        _localize = localize ?? Localize;
    }

    public void Apply(CapabilityResult result, string desiredStateKey = "Capability.State.Available")
    {
        ArgumentNullException.ThrowIfNull(result);
        IsLoading = false;
        IsEnabled = result.Status == CapabilityStatus.Supported;
        IsPendingRestart = false;
        CurrentState = _localize("Capability.Status." + result.Status);
        DesiredState = _localize(desiredStateKey);
        var localizedReason = _localize(result.ReasonCode);
        Reason = localizedReason == result.ReasonCode
            ? _localize("Capability.Reason." + result.Status)
            : localizedReason;
        SafeNextAction = _localize(SafeActionKey(result.Status));
        RefreshedAt = result.CheckedAt;
    }

    public void MarkPendingRestart(string currentStateKey, string desiredStateKey)
    {
        IsPendingRestart = true;
        CurrentState = _localize(currentStateKey);
        DesiredState = _localize(desiredStateKey);
    }

    private static string SafeActionKey(CapabilityStatus status) => status switch
    {
        CapabilityStatus.RequiresElevation => "Capability.Action.RunElevated",
        CapabilityStatus.RequiresUpdate => "Capability.Action.UpdateWsl",
        CapabilityStatus.Unavailable => "Capability.Action.InstallPrerequisite",
        CapabilityStatus.Unsupported => "Capability.Action.ReviewRequirements",
        CapabilityStatus.Unknown => "Capability.Action.RetryProbe",
        _ => "Capability.Action.None"
    };

    private static string Localize(string key) => Properties.Resources.ResourceManager.GetString(key) ?? key;
}
