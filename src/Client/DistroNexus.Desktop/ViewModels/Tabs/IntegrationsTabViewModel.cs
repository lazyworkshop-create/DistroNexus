using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Interfaces;

namespace DistroNexus.Desktop.ViewModels.Tabs;

/// <summary>
/// ViewModel for the Integrations tab of InstanceDetailDialog.
/// Handles Docker Desktop integration status and toggle.
/// </summary>
public partial class IntegrationsTabViewModel : ObservableObject
{
    private readonly WslInstanceViewModel _instance;
    private readonly IDockerIntegrationService _dockerIntegrationService;
    private readonly IDialogService _dialogService;

    private bool _initialized;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public WslInstanceViewModel Instance => _instance;

    public IntegrationsTabViewModel(
        WslInstanceViewModel instance,
        IDockerIntegrationService dockerIntegrationService,
        IDialogService dialogService)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _dockerIntegrationService = dockerIntegrationService ?? throw new ArgumentNullException(nameof(dockerIntegrationService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        _initialized = true;
        // Integrations tab initialization will be implemented in Phase 4
        return Task.CompletedTask;
    }
}
