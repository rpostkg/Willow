using Microsoft.Win32;
using Willow.Services;
using Xunit;

namespace Willow.Tests;

public class AppPermissionsServiceTests
{
    private const string ConsentBase =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    // Helper: write directly to HKCU only (doesn't need admin).
    private static void SetHkcu(string capability, string value) =>
        Registry.CurrentUser
            .CreateSubKey($@"{ConsentBase}\{capability}")
            .SetValue("Value", value);

    [Fact]
    public void GetGlobalToggle_WhenHkcuDeny_ReturnsFalse()
    {
        // HKCU Deny alone makes the effective state Off, regardless of HKLM.
        SetHkcu("webcam", "Deny");
        Assert.False(AppPermissionsService.GetGlobalToggle("webcam"));
    }

    [Fact]
    public void SetGlobalToggle_False_WritesHkcuDenyAndReadsFalse()
    {
        // Deny writes to HKCU always work; HKLM write may silently fail without admin
        // but HKCU Deny alone is sufficient to return false.
        AppPermissionsService.SetGlobalToggle("microphone", false);
        Assert.False(AppPermissionsService.GetGlobalToggle("microphone"));
    }
}
