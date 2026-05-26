using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Willow.Models;

namespace Willow.Services;

public static class AppPermissionsService
{
    private const string ConsentStoreBase =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    public static List<AppPermission> GetPermissions()
    {
        var camStates = ReadCapabilityStore($@"{ConsentStoreBase}\webcam");
        var micStates = ReadCapabilityStore($@"{ConsentStoreBase}\microphone");
        var locStates = ReadCapabilityStore($@"{ConsentStoreBase}\location");

        var allKeys = new HashSet<string>(camStates.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(micStates.Keys);
        allKeys.UnionWith(locStates.Keys);

        return allKeys
            .Select(key => new AppPermission
            {
                AppKey      = key,
                DisplayName = GetDisplayName(key),
                HasCamera   = camStates.GetValueOrDefault(key, false),
                HasMic      = micStates.GetValueOrDefault(key, false),
                HasLocation = locStates.GetValueOrDefault(key, false),
            })
            .OrderBy(a => a.DisplayName)
            .ToList();
    }

    public static void SetPermission(string appKey, string capability, bool allow)
    {
        var path = $@"{ConsentStoreBase}\{capability}\{appKey}";
        RegistryService.WriteValue("CurrentUser", path, "Value",
            allow ? "Allow" : "Deny", "String");
    }

    internal static string GetDisplayName(string appKey)
    {
        // Win32 apps: "NonPackaged\C:#Windows#System32#notepad.exe"
        const string prefix = @"NonPackaged\";
        if (appKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var encoded = appKey[prefix.Length..];
            var path = encoded.Replace('#', Path.DirectorySeparatorChar);
            var name = Path.GetFileNameWithoutExtension(path);
            return string.IsNullOrEmpty(name) ? appKey : name;
        }

        // UWP apps: "Microsoft.WindowsCamera_8wekyb3d8bbwe"
        var lastUnderscore = appKey.LastIndexOf('_');
        return lastUnderscore > 0 ? appKey[..lastUnderscore] : appKey;
    }

    private static Dictionary<string, bool> ReadCapabilityStore(string registryPath)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(registryPath);
            if (root == null) return result;

            foreach (var subKeyName in root.GetSubKeyNames())
            {
                if (subKeyName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
                {
                    using var nonPkg = root.OpenSubKey("NonPackaged");
                    if (nonPkg == null) continue;
                    foreach (var win32Key in nonPkg.GetSubKeyNames())
                    {
                        var appKey = $@"NonPackaged\{win32Key}";
                        var allowed = IsAllowed(nonPkg, win32Key);
                        if (allowed.HasValue)
                            result[appKey] = allowed.Value;
                    }
                }
                else
                {
                    var allowed = IsAllowed(root, subKeyName);
                    if (allowed.HasValue)
                        result[subKeyName] = allowed.Value;
                }
            }
        }
        catch { }
        return result;
    }

    private static bool? IsAllowed(RegistryKey parent, string subKeyName)
    {
        try
        {
            using var key = parent.OpenSubKey(subKeyName);
            var val = key?.GetValue("Value") as string;
            if (val == null) return null;
            return val.Equals("Allow", StringComparison.OrdinalIgnoreCase);
        }
        catch { return null; }
    }
}
