using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Windows;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// View model for a single WSL instance.
/// </summary>
public partial class WslInstanceViewModel : ObservableObject
{
    private readonly IWslManagerService _wslManager;
    private readonly ITerminalService _terminalService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly ITagService _tagService;
    private readonly IBackupService _backupService;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Event raised when the instance requests a refresh of the main list (e.g. after deletion).
    /// </summary>
    public event EventHandler? RefreshRequested;

    [ObservableProperty]
    private WslInstance _instance;

    [ObservableProperty]
    private bool _isLoadingDiskSize;

    [ObservableProperty]
    private bool _isForceRefreshing;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>True when this instance is checked in multi-select mode.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Docker integration status for this instance as loaded on the dashboard.
    /// null = not yet loaded / not applicable.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDockerStatusVisible))]
    private bool? _dockerIntegrationEnabled;

    /// <summary>True when Docker status has been queried and should be shown on the card.</summary>
    public bool IsDockerStatusVisible => _dockerIntegrationEnabled.HasValue;

    public string Name => Instance.Name;
    public string State => Instance.State == "Running" ? Properties.Resources.StateRunning : 
                          (Instance.State == "Stopped" ? Properties.Resources.StateStopped : Instance.State);
    public string RawState => Instance.State;
    public bool IsRunning => Instance.IsRunning;
    public bool IsWslV2 => Instance.Version == 2;
    public string InstallPath => WslInstance.NormalizeWindowsPath(Instance.InstallPath);
    public string Distribution => Instance.Distribution;
    public long DiskSize => Instance.Size;
    
    /// <summary>
    /// Gets the disk size formatted for display.
    /// Shows "Click to load" if size is unknown and instance is running.
    /// </summary>
    public string DiskSizeDisplay
    {
        get
        {
            if (IsForceRefreshing)
                return Properties.Resources.StatusForceRefreshing;
            
            if (IsLoadingDiskSize)
                return Properties.Resources.StatusLoading;
            
            if (DiskSize <= 0 && IsRunning)
                return Properties.Resources.StatusClickToLoad;
            
            if (DiskSize <= 0)
                return Properties.Resources.StatusUnknown;
            
            return FormatFileSize(DiskSize);
        }
    }

    public WslInstanceViewModel(
        WslInstance instance,
        IWslManagerService wslManager,
        ITerminalService terminalService,
        ISettingsService settingsService,
        ILogger logger,
        ITagService tagService,
        IBackupService backupService,
        IServiceProvider serviceProvider)
    {
        _instance = instance;
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _terminalService = terminalService ?? throw new ArgumentNullException(nameof(terminalService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return Properties.Resources.StatusUnknown;
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    private async Task ShowAlert(string title, string message)
    {
        var uiMessageBox = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            MaxWidth = 400
        };

        await uiMessageBox.ShowDialogAsync();
    }

    /// <summary>
    /// Forces a complete refresh of this instance, starting it and loading full information.
    /// Shows a confirmation dialog before proceeding.
    /// </summary>
    [RelayCommand]
    private async Task ForceRefreshAsync()
    {
        if (IsForceRefreshing)
            return;

        try
        {
            // Show confirmation dialog
            var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
                Properties.Resources.ConfirmForceRefreshTitle,
                Properties.Resources.ConfirmForceRefreshMessage,
                "Force Refresh");

            if (!confirmed)
                return;

            IsForceRefreshing = true;
            OnPropertyChanged(nameof(DiskSizeDisplay));

            _logger.LogInformation("Starting force refresh for instance {Name}", Name);

            // Call force refresh method
            var refreshedInstance = await _wslManager.ForceRefreshInstanceAsync(Name);

            if (refreshedInstance != null)
            {
                Instance = refreshedInstance;
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(RawState));
                OnPropertyChanged(nameof(IsRunning));

                // Auto-load disk size
                await LoadDiskSizeAsync();

                _logger.LogInformation("Force refresh completed for {Name}", Name);
            }
            else
            {
                await ShowAlert(Properties.Resources.ErrorTitle, Properties.Resources.ErrorForceRefreshNull);
                _logger.LogError("Force refresh returned null for instance {Name}", Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Force refresh failed for instance {Name}", Name);
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorForceRefreshEx, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsForceRefreshing = false;
            OnPropertyChanged(nameof(DiskSizeDisplay));
        }
    }

    /// <summary>
    /// Loads the disk size for this instance.
    /// Only works reliably when the instance is running to avoid auto-starting stopped instances.
    /// </summary>
    [RelayCommand]
    public async Task LoadDiskSizeAsync()
    {
        if (IsLoadingDiskSize || DiskSize > 0)
            return;

        try
        {
            IsLoadingDiskSize = true;
            OnPropertyChanged(nameof(DiskSizeDisplay));
            
            _logger.LogInformation("Loading disk size for instance {Name}", Name);
            
            var size = await _wslManager.GetInstanceDiskSizeAsync(Name);
            
            Instance.Size = size;
            OnPropertyChanged(nameof(DiskSize));
            OnPropertyChanged(nameof(DiskSizeDisplay));
            
            _logger.LogInformation("Loaded disk size for {Name}: {Size} bytes", Name, size);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load disk size for instance {Name}", Name);
        }
        finally
        {
            IsLoadingDiskSize = false;
            OnPropertyChanged(nameof(DiskSizeDisplay));
        }
    }

    /// <summary>
    /// Updates the instance state and notifies property changes.
    /// </summary>
    /// <param name="newState">The new state value.</param>
    public void UpdateState(string newState)
    {
        Instance.State = newState;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(RawState));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(DiskSizeDisplay));
    }

