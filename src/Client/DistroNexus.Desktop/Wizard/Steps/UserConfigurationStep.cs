using CommunityToolkit.Mvvm.ComponentModel;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Windows.Controls;

namespace DistroNexus.Desktop.Wizard.Steps;

/// <summary>
/// Step 3: Configure user account.
/// </summary>
public partial class UserConfigurationStep : WizardStepBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    public override string StepId => "user-configuration";
    public override string Title => Properties.Resources.WizardStepConfigureUser;
    public override string Description => "Set up the default user for this instance";

    /// <summary>
    /// Gets the WSL version index for ComboBox binding (0 = WSL1, 1 = WSL2).
    /// </summary>
    public int WslVersionIndex
    {
        get => (Context?.WslVersion ?? 2) - 1;
        set
        {
            if (Context != null)
            {
                Context.WslVersion = value + 1;
            }
        }
    }

    public UserConfigurationStep(ISettingsService settingsService, ILogger logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override UserControl CreateContent()
    {
        return new UserConfigurationStepView { DataContext = this };
    }

    public override async Task OnEnterAsync()
    {
        // Load default settings if username not set
        if (Context != null && string.IsNullOrEmpty(Context.Username))
        {
            var settings = _settingsService.LoadSettings();
            Context.Username = settings.DefaultUsername;
            Context.WslVersion = settings.DefaultWslVersion;
            OnPropertyChanged(nameof(WslVersionIndex));
        }
    }

    public override bool Validate()
    {
        if (Context?.CreateUser == true)
        {
            if (string.IsNullOrWhiteSpace(Context.Username))
            {
                ErrorMessage = Properties.Resources.ErrorUsernameRequired;
                return false;
            }

            // Validate username format (Linux username rules)
            if (!IsValidLinuxUsername(Context.Username))
            {
                ErrorMessage = Properties.Resources.ErrorUsernameFormat;
                return false;
            }

            // Check password strength if provided
            if (!string.IsNullOrWhiteSpace(Context.Password))
            {
                if (Context.Password.Length < 4)
                {
                    ErrorMessage = Properties.Resources.ErrorPasswordMinLength;
                    return false;
                }

                if (Context.Password != Context.ConfirmPassword)
                {
                    ErrorMessage = Properties.Resources.ErrorPasswordMismatch;
                    return false;
                }
            }
            else if (!string.IsNullOrWhiteSpace(Context.ConfirmPassword))
            {
                ErrorMessage = Properties.Resources.ErrorPasswordMismatch;
                return false;
            }
        }

        // Validate WSL version
        if (Context?.WslVersion != 1 && Context?.WslVersion != 2)
        {
            ErrorMessage = Properties.Resources.ErrorInvalidWslVersion;
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }

    private static bool IsValidLinuxUsername(string username)
    {
        if (string.IsNullOrEmpty(username) || username.Length > 32)
            return false;

        // Must start with a lowercase letter
        if (!char.IsLetter(username[0]) || !char.IsLower(username[0]))
            return false;

        // Can only contain lowercase letters, digits, hyphens, and underscores
        return username.All(c => char.IsLower(c) || char.IsDigit(c) || c == '-' || c == '_');
    }

    /// <summary>
    /// Applies default values for quick install mode.
    /// </summary>
    public override async Task ApplyQuickInstallDefaultsAsync()
    {
        if (Context == null)
            return;

        // Load default settings
        var settings = _settingsService.LoadSettings();

        // Use root user for quick install (no password needed)
        Context.Username = "root";
        Context.Password = string.Empty;
        Context.ConfirmPassword = string.Empty;
        Context.CreateUser = false;
        Context.WslVersion = settings.DefaultWslVersion;

        _logger.LogInformation("Applied quick install user defaults: Username=root, WSL Version={Version}", 
            Context.WslVersion);
    }
}
