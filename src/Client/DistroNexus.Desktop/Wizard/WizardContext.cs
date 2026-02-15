using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.Wizard;

/// <summary>
/// Shared context data between wizard steps.
/// </summary>
public partial class WizardContext : ObservableObject
{
    // Quick Install Mode - uses defaults and skips detailed configuration
    [ObservableProperty]
    private bool _useQuickInstall;

    // Step 1: Distribution Selection
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

    // Step 3b: Template Selection
    [ObservableProperty]
    private Template? _selectedTemplate;

    [ObservableProperty]
    private bool _applyTemplateAfterInstall = true;

    // Step 4: Review Options
    [ObservableProperty]
    private bool _setAsDefault;

    [ObservableProperty]
    private bool _launchAfterInstall = true;

    // Installation Progress
    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private string _installStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isInstalling;

    // Result
    [ObservableProperty]
    private bool _installCompleted;

    [ObservableProperty]
    private bool _installFailed;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _resultMessage = string.Empty;

    [ObservableProperty]
    private string _logFilePath = string.Empty;

    /// <summary>
    /// Creates InstallOptions from the current context.
    /// </summary>
    public InstallOptions ToInstallOptions()
    {
        return new InstallOptions
        {
            InstanceName = InstanceName,
            Package = SelectedDistribution,
            InstallPath = InstallPath,
            Username = CreateUser ? Username : "root",
            Password = CreateUser ? Password : null,
            WslVersion = WslVersion,
            SetAsDefault = SetAsDefault,
            LaunchAfterInstall = LaunchAfterInstall,
            TemplateId = ApplyTemplateAfterInstall ? SelectedTemplate?.Id : null
        };
    }

    /// <summary>
    /// Resets the context to initial state.
    /// </summary>
    public void Reset()
    {
        SelectedDistribution = null;
        InstallPath = string.Empty;
        InstanceName = string.Empty;
        IsPathValid = false;
        PathValidationMessage = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        CreateUser = true;
        WslVersion = 2;
        SelectedTemplate = null;
        ApplyTemplateAfterInstall = true;
        SetAsDefault = false;
        LaunchAfterInstall = true;
        InstallProgress = 0;
        InstallStatusMessage = string.Empty;
        IsInstalling = false;
        InstallCompleted = false;
        InstallFailed = false;
        ErrorMessage = string.Empty;
        ResultMessage = string.Empty;
        LogFilePath = string.Empty;
    }
}
