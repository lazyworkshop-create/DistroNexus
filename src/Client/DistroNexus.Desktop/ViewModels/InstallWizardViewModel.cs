using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// View model for the installation wizard dialog.
/// </summary>
public partial class InstallWizardViewModel : ObservableObject
{
    private readonly ICatalogService _catalogService;
    private readonly IWslManagerService _wslManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<InstallWizardViewModel> _logger;
    private CancellationTokenSource? _installCts;

    #region Observable Properties

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private int _totalSteps = 4;

    [ObservableProperty]
    private string _stepTitle = "Select Distribution";

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoNext = true;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private string _installStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _installCompleted;

    [ObservableProperty]
    private bool _installFailed;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // Step 1: Distribution Selection
    [ObservableProperty]
    private ObservableCollection<DistroPackage> _availableDistributions = new();

    [ObservableProperty]
    private DistroPackage? _selectedDistribution;

    // Step 2: Installation Path
    [ObservableProperty]
    private string _installPath = string.Empty;

    [ObservableProperty]
    private string _instanceName = string.Empty;

    [ObservableProperty]
    private bool _isPathValid;

    [ObservableProperty]
    private string _pathValidationMessage = string.Empty;

    // Step 3: User Configuration
    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _createUser = true;

    [ObservableProperty]
    private int _wslVersion = 2;

    // Step 4: Review
    [ObservableProperty]
    private bool _setAsDefault;

    [ObservableProperty]
    private bool _launchAfterInstall = true;

    #endregion

    /// <summary>
    /// Event raised when the wizard is completed or cancelled.
    /// </summary>
    public event EventHandler<bool>? WizardCompleted;

    public InstallWizardViewModel(
        ICatalogService catalogService,
        IWslManagerService wslManager,
        ISettingsService settingsService,
        ILogger<InstallWizardViewModel> logger)
    {
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing install wizard");

            // Load default settings
            var settings = await _settingsService.LoadSettingsAsync();
            InstallPath = settings.DefaultInstallPath;
            WslVersion = settings.DefaultWslVersion;
            Username = settings.DefaultUsername;

            // Load available distributions
            var packages = await _catalogService.LoadCatalogAsync();
            AvailableDistributions.Clear();
            foreach (var package in packages)
            {
                AvailableDistributions.Add(package);
            }

            _logger.LogInformation("Loaded {Count} available distributions", AvailableDistributions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize install wizard");
            ErrorMessage = $"Failed to load distributions: {ex.Message}";
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            UpdateStepState();
        }
    }

    [RelayCommand]
    private void GoNext()
    {
        if (!ValidateCurrentStep())
            return;

        if (CurrentStep < TotalSteps)
        {
            CurrentStep++;
            UpdateStepState();
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (!ValidateCurrentStep())
            return;

        try
        {
            IsInstalling = true;
            InstallProgress = 0;
            InstallStatusMessage = "Preparing installation...";
            _installCts = new CancellationTokenSource();

            _logger.LogInformation("Starting installation of {DistroName} to {Path}", 
                SelectedDistribution?.Name, InstallPath);

            var options = new InstallOptions
            {
                InstanceName = InstanceName,
                Package = SelectedDistribution,
                InstallPath = InstallPath,
                Username = CreateUser ? Username : "root",
                Password = CreateUser ? Password : null,
                WslVersion = WslVersion,
                SetAsDefault = SetAsDefault,
                LaunchAfterInstall = LaunchAfterInstall
            };

            // Progress callback
            var progress = new Progress<(double percentage, string message)>(p =>
            {
                InstallProgress = p.percentage;
                InstallStatusMessage = p.message;
            });

            await _wslManager.InstallInstanceAsync(options, progress, _installCts.Token);

            InstallProgress = 100;
            InstallStatusMessage = "Installation completed successfully!";
            InstallCompleted = true;
            
            _logger.LogInformation("Installation completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Installation cancelled by user");
            InstallStatusMessage = "Installation cancelled.";
            InstallFailed = true;
            ErrorMessage = "Installation was cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Installation failed");
            InstallFailed = true;
            ErrorMessage = $"Installation failed: {ex.Message}";
            InstallStatusMessage = "Installation failed.";
        }
        finally
        {
            IsInstalling = false;
            _installCts?.Dispose();
            _installCts = null;
        }
    }

