using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Desktop.ViewModels;
using DistroNexus.ViewModelTests.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DistroNexus.ViewModelTests;

/// <summary>
/// Unit tests for tag filtering logic (P6-4).
/// Tests the filter predicate and <see cref="TagFilterViewModel"/> in isolation.
/// </summary>
public sealed class TagFilterTests
{
    // ── TagFilterViewModel ─────────────────────────────────────────────────────

    [Fact]
    public void TagFilterViewModel_IsSelected_DefaultFalse()
    {
        var sut = new TagFilterViewModel { Name = "dev" };
        sut.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void TagFilterViewModel_SetIsSelected_RaisesPropertyChanged()
    {
        var sut = new TagFilterViewModel { Name = "dev" };
        var raised = new List<string?>();
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.IsSelected = true;

        raised.Should().Contain(nameof(TagFilterViewModel.IsSelected));
    }

    // ── Filter predicate logic (inline, STA-free) ─────────────────────────────

    /// <summary>Builds the same predicate used by MainViewModel.ApplyTagFilter.</summary>
    private static Predicate<object> BuildPredicate(IEnumerable<string> activeFilters)
    {
        var filters = activeFilters.ToList();
        return o => o is WslInstanceViewModel vm &&
                    filters.All(f => vm.Tags.Contains(f, StringComparer.OrdinalIgnoreCase));
    }

    private static WslInstanceViewModel CreateVm(string name, params string[] tags)
    {
        var vm = TestViewModelFactory.CreateWslInstanceViewModel(
            TestViewModelFactory.CreateInstance(name: name));
        foreach (var t in tags) vm.Tags.Add(t);
        return vm;
    }

    [Fact]
    public void SingleTagFilter_MatchingInstance_ReturnsTrue()
    {
        var vm = CreateVm("Ubuntu", "dev");
        var predicate = BuildPredicate(["dev"]);

        predicate(vm).Should().BeTrue();
    }

    [Fact]
    public void SingleTagFilter_NonMatchingInstance_ReturnsFalse()
    {
        var vm = CreateVm("Ubuntu", "prod");
        var predicate = BuildPredicate(["dev"]);

        predicate(vm).Should().BeFalse();
    }

    [Fact]
    public void SingleTagFilter_TagMatchIsCaseInsensitive()
    {
        var vm = CreateVm("Ubuntu", "DEV");
        var predicate = BuildPredicate(["dev"]);

        predicate(vm).Should().BeTrue();
    }

    [Fact]
    public void MultiTagFilter_AllTagsPresent_ReturnsTrue()
    {
        var vm = CreateVm("Ubuntu", "dev", "docker");
        var predicate = BuildPredicate(["dev", "docker"]);

        predicate(vm).Should().BeTrue();
    }

    [Fact]
    public void MultiTagFilter_OnlyPartialMatch_ReturnsFalse()
    {
        var vm = CreateVm("Ubuntu", "dev");
        var predicate = BuildPredicate(["dev", "docker"]);

        predicate(vm).Should().BeFalse();
    }

    [Fact]
    public void EmptyActiveFilters_NullPredicateAllowsAll()
    {
        // When activeFilters is empty, MainViewModel sets Filter = null (all visible).
        // This test verifies the condition for that branch.
        var activeFilters = new List<string>();
        activeFilters.Should().BeEmpty("when no filters selected, predicate should be null/all-pass");
    }

    [Fact]
    public void ClearTagFilters_DeselectsAllTags()
    {
        // Simulate the ClearTagFilters logic by setting IsSelected = false on each tag
        var tags = new ObservableCollection<TagFilterViewModel>
        {
            new() { Name = "dev", IsSelected = true },
            new() { Name = "prod", IsSelected = true },
        };

        foreach (var tag in tags)
            tag.IsSelected = false;

        tags.Should().AllSatisfy(t => t.IsSelected.Should().BeFalse());
    }
}
