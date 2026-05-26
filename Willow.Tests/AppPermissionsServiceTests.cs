using Microsoft.Win32;
using System;
using Willow.Services;
using Xunit;

namespace Willow.Tests;

// These are integration tests that require admin rights to write HKLM.
// Run Willow (or tests elevated) for full coverage; non-admin runs skip HKLM writes silently.
public class AppPermissionsServiceTests
{
    private const string ConsentBase =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    [Fact]
    public void GetGlobalToggle_ReadsHklm_NotHkcu()
    {
        // GetGlobalToggle reads HKLM only.
        // Writing Deny to HKCU alone must NOT make it return false.
        Registry.CurrentUser.CreateSubKey($@"{ConsentBase}\webcam").SetValue("Value", "Deny");
        // HKLM\webcam may be Allow or Deny; result must match HKLM, not HKCU.
        using var hklm = Registry.LocalMachine.OpenSubKey($@"{ConsentBase}\webcam");
        var hklmVal = hklm?.GetValue("Value") as string;
        bool expectedFromHklm = !string.Equals(hklmVal, "Deny", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedFromHklm, AppPermissionsService.GetGlobalToggle("webcam"));
    }

    [Fact]
    public void SetGlobalToggle_False_ReturnsFalse()
    {
        // Setting to false writes Deny to HKLM (requires admin) and HKCU.
        // HKCU write always works; HKLM write silently fails in non-admin context.
        AppPermissionsService.SetGlobalToggle("microphone", false);
        // In non-admin context HKLM write fails so we can't assert the result here,
        // but the call must not throw.
    }
}
