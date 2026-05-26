using Willow.Models;
using Willow.ViewModels;
using System.Linq;
using Xunit;

namespace Willow.Tests;

public class OptimizerViewModelFilterTests
{
    // The OptimizerViewModel constructor calls TweakLoaderService.LoadTweaks() which
    // looks for YAML files in the output directory. In the test environment those files
    // are absent, so Tweaks starts empty. We populate it manually after construction.

    private static OptimizerViewModel MakeVm(params Tweak[] tweaks)
    {
        var vm = new OptimizerViewModel();
        vm.Tweaks.Clear(); // remove any real tweaks loaded from YAML files in the output dir
        foreach (var t in tweaks)
            vm.Tweaks.Add(t);
        return vm;
    }

    private static Tweak T(string name, string category = "General") =>
        new() { Id = name, Name = name, Category = category };

    // ── FilteredTweaks ─────────────────────────────────────────────────────────

    [Fact]
    public void FilteredTweaks_NoQuery_ReturnsAllTweaks()
    {
        var vm = MakeVm(T("Alpha"), T("Beta"), T("Gamma"));
        Assert.Equal(3, vm.FilteredTweaks.Count());
    }

    [Fact]
    public void FilteredTweaks_MatchingQuery_ReturnsOnlyMatches()
    {
        var vm = MakeVm(T("DisableUAC"), T("EnableDarkMode"), T("TelemetryOff"));
        vm.NameQuery = "disable";
        var match = Assert.Single(vm.FilteredTweaks);
        Assert.Equal("DisableUAC", match.Name);
    }

    [Fact]
    public void FilteredTweaks_QueryIsCaseInsensitive()
    {
        var vm = MakeVm(T("DisableUAC"), T("EnableDarkMode"));
        vm.NameQuery = "DISABLE";
        Assert.Single(vm.FilteredTweaks);
    }

    [Fact]
    public void FilteredTweaks_NoMatchingQuery_ReturnsEmpty()
    {
        var vm = MakeVm(T("DisableUAC"), T("EnableDarkMode"));
        vm.NameQuery = "zzznomatch";
        Assert.Empty(vm.FilteredTweaks);
    }

    [Fact]
    public void FilteredTweaks_CategoryAll_ReturnsAllTweaks()
    {
        var vm = MakeVm(T("A", "Privacy"), T("B", "Performance"));
        vm.SelectedCategory = "All";
        Assert.Equal(2, vm.FilteredTweaks.Count());
    }

    [Fact]
    public void FilteredTweaks_SpecificCategory_FiltersToThatCategory()
    {
        var vm = MakeVm(T("A", "Privacy"), T("B", "Performance"), T("C", "Privacy"));
        vm.SelectedCategory = "Privacy";
        var results = vm.FilteredTweaks.ToList();
        Assert.Equal(2, results.Count);
        Assert.All(results, t => Assert.Equal("Privacy", t.Category));
    }

    [Fact]
    public void FilteredTweaks_QueryAndCategory_BothApplied()
    {
        var vm = MakeVm(T("DisableUAC", "Privacy"), T("DisableTelemetry", "Privacy"), T("DisableFoo", "Performance"));
        vm.NameQuery = "Disable";
        vm.SelectedCategory = "Privacy";
        Assert.Equal(2, vm.FilteredTweaks.Count());
    }

    // ── AreTweaksSelected ──────────────────────────────────────────────────────

    [Fact]
    public void AreTweaksSelected_NoEnabledTweaks_ReturnsFalse()
    {
        var vm = MakeVm(T("A"), T("B"));
        Assert.False(vm.AreTweaksSelected);
    }

    [Fact]
    public void AreTweaksSelected_OneTweakEnabled_ReturnsTrue()
    {
        var tweak = T("A");
        tweak.IsEnabled = true;
        var vm = MakeVm(tweak, T("B"));
        Assert.True(vm.AreTweaksSelected);
    }

    // ── CanReviewOrRevert ──────────────────────────────────────────────────────

    [Fact]
    public void CanReviewOrRevert_TweaksSelectedNotInformed_ReturnsFalse()
    {
        var tweak = T("A");
        tweak.IsEnabled = true;
        var vm = MakeVm(tweak);
        vm.IsInformedOfBackups = false;
        Assert.False(vm.CanReviewOrRevert);
    }

    [Fact]
    public void CanReviewOrRevert_TweaksSelectedAndInformed_ReturnsTrue()
    {
        var tweak = T("A");
        tweak.IsEnabled = true;
        var vm = MakeVm(tweak);
        vm.IsInformedOfBackups = true;
        Assert.True(vm.CanReviewOrRevert);
    }

    [Fact]
    public void CanReviewOrRevert_NoTweaksSelectedAndInformed_ReturnsFalse()
    {
        var vm = MakeVm(T("A"), T("B"));
        vm.IsInformedOfBackups = true;
        Assert.False(vm.CanReviewOrRevert);
    }
}
