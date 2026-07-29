using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.ViewModels;

public sealed partial class ApplicationsViewModel : ObservableObject
{
    private readonly IPowerShellModuleClient _module;
    private string? _discoveryToken;
    public ObservableCollection<WslgApplicationProjection> Applications { get; } = [];
    public ObservableCollection<WslgApplicationProjection> FilteredApplications { get; } = [];
    [ObservableProperty] private WslgApplicationProjection? _selectedApplication;
    [ObservableProperty] private string _distributionName = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _status = L("Applications_SelectDistribution", "Select a WSL distribution to discover applications.");
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private bool _isBusy;
    public ApplicationsViewModel(IPowerShellModuleClient module) => _module=module;
    partial void OnSearchTextChanged(string value) => Filter();
    private void Filter() { FilteredApplications.Clear(); foreach(var app in Applications.Where(a => string.IsNullOrWhiteSpace(SearchText) || a.Name.Contains(SearchText,StringComparison.OrdinalIgnoreCase) || a.Categories.Any(c=>c.Contains(SearchText,StringComparison.OrdinalIgnoreCase)))) FilteredApplications.Add(app); }
    [RelayCommand] private async Task RefreshAsync(CancellationToken ct=default) { IsBusy=true; _discoveryToken=null; try { var result=await _module.DiscoverWslgApplicationsAsync(DistributionName.Trim(),ct); IsAvailable=result.Status.IsAvailable; Status=result.Status.Reason; Applications.Clear(); if(!result.Status.IsAvailable)return; _discoveryToken=result.DiscoveryToken; foreach(var a in result.Applications) Applications.Add(a); Filter(); Status=string.Format(L("Applications_Discovered", "{0} applications discovered."), Applications.Count); } catch(Exception ex) { Status=MainViewModel.FormatAlertMessage(ex); } finally{IsBusy=false;} }
    [RelayCommand] private async Task LaunchAsync(CancellationToken ct=default) { if(SelectedApplication is null || _discoveryToken is null)return; try { var result=await _module.LaunchWslgApplicationAsync(_discoveryToken,SelectedApplication.ApplicationId,ct); Status=result.Diagnostic; if(!result.Succeeded)_discoveryToken=null; } catch(Exception ex) { _discoveryToken=null; Status=MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task TogglePinAsync(CancellationToken ct=default) { if(SelectedApplication is null || _discoveryToken is null)return; try { var result=await _module.SetWslgApplicationPinAsync(_discoveryToken,SelectedApplication.ApplicationId,!SelectedApplication.IsPinned,ct); if(!result.Succeeded){_discoveryToken=null; Status=result.Diagnostic; return;} var i=Applications.IndexOf(SelectedApplication); Applications[i]=SelectedApplication with { IsPinned=!SelectedApplication.IsPinned }; SelectedApplication=Applications[i]; Filter(); } catch(Exception ex) { _discoveryToken=null; Status=MainViewModel.FormatAlertMessage(ex); } }
    [RelayCommand] private async Task RevealEntryAsync(CancellationToken ct=default) { if(SelectedApplication is null || _discoveryToken is null)return; try { var result=await _module.RevealWslgApplicationAsync(_discoveryToken,SelectedApplication.ApplicationId,ct); Status=result.Diagnostic; if(!result.Succeeded)_discoveryToken=null; } catch(Exception ex) { _discoveryToken=null; Status=MainViewModel.FormatAlertMessage(ex); } }
    private static string L(string key, string fallback) => Properties.Resources.ResourceManager.GetString(key) ?? fallback;
}