    [RelayCommand]
    private void CancelInstall()
    {
        _installCts?.Cancel();
    }

    [RelayCommand]
    private void Cancel()
    {
        _logger.LogInformation("Install wizard cancelled");
        WizardCompleted?.Invoke(this, false);
    }

    [RelayCommand]
    private void Finish()
    {
        _logger.LogInformation("Install wizard completed");
        WizardCompleted?.Invoke(this, true);
    }

    [RelayCommand]
    private void BrowseInstallPath()
    {
        // Use folder browser dialog
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Installation Directory",
            InitialDirectory = string.IsNullOrEmpty(InstallPath) 
                ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) 
                : InstallPath
        };

        if (dialog.ShowDialog() == true)
        {
            InstallPath = dialog.FolderName;
            ValidateInstallPath();
        }
    }

    private void UpdateStepState()
    {
        CanGoBack = CurrentStep > 1 && !IsInstalling;
        CanGoNext = CurrentStep < TotalSteps && !IsInstalling;

        StepTitle = CurrentStep switch
        {
            1 => "Select Distribution",
            2 => "Choose Installation Path",
            3 => "Configure User Account",
            4 => "Review and Install",
            _ => "Installation"
        };

        // Auto-generate instance name when moving to step 2
        if (CurrentStep == 2 && string.IsNullOrEmpty(InstanceName) && SelectedDistribution != null)
        {
            InstanceName = SelectedDistribution.Id;
            ValidateInstallPath();
        }
    }

    private bool ValidateCurrentStep()
    {
        ErrorMessage = string.Empty;

        switch (CurrentStep)
        {
            case 1:
                if (SelectedDistribution == null)
                {
                    ErrorMessage = "Please select a distribution to install.";
                    return false;
                }
                break;

            case 2:
                if (string.IsNullOrWhiteSpace(InstallPath))
                {
                    ErrorMessage = "Please specify an installation path.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(InstanceName))
                {
                    ErrorMessage = "Please specify an instance name.";
                    return false;
                }
                if (!IsPathValid)
                {
                    ErrorMessage = PathValidationMessage;
                    return false;
                }
                break;

            case 3:
                if (CreateUser)
                {
                    if (string.IsNullOrWhiteSpace(Username))
                    {
                        ErrorMessage = "Please enter a username.";
                        return false;
                    }
                    if (Password != ConfirmPassword)
                    {
                        ErrorMessage = "Passwords do not match.";
                        return false;
                    }
                }
                break;
        }

        return true;
    }

    partial void OnInstallPathChanged(string value)
    {
        ValidateInstallPath();
    }

    partial void OnInstanceNameChanged(string value)
    {
        ValidateInstallPath();
    }

    private void ValidateInstallPath()
    {
        if (string.IsNullOrWhiteSpace(InstallPath))
        {
            IsPathValid = false;
            PathValidationMessage = "Installation path is required.";
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(InstallPath);
            var instancePath = Path.Combine(fullPath, InstanceName);

            if (Directory.Exists(instancePath))
            {
                IsPathValid = false;
                PathValidationMessage = "A directory already exists at this location.";
                return;
            }

            // Check if parent directory exists or can be created
            var parentDir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                // Check if we can create it
                try
                {
                    var testPath = Path.Combine(parentDir, ".distronexus_test");
                    Directory.CreateDirectory(testPath);
                    Directory.Delete(testPath);
                }
                catch
                {
                    IsPathValid = false;
                    PathValidationMessage = "Cannot create directory at this location.";
                    return;
                }
            }

            IsPathValid = true;
            PathValidationMessage = $"Instance will be installed to: {instancePath}";
        }
        catch (Exception ex)
        {
            IsPathValid = false;
            PathValidationMessage = $"Invalid path: {ex.Message}";
        }
    }
}
