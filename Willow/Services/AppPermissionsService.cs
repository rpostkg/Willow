using Microsoft.Win32;
using System;

namespace Willow.Services;

public static class AppPermissionsService
{
    private const string ConsentStoreBase =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    // Windows Settings "Camera/Microphone/Location access" master toggle is stored in HKLM.
    // HKCU root Value was written incorrectly by earlier Willow code; Windows Settings never
    // reads it for the global toggle display, so we read only HKLM to match what Settings shows.
    public static bool GetGlobalToggle(string capability)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{ConsentStoreBase}\{capability}");
            var val = key?.GetValue("Value") as string;
            return !string.Equals(val, "Deny", StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    // Writes to HKLM (the device-level toggle Windows Settings controls) and also keeps
    // HKCU root in sync, repairing any stale "Deny" written by earlier Willow versions.
    public static void SetGlobalToggle(string capability, bool allow)
    {
        var str = allow ? "Allow" : "Deny";
        RegistryService.WriteValue("LocalMachine", $@"{ConsentStoreBase}\{capability}", "Value", str, "String");
        RegistryService.WriteValue("CurrentUser",  $@"{ConsentStoreBase}\{capability}", "Value", str, "String");
    }
}
