using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>A discoverable tab descriptor whose content/action is capability-gated.</summary>
public sealed partial class CapabilityTabItemViewModel : ObservableObject
{
    public string Id { get; }
    public string Header { get; }
    public bool IsDiscoverable { get; }
    public CapabilityAvailabilityViewModel Availability { get; }

    public bool IsEnabled => Availability.IsEnabled;
    public string UnavailableExplanation => string.Join(Environment.NewLine,
        new[] { Availability.Reason, Availability.SafeNextAction }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public CapabilityTabItemViewModel(string id, string header, bool isDiscoverable = true,
        Func<string, string>? localize = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        Id = id;
        Header = header;
        IsDiscoverable = isDiscoverable;
        Availability = new CapabilityAvailabilityViewModel(localize);
        Availability.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(CapabilityAvailabilityViewModel.IsEnabled)) OnPropertyChanged(nameof(IsEnabled));
            if (args.PropertyName is nameof(CapabilityAvailabilityViewModel.Reason) or nameof(CapabilityAvailabilityViewModel.SafeNextAction))
                OnPropertyChanged(nameof(UnavailableExplanation));
        };
    }

    public void Apply(CapabilityResult result) => Availability.Apply(result);
}
