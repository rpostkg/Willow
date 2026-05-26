using Microsoft.Win32;
using System;

namespace Willow.Services;

public static class AppPermissionsService
{
    // Group Policy keys — these override ConsentStore and the SQLite database in recent Windows 11 builds.
    // ConsentStore registry entries are no longer authoritative since Windows uses a SQLite database
    // (C:\ProgramData\Microsoft\Windows\CapabilityAccessManager) as the true state store.
    private const string AppPrivacyPolicy =
        @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy";
    private const string LocationPolicy =
        @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors";

    private static string? PolicyValueName(string capability) => capability.ToLowerInvariant() switch
    {
        "webcam"     => "LetAppsAccessCamera",
        "microphone" => "LetAppsAccessMicrophone",
        _            => null
    };

    // Returns false only when our own Policy key has force-denied the capability.
    // If no Policy key is set we return true (Windows default = On).
    public static bool GetGlobalToggle(string capability)
    {
        try
        {
            if (capability.Equals("location", StringComparison.OrdinalIgnoreCase))
            {
                using var key = Registry.LocalMachine.OpenSubKey(LocationPolicy);
                var val = key?.GetValue("DisableLocation");
                return !(val is int i && i == 1);
            }

            var policyName = PolicyValueName(capability);
            if (policyName != null)
            {
                using var key = Registry.LocalMachine.OpenSubKey(AppPrivacyPolicy);
                var val = key?.GetValue(policyName);
                return !(val is int i && i == 2);
            }

            return true;
        }
        catch { return true; }
    }

    // Writes a force-deny Policy value (allow=false) or removes it to restore Windows control (allow=true).
    public static void SetGlobalToggle(string capability, bool allow)
    {
        if (capability.Equals("location", StringComparison.OrdinalIgnoreCase))
        {
            if (allow)
                RegistryService.DeleteValue("LocalMachine", LocationPolicy, "DisableLocation");
            else
                RegistryService.WriteValue("LocalMachine", LocationPolicy, "DisableLocation", "1", "DWord");
            return;
        }

        var policyName = PolicyValueName(capability);
        if (policyName == null) return;

        if (allow)
            RegistryService.DeleteValue("LocalMachine", AppPrivacyPolicy, policyName);
        else
            RegistryService.WriteValue("LocalMachine", AppPrivacyPolicy, policyName, "2", "DWord");
    }
}
