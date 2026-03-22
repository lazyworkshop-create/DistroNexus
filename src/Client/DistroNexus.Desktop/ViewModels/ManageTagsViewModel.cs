using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DistroNexus.Core.Interfaces;
using System.Collections.ObjectModel;

namespace DistroNexus.Desktop.ViewModels;

/// <summary>
/// Represents a single tag entry in the Manage Tags list, including its usage count.
/// </summary>
public partial class TagItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private int _usedByCount;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _pendingName;

    public TagItemViewModel(string name, int usedByCount)
    {
        _name = name;
        _usedByCount = usedByCount;
        _pendingName = name;
    }
}

/// <summary>
/// ViewModel for the Manage Tags section in the Settings page.
/// Covers requirement E-02-9.
/// </summary>
public partial class ManageTagsViewModel : ObservableObject
{
    private readonly ITagService _tagService;
    private readonly IWslManagerService _wslManager;
    private readonly IDialogService _dialogService;
    private bool _initialized;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<TagItemViewModel> _tags = [];

    public ManageTagsViewModel(
        ITagService tagService,
        IWslManagerService wslManager,
        IDialogService dialogService)
    {
        _tagService = tagService ?? throw new ArgumentNullException(nameof(tagService));
        _wslManager = wslManager ?? throw new ArgumentNullException(nameof(wslManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    public async Task LoadAsync()
    {
        if (_initialized) return;
        _initialized = true;

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var instances = await _wslManager.GetInstancesAsync();
            var allTags = await _tagService.GetAllTagsAsync();

            // Count usage per tag
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in allTags) counts[tag] = 0;

            foreach (var inst in instances)
            {
                var instTags = await _tagService.GetTagsAsync(inst.Name);
                foreach (var t in instTags)
                    if (counts.ContainsKey(t)) counts[t]++;
            }

            Tags = new ObservableCollection<TagItemViewModel>(
                allTags.Select(t => new TagItemViewModel(t, counts.TryGetValue(t, out int c) ? c : 0)));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RenameTagAsync(TagItemViewModel item)
    {
        if (item is null) return;
        string oldName = item.Name;
        string newName = item.PendingName.Trim();

        if (string.IsNullOrWhiteSpace(newName) || newName.Equals(oldName, StringComparison.OrdinalIgnoreCase))
        {
            item.IsRenaming = false;
            item.PendingName = oldName;
            return;
        }

        IsLoading = true;
        try
        {
            var instances = await _wslManager.GetInstancesAsync();
            foreach (var inst in instances)
            {
                var currentTags = await _tagService.GetTagsAsync(inst.Name);
                if (currentTags.Any(t => t.Equals(oldName, StringComparison.OrdinalIgnoreCase)))
                {
                    var updated = currentTags
                        .Select(t => t.Equals(oldName, StringComparison.OrdinalIgnoreCase) ? newName : t)
                        .ToList();
                    await _tagService.SetTagsAsync(inst.Name, updated);
                }
            }

            item.Name = newName;
            item.PendingName = newName;
            item.IsRenaming = false;
        }
        catch (Exception ex)
        {
            item.IsRenaming = false;
            item.PendingName = oldName;
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteTagAsync(TagItemViewModel item)
    {
        if (item is null) return;

        bool confirmed = await _dialogService.ShowConfirmAsync(
            Properties.Resources.ManageTags_DeleteConfirmTitle,
            string.Format(Properties.Resources.ManageTags_DeleteConfirm, item.Name));
        if (!confirmed) return;

        IsLoading = true;
        try
        {
            var instances = await _wslManager.GetInstancesAsync();
            foreach (var inst in instances)
            {
                await _tagService.RemoveTagAsync(inst.Name, item.Name);
            }

            Tags.Remove(item);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task BulkDeleteAsync()
    {
        var selected = Tags.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0) return;

        bool confirmed = await _dialogService.ShowConfirmAsync(
            Properties.Resources.ManageTags_BulkDeleteTitle,
            string.Format(Properties.Resources.ManageTags_BulkDeleteConfirm, selected.Count));
        if (!confirmed) return;

        IsLoading = true;
        try
        {
            var instances = await _wslManager.GetInstancesAsync();
            foreach (var item in selected)
            {
                foreach (var inst in instances)
                    await _tagService.RemoveTagAsync(inst.Name, item.Name);
                Tags.Remove(item);
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                Properties.Resources.ErrorTitle,
                string.Format(Properties.Resources.ErrorGenericOperation, ex.Message));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
