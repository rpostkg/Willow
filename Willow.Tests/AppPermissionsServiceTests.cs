using Microsoft.Win32;
using System;
using Willow.Services;
using Xunit;

namespace Willow.Tests;

// Integration tests that require admin rights to write HKLM policy keys.
// Non-admin runs verify read behavior only (policy keys absent → returns true).
public class AppPermissionsServiceTests
{
    private const string AppPrivacyPolicy =
        @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy";
    private const string LocationPolicy =
        @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors";

    [Fact]
    public void GetGlobalToggle_NoPolicyKey_ReturnsTrue()
    {
        // When no policy key is present, capability defaults to enabled.
        // This verifies we don't read stale ConsentStore values anymore.
        using var key = Registry.LocalMachine.OpenSubKey(AppPrivacyPolicy);
        var val = key?.GetValue("LetAppsAccessCamera");
        if (val == null)
        {
            // No policy key set — must return true.
            Assert.True(AppPermissionsService.GetGlobalToggle("webcam"));
        }
        // If a policy key happens to be set on this machine, skip the assertion.
    }

    [Fact]
    public void GetGlobalToggle_IgnoresConsentStore()
    {
        // Writing Deny to ConsentStore (old approach) must NOT make GetGlobalToggle return false.
        const string consentBase =
            @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";
        Registry.CurrentUser.CreateSubKey($@"{consentBase}\webcam").SetValue("Value", "Deny");
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AppPrivacyPolicy);
            var policyVal = key?.GetValue("LetAppsAccessCamera");
            bool expectedFromPolicy = !(policyVal is int i && i == 2);
            Assert.Equal(expectedFromPolicy, AppPermissionsService.GetGlobalToggle("webcam"));
        }
        finally
        {
            // Clean up the HKCU Deny we wrote.
            using var cleanup = Registry.CurrentUser.OpenSubKey($@"{consentBase}\webcam", true);
            cleanup?.DeleteValue("Value", false);
        }
    }

    [Fact]
    public void SetGlobalToggle_False_DoesNotThrow()
    {
        // Admin context: writes DisableLocation=1 to HKLM policy key.
        // Non-admin: HKLM write silently fails — must not throw either way.
        AppPermissionsService.SetGlobalToggle("location", false);
    }

    [Fact]
    public void SetGlobalToggle_True_DoesNotThrow()
    {
        // Deleting a policy key that doesn't exist must not throw.
        AppPermissionsService.SetGlobalToggle("microphone", true);
    }
}