    /// <summary>
    /// Updates the disk size and notifies property changes.
    /// </summary>
    /// <param name="newSize">The new disk size in bytes.</param>
    public void UpdateDiskSize(long newSize)
    {
        Instance.Size = newSize;
        OnPropertyChanged(nameof(Instance));
        OnPropertyChanged(nameof(DiskSize));
        OnPropertyChanged(nameof(DiskSizeDisplay));
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        try
        {
            _logger.LogInformation("Starting instance {Name} with keep-alive task", Name);
            
            // Start instance with a background keep-alive process
            var success = await _wslManager.StartInstanceWithKeepAliveAsync(Name);
            
            if (success)
            {
                Instance.State = "Running";
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(RawState));
                OnPropertyChanged(nameof(IsRunning));
                _logger.LogInformation("Instance {Name} started with keep-alive task", Name);
            }
            else
            {
                await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorStartInstanceFailed, Name));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start instance {Name}", Name);
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorStartInstanceEx, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        try
        {
            var settings = _settingsService.LoadSettings();

            if (settings.ShowConfirmationDialogs)
            {
                // Show custom confirmation dialog
                var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
                    Properties.Resources.ConfirmStopTitle,
                    string.Format(Properties.Resources.ConfirmStopMessage, Name),
                    Properties.Resources.ButtonStop);

                if (!confirmed)
                {
                    _logger.LogInformation("User canceled stop operation for instance {Name}", Name);
                    return;
                }
            }

            _logger.LogInformation("Stopping instance {Name}", Name);
            
            var success = await _wslManager.StopInstanceAsync(Name);
            
