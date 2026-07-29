using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.ViewModels;

public partial class ConfigurationSettingFieldViewModel(WslSettingDefinition definition, string? current,
    bool isSupported, string unsupportedReason, string experimentalBadge = "Experimental") : ObservableObject
{
    public WslSettingDefinition Definition { get; } = definition;
    public string Id => $"{Definition.Section}.{Definition.Key}";
    public string Label => Definition.Experimental ? $"{Definition.Key} ({experimentalBadge})" : Definition.Key;
    /// <summary>The last value durably saved (or loaded) from the document.</summary>
    public string? Current { get; private set; } = current;
    [ObservableProperty] private string? _desired = current;
    public bool IsSupported { get; } = isSupported;
    public string UnsupportedReason { get; } = unsupportedReason;
    [ObservableProperty] private string _validationError = string.Empty;
    public bool IsDirty => !string.Equals(Current, Desired, StringComparison.Ordinal);
    partial void OnDesiredChanged(string? value) => OnPropertyChanged(nameof(IsDirty));

    public void CommitDesired()
    {
        Current = Desired;
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(IsDirty));
    }
}
