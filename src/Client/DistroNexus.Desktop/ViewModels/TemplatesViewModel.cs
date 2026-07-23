using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels;

public partial class TemplatesViewModel : ObservableObject
{
    private readonly ITemplateService _templateService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<TemplatesViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITemplateMarketplaceService _marketplaceService;
    private readonly IDialogService _dialogService;
    private List<Template> _allTemplates = new();

    [ObservableProperty]
    private ObservableCollection<Template> _templates = new();

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _selectedCategory = "All";

    partial void OnSelectedCategoryChanged(string value)
    {
        RebuildScenarioTagOptions();
        FilterTemplates();
    }

    [ObservableProperty]
    private string _selectedScenarioTag = "All";

    partial void OnSelectedScenarioTagChanged(string value)
    {
        FilterTemplates();
    }

    [ObservableProperty]
    private ObservableCollection<string> _categoryOptions = new(["All"]);

    [ObservableProperty]
    private ObservableCollection<string> _scenarioTagOptions = new(["All"]);

    partial void OnSearchQueryChanged(string value)
    {
        FilterTemplates();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportTemplateCommand))]
    private Template? _selectedTemplate;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty] private ObservableCollection<TemplateSource> _marketplaceSources = new();
    [ObservableProperty] private TemplateSource? _selectedMarketplaceSource;
    [ObservableProperty] private ObservableCollection<TemplateMarketplaceEntry> _marketplaceEntries = new();
    private List<TemplateMarketplaceEntry> _allMarketplaceEntries = new();
    [ObservableProperty] private string _marketplaceSearchQuery = "";
    [ObservableProperty] private TemplateMarketplaceEntry? _selectedMarketplaceEntry;
    [ObservableProperty] private string _marketplaceSourceUrl = "";
    [ObservableProperty] private string _marketplaceSourceKind = "Remote";
    [ObservableProperty] private string _marketplaceStatus = "";
    [ObservableProperty] private ObservableCollection<TemplateArtifactHistoryEntry> _marketplaceArtifactHistory = new();
    [ObservableProperty] private TemplateArtifactHistoryEntry? _selectedMarketplaceArtifact;
    [ObservableProperty] private TemplateManifestV2? _selectedMarketplaceManifest;
    [ObservableProperty] private TemplateScriptDiff? _marketplaceScriptDiff;
    [ObservableProperty] private string _marketplaceCapabilitiesDisplay = "";
    [ObservableProperty] private string _marketplaceScriptsDisplay = "";
    [ObservableProperty] private string _marketplaceCompatibilityDisplay = "";
    [ObservableProperty] private string _marketplaceHealthDisplay = "";
    [ObservableProperty] private string _marketplaceSignatureVerificationDisplay = "";
    [ObservableProperty] private string _marketplaceTrustStateDisplay = "";
    [ObservableProperty] private string _marketplaceDiffAddedDisplay = "";
    [ObservableProperty] private string _marketplaceDiffRemovedDisplay = "";
    [ObservableProperty] private string _marketplaceDiffChangedDisplay = "";
    [ObservableProperty] private string _marketplaceDiffTextDisplay = "";

    partial void OnMarketplaceSearchQueryChanged(string value) => FilterMarketplaceEntries();

    public TemplatesViewModel(
        ITemplateService templateService,
        INavigationService navigationService,
        ILogger<TemplatesViewModel> logger,
        IServiceProvider serviceProvider,
        ITemplateMarketplaceService marketplaceService,
        IDialogService dialogService)
    {
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _marketplaceService = marketplaceService ?? throw new ArgumentNullException(nameof(marketplaceService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadTemplatesAsync();
        await LoadMarketplaceAsync();
    }

    [RelayCommand]
    private async Task LoadMarketplaceAsync()
    {
        try { MarketplaceSources = new ObservableCollection<TemplateSource>(await _marketplaceService.GetSourcesAsync()); MarketplaceStatus = string.Format(L("MarketplaceSourcesLoaded"), MarketplaceSources.Count); }
        catch (Exception ex) { _logger.LogWarning(ex, "Unable to load marketplace sources"); MarketplaceStatus = L("MarketplaceSourcesUnavailable"); }
    }

    partial void OnSelectedMarketplaceSourceChanged(TemplateSource? value) => _ = LoadMarketplaceHistoryAsync();
    private async Task LoadMarketplaceHistoryAsync()
    {
        if (SelectedMarketplaceSource is null) { _allMarketplaceEntries = []; MarketplaceEntries = new(); MarketplaceArtifactHistory = new(); SetMarketplaceManifest(null); MarketplaceScriptDiff = null; return; }
        if (SelectedMarketplaceSource.Kind == TemplateSourceKind.BuiltIn)
        {
            _allMarketplaceEntries = [];
            MarketplaceEntries = new();
            MarketplaceArtifactHistory = new();
            SetMarketplaceManifest(null);
            MarketplaceScriptDiff = null;
            MarketplaceStatus = L("MarketplaceSourcesLoaded");
            return;
        }
        try
        {
            var status = await _marketplaceService.GetStatusAsync(SelectedMarketplaceSource.Id);
            _allMarketplaceEntries = (await _marketplaceService.DiscoverAsync() ?? []).Where(x => x.Source.Id == SelectedMarketplaceSource.Id).ToList();
            FilterMarketplaceEntries();
            if (SelectedMarketplaceEntry is null) SetMarketplaceManifest(status.Manifest, status);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "Marketplace history unavailable for selected source"); MarketplaceArtifactHistory = new(); SetMarketplaceManifest(null); SetMarketplaceSignatureFailure(ex); MarketplaceScriptDiff = null; }
    }

    [RelayCommand]
    private async Task AddMarketplaceSourceAsync()
    {
        if (string.IsNullOrWhiteSpace(MarketplaceSourceUrl)) return;
        try
        {
            var kind = string.Equals(MarketplaceSourceKind, nameof(TemplateSourceKind.UserLocal), StringComparison.Ordinal) ? TemplateSourceKind.UserLocal : TemplateSourceKind.Remote;
            var requiresExplicitConfirmation = kind == TemplateSourceKind.UserLocal || !MarketplaceSourceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            var accepted = false;
            if (requiresExplicitConfirmation)
            {
                var message = string.Format(L("MarketplaceUnsafeSourceConfirmation"), MarketplaceSourceUrl);
                accepted = await _dialogService.ShowConfirmAsync(L("MarketplaceUnsafeSourceTitle"), message);
                if (!accepted) { MarketplaceStatus = L("MarketplaceSourceDeclined"); return; }
            }
            await _marketplaceService.AddSourceAsync(MarketplaceSourceUrl, kind, accepted);
            MarketplaceSourceUrl = string.Empty;
            await LoadMarketplaceAsync();
            await LoadTemplatesAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Unable to add marketplace source"); MarketplaceStatus = FormatMarketplaceError(ex); }
    }

    [RelayCommand]
    private async Task SetMarketplaceSourceEnabledAsync(bool enabled)
    {
        if (SelectedMarketplaceSource is null) return;
        if (SelectedMarketplaceSource.Kind == TemplateSourceKind.BuiltIn) return;
        if (!await _dialogService.ShowConfirmAsync(L("MarketplaceLifecycleTitle"), enabled ? L("MarketplaceEnableConfirmation") : L("MarketplaceDisableConfirmation"))) { MarketplaceStatus = L("MarketplaceOperationDeclined"); return; }
        try { await _marketplaceService.SetSourceEnabledAsync(SelectedMarketplaceSource.Id, enabled); await LoadMarketplaceAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Unable to change marketplace source lifecycle"); MarketplaceStatus = FormatMarketplaceError(ex); }
    }

    [RelayCommand]
    private async Task RemoveMarketplaceSourceAsync()
    {
        if (SelectedMarketplaceSource is null) return;
        if (SelectedMarketplaceSource.Kind == TemplateSourceKind.BuiltIn) return;
        if (!await _dialogService.ShowConfirmAsync(L("MarketplaceRemoveTitle"), L("MarketplaceRemoveConfirmation"))) { MarketplaceStatus = L("MarketplaceOperationDeclined"); return; }
        try { await _marketplaceService.RemoveSourceAsync(SelectedMarketplaceSource.Id); SelectedMarketplaceSource = null; await LoadMarketplaceAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Unable to remove marketplace source"); MarketplaceStatus = FormatMarketplaceError(ex); }
    }

    [RelayCommand]
    private async Task DownloadMarketplaceArtifactAsync()
    {
        if (SelectedMarketplaceSource is null) return;
        try
        {
            IsLoading = true;
            var entry = SelectedMarketplaceEntry;
            if (entry is null)
            {
                // Refreshing the manifest here is diagnostic-only: the exact entry identity is
                // still mandatory before any artifact request can start.
                await _marketplaceService.FetchManifestAsync(SelectedMarketplaceSource.Id);
                throw new WslOperationFailedException("Select an exact marketplace catalog entry before downloading.", DistroNexusErrorCode.TemplateNotFound);
            }
            await _marketplaceService.DownloadArtifactAsync(SelectedMarketplaceSource.Id, entry.Manifest.Id, entry.ManifestDigest);
            MarketplaceStatus = L("MarketplaceArtifactVerified");
            await LoadTemplatesAsync();
            await LoadMarketplaceHistoryAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Unable to download marketplace artifact"); SetMarketplaceSignatureFailure(ex); MarketplaceStatus = FormatMarketplaceError(ex); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ReviewMarketplaceUpdateAsync()
    {
        if (SelectedMarketplaceSource is null || SelectedMarketplaceEntry is null) return;
        try
        {
            var review = await _marketplaceService.ReviewUpdateAsync(SelectedMarketplaceSource.Id, SelectedMarketplaceEntry.Manifest.Id, SelectedMarketplaceEntry.ManifestDigest);
            MarketplaceStatus = review.RequiresReview ? string.Format(L("MarketplaceReviewRequired"), BoolText(review.ScriptsChanged), BoolText(review.PublisherChanged), JoinValues(review.NewlyRequestedCapabilities)) : L("MarketplaceNoMaterialChanges");
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Unable to review marketplace update"); SetMarketplaceSignatureFailure(ex); MarketplaceStatus = FormatMarketplaceError(ex); }
    }

    [RelayCommand]
    private async Task ApproveMarketplaceCandidateAsync()
    {
        if (SelectedMarketplaceSource is null || SelectedMarketplaceArtifact is null) return;
        try
        {
            var grant = await _marketplaceService.CreateReviewGrantAsync(SelectedMarketplaceSource.Id, SelectedMarketplaceArtifact.Artifact.Sha256);
            SetMarketplaceManifest(grant.Manifest, await _marketplaceService.GetStatusAsync(SelectedMarketplaceSource.Id));
            SetMarketplaceScriptDiff(grant.ScriptDiff);
            var reviewDetails = string.Format(L("MarketplaceReviewConfirmation"), MarketplaceDiffAddedDisplay, MarketplaceDiffRemovedDisplay, MarketplaceDiffChangedDisplay, MarketplaceDiffTextDisplay, grant.ScriptDiff.IsTruncated ? L("MarketplaceDiffTruncated") : L("MarketplaceDiffComplete"), MarketplaceCapabilitiesDisplay, MarketplaceScriptsDisplay, MarketplaceCompatibilityDisplay, MarketplaceHealthDisplay);
            if (!await _dialogService.ShowConfirmAsync(L("MarketplaceReviewTitle"), reviewDetails)) { MarketplaceStatus = L("MarketplaceOperationDeclined"); return; }
            await _marketplaceService.ApproveCandidateAsync(grant.Token);
            MarketplaceStatus = L("MarketplaceCandidateApproved");
            await LoadTemplatesAsync(); await LoadMarketplaceEntryHistoryAsync(grant.Manifest.Id);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Unable to promote marketplace candidate"); SetMarketplaceSignatureFailure(ex); MarketplaceStatus = FormatMarketplaceError(ex); }
    }

    [RelayCommand]
    private async Task RollbackMarketplaceArtifactAsync()
    {
        if (SelectedMarketplaceSource is null || SelectedMarketplaceArtifact is null) return;
        if (!await _dialogService.ShowConfirmAsync(L("MarketplaceRollbackTitle"), string.Format(L("MarketplaceRollbackConfirmation"), SelectedMarketplaceArtifact.Artifact.Sha256))) { MarketplaceStatus = L("MarketplaceOperationDeclined"); return; }
        try
        {
            await _marketplaceService.RollbackAsync(SelectedMarketplaceArtifact.Manifest.Id, SelectedMarketplaceArtifact.Artifact.Sha256);
            MarketplaceStatus = L("MarketplaceRollbackComplete");
            await LoadTemplatesAsync();
            await LoadMarketplaceHistoryAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Unable to roll back marketplace artifact"); MarketplaceStatus = FormatMarketplaceError(ex); }
    }

    partial void OnSelectedMarketplaceArtifactChanged(TemplateArtifactHistoryEntry? value)
    {
        if (value is not null) SetMarketplaceManifest(value.Manifest);
    }
    partial void OnSelectedMarketplaceEntryChanged(TemplateMarketplaceEntry? value)
    {
        if (value is null) return;
        SetMarketplaceManifest(value.Manifest);
        _ = LoadMarketplaceEntryHistoryAsync(value.Manifest.Id);
        _ = LoadMarketplaceEntryStatusAsync(value);
    }
    private async Task LoadMarketplaceEntryStatusAsync(TemplateMarketplaceEntry entry)
    {
        if (SelectedMarketplaceSource is null) return;
        try
        {
            var status = await _marketplaceService.GetStatusAsync(SelectedMarketplaceSource.Id, entry.Manifest.Id, entry.ManifestDigest);
            if (ReferenceEquals(entry, SelectedMarketplaceEntry)) SetMarketplaceManifest(status.Manifest ?? entry.Manifest, status);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Marketplace entry status unavailable for {TemplateId}", entry.Manifest.Id);
            if (ReferenceEquals(entry, SelectedMarketplaceEntry)) SetMarketplaceSignatureFailure(ex);
        }
    }
    private async Task LoadMarketplaceEntryHistoryAsync(string templateId)
    {
        try { MarketplaceArtifactHistory = new ObservableCollection<TemplateArtifactHistoryEntry>(await _marketplaceService.GetArtifactHistoryAsync(templateId)); }
        catch { MarketplaceArtifactHistory = new(); }
    }
    private void FilterMarketplaceEntries()
    {
        var query = MarketplaceSearchQuery.Trim();
        var matching = string.IsNullOrEmpty(query) ? _allMarketplaceEntries : _allMarketplaceEntries.Where(x =>
            x.Manifest.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            x.Manifest.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            x.Manifest.Version.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            x.Manifest.Compatibility.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        MarketplaceEntries = new ObservableCollection<TemplateMarketplaceEntry>(matching);
        SelectedMarketplaceEntry = MarketplaceEntries.FirstOrDefault();
        if (SelectedMarketplaceEntry is null) { SetMarketplaceManifest(null); MarketplaceArtifactHistory = new(); }
    }

    private void SetMarketplaceManifest(TemplateManifestV2? manifest, TemplateMarketplaceStatus? status = null)
    {
        SelectedMarketplaceManifest = manifest;
        MarketplaceCapabilitiesDisplay = manifest is null ? string.Empty : JoinValues(manifest.Capabilities);
        MarketplaceScriptsDisplay = manifest is null ? string.Empty : JoinValues(manifest.ScriptHashes);
        MarketplaceCompatibilityDisplay = manifest?.Compatibility ?? string.Empty;
        MarketplaceHealthDisplay = manifest is null ? string.Empty : JoinValues(manifest.HealthChecks);
        MarketplaceSignatureVerificationDisplay = status?.SignatureStatus switch { TemplateSignatureStatus.Verified => L("MarketplaceSignatureVerified"), TemplateSignatureStatus.Invalid => L("MarketplaceSignatureInvalid"), TemplateSignatureStatus.NotPresent => L("MarketplaceSignatureNotPresent"), _ => string.Empty };
        MarketplaceTrustStateDisplay = status is null ? string.Empty : status.CanExecute ? L("MarketplaceTrustApproved") : status.TrustState == TemplateTrustState.Untrusted ? L("MarketplaceTrustUnavailable") : L("MarketplaceTrustReviewRequired");
    }

    private void SetMarketplaceScriptDiff(TemplateScriptDiff? diff)
    {
        MarketplaceScriptDiff = diff;
        MarketplaceDiffAddedDisplay = JoinValues(diff?.Added);
        MarketplaceDiffRemovedDisplay = JoinValues(diff?.Removed);
        MarketplaceDiffChangedDisplay = JoinValues(diff?.Changed);
        MarketplaceDiffTextDisplay = diff?.TextChanges is null ? string.Empty : string.Join(Environment.NewLine, diff.TextChanges.Select(change => $"{change.ScriptId}:" + Environment.NewLine + change.PreviousText + Environment.NewLine + "---" + Environment.NewLine + change.CandidateText));
    }

    private static string JoinValues<T>(IEnumerable<T>? values) => values is null ? string.Empty : string.Join(", ", values);
    private string BoolText(bool value) => L(value ? "MarketplaceYes" : "MarketplaceNo");
    private static string L(string key) => DistroNexus.Desktop.Properties.Resources.ResourceManager.GetString(key) ?? key;
    private void SetMarketplaceSignatureFailure(Exception exception)
    {
        if (exception is WslOperationFailedException { Code: DistroNexusErrorCode.TemplateArtifactIntegrityFailed })
        {
            MarketplaceSignatureVerificationDisplay = L("MarketplaceSignatureInvalid");
            MarketplaceTrustStateDisplay = L("MarketplaceTrustUnavailable");
        }
    }
    private static string FormatMarketplaceError(Exception exception) => exception is WslOperationFailedException failed
        ? string.Format(L("MarketplaceOperationFailed"), $"DN-{(int)failed.Code}")
        : L("MarketplaceOperationFailedGeneric");

    [RelayCommand]
    private async Task LoadTemplatesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading templates...";
            
            var templates = await _templateService.LoadTemplatesAsync(true);
            _allTemplates = templates;
            CategoryOptions = new ObservableCollection<string>(new[] { "All" }
                .Concat(_allTemplates.Select(t => t.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c)));

            if (!CategoryOptions.Contains(SelectedCategory))
            {
                SelectedCategory = "All";
            }

            RebuildScenarioTagOptions();

            FilterTemplates();
            
            var path = _templateService.GetTemplateScriptsPath();
            StatusMessage = $"Ready ({_allTemplates.Count} templates). Src: {path}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading templates");
            StatusMessage = "Error loading templates";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void FilterTemplates()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            Templates = new ObservableCollection<Template>(ApplyAdvancedFilters(_allTemplates));
        }
        else
        {
            var filtered = _allTemplates.Where(t => 
                t.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) || 
                t.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                t.Category.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                t.ScenarioTags.Any(tag => tag.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
            ).ToList();
            Templates = new ObservableCollection<Template>(ApplyAdvancedFilters(filtered));
        }
    }

    private void RebuildScenarioTagOptions()
    {
        IEnumerable<Template> source = _allTemplates;

        if (!string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            source = source.Where(t => string.Equals(t.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        ScenarioTagOptions = new ObservableCollection<string>(new[] { "All" }
            .Concat(source.SelectMany(t => t.ScenarioTags)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t)));

        if (!ScenarioTagOptions.Contains(SelectedScenarioTag))
        {
            SelectedScenarioTag = "All";
        }
    }

    private IEnumerable<Template> ApplyAdvancedFilters(IEnumerable<Template> source)
    {
        var filtered = source;

        if (!string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(t => string.Equals(t.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedScenarioTag, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(t => t.ScenarioTags.Any(tag => string.Equals(tag, SelectedScenarioTag, StringComparison.OrdinalIgnoreCase)));
        }

        return filtered;
    }

    [RelayCommand]
    private void GoBack()
    {
        if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
        {
             mainVm.IsOnDashboard = true;
        }
    }

    [RelayCommand]
    private async Task ImportTemplateAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Import Template"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Importing template...";
                var imported = await _templateService.ImportTemplateAsync(openFileDialog.FileName);
                if (imported != null)
                {
                    await LoadTemplatesAsync();
                    StatusMessage = "Template imported successfully";
                }
                else
                {
                    MessageBox.Show("Failed to import template. Check logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing template");
                 MessageBox.Show($"Error importing template: {MainViewModel.FormatAlertMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task ExportTemplateAsync(Template? template)
    {
         var target = template ?? SelectedTemplate;
         if (target == null) return;

        var saveFileDialog = new SaveFileDialog
        {
            FileName = $"{target.Id}.json",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Export Template"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                IsLoading = true;
                 StatusMessage = "Exporting template...";
                await _templateService.ExportTemplateAsync(target.Id, saveFileDialog.FileName);
                StatusMessage = "Template exported successfully";
            }
             catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting template");
                 MessageBox.Show($"Error exporting template: {MainViewModel.FormatAlertMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task RemoveTemplateAsync(Template? template)
    {
        var target = template ?? SelectedTemplate;
        if (target == null || !target.IsCustom) return;
        
        if (MessageBox.Show($"Are you sure you want to delete template '{target.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
             try
            {
                IsLoading = true;
                await _templateService.RemoveCustomTemplateAsync(target.Id);
                await LoadTemplatesAsync();
                if (SelectedTemplate == target) SelectedTemplate = null;
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error removing template");
                 MessageBox.Show($"Error removing template: {MainViewModel.FormatAlertMessage(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
    
    [RelayCommand]
    private void InstallNewInstance(Template? template)
    {
        if (template == null) return;

        if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
        {
            mainVm.ShowInstallWizardCommand.Execute(new InstallWizardStartupRequest
            {
                TemplateId = template.Id
            });
        }
    }

    [RelayCommand]
    private void ApplyToInstance(Template? template)
    {
        if (template == null) return;
        MessageBox.Show("Applying template to existing instance is coming soon.", "Feature Not Implemented");
    }

    private bool CanReferenceTemplate() => SelectedTemplate != null;
    private bool CanRemoveTemplate() => SelectedTemplate != null && SelectedTemplate.IsCustom;
}
