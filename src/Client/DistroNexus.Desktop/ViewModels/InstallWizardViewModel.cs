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
    private readonly IPowerShellModuleClient _moduleClient;
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

    [ObservableProperty]
    private ObservableCollection<string> _installLogs = new();

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

    [ObservableProperty]
    private bool _useLocalCache = true;

    [ObservableProperty]
    private bool _isQuickMode = false;

    #endregion

    /// <summary>
    /// Event raised when the wizard is completed or cancelled.
    /// </summary>
    public event EventHandler<bool>? WizardCompleted;

    public InstallWizardViewModel(
        IPowerShellModuleClient moduleClient,
        IWslManagerService wslManager,
        ISettingsService settingsService,
        ILogger<InstallWizardViewModel> logger)
    {
        _moduleClient = moduleClient ?? throw new ArgumentNullException(nameof(moduleClient));
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
            var settings = _settingsService.LoadSettings();
            InstallPath = settings.DefaultInstallPath;
            WslVersion = settings.DefaultWslVersion;
            Username = settings.DefaultUsername;

            // Load available distributions
            var packages = await _moduleClient.GetPackagesAsync();
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
            ErrorMessage = string.Format(Properties.Resources.LoadDistributionsError, ex.Message);
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
    private void ToggleQuickMode()
    {
        IsQuickMode = !IsQuickMode;
        
        // Reset to step 1 when toggling modes
        CurrentStep = 1;
        UpdateStepState();
        
        _logger.LogInformation("Toggled quick mode: {IsQuickMode}", IsQuickMode);
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
            InstallStatusMessage = Properties.Resources.StatusPreparingInstall;
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
                LaunchAfterInstall = LaunchAfterInstall,
                UseLocalCache = UseLocalCache,
                InitCommands = GetInitializationCommands()
            };

            // Clear previous logs
            InstallLogs.Clear();
            AddInstallLog(Properties.Resources.LogStartingInstall);

            // Progress callback
            var progress = new Progress<(double percentage, string message)>(p =>
            {
                InstallProgress = p.percentage;
                InstallStatusMessage = p.message;
                AddInstallLog($"[{p.percentage:F1}%] {p.message}");
            });

            await _wslManager.InstallInstanceAsync(options, progress, _installCts.Token);

            InstallProgress = 100;
            InstallStatusMessage = Properties.Resources.InstallSuccessLabel;
            InstallCompleted = true;
            
                _logger.LogInformation("Installation completed successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Installation cancelled by user");
            InstallStatusMessage = Properties.Resources.StatusInstallCancelled;
            InstallFailed = true;
            ErrorMessage = Properties.Resources.ErrorInstallCancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Installation failed");
            InstallFailed = true;
            ErrorMessage = string.Format(Properties.Resources.ErrorInstallFailed, ex.Message);
            InstallStatusMessage = Properties.Resources.StatusInstallFailed;
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
            Title = Properties.Resources.SelectInstallDirTitle,
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
        // Update total steps based on mode
        TotalSteps = IsQuickMode ? 2 : 4;
        
        CanGoBack = CurrentStep > 1 && !IsInstalling;
        CanGoNext = CurrentStep < TotalSteps && !IsInstalling;

        StepTitle = IsQuickMode switch
        {
            true => CurrentStep switch
            {
                1 => Properties.Resources.StepTitleSelectDistro,
                2 => Properties.Resources.StepTitleQuickInstall,
                _ => Properties.Resources.StepTitleInstallation
            },
            false => CurrentStep switch
            {
                1 => Properties.Resources.StepTitleSelectDistro,
                2 => Properties.Resources.StepTitleChoosePath,
                3 => Properties.Resources.StepTitleConfigUser,
                4 => Properties.Resources.StepTitleReview,
                _ => Properties.Resources.StepTitleInstallation
            }
        };

        // Auto-generate instance name when moving to step 2 (in quick mode) or step 2 (in normal mode)
        if (CurrentStep == 2 && string.IsNullOrEmpty(InstanceName) && SelectedDistribution != null)
        {
            InstanceName = SelectedDistribution.Id;
            
            // Set default values for quick mode
            if (IsQuickMode)
            {
                InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DistroNexus", "Instances");
                Username = "user";
                Password = "password";
                CreateUser = true;
                SetAsDefault = false;
                UseLocalCache = true;
            }
            
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
                    ErrorMessage = Properties.Resources.ValMsgSelectDistro;
                    return false;
                }
                break;

            case 2:
                // In quick mode, this is the final validation before installation
                // In normal mode, this is just the path validation
                if (string.IsNullOrWhiteSpace(InstallPath))
                {
                    ErrorMessage = Properties.Resources.ValMsgSpecifyPath;
                    return false;
                }
                if (string.IsNullOrWhiteSpace(InstanceName))
                {
                    ErrorMessage = Properties.Resources.ValMsgSpecifyName;
                    return false;
                }
                if (!IsPathValid)
                {
                    ErrorMessage = PathValidationMessage;
                    return false;
                }
                
                // Additional quick mode validation
                if (IsQuickMode && CreateUser)
                {
                    if (string.IsNullOrWhiteSpace(Username))
                    {
                        ErrorMessage = Properties.Resources.ValMsgEnterUsername;
                        return false;
                    }
                    if (string.IsNullOrWhiteSpace(Password))
                    {
                        ErrorMessage = Properties.Resources.ValMsgEnterPassword;
                        return false;
                    }
                    if (Password != ConfirmPassword)
                    {
                        ErrorMessage = Properties.Resources.ValMsgPasswordsNoMatch;
                        return false;
                    }
                }
                break;

            case 3:
                // Only in normal mode
                if (CreateUser)
                {
                    if (string.IsNullOrWhiteSpace(Username))
                    {
                        ErrorMessage = Properties.Resources.ValMsgEnterUsername;
                        return false;
                    }
                    if (Password != ConfirmPassword)
                    {
                        ErrorMessage = Properties.Resources.ValMsgPasswordsNoMatch;
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

    /// <summary>
    /// Adds a log entry to the installation logs.
    /// </summary>
    private void AddInstallLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Application.Current.Dispatcher.Invoke(() =>
        {
            InstallLogs.Insert(0, $"[{timestamp}] {message}");
            
            // Keep only the last 100 log entries
            while (InstallLogs.Count > 100)
            {
                InstallLogs.RemoveAt(InstallLogs.Count - 1);
            }
        });
        
        _logger.LogInformation("Install log: {Message}", message);
    }

    /// <summary>
    /// Gets initialization commands based on the selected distribution type.
    /// </summary>
    private List<string> GetInitializationCommands()
    {
        var commands = new List<string>();
        
        if (SelectedDistribution?.Category?.Contains("Debian", StringComparison.OrdinalIgnoreCase) == true)
        {
            commands.AddRange([
                "apt update",
                "apt upgrade -y",
                "apt install -y curl wget git vim nano"
            ]);
        }
        else if (SelectedDistribution?.Category?.Contains("RedHat", StringComparison.OrdinalIgnoreCase) == true ||
                 SelectedDistribution?.Category?.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) == true)
        {
            commands.AddRange([
                "dnf update -y",
                "dnf install -y curl wget git vim nano"
            ]);
        }
        else if (SelectedDistribution?.Category?.Contains("Arch", StringComparison.OrdinalIgnoreCase) == true)
        {
            commands.AddRange([
                "pacman -Syu --noconfirm",
                "pacman -S --noconfirm curl wget git vim nano"
            ]);
        }
        else
        {
            // Default commands for other distributions
            commands.AddRange([
                "echo 'Initializing system...'",
                "echo 'System initialization complete'"
            ]);
        }
        
        _logger.LogInformation("Added {Count} initialization commands for distribution {DistroCategory}", 
            commands.Count, SelectedDistribution?.Category ?? "Unknown");
        
        return commands;
    }

    private void ValidateInstallPath()
    {
        if (string.IsNullOrWhiteSpace(InstallPath))
        {
            IsPathValid = false;
            PathValidationMessage = Properties.Resources.ValMsgPathRequired;
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(InstallPath);
            var instancePath = Path.Combine(fullPath, InstanceName);

            if (Directory.Exists(instancePath))
            {
                IsPathValid = false;
                PathValidationMessage = Properties.Resources.ValMsgDirExists;
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
                    PathValidationMessage = Properties.Resources.ValMsgCannotCreateDir;
                    return;
                }
            }

            IsPathValid = true;
            PathValidationMessage = string.Format(Properties.Resources.ValMsgInstallTarget, instancePath);
        }
        catch (Exception ex)
        {
            IsPathValid = false;
            PathValidationMessage = string.Format(Properties.Resources.ValMsgInvalidPath, ex.Message);
        }
    }

    /// <summary>
    /// Logs a message to the installation log.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void LogMessage(string message)
    {
        if (message == null) return;
        
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}";
        
        InstallLogs.Insert(0, logEntry);
        
        // Keep only last 1000 log entries
        while (InstallLogs.Count > 1000)
        {
            InstallLogs.RemoveAt(InstallLogs.Count - 1);
        }
        
        _logger.LogDebug("Wizard log: {Message}", message);
    }

    /// <summary>
    /// Clears all installation logs.
    /// </summary>
    public void ClearLog()
    {
        InstallLogs.Clear();
        _logger.LogDebug("Wizard log cleared");
    }
}
