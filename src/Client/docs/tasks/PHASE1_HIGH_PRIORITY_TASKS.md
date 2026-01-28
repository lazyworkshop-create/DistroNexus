# Phase 1: High Priority Tasks

**Phase**: 1 - High Priority  
**Status**: Not Started  
**Target Completion**: Week 1-2  
**Document Version**: 1.0  
**Created**: 2026-01-29

---

## Overview

This phase addresses the most critical feature gaps that significantly impact user workflow and functionality compared to v1.x.

---

## Task 1.1: Download All Distributions

### Requirements

**User Story**: As a user, I want to download all available distributions at once so that I can work offline without manually downloading each package.

**Acceptance Criteria**:
- [ ] "Download All" button visible in Package Manager page
- [ ] Confirmation dialog before starting batch download
- [ ] Progress indicator showing current/total downloads
- [ ] Skip already cached packages
- [ ] Continue on individual download failure with error summary
- [ ] Final summary dialog showing success/failure count

### Analysis

**v1.x Implementation Reference**: [package_manager_tab.go#L216-L248](../../internal/ui/package_manager_tab.go)

```go
// v1.x approach
btnDownloadAll := widget.NewButtonWithIcon("", theme.DownloadIcon(), func() {
    dialog.ShowConfirm("Download All", "Download all official distributions?...", func(ok bool) {
        for i, task := range downloadTasks {
            log(fmt.Sprintf("[%d/%d] Downloading %s...\n", i+1, len(downloadTasks), task.Ver))
            err := logic.DownloadDistroOnly(...)
        }
    })
})
```

**Current v2.0 State**:
- `PackageManagerViewModel` has `DownloadPackageAsync(DistroPackage package)` for single downloads
- No batch download capability exists
- `IDownloadService.DownloadFileAsync()` supports progress reporting

**Dependencies**:
- `ICatalogService` - to get list of all packages
- `IDownloadService` - for actual downloads
- `ISettingsService` - to get cache path

### Implementation Details

#### 1.1.1 Add ViewModel Properties and Commands

**File**: `src/Client/DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs`

```csharp
// Add new observable properties
[ObservableProperty]
private bool _isDownloadingAll;

[ObservableProperty]
private int _downloadAllProgress;

[ObservableProperty]
private int _downloadAllTotal;

[ObservableProperty]
private string _downloadAllCurrentItem = string.Empty;

// Add command
[RelayCommand]
private async Task DownloadAllAsync()
{
    // Implementation
}
```

#### 1.1.2 Implement DownloadAllAsync Method

```csharp
[RelayCommand]
private async Task DownloadAllAsync()
{
    // 1. Show confirmation dialog
    var result = MessageBox.Show(
        "Download all distributions? This may take a long time and require significant disk space.",
        "Confirm Download All",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);

    if (result != MessageBoxResult.Yes)
        return;

    try
    {
        IsDownloadingAll = true;
        
        // 2. Get packages that need downloading (not cached)
        var packagesToDownload = Packages.Where(p => !p.IsCached).ToList();
        DownloadAllTotal = packagesToDownload.Count;
        DownloadAllProgress = 0;

        var successCount = 0;
        var failedPackages = new List<string>();

        // 3. Download each package sequentially
        foreach (var package in packagesToDownload)
        {
            DownloadAllCurrentItem = package.Name;
            DownloadAllProgress++;
            StatusMessage = $"Downloading [{DownloadAllProgress}/{DownloadAllTotal}]: {package.Name}";

            try
            {
                await DownloadPackageInternalAsync(package);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download {PackageName}", package.Name);
                failedPackages.Add(package.Name);
            }
        }

        // 4. Show summary
        var summaryMessage = $"Download complete.\n\nSuccess: {successCount}\nFailed: {failedPackages.Count}";
        if (failedPackages.Count > 0)
        {
            summaryMessage += $"\n\nFailed packages:\n- {string.Join("\n- ", failedPackages)}";
        }

        MessageBox.Show(summaryMessage, "Download All Complete", 
            MessageBoxButton.OK, 
            failedPackages.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }
    finally
    {
        IsDownloadingAll = false;
        DownloadAllCurrentItem = string.Empty;
        StatusMessage = "Ready";
        await LoadCatalogAsync(); // Refresh to show updated cache status
    }
}

private async Task DownloadPackageInternalAsync(DistroPackage package)
{
    var settings = await _settingsService.LoadSettingsAsync();
    var cachePath = settings.PackageCachePath;
    
    if (string.IsNullOrEmpty(cachePath))
    {
        cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DistroNexus", "Cache");
    }

    if (!Directory.Exists(cachePath))
        Directory.CreateDirectory(cachePath);

    var fileName = Path.GetFileName(new Uri(package.DownloadUrl).LocalPath);
    var destination = Path.Combine(cachePath, fileName);

    await _downloadService.DownloadFileAsync(package.DownloadUrl, destination, null);
    
    package.IsCached = true;
    package.LocalPath = destination;
}
```

#### 1.1.3 Update XAML UI

**File**: `src/Client/DistroNexus.Desktop/Views/PackageManagerPage.xaml`

Add button to header toolbar:

```xml
<Button x:Name="DownloadAllButton"
        Command="{Binding DownloadAllCommand}"
        IsEnabled="{Binding IsDownloadingAll, Converter={StaticResource InverseBoolConverter}}"
        ToolTip="Download all distributions">
    <ui:SymbolIcon Symbol="ArrowDownload24" />
</Button>

<!-- Progress indicator (visible during batch download) -->
<StackPanel Orientation="Horizontal" 
            Visibility="{Binding IsDownloadingAll, Converter={StaticResource BoolToVisibilityConverter}}">
    <ui:ProgressRing IsActive="True" Width="16" Height="16" />
    <TextBlock Text="{Binding DownloadAllCurrentItem}" Margin="8,0,0,0" />
    <TextBlock Text="{Binding DownloadAllProgress, StringFormat='({0}/'}" />
    <TextBlock Text="{Binding DownloadAllTotal, StringFormat='{}{0})'}" />
</StackPanel>
```

### Tasks Checklist

- [ ] **Task 1.1.1**: Add observable properties for batch download state
- [ ] **Task 1.1.2**: Implement `DownloadAllAsync` command in ViewModel
- [ ] **Task 1.1.3**: Add `DownloadPackageInternalAsync` helper method
- [ ] **Task 1.1.4**: Update `PackageManagerPage.xaml` with Download All button
- [ ] **Task 1.1.5**: Add progress indicator for batch download
- [ ] **Task 1.1.6**: Add `ISettingsService` dependency injection if not present
- [ ] **Task 1.1.7**: Write unit tests for `DownloadAllAsync`
- [ ] **Task 1.1.8**: Test with actual package downloads
- [ ] **Task 1.1.9**: Update documentation

---

## Task 1.2: Update Distribution Sources from Remote URL

### Requirements

**User Story**: As a user, I want to refresh the distribution catalog from a remote URL (including custom URLs) so that I can access the latest available distributions.

**Acceptance Criteria**:
- [ ] "Update Sources" button in Package Manager
- [ ] Support for default GitHub-hosted catalog
- [ ] Support for custom catalog URL from settings
- [ ] Progress indicator during update
- [ ] Error handling for network failures (fall back to offline mode)
- [ ] Automatic catalog reload after successful update

### Analysis

**v1.x Implementation Reference**: [package_manager_tab.go#L191-L214](../../internal/ui/package_manager_tab.go)

```go
// v1.x approach
btnUpdateSources := widget.NewButtonWithIcon("", theme.SearchReplaceIcon(), func() {
    showBlockingProgress("Updating Sources...", mw.Window, func(log func(string)) error {
        srcUrl := mw.Settings.DistroSourceUrl  // Custom URL support
        return logic.UpdateDistroList(ctx, projectRoot, srcUrl, log)
    }, refreshFunc)
})
```

**Current v2.0 State**:
- `CatalogService.RefreshCatalogAsync()` exists but doesn't use custom URL
- `GlobalSettings.CatalogUrl` property exists but is not utilized
- No UI trigger for updating sources

**Dependencies**:
- `ICatalogService` - needs method update
- `ISettingsService` - to read custom URL
- Network connectivity

### Implementation Details

#### 1.2.1 Update ICatalogService Interface

**File**: `src/Client/DistroNexus.Core/Interfaces/ICatalogService.cs`

```csharp
/// <summary>
/// Refreshes the catalog from a remote source.
/// </summary>
/// <param name="sourceUrl">Optional custom source URL. If null, uses default or settings URL.</param>
/// <param name="cancellationToken">Cancellation token.</param>
Task RefreshCatalogFromRemoteAsync(string? sourceUrl = null, CancellationToken cancellationToken = default);
```

#### 1.2.2 Implement in CatalogService

**File**: `src/Client/DistroNexus.Core/Services/CatalogService.cs`

```csharp
private const string DefaultCatalogUrl = "https://raw.githubusercontent.com/your-repo/distros.json";

public async Task RefreshCatalogFromRemoteAsync(string? sourceUrl = null, CancellationToken cancellationToken = default)
{
    // 1. Determine URL to use
    var url = sourceUrl;
    
    if (string.IsNullOrEmpty(url))
    {
        var settings = await _settingsService.LoadSettingsAsync();
        url = string.IsNullOrEmpty(settings.CatalogUrl) ? DefaultCatalogUrl : settings.CatalogUrl;
    }

    _logger.LogInformation("Refreshing catalog from {Url}", url);

    // 2. Download catalog JSON
    using var httpClient = new HttpClient();
    httpClient.Timeout = TimeSpan.FromSeconds(30);
    
    var response = await httpClient.GetStringAsync(url, cancellationToken);

    // 3. Parse and validate
    var remoteDistros = JsonSerializer.Deserialize<Dictionary<string, DistroConfig>>(response);
    
    if (remoteDistros == null || remoteDistros.Count == 0)
    {
        throw new InvalidOperationException("Remote catalog is empty or invalid");
    }

    // 4. Merge with local catalog (preserve LocalPath for cached items)
    await MergeCatalogAsync(remoteDistros);

    // 5. Save to local config
    await SaveCatalogAsync();

    _logger.LogInformation("Catalog refreshed successfully with {Count} distributions", remoteDistros.Count);
}

private async Task MergeCatalogAsync(Dictionary<string, DistroConfig> remoteDistros)
{
    var localCatalog = await LoadCatalogAsync();
    var localLookup = localCatalog.ToDictionary(p => p.Id, p => p);

    foreach (var (family, config) in remoteDistros)
    {
        foreach (var (versionKey, version) in config.Versions)
        {
            var packageId = $"{family}_{versionKey}";
            
            if (localLookup.TryGetValue(packageId, out var existing))
            {
                // Preserve local cache info
                // Update URL and other remote fields
                existing.DownloadUrl = version.Url;
                existing.Name = version.Name;
            }
            else
            {
                // Add new package
                // ...
            }
        }
    }
}
```

#### 1.2.3 Add ViewModel Command

**File**: `src/Client/DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs`

```csharp
[ObservableProperty]
private bool _isUpdatingSources;

[RelayCommand]
private async Task UpdateSourcesAsync()
{
    try
    {
        IsUpdatingSources = true;
        StatusMessage = "Updating distribution sources...";

        await _catalogService.RefreshCatalogFromRemoteAsync();

        await LoadCatalogAsync();
        
        StatusMessage = "Sources updated successfully";
        MessageBox.Show("Distribution catalog updated successfully.", 
            "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (HttpRequestException ex)
    {
        _logger.LogWarning(ex, "Network error while updating sources");
        IsOfflineMode = true;
        StatusMessage = "Offline Mode - Update failed";
        MessageBox.Show($"Failed to update sources (network error).\n\nWorking in Offline Mode.\n\nDetails: {ex.Message}", 
            "Update Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to update sources");
        StatusMessage = $"Update failed: {ex.Message}";
        MessageBox.Show($"Failed to update sources: {ex.Message}", 
            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    finally
    {
        IsUpdatingSources = false;
    }
}
```

#### 1.2.4 Update XAML

**File**: `src/Client/DistroNexus.Desktop/Views/PackageManagerPage.xaml`

```xml
<Button x:Name="UpdateSourcesButton"
        Command="{Binding UpdateSourcesCommand}"
        IsEnabled="{Binding IsUpdatingSources, Converter={StaticResource InverseBoolConverter}}"
        ToolTip="Update distribution sources from remote">
    <ui:SymbolIcon Symbol="ArrowSync24" />
</Button>
```

### Tasks Checklist

- [ ] **Task 1.2.1**: Add `RefreshCatalogFromRemoteAsync` to `ICatalogService`
- [ ] **Task 1.2.2**: Implement remote catalog fetching in `CatalogService`
- [ ] **Task 1.2.3**: Implement catalog merging logic (preserve local cache info)
- [ ] **Task 1.2.4**: Add `UpdateSourcesAsync` command to ViewModel
- [ ] **Task 1.2.5**: Add Update Sources button to UI
- [ ] **Task 1.2.6**: Handle offline/network error scenarios
- [ ] **Task 1.2.7**: Add settings UI for custom catalog URL (SettingsPage)
- [ ] **Task 1.2.8**: Write unit tests for catalog update
- [ ] **Task 1.2.9**: Test with real GitHub-hosted catalog
- [ ] **Task 1.2.10**: Update documentation

---

## Task 1.3: Custom Terminal Start Path

### Requirements

**User Story**: As a user, I want to specify a default directory where the terminal opens so that I can start work in my preferred location.

**Acceptance Criteria**:
- [ ] Terminal opens in `TerminalStartPath` setting (if configured)
- [ ] Falls back to home directory (`~`) if not configured
- [ ] Setting is configurable in Settings page
- [ ] Works with both Windows Terminal and fallback cmd.exe

### Analysis

**v1.x Implementation Reference**: [home_tab.go#L207-L213](../../internal/ui/home_tab.go)

```go
// v1.x approach
btnTerminal.OnTapped = func() {
    startPath := mw.Settings.DefaultTerminalStartPath
    err := logic.StartDistro(ctx, projectRoot, d.Name, true, startPath)
}
```

**Current v2.0 State**:
- `WslInstanceViewModel.OpenTerminal()` uses hardcoded command
- `GlobalSettings.TerminalStartPath` exists but is ignored
- No way to configure start path in UI

### Implementation Details

#### 1.3.1 Update OpenTerminal Method

**File**: `src/Client/DistroNexus.Desktop/ViewModels/MainViewModel.cs` (in `WslInstanceViewModel`)

```csharp
[RelayCommand]
private async Task OpenTerminalAsync()
{
    try
    {
        _logger.LogInformation("Opening terminal for instance {Name}", Name);

        // Get terminal start path from settings
        var settings = await _settingsService.LoadSettingsAsync();
        var startPath = settings.TerminalStartPath;
        
        // Build WSL command with optional start path
        var wslArgs = $"-d {Name}";
        if (!string.IsNullOrWhiteSpace(startPath))
        {
            // Convert Windows path to WSL path if necessary, or use as-is for Linux paths
            var linuxPath = startPath.StartsWith("/") ? startPath : ConvertToWslPath(startPath);
            wslArgs += $" --cd \"{linuxPath}\"";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "wt.exe",
            Arguments = $"-w 0 wsl {wslArgs}",
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to open terminal for instance {Name}", Name);

        // Fallback to cmd if Windows Terminal is not available
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            var startPath = settings.TerminalStartPath;
            var wslArgs = $"-d {Name}";
            
            if (!string.IsNullOrWhiteSpace(startPath))
            {
                var linuxPath = startPath.StartsWith("/") ? startPath : ConvertToWslPath(startPath);
                wslArgs += $" --cd \"{linuxPath}\"";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c wsl {wslArgs}",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch (Exception fallbackEx)
        {
            _logger.LogError(fallbackEx, "Failed to open fallback terminal");
            MessageBox.Show($"Failed to open terminal: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

private static string ConvertToWslPath(string windowsPath)
{
    // Convert C:\path\to\dir to /mnt/c/path/to/dir
    if (string.IsNullOrEmpty(windowsPath) || windowsPath.Length < 2)
        return windowsPath;

    if (char.IsLetter(windowsPath[0]) && windowsPath[1] == ':')
    {
        var driveLetter = char.ToLower(windowsPath[0]);
        var remainingPath = windowsPath.Substring(2).Replace('\\', '/');
        return $"/mnt/{driveLetter}{remainingPath}";
    }

    return windowsPath;
}
```

#### 1.3.2 Add Dependency Injection for ISettingsService

The `WslInstanceViewModel` needs access to `ISettingsService`. Update the constructor:

```csharp
public partial class WslInstanceViewModel : ObservableObject
{
    private readonly IWslManagerService _wslManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    public WslInstanceViewModel(
        WslInstance instance,
        IWslManagerService wslManager,
        ISettingsService settingsService,
        ILogger logger)
    {
        _instance = instance;
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

#### 1.3.3 Update Settings UI

**File**: `src/Client/DistroNexus.Desktop/Views/SettingsPage.xaml`

Ensure the Terminal Start Path setting is properly bound and editable:

```xml
<ui:CardControl Header="Terminal Start Path" 
                Description="Default directory when opening terminal (leave empty for home ~)">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="Auto" />
        </Grid.ColumnDefinitions>
        <ui:TextBox Text="{Binding TerminalStartPath, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                    PlaceholderText="~ (home directory)" />
        <Button Grid.Column="1" 
                Command="{Binding BrowseTerminalPathCommand}"
                Margin="8,0,0,0">
            <ui:SymbolIcon Symbol="FolderOpen24" />
        </Button>
    </Grid>
</ui:CardControl>
```

### Tasks Checklist

- [ ] **Task 1.3.1**: Update `OpenTerminal` to read `TerminalStartPath` from settings
- [ ] **Task 1.3.2**: Implement `ConvertToWslPath` helper method
- [ ] **Task 1.3.3**: Add `ISettingsService` dependency to `WslInstanceViewModel`
- [ ] **Task 1.3.4**: Update `MainViewModel` to pass `ISettingsService` when creating `WslInstanceViewModel`
- [ ] **Task 1.3.5**: Ensure Settings page has editable Terminal Start Path field
- [ ] **Task 1.3.6**: Add folder browser for Terminal Start Path setting
- [ ] **Task 1.3.7**: Test with Windows Terminal
- [ ] **Task 1.3.8**: Test fallback with cmd.exe
- [ ] **Task 1.3.9**: Test with both Windows and Linux-style paths
- [ ] **Task 1.3.10**: Update documentation

---

## Summary

| Task | Priority | Estimated Effort | Dependencies |
|------|----------|------------------|--------------|
| 1.1 Download All | HIGH | 4-6 hours | ICatalogService, IDownloadService |
| 1.2 Update Sources | HIGH | 6-8 hours | ICatalogService, Network |
| 1.3 Terminal Path | HIGH | 2-3 hours | ISettingsService |

**Total Estimated Effort**: 12-17 hours

---

## Testing Requirements

### Unit Tests
- [ ] `DownloadAllAsync` with mocked services
- [ ] `RefreshCatalogFromRemoteAsync` with mocked HTTP responses
- [ ] `ConvertToWslPath` with various path formats

### Integration Tests
- [ ] Full batch download workflow
- [ ] Catalog update with real network
- [ ] Terminal opening with custom path

### Manual Testing
- [ ] Download All with slow network
- [ ] Update Sources with invalid URL
- [ ] Terminal path with special characters

---

**Next Phase**: [Phase 2 - Medium Priority Tasks](./PHASE2_MEDIUM_PRIORITY_TASKS.md)
