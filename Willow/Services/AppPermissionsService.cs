using Microsoft.Win32;
using System;

namespace Willow.Services;

public static class AppPermissionsService
{
    private const string ConsentStoreBase =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    public static bool GetGlobalToggle(string capability)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{ConsentStoreBase}\{capability}");
            var val = key?.GetValue("Value") as string;
            return !string.Equals(val, "Deny", StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    public static void SetGlobalToggle(string capability, bool allow)
    {
        RegistryService.WriteValue("CurrentUser", $@"{ConsentStoreBase}\{capability}", "Value",
            allow ? "Allow" : "Deny", "String");
    }
}