            if (success)
            {
                Instance.State = "Stopped";
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(RawState));
                OnPropertyChanged(nameof(IsRunning));
            }
            else
            {
                await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorStopInstanceFailed, Name));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop instance {Name}", Name);
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorStopInstanceEx, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (IsBusy) return;

        var settings = _settingsService.LoadSettings();

        if (settings.ShowConfirmationDialogs)
        {
            // Use custom confirmation dialog
            var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
                Properties.Resources.ConfirmRemoveTitle,
                string.Format(Properties.Resources.ConfirmRemoveMessage, Name),
                Properties.Resources.ButtonRemove);

            if (!confirmed)
                return;
        }

        var instanceName = Name;

        // Check if a backup schedule exists and warn user (E-04-2)
        try
        {
            var schedules = await _backupService.GetSchedulesAsync();
            var hasSchedule = schedules.Any(s =>
                string.Equals(s.Name, instanceName, StringComparison.OrdinalIgnoreCase));
            if (hasSchedule)
            {
                var confirm = new Wpf.Ui.Controls.MessageBox
                {
                    Title = Properties.Resources.ConfirmRemoveTitle,
                    Content = string.Format(Properties.Resources.ConfirmRemoveWithBackupMessage, instanceName),
                    PrimaryButtonText = Properties.Resources.ButtonRemove,
                    CloseButtonText = Properties.Resources.ButtonClose
                };
                var result = await confirm.ShowDialogAsync();
                if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check backup schedule for instance {Name}", instanceName);
        }

        try
        {
            IsBusy = true;
            _logger.LogInformation("Removing instance {Name}", instanceName);

            await _wslManager.RemoveInstanceAsync(instanceName);

            try
            {
                await _tagService.DeleteInstanceTagsAsync(instanceName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tag cleanup failed for removed instance {Name}", instanceName);
            }

            await ShowAlert(Properties.Resources.SuccessTitle, string.Format(Properties.Resources.SuccessInstanceRemoved, instanceName));

            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove instance {Name}", Name);
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorRemoveInstanceEx, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenTerminalAsync()
    {
        try
        {
            _logger.LogInformation("Opening terminal for instance {Name}", Name);
            
            var success = await _terminalService.OpenTerminalAsync(Name);
            
            if (success)
            {
                // If the terminal opened successfully, the instance is now running
                if (!IsRunning)
                {
                    UpdateState("Running");
                    
                    // Also trigger disk size load since it might now be available
                    _ = LoadDiskSizeAsync();
                }
            }
            else
            {
                await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorOpenTerminalFailed, Name));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open terminal for instance {Name}", Name);
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorOpenTerminalEx, MainViewModel.FormatAlertMessage(ex)));
        }
    }

    [RelayCommand]
    private async Task MoveAsync()
    {
        if (IsBusy) return;

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = string.Format(Properties.Resources.SelectMoveLocationTitle, Name)
        };

        if (dialog.ShowDialog() != true)
            return;

        var newPath = dialog.FolderName;

        var confirmed = DistroNexus.Desktop.Views.ConfirmDialog.Show(
            Properties.Resources.ConfirmMoveTitle,
            string.Format(Properties.Resources.ConfirmMoveMessage, Name, newPath),
            "Move");

        if (!confirmed)
            return;

        try
        {
            IsBusy = true;
            _logger.LogInformation("Moving instance {Name} to {NewPath}", Name, newPath);
            
            await _wslManager.MoveInstanceAsync(Name, newPath);
            
            Instance.InstallPath = newPath;
            OnPropertyChanged(nameof(InstallPath));
            
            await ShowAlert(Properties.Resources.SuccessTitle, string.Format(Properties.Resources.SuccessInstanceMoved, Name));

            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move instance {Name}", Name);
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorMoveInstanceEx, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        if (IsBusy) return;

        var inputDialog = new Wpf.Ui.Controls.MessageBox
        {
            Title = Properties.Resources.RenameTitle,
            Content = new System.Windows.Controls.TextBox
            {
                Text = Name,
                MinWidth = 200
            },
            PrimaryButtonText = Properties.Resources.ButtonRename,
            CloseButtonText = Properties.Resources.ButtonCancel
        };

        var result = await inputDialog.ShowDialogAsync();
        
        if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;

        var textBox = inputDialog.Content as System.Windows.Controls.TextBox;
        var newName = textBox?.Text?.Trim();

        if (string.IsNullOrEmpty(newName) || newName == Name)
            return;

        try
        {
            var oldName = Name;
            IsBusy = true;
            _logger.LogInformation("Renaming instance {OldName} to {NewName}", oldName, newName);

            await _wslManager.RenameInstanceAsync(oldName, newName);

            Instance.Name = newName;
            OnPropertyChanged(nameof(Name));

            try
            {
                await _tagService.RenameInstanceTagsAsync(oldName, newName);
            }
            catch (Exception tagEx)
            {
                _logger.LogWarning(tagEx, "Tag migration failed for rename {OldName} -> {NewName}", oldName, newName);
            }

            await ShowAlert(Properties.Resources.SuccessTitle, string.Format(Properties.Resources.SuccessInstanceRenamed, newName));

            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename instance {Name}", Name);
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorRenameInstanceEx, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SetCredentialsAsync()
    {
        if (IsBusy) return;

        // Username Dialog
        var userTextBox = new System.Windows.Controls.TextBox 
        { 
            Text = "root",
            MinWidth = 200 
        };
        
        var userDialog = new Wpf.Ui.Controls.MessageBox
        {
            Title = Properties.Resources.SetCredentialsTitle,
            Content = new System.Windows.Controls.StackPanel
            {
                Children = 
                {
                    new System.Windows.Controls.TextBlock { Text = Properties.Resources.PromptEnterUsername, Margin = new Thickness(0,0,0,10) },
                    userTextBox
                }
            },
            PrimaryButtonText = "Next",
            CloseButtonText = Properties.Resources.ButtonCancel
        };

        var userResult = await userDialog.ShowDialogAsync();
        if (userResult != Wpf.Ui.Controls.MessageBoxResult.Primary) return;
        
        var username = userTextBox.Text;
        if (string.IsNullOrEmpty(username)) return;

        // Password Dialog
        var passwordBox = new System.Windows.Controls.PasswordBox { MinWidth = 200 };
        
        var passDialog = new Wpf.Ui.Controls.MessageBox
        {
            Title = Properties.Resources.SetCredentialsTitle,
            Content = new System.Windows.Controls.StackPanel
            {
                Children = 
                {
                    new System.Windows.Controls.TextBlock { Text = Properties.Resources.PromptEnterPassword, Margin = new Thickness(0,0,0,10) },
                    passwordBox
                }
            },
            PrimaryButtonText = "OK",
            CloseButtonText = Properties.Resources.ButtonCancel
        };

        var passResult = await passDialog.ShowDialogAsync();
        if (passResult != Wpf.Ui.Controls.MessageBoxResult.Primary) return;
        
        var password = passwordBox.Password;

        try
        {
            IsBusy = true;
            _logger.LogInformation("Setting credentials for instance {Name}", Name);
            
            await _wslManager.SetCredentialsAsync(Name, username, password);
            
            await ShowAlert(Properties.Resources.SuccessTitle, string.Format(Properties.Resources.SuccessCredentialsSet, Name));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set credentials for instance {Name}", Name);
            await ShowAlert(Properties.Resources.ErrorTitle, string.Format(Properties.Resources.ErrorSetCredentialsEx, MainViewModel.FormatAlertMessage(ex)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Opens the InstanceDetailDialog for this instance.
    /// </summary>
    [RelayCommand]
    private void OpenDetails()
    {
        var wslManager = _serviceProvider.GetRequiredService<IWslManagerService>();
        var dockerSvc = _serviceProvider.GetRequiredService<IDockerIntegrationService>();
        var networkSvc = _serviceProvider.GetRequiredService<INetworkService>();
        var backupSvc = _serviceProvider.GetRequiredService<IBackupService>();
        var wslConfigSvc = _serviceProvider.GetRequiredService<IWslConfigService>();
        var tagSvc = _serviceProvider.GetRequiredService<ITagService>();
        var dialogSvc = _serviceProvider.GetRequiredService<IDialogService>();

        var vm = new InstanceDetailViewModel(this, wslManager, dockerSvc, networkSvc, backupSvc, wslConfigSvc, tagSvc, dialogSvc);
        var dialog = new InstanceDetailDialog(vm)
        {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    /// <summary>
    /// Initiates disk compaction for this instance (navigates to Disk tab).
    /// </summary>
    [RelayCommand]
    private void CompactDisk()
    {
        OpenDetailsCommand.Execute(null);
    }

    /// <summary>
    /// Exports this instance to a TAR file.
    /// </summary>
    [RelayCommand]
    private async Task ExportInstanceAsync()
    {
        var dialogSvc = _serviceProvider.GetRequiredService<IDialogService>();

        // Check running status — prompt auto-stop
        if (IsRunning)
        {
            bool stopOk = await dialogSvc.ShowConfirmAsync(
                Properties.Resources.Export_StopPromptTitle,
                Properties.Resources.Export_StopPrompt);
            if (!stopOk) return;
        }

        // Open SaveFileDialog
        var dlg = new SaveFileDialog
        {
            Title = Properties.Resources.Export_SaveDialogTitle,
            Filter = "TAR archive (*.tar)|*.tar|All files (*.*)|*.*",
            FileName = $"{Name}-{DateTime.Now:yyyyMMdd}.tar"
        };
        if (dlg.ShowDialog() != true) return;

        string destPath = dlg.FileName;
        bool force = System.IO.File.Exists(destPath);

        IsBusy = true;
        try
        {
            await _wslManager.ExportInstanceAsync(Name, destPath, force);

            long fileSize = new System.IO.FileInfo(destPath).Length;
            string sizeDisplay = FormatFileSize(fileSize);
            await dialogSvc.ShowAlertAsync(
                Properties.Resources.Export_CompleteTitle,
                string.Format(Properties.Resources.Export_Complete, destPath, sizeDisplay));
        }
        catch (WslOperationException ex)
        {
            try { if (System.IO.File.Exists(destPath)) System.IO.File.Delete(destPath); } catch { /* best effort */ }
            await dialogSvc.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, $"[{(int)ex.Code}] {ex.Message}"));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            try { if (System.IO.File.Exists(destPath)) System.IO.File.Delete(destPath); } catch { /* best effort */ }
            await dialogSvc.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }
}