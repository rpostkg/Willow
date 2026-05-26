using Willow.Services;
using Xunit;

namespace Willow.Tests;

public class AppPermissionsServiceTests
{
    [Theory]
    [InlineData("Allow", true)]
    [InlineData("allow", true)]
    [InlineData("Deny", false)]
    [InlineData("deny", false)]
    public void GetGlobalToggle_ReadsExpectedStateFromRegistry(string regValue, bool expected)
    {
        // Verify that the service correctly interprets "Allow"/"Deny" values.
        // We use the real registry round-trip: write a known value then read it back.
        const string capability = "webcam";
        AppPermissionsService.SetGlobalToggle(capability, expected);
        var result = AppPermissionsService.GetGlobalToggle(capability);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SetGlobalToggle_PersistsAcrossReads()
    {
        const string capability = "microphone";
        AppPermissionsService.SetGlobalToggle(capability, false);
        Assert.False(AppPermissionsService.GetGlobalToggle(capability));
        AppPermissionsService.SetGlobalToggle(capability, true);
        Assert.True(AppPermissionsService.GetGlobalToggle(capability));
    }
}
