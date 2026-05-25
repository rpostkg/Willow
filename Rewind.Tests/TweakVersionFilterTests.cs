using Rewind.Models;
using Rewind.Services;
using Xunit;

namespace Rewind.Tests;

public class TweakVersionFilterTests
{
    private static Tweak MakeTweak(int min, int max) =>
        new() { Id = "t", MinVersion = min, MaxVersion = max };

    [Fact]
    public void PassesVersionFilter_BuildWithinRange_ReturnsTrue()
    {
        Assert.True(TweakLoaderService.PassesVersionFilter(MakeTweak(19041, 22621), 20348));
    }

    [Fact]
    public void PassesVersionFilter_BuildBelowMin_ReturnsFalse()
    {
        Assert.False(TweakLoaderService.PassesVersionFilter(MakeTweak(22000, 22621), 19041));
    }

    [Fact]
    public void PassesVersionFilter_BuildAboveMax_ReturnsFalse()
    {
        Assert.False(TweakLoaderService.PassesVersionFilter(MakeTweak(19041, 22000), 26100));
    }

    [Fact]
    public void PassesVersionFilter_BuildEqualsMin_ReturnsTrue()
    {
        Assert.True(TweakLoaderService.PassesVersionFilter(MakeTweak(22000, 26100), 22000));
    }

    [Fact]
    public void PassesVersionFilter_BuildEqualsMax_ReturnsTrue()
    {
        Assert.True(TweakLoaderService.PassesVersionFilter(MakeTweak(19041, 22621), 22621));
    }

    [Fact]
    public void PassesVersionFilter_DefaultRange_AlwaysPasses()
    {
        var tweak = new Tweak { Id = "t" }; // MinVersion=0, MaxVersion=int.MaxValue
        Assert.True(TweakLoaderService.PassesVersionFilter(tweak, 19041));
        Assert.True(TweakLoaderService.PassesVersionFilter(tweak, 26100));
    }
}
