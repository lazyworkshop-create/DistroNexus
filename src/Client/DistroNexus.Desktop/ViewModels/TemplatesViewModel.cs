using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Wizard;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>Template/catalog presentation client. Product operations remain in the typed module boundary.</summary>
public partial class TemplatesViewModel : ObservableObject
{
    private readonly IPowerShellModuleClient _moduleClient;
    private readonly ILogger<TemplatesViewModel> _logger;
    private readonly IDialogService _dialogService;
    private List<TemplateDisplay> _allTemplates = [];
    private List<TemplateMarketplaceEntryDisplay> _allMarketplaceEntries = [];
    private string? _reviewToken;

    [ObservableProperty] private ObservableCollection<TemplateDisplay> _templates = new();
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private string _selectedScenarioTag = "All";
    [ObservableProperty] private ObservableCollection<string> _categoryOptions = new(["All"]);
    [ObservableProperty] private ObservableCollection<string> _scenarioTagOptions = new(["All"]);
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(RemoveTemplateCommand))] [NotifyCanExecuteChangedFor(nameof(ExportTemplateCommand))] private TemplateDisplay? _selectedTemplate;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private ObservableCollection<TemplateSourceDisplay> _marketplaceSources = new();
    [ObservableProperty] private TemplateSourceDisplay? _selectedMarketplaceSource;
    [ObservableProperty] private ObservableCollection<TemplateMarketplaceEntryDisplay> _marketplaceEntries = new();
    [ObservableProperty] private string _marketplaceSearchQuery = "";
    [ObservableProperty] private TemplateMarketplaceEntryDisplay? _selectedMarketplaceEntry;
    [ObservableProperty] private string _marketplaceSourceUrl = "";
    [ObservableProperty] private string _marketplaceSourceKind = "Remote";
    [ObservableProperty] private string _marketplaceStatus = "";
    [ObservableProperty] private ObservableCollection<TemplateArtifactHistoryDisplay> _marketplaceArtifactHistory = new();
    [ObservableProperty] private TemplateArtifactHistoryDisplay? _selectedMarketplaceArtifact;
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

    public TemplatesViewModel(IPowerShellModuleClient moduleClient, ILogger<TemplatesViewModel> logger, IDialogService dialogService)
    { _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient)); _logger = logger ?? throw new ArgumentNullException(nameof(logger)); _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService)); }

    partial void OnSearchQueryChanged(string value) => FilterTemplates();
    partial void OnSelectedCategoryChanged(string value) { RebuildScenarioTagOptions(); FilterTemplates(); }
    partial void OnSelectedScenarioTagChanged(string value) => FilterTemplates();
    partial void OnMarketplaceSearchQueryChanged(string value) => FilterMarketplaceEntries();
    partial void OnSelectedMarketplaceSourceChanged(TemplateSourceDisplay? value) => _ = LoadMarketplaceEntriesAsync();
    partial void OnSelectedMarketplaceEntryChanged(TemplateMarketplaceEntryDisplay? value) { if (value is not null) _ = LoadMarketplaceEntryAsync(value); }

    [RelayCommand] public async Task InitializeAsync() { await LoadTemplatesAsync(); await LoadMarketplaceAsync(); }
    [RelayCommand] private async Task LoadTemplatesAsync()
    {
        try { IsLoading = true; _allTemplates = (await _moduleClient.GetTemplatesAsync(true)).ToList(); CategoryOptions = new ObservableCollection<string>(new[] { "All" }.Concat(_allTemplates.Select(x => x.Category).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))); RebuildScenarioTagOptions(); FilterTemplates(); StatusMessage = $"Ready ({_allTemplates.Count} templates)."; }
        catch (Exception ex) { _logger.LogError(ex, "Error loading templates"); StatusMessage = "Error loading templates"; }
        finally { IsLoading = false; }
    }
    private void FilterTemplates()
    {
        var items = _allTemplates.Where(x => (string.IsNullOrWhiteSpace(SearchQuery) || x.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) || x.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) || x.Category.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) || x.Tags.Any(tag => tag.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))) && (SelectedCategory == "All" || string.Equals(x.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase)) && (SelectedScenarioTag == "All" || x.Tags.Any(tag => string.Equals(tag, SelectedScenarioTag, StringComparison.OrdinalIgnoreCase))));
        Templates = new(items);
    }
    private void RebuildScenarioTagOptions() { var source = SelectedCategory == "All" ? _allTemplates : _allTemplates.Where(x => string.Equals(x.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase)); ScenarioTagOptions = new ObservableCollection<string>(new[] { "All" }.Concat(source.SelectMany(x => x.Tags).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))); if (!ScenarioTagOptions.Contains(SelectedScenarioTag)) SelectedScenarioTag = "All"; }

    [RelayCommand] private async Task LoadMarketplaceAsync() { try { MarketplaceSources = new(await _moduleClient.GetTemplateSourcesAsync()); MarketplaceStatus = string.Format(L("MarketplaceSourcesLoaded"), MarketplaceSources.Count); } catch (Exception ex) { _logger.LogWarning(ex, "Unable to load marketplace sources"); MarketplaceStatus = L("MarketplaceSourcesUnavailable"); } }
    private async Task LoadMarketplaceEntriesAsync() { if (SelectedMarketplaceSource is null) { MarketplaceEntries = new(); MarketplaceArtifactHistory = new(); return; } try { _allMarketplaceEntries = (await _moduleClient.GetTemplateMarketplaceEntriesAsync()).Where(x => x.SourceId == SelectedMarketplaceSource.Id).ToList(); FilterMarketplaceEntries(); } catch (Exception ex) { _logger.LogWarning(ex, "Unable to load marketplace entries"); MarketplaceStatus = FormatMarketplaceError(ex); } }
    private async Task LoadMarketplaceEntryAsync(TemplateMarketplaceEntryDisplay entry) { try { var status = await _moduleClient.GetTemplateMarketplaceStatusAsync(entry.SourceId, entry.TemplateId, entry.ManifestDigest); MarketplaceCapabilitiesDisplay = string.Join(", ", entry.Capabilities); MarketplaceScriptsDisplay = string.Empty; MarketplaceCompatibilityDisplay = string.Empty; MarketplaceHealthDisplay = string.Empty; MarketplaceSignatureVerificationDisplay = status.SignatureStatus.ToString(); MarketplaceTrustStateDisplay = status.CanExecute ? L("MarketplaceTrustApproved") : status.TrustState.ToString(); MarketplaceArtifactHistory = new(await _moduleClient.GetTemplateMarketplaceHistoryAsync(entry.TemplateId)); } catch (Exception ex) { MarketplaceStatus = FormatMarketplaceError(ex); } }
    private void FilterMarketplaceEntries() { var query = MarketplaceSearchQuery.Trim(); MarketplaceEntries = new(string.IsNullOrEmpty(query) ? _allMarketplaceEntries : _allMarketplaceEntries.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || x.TemplateId.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Version.Contains(query, StringComparison.OrdinalIgnoreCase))); SelectedMarketplaceEntry = MarketplaceEntries.FirstOrDefault(); }
    [RelayCommand] private async Task AddMarketplaceSourceAsync() { if (string.IsNullOrWhiteSpace(MarketplaceSourceUrl)) return; try { var kind = string.Equals(MarketplaceSourceKind, nameof(TemplateSourceKind.UserLocal), StringComparison.Ordinal) ? TemplateSourceKind.UserLocal : TemplateSourceKind.Remote; var unsafeSource = kind == TemplateSourceKind.UserLocal || !MarketplaceSourceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase); if (unsafeSource && !await _dialogService.ShowConfirmAsync(L("MarketplaceUnsafeSourceTitle"), string.Format(L("MarketplaceUnsafeSourceConfirmation"), MarketplaceSourceUrl))) { MarketplaceStatus = L("MarketplaceSourceDeclined"); return; } await _moduleClient.AddTemplateSourceAsync(MarketplaceSourceUrl, kind, unsafeSource); MarketplaceSourceUrl = ""; await LoadMarketplaceAsync(); } catch (Exception ex) { MarketplaceStatus = FormatMarketplaceError(ex); } }
    [RelayCommand] private async Task SetMarketplaceSourceEnabledAsync(bool enabled) { if (SelectedMarketplaceSource is null || SelectedMarketplaceSource.Kind == TemplateSourceKind.BuiltIn) return; if (!await _dialogService.ShowConfirmAsync(L("MarketplaceLifecycleTitle"), enabled ? L("MarketplaceEnableConfirmation") : L("MarketplaceDisableConfirmation"))) return; try { await _moduleClient.SetTemplateSourceEnabledAsync(SelectedMarketplaceSource.Id, enabled); await LoadMarketplaceAsync(); } catch (Exception ex) { MarketplaceStatus = FormatMarketplaceError(ex); } }
    [RelayCommand] private async Task RemoveMarketplaceSourceAsync() { if (SelectedMarketplaceSource is null || SelectedMarketplaceSource.Kind == TemplateSourceKind.BuiltIn) return; if (!await _dialogService.ShowConfirmAsync(L("MarketplaceRemoveTitle"), L("MarketplaceRemoveConfirmation"))) return; try { await _moduleClient.RemoveTemplateSourceAsync(SelectedMarketplaceSource.Id); await LoadMarketplaceAsync(); } catch (Exception ex) { MarketplaceStatus = FormatMarketplaceError(ex); } }
    [RelayCommand] private async Task DownloadMarketplaceArtifactAsync() { if (SelectedMarketplaceEntry is null) return; try { await _moduleClient.DownloadTemplateMarketplaceArtifactAsync(SelectedMarketplaceEntry.SourceId, SelectedMarketplaceEntry.TemplateId, SelectedMarketplaceEntry.ManifestDigest); MarketplaceStatus = L("MarketplaceArtifactVerified"); await LoadTemplatesAsync(); } catch (Exception ex) { MarketplaceStatus = FormatMarketplaceError(ex); } }
    [RelayCommand] private async Task ReviewMarketplaceUpdateAsync() { if (SelectedMarketplaceEntry is null) return; try { var review = await _moduleClient.ReviewTemplateMarketplaceCandidateAsync(SelectedMarketplaceEntry.SourceId, SelectedMarketplaceEntry.TemplateId, SelectedMarketplaceEntry.ManifestDigest); _reviewToken = review.ReviewToken; MarketplaceDiffAddedDisplay = review.AddedScriptCount.ToString(); MarketplaceDiffRemovedDisplay = review.RemovedScriptCount.ToString(); MarketplaceDiffChangedDisplay = string.Join(", ", review.ChangedScriptIdentifiers); MarketplaceDiffTextDisplay = review.IsTruncated ? L("MarketplaceDiffTruncated") : L("MarketplaceDiffComplete"); MarketplaceStatus = L("MarketplaceReviewRequired"); } catch (Exception ex) { MarketplaceStatus = FormatMarketplaceError(ex); } }
    [RelayCommand] private async Task ApproveMarketplaceCandidateAsync() { if (string.IsNullOrWhiteSpace(_reviewToken)) return; if (!await _dialogService.ShowConfirmAsync(L("MarketplaceReviewTitle"), MarketplaceDiffTextDisplay)) return; try { await _moduleClient.ApproveTemplateMarketplaceCandidateAsync(_reviewToken); _reviewToken = null; MarketplaceStatus = L("MarketplaceCandidateApproved"); await LoadTemplatesAsync(); } catch (Exception ex) { MarketplaceStatus = FormatMarketplaceError(ex); } }
    [RelayCommand] private async Task RollbackMarketplaceArtifactAsync() { if (SelectedMarketplaceArtifact is null) return; if (!await _dialogService.ShowConfirmAsync(L("MarketplaceRollbackTitle"), string.Format(L("MarketplaceRollbackConfirmation"), SelectedMarketplaceArtifact.ArtifactSha256))) return; try { await _moduleClient.RollbackTemplateMarketplaceArtifactAsync(SelectedMarketplaceArtifact.TemplateId, SelectedMarketplaceArtifact.ArtifactSha256); MarketplaceStatus = L("MarketplaceRollbackComplete"); await LoadTemplatesAsync(); } catch (Exception ex) { MarketplaceStatus = FormatMarketplaceError(ex); } }
    [RelayCommand] private async Task ImportTemplateAsync() { var dialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" }; if (dialog.ShowDialog() != true) return; try { var preview = await _moduleClient.PreviewTemplateImportFileAsync(dialog.FileName); await _moduleClient.ImportTemplateAsync(preview.PreviewToken); await LoadTemplatesAsync(); } catch (Exception ex) { StatusMessage = FormatMarketplaceError(ex); } }
    [RelayCommand] private async Task ExportTemplateAsync(TemplateDisplay? template) { var target = template ?? SelectedTemplate; if (target is null) return; try { var preview = await _moduleClient.PreviewTemplateExportAsync(target.Id); var result = await _moduleClient.ExportTemplateAsync(preview.PreviewToken); StatusMessage = result.Content; } catch (Exception ex) { StatusMessage = FormatMarketplaceError(ex); } }
    [RelayCommand] private async Task RemoveTemplateAsync(TemplateDisplay? template) { var target = template ?? SelectedTemplate; if (target is null || !target.IsCustom) return; if (MessageBox.Show($"Are you sure you want to delete template '{target.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; try { var preview = await _moduleClient.PreviewTemplateRemoveAsync(target.Id); await _moduleClient.RemoveTemplateAsync(preview.PreviewToken); await LoadTemplatesAsync(); } catch (Exception ex) { StatusMessage = FormatMarketplaceError(ex); } }
    [RelayCommand] private void InstallNewInstance(TemplateDisplay? template) { if (template is not null && Application.Current.MainWindow?.DataContext is MainViewModel main) main.ShowInstallWizardCommand.Execute(new InstallWizardStartupRequest { TemplateId = template.Id }); }
    [RelayCommand] private void ApplyToInstance(TemplateDisplay? template) { if (template is not null) MessageBox.Show("Apply a template from the instance workflow.", "Template application"); }
    [RelayCommand] private void GoBack() { if (Application.Current.MainWindow?.DataContext is MainViewModel main) main.IsOnDashboard = true; }
    private static string L(string key) => DistroNexus.Desktop.Properties.Resources.ResourceManager.GetString(key) ?? key;
    private static string FormatMarketplaceError(Exception exception) => exception is WslOperationFailedException failed ? $"DN-{(int)failed.Code}" : L("MarketplaceOperationFailedGeneric");
}
