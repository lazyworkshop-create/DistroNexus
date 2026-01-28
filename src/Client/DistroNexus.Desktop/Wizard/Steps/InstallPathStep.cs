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
            var settings = await _settingsService.LoadSettingsAsync();
            Context.InstallPath = settings.DefaultInstallPath;
        }

        // Subscribe to context property changes
        if (Context != null)
        {
            Context.PropertyChanged += OnContextPropertyChanged;
        }

        ValidateInstallPath();
    }

    public override Task OnExitAsync()
    {
        // Unsubscribe from context property changes
        if (Context != null)
        {
            Context.PropertyChanged -= OnContextPropertyChanged;
        }

        return Task.CompletedTask;
    }

    private void OnContextPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WizardContext.InstallPath) or nameof(WizardContext.InstanceName))
        {
            // Reset instance name check when instance name changes
            if (e.PropertyName == nameof(WizardContext.InstanceName))
            {
                InstanceNameChecked = false;
                InstanceNameExists = false;
            }
            
            ValidateInstallPath();
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
            ValidateInstallPath();
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

            // If instance name check is in progress, don't mark as valid yet
            if (IsValidatingInstance)
            {
                Context.IsPathValid = false;
                Context.PathValidationMessage = "Checking if instance name is available...";
                ValidationIcon = SymbolRegular.ArrowSync24;
                ValidationColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)); // Blue
                return;
            }

            // If instance name exists, mark as invalid
            if (InstanceNameChecked && InstanceNameExists)
            {
                Context.IsPathValid = false;
                Context.PathValidationMessage = $"An instance named '{Context.InstanceName}' already exists. Please choose a different name.";
                UpdateValidationVisuals(false);
                return;
            }

            // Check if instance name already exists in WSL (async check)
            if (!InstanceNameChecked)
            {
                _ = CheckInstanceNameExistsAsync();
                
                // Don't mark as valid until check completes
                Context.IsPathValid = false;
                Context.PathValidationMessage = "Checking if instance name is available...";
                ValidationIcon = SymbolRegular.ArrowSync24;
                ValidationColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)); // Blue
                return;
            }

            // All checks passed
            Context.IsPathValid = true;
            Context.PathValidationMessage = $"✓ Instance will be installed to: {instancePath}";
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
    /// Checks asynchronously if an instance with this name already exists.
    /// </summary>
    private async Task CheckInstanceNameExistsAsync()
    {
        if (Context == null || string.IsNullOrWhiteSpace(Context.InstanceName))
            return;

        var currentInstanceName = Context.InstanceName;

        try
        {
            IsValidatingInstance = true;
            
            var instances = await _wslManager.GetInstancesAsync();
            
            // Only update if the instance name hasn't changed
            if (Context.InstanceName == currentInstanceName)
            {
                InstanceNameExists = instances.Any(i => 
                    string.Equals(i.Name, Context.InstanceName, StringComparison.OrdinalIgnoreCase));
                InstanceNameChecked = true;

                // Re-trigger validation to update UI with the result
                ValidateInstallPath();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if instance name exists");
            InstanceNameChecked = false;
            
            // Show warning but allow user to proceed (will be checked again during installation)
            if (Context.InstanceName == currentInstanceName)
            {
                Context.PathValidationMessage = "Warning: Could not verify if instance name already exists. The installation will check again before proceeding.";
                ValidationIcon = SymbolRegular.Warning24;
                ValidationColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 185, 0)); // Orange
            }
        }
        finally
        {
            IsValidatingInstance = false;
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
        // First run the validation logic
        ValidateInstallPath();

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

        // Check if validation is still in progress
        if (IsValidatingInstance)
        {
            ErrorMessage = "Still checking if instance name is available. Please wait...";
            return false;
        }

        // If instance name hasn't been checked yet, force a synchronous check
        if (!InstanceNameChecked && !string.IsNullOrWhiteSpace(Context.InstanceName))
        {
            ErrorMessage = "Verifying instance name availability...";
            
            try
            {
                // Trigger async check and wait
                var checkTask = CheckInstanceNameExistsAsync();
                
                // Wait up to 5 seconds for the check to complete
                if (!checkTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    ErrorMessage = "Instance name validation timed out. Please check your WSL installation and try again.";
                    return false;
                }

                // After check completes, re-validate
                ValidateInstallPath();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed during synchronous instance name check");
                ErrorMessage = "Failed to verify instance name. Please try again.";
                return false;
            }
        }

        // Check if instance name already exists (from completed async check)
        if (InstanceNameChecked && InstanceNameExists)
        {
            ErrorMessage = $"An instance named '{Context.InstanceName}' already exists. Please choose a different name.";
            return false;
        }

        // Check final path validity
        if (Context?.IsPathValid != true)
        {
            ErrorMessage = Context?.PathValidationMessage ?? "Invalid path or instance name.";
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Applies default values for quick install mode.
    /// </summary>
    public override async Task ApplyQuickInstallDefaultsAsync()
    {
        if (Context == null)
            return;

        // Load default path from settings
        var settings = await _settingsService.LoadSettingsAsync();
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
