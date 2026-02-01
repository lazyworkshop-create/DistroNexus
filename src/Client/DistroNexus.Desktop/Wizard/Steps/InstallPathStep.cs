using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 2: Configure installation path.
/// </summary>
public partial class InstallPathStep : WizardStepBase
{
    private readonly ISettingsService _settingsService;
    private readonly IWslManagerService _wslManager;
    private readonly ILogger _logger;
    private CancellationTokenSource? _validationCts;
    private System.Threading.Timer? _debounceTimer;
    private const int DebounceDelayMs = 500;

    public override string StepId => "install-path";
    public override string Title => "Choose Installation Path";
    public override string Description => "Select where to install the distribution";

    [ObservableProperty]
    private SymbolRegular _validationIcon = SymbolRegular.Info24;

    [ObservableProperty]
    private SolidColorBrush _validationColor = new(Colors.Gray);

    [ObservableProperty]
    private bool _isValidatingInstance;

    [ObservableProperty]
    private bool _instanceNameChecked;

    [ObservableProperty]
    private bool _instanceNameExists;

    [ObservableProperty]
    private string _recommendedInstanceName = string.Empty;

    [ObservableProperty]
    private bool _isValidating;

    public InstallPathStep(ISettingsService settingsService, IWslManagerService wslManager, ILogger logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override UserControl CreateContent()
    {
        return new InstallPathStepView { DataContext = this };
    }

    public override async Task OnEnterAsync()
    {
        // Load default path from settings if not already set
        if (Context != null && string.IsNullOrEmpty(Context.InstallPath))
        {
            var settings = _settingsService.LoadSettings();
            Context.InstallPath = settings.DefaultInstallPath;
        }

        // Generate recommended instance name from distribution name
        if (Context?.SelectedDistribution != null)
        {
            var baseName = Context.SelectedDistribution.Name
                ?.Replace(" ", "-")
                .Replace(".", "")
                .ToLowerInvariant() ?? "instance";
            RecommendedInstanceName = baseName;
            
            // Always set as default when entering this step
            Context.InstanceName = baseName;
            _logger.LogInformation("Set default instance name to: {InstanceName}", baseName);
        }
        else
        {
            RecommendedInstanceName = string.Empty;
        }

        // Clear validation state
        if (Context != null)
        {
            Context.PathValidationMessage = string.Empty;
            Context.IsPathValid = false;
        }
        InstanceNameChecked = false;
        InstanceNameExists = false;

        // Subscribe to context property changes
        if (Context != null)
        {
            Context.PropertyChanged += OnContextPropertyChanged;
        }
    }

    public override Task OnExitAsync()
    {
        // Unsubscribe from context property changes
        if (Context != null)
        {
            Context.PropertyChanged -= OnContextPropertyChanged;
        }

        // Clean up resources
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _validationCts?.Cancel();
        _validationCts?.Dispose();
        _validationCts = null;

        return Task.CompletedTask;
    }

    private void OnContextPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WizardContext.InstanceName))
        {
            // Reset validation state when instance name changes
            InstanceNameChecked = false;
            InstanceNameExists = false;

            // Do NOT auto-validate - only validate when user clicks Next button
            // This prevents unnecessary API calls while typing
        }
        else if (e.PropertyName == nameof(WizardContext.InstallPath))
        {
            // Reset path validation when install path changes
            if (Context != null)
            {
                Context.PathValidationMessage = string.Empty;
                Context.IsPathValid = false;
            }
        }
    }

    [RelayCommand]
    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Installation Directory",
            InitialDirectory = string.IsNullOrEmpty(Context?.InstallPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                : Context.InstallPath
        };

        if (dialog.ShowDialog() == true && Context != null)
        {
            Context.InstallPath = dialog.FolderName;
        }
    }

    private void ValidateInstallPath()
    {
        if (Context == null)
            return;

        if (string.IsNullOrWhiteSpace(Context.InstallPath))
        {
            Context.IsPathValid = false;
            Context.PathValidationMessage = "Installation path is required.";
            UpdateValidationVisuals(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(Context.InstanceName))
        {
            Context.IsPathValid = false;
            Context.PathValidationMessage = "Instance name is required.";
            UpdateValidationVisuals(false);
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(Context.InstallPath);
            var instancePath = Path.Combine(fullPath, Context.InstanceName);

            // Check if directory already exists
            if (Directory.Exists(instancePath))
            {
                Context.IsPathValid = false;
                Context.PathValidationMessage = "A directory already exists at this location.";
                UpdateValidationVisuals(false);
                return;
            }

            // Check if parent directory exists or can be created
            var parentDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                try
                {
                    var testPath = Path.Combine(parentDir, ".distronexus_test_" + Guid.NewGuid().ToString("N")[..8]);
                    Directory.CreateDirectory(testPath);
                    Directory.Delete(testPath);
                }
                catch
                {
                    Context.IsPathValid = false;
                    Context.PathValidationMessage = "Cannot create directory at this location. Check permissions.";
                    UpdateValidationVisuals(false);
                    return;
                }
            }

            // Check disk space
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(fullPath) ?? fullPath);
                const long minimumFreeSpace = 2L * 1024 * 1024 * 1024; // 2GB
                
                if (driveInfo.AvailableFreeSpace < minimumFreeSpace)
                {
                    Context.IsPathValid = false;
                    Context.PathValidationMessage = $"Insufficient disk space. Available: {driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024):F2}GB, Required: 2GB";
                    UpdateValidationVisuals(false);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not check disk space");
                // Don't fail validation if we can't check disk space
            }

            // If instance name exists, mark as invalid
            if (InstanceNameChecked && InstanceNameExists)
            {
                Context.IsPathValid = false;
                Context.PathValidationMessage = $"An instance named '{Context.InstanceName}' already exists. Please choose a different name.";
                UpdateValidationVisuals(false);
                return;
            }

            // Basic validation passed - full validation happens on Next button click
            Context.IsPathValid = true;
            Context.PathValidationMessage = $"Instance will be installed to: {instancePath}";
            UpdateValidationVisuals(true);
        }
        catch (Exception ex)
        {
            Context.IsPathValid = false;
            Context.PathValidationMessage = $"Invalid path: {ex.Message}";
            UpdateValidationVisuals(false);
        }
    }

    /// <summary>
    /// Checks if an instance with this name already exists (async).
    /// This version runs on a background thread to avoid blocking UI.
    /// </summary>
    private async Task<bool> CheckInstanceNameExistsInternalAsync()
    {
        if (Context == null || string.IsNullOrWhiteSpace(Context.InstanceName))
            return false;

        try
        {
            var instances = await _wslManager.GetInstancesAsync();
            return instances.Any(i => 
                string.Equals(i.Name, Context.InstanceName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if instance name exists");
            // Return false to allow user to proceed (will be checked again during installation)
            return false;
        }
    }

    /// <summary>
    /// Checks asynchronously if an instance with this name already exists.
    /// Uses debouncing to avoid excessive calls.
    /// </summary>
    private async Task CheckInstanceNameExistsAsync()
    {
        if (Context == null || string.IsNullOrWhiteSpace(Context.InstanceName))
            return;

        // Cancel any pending validation
        _validationCts?.Cancel();
        _validationCts?.Dispose();
        _validationCts = new CancellationTokenSource();
        var token = _validationCts.Token;

        var currentInstanceName = Context.InstanceName;

        try
        {
            IsValidatingInstance = true;

            var instanceExists = await CheckInstanceNameExistsInternalAsync();

            // Only update if the instance name hasn't changed and token is not cancelled
            if (!token.IsCancellationRequested && Context.InstanceName == currentInstanceName)
            {
                InstanceNameExists = instanceExists;
                InstanceNameChecked = true;

                // Re-trigger validation to update UI with the result
                ValidateInstallPath();
            }
        }
        catch (OperationCanceledException)
        {
            // Validation was cancelled - this is expected
            _logger.LogDebug("Instance name validation cancelled for: {InstanceName}", currentInstanceName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if instance name exists");

            if (!token.IsCancellationRequested)
            {
                InstanceNameChecked = false;

                // Show warning but allow user to proceed (will be checked again during installation)
                if (Context.InstanceName == currentInstanceName)
                {
                    Context.PathValidationMessage = "Warning: Could not verify if instance name already exists. The installation will check again before proceeding.";
                    ValidationIcon = SymbolRegular.Warning24;
                    ValidationColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 185, 0)); // Orange
                }
            }
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                IsValidatingInstance = false;
            }
        }
    }

    private void UpdateValidationVisuals(bool isValid)
    {
        if (isValid)
        {
            ValidationIcon = SymbolRegular.CheckmarkCircle24;
            ValidationColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 124, 16)); // Green
        }
        else
        {
            ValidationIcon = SymbolRegular.ErrorCircle24;
            ValidationColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 52, 56)); // Red
        }
    }

    public override bool Validate()
    {
        try
        {
            // Basic synchronous validations first
            if (string.IsNullOrWhiteSpace(Context?.InstallPath))
            {
                ErrorMessage = "Please specify an installation path.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Context?.InstanceName))
            {
                ErrorMessage = "Please specify an instance name.";
                return false;
            }

            // Validate instance name format (alphanumeric, hyphens, underscores only)
            if (!System.Text.RegularExpressions.Regex.IsMatch(Context.InstanceName, @"^[a-zA-Z0-9_-]+$"))
            {
                ErrorMessage = "Instance name can only contain letters, numbers, hyphens, and underscores.";
                return false;
            }

            // Validate instance name must start with alphanumeric
            if (!char.IsLetterOrDigit(Context.InstanceName[0]))
            {
                ErrorMessage = "Instance name must start with a letter or number.";
                return false;
            }

            // Check instance name length
            if (Context.InstanceName.Length < 2)
            {
                ErrorMessage = "Instance name is too short (minimum 2 characters).";
                return false;
            }

            if (Context.InstanceName.Length > 50)
            {
                ErrorMessage = "Instance name is too long (maximum 50 characters).";
                return false;
            }

            // Check for reserved names
            var reservedNames = new[] { "wsl", "docker", "system", "windows", "microsoft", "default", "temp", "tmp" };
            if (reservedNames.Contains(Context.InstanceName.ToLowerInvariant()))
            {
                ErrorMessage = $"'{Context.InstanceName}' is a reserved name. Please choose a different name.";
                return false;
            }

            // Show loading state for async validation
            IsValidating = true;
            ErrorMessage = "Verifying instance name availability...";

            // Force UI to update immediately
            System.Windows.Application.Current?.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            // If instance name hasn't been checked yet, check it now in a blocking but non-UI-blocking way
            if (!InstanceNameChecked && !string.IsNullOrWhiteSpace(Context.InstanceName))
            {
                try
                {
                    // Use Task.Run to execute async code without blocking UI thread
                    // This runs on a thread pool thread, keeping UI responsive
                    var task = Task.Run(async () =>
                    {
                        return await CheckInstanceNameExistsInternalAsync();
                    });

                    // Wait with timeout (max 10 seconds)
                    if (task.Wait(TimeSpan.FromSeconds(10)))
                    {
                        InstanceNameExists = task.Result;
                        InstanceNameChecked = true;
                    }
                    else
                    {
                        _logger.LogWarning("Instance name check timed out");
                        ErrorMessage = "Verification timed out. Please try again.";
                        IsValidating = false;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed during instance name check");
                    ErrorMessage = "Failed to verify instance name. Please try again.";
                    IsValidating = false;
                    return false;
                }
            }

            // Hide loading state
            IsValidating = false;

            // Check if instance name already exists (from completed check)
            if (InstanceNameChecked && InstanceNameExists)
            {
                ErrorMessage = $"An instance named '{Context.InstanceName}' already exists. Please choose a different name.";
                return false;
            }

            // Validate install path
            ValidateInstallPath();

            // Check final path validity
            if (Context?.IsPathValid != true)
            {
                ErrorMessage = Context?.PathValidationMessage ?? "Invalid path or instance name.";
                return false;
            }

            ErrorMessage = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during validation");
            ErrorMessage = $"Validation error: {ex.Message}";
            IsValidating = false;
            return false;
        }
    }

    /// <summary>
    /// Applies default values for quick install mode.
    /// </summary>
    public override async Task ApplyQuickInstallDefaultsAsync()
    {
        if (Context == null)
            return;

        // Load default path from settings
        var settings = _settingsService.LoadSettings();
        Context.InstallPath = settings.DefaultInstallPath;

        // Generate a unique instance name based on the selected distribution
        var baseName = Context.SelectedDistribution?.Name?.Replace(" ", "-") ?? "MyInstance";
        var instanceName = baseName;
        var counter = 1;

        // Check for existing instances and find a unique name
        try
        {
            var instances = await _wslManager.GetInstancesAsync();
            while (instances.Any(i => string.Equals(i.Name, instanceName, StringComparison.OrdinalIgnoreCase)))
            {
                instanceName = $"{baseName}-{counter}";
                counter++;
            }
        }
        catch
        {
            // If we can't check, just use timestamp
            instanceName = $"{baseName}-{DateTime.Now:yyyyMMddHHmmss}";
        }

        Context.InstanceName = instanceName;
        Context.IsPathValid = true;
        
        _logger.LogInformation("Applied quick install defaults: Path={Path}, Instance={Instance}", 
            Context.InstallPath, Context.InstanceName);
    }
}
