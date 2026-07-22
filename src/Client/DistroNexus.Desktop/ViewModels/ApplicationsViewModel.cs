using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Desktop.ViewModels;

public sealed partial class ApplicationsViewModel : ObservableObject
{
    private readonly IWslgApplicationService _applications;
    public ObservableCollection<WslgApplication> Applications { get; } = [];
    public ObservableCollection<WslgApplication> FilteredApplications { get; } = [];
    [ObservableProperty] private WslgApplication? _selectedApplication;
    [ObservableProperty] private string _distributionName = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _status = L("Applications_SelectDistribution", "Select a WSL distribution to discover applications.");
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private bool _isBusy;
    public ApplicationsViewModel(IWslgApplicationService applications) => _applications=applications;
    partial void OnSearchTextChanged(string value) => Filter();
    private void Filter() { FilteredApplications.Clear(); foreach(var app in Applications.Where(a => string.IsNullOrWhiteSpace(SearchText) || a.Name.Contains(SearchText,StringComparison.OrdinalIgnoreCase) || a.Categories.Any(c=>c.Contains(SearchText,StringComparison.OrdinalIgnoreCase)))) FilteredApplications.Add(app); }
    [RelayCommand] private async Task RefreshAsync(CancellationToken ct=default) { IsBusy=true; try { var s=await _applications.GetStatusAsync(DistributionName.Trim(),ct); IsAvailable=s.IsAvailable; Status=s.Reason; Applications.Clear(); if(!s.IsAvailable)return; foreach(var a in await _applications.DiscoverAsync(DistributionName.Trim(),ct)) Applications.Add(a); Filter(); Status=string.Format(L("Applications_Discovered", "{0} applications discovered."), Applications.Count); } catch(Exception ex) { Status=MainViewModel.FormatAlertMessage(ex); } finally{IsBusy=false;} }
    [RelayCommand] private async Task LaunchAsync(CancellationToken ct=default) { if(SelectedApplication is null)return; var r=await _applications.LaunchAsync(SelectedApplication,ct); Status=r.Diagnostic; }
    [RelayCommand] private async Task TogglePinAsync(CancellationToken ct=default) { if(SelectedApplication is null)return; await _applications.SetPinnedAsync(SelectedApplication.Id,!SelectedApplication.IsPinned,ct); var i=Applications.IndexOf(SelectedApplication); Applications[i]=SelectedApplication with { IsPinned=!SelectedApplication.IsPinned }; SelectedApplication=Applications[i]; Filter(); }
    [RelayCommand] private void CopyCommand() { if(SelectedApplication is not null) System.Windows.Clipboard.SetText(string.Join(' ',new[]{SelectedApplication.Executable}.Concat(SelectedApplication.Arguments))); }
    [RelayCommand] private async Task RevealEntryAsync(CancellationToken ct=default) { if(SelectedApplication is null)return; Status=(await _applications.RevealAsync(SelectedApplication,ct)).Diagnostic; }
    private static string L(string key, string fallback) => Properties.Resources.ResourceManager.GetString(key) ?? fallback;
}
