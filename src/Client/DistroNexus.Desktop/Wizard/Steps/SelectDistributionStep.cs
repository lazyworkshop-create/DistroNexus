using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 1: Select a distribution to install.
/// </summary>
public partial class SelectDistributionStep : WizardStepBase
{
    private readonly ICatalogService _catalogService;
    private readonly ILogger _logger;

    public override string StepId => "select-distribution";
    public override string Title => Properties.Resources.WizardStepSelectDistribution;
    public override string Description => "Choose a Linux distribution to install";

    [ObservableProperty]
    private ObservableCollection<DistroPackage> _availableDistributions = [];

    [ObservableProperty]
    private bool _isLoading;

    public SelectDistributionStep(ICatalogService catalogService, ILogger logger)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override UserControl CreateContent()
    {
        return new SelectDistributionStepView { DataContext = this };
    }

    public override async Task OnEnterAsync()
    {
        if (AvailableDistributions.Count == 0)
        {
            await LoadDistributionsAsync();
        }
    }

    private async Task LoadDistributionsAsync()
    {
        try
        {
            IsLoading = true;
            _logger.LogInformation("Loading available distributions");

            var packages = await _catalogService.LoadCatalogAsync();
            AvailableDistributions.Clear();
            
            foreach (var package in packages)
            {
                AvailableDistributions.Add(package);
            }

            _logger.LogInformation("Loaded {Count} distributions", AvailableDistributions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load distributions");
            ErrorMessage = $"Failed to load distributions: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override bool Validate()
    {
        if (Context?.SelectedDistribution == null)
        {
            ErrorMessage = Properties.Resources.ErrorSelectDistribution;
            return false;
        }

        // Validate that the distribution has a download URL
        if (string.IsNullOrWhiteSpace(Context.SelectedDistribution.DownloadUrl))
        {
            ErrorMessage = Properties.Resources.ErrorInvalidDownloadUrl;
            return false;
        }

        // Validate URL format
        if (!Uri.TryCreate(Context.SelectedDistribution.DownloadUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ErrorMessage = Properties.Resources.ErrorInvalidUrlFormat;
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }

    public override Task OnExitAsync()
    {
        // Auto-generate instance name if not set
        if (Context != null && string.IsNullOrEmpty(Context.InstanceName) && Context.SelectedDistribution != null)
        {
            Context.InstanceName = Context.SelectedDistribution.Id;
        }
        
        return Task.CompletedTask;
    }
}
