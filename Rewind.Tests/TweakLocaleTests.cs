using Rewind.Models;
using Rewind.Services;
using System.Collections.Generic;
using Xunit;

namespace Rewind.Tests;

public class TweakLocaleTests
{
    private static Tweak MakeTweak(string id, string name = "Original", string category = "Cat", string desc = "Desc") =>
        new() { Id = id, Name = name, Category = category, Description = desc };

    private static TweakLoaderService.TweakLocaleEntry Entry(string id, string? name = null, string? category = null, string? desc = null) =>
        new() { Id = id, Name = name, Category = category, Description = desc };

    [Fact]
    public void ApplyLocaleEntries_MatchingId_OverridesName()
    {
        var tweaks = new List<Tweak> { MakeTweak("t1", name: "Original") };
        TweakLoaderService.ApplyLocaleEntries(tweaks, [Entry("t1", name: "Localized")]);
        Assert.Equal("Localized", tweaks[0].Name);
    }

    [Fact]
    public void ApplyLocaleEntries_MatchingId_OverridesCategoryAndDescription()
    {
        var tweaks = new List<Tweak> { MakeTweak("t1", category: "OldCat", desc: "OldDesc") };
        TweakLoaderService.ApplyLocaleEntries(tweaks, [Entry("t1", category: "NewCat", desc: "NewDesc")]);
        Assert.Equal("NewCat", tweaks[0].Category);
        Assert.Equal("NewDesc", tweaks[0].Description);
    }

    [Fact]
    public void ApplyLocaleEntries_NullNameInEntry_LeavesOriginalName()
    {
        var tweaks = new List<Tweak> { MakeTweak("t1", name: "Keep") };
        TweakLoaderService.ApplyLocaleEntries(tweaks, [Entry("t1", name: null, category: "NewCat")]);
        Assert.Equal("Keep", tweaks[0].Name);
        Assert.Equal("NewCat", tweaks[0].Category);
    }

    [Fact]
    public void ApplyLocaleEntries_UnknownId_NoTweakModified()
    {
        var tweaks = new List<Tweak> { MakeTweak("t1", name: "Unchanged") };
        TweakLoaderService.ApplyLocaleEntries(tweaks, [Entry("unknown", name: "X")]);
        Assert.Equal("Unchanged", tweaks[0].Name);
    }

    [Fact]
    public void ApplyLocaleEntries_EmptyEntries_NoChanges()
    {
        var tweaks = new List<Tweak> { MakeTweak("t1", name: "Unchanged") };
        TweakLoaderService.ApplyLocaleEntries(tweaks, []);
        Assert.Equal("Unchanged", tweaks[0].Name);
    }

    [Fact]
    public void ApplyLocaleEntries_TwoEntries_OnlyMatchingOneModified()
    {
        var tweaks = new List<Tweak>
        {
            MakeTweak("match", name: "Before"),
            MakeTweak("other", name: "Untouched"),
        };
        TweakLoaderService.ApplyLocaleEntries(tweaks,
        [
            Entry("match", name: "After"),
            Entry("no-match", name: "Should Not Apply"),
        ]);
        Assert.Equal("After", tweaks[0].Name);
        Assert.Equal("Untouched", tweaks[1].Name);
    }
}
