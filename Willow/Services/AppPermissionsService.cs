using Microsoft.Win32;
using System;

namespace Willow.Services;

public static class AppPermissionsService
{
    private const string ConsentStoreBase =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    // Effective permission requires BOTH hives to allow:
    //   HKLM controls the device-level toggle ("Camera access - Anyone using this device...")
    //   HKCU controls the user-level toggle ("Let apps access your camera")
    // If either says Deny the capability is blocked, matching Windows Settings behaviour.
    public static bool GetGlobalToggle(string capability) =>
        ReadHiveAllowed(Registry.CurrentUser,  capability) &&
        ReadHiveAllowed(Registry.LocalMachine, capability);

    // Writes the same value to both hives so both Windows Settings toggles stay in sync.
    public static void SetGlobalToggle(string capability, bool allow)
    {
        var str = allow ? "Allow" : "Deny";
        RegistryService.WriteValue("CurrentUser",  $@"{ConsentStoreBase}\{capability}", "Value", str, "String");
        RegistryService.WriteValue("LocalMachine", $@"{ConsentStoreBase}\{capability}", "Value", str, "String");
    }

    private static bool ReadHiveAllowed(RegistryKey hive, string capability)
    {
        try
        {
            using var key = hive.OpenSubKey($@"{ConsentStoreBase}\{capability}");
            var val = key?.GetValue("Value") as string;
            return !string.Equals(val, "Deny", StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }
}
