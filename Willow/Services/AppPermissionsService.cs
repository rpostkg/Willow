using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Management.Deployment;
using Willow.Models;

namespace Willow.Services;

public static class AppPermissionsService
{
    private const string ConsentStoreBase =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    private static readonly Dictionary<string, string> _nameCache = new(StringComparer.OrdinalIgnoreCase);

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
                AppKey           = key,
                DisplayName      = GetDisplayName(key),
                HasCamera        = camStates.GetValueOrDefault(key, false),
                HasMic           = micStates.GetValueOrDefault(key, false),
                HasLocation      = locStates.GetValueOrDefault(key, false),
                HasCameraEntry   = camStates.ContainsKey(key),
                HasMicEntry      = micStates.ContainsKey(key),
                HasLocationEntry = locStates.ContainsKey(key),
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
        // Resolve via PackageManager to get the localized display name; cache the result.
        if (_nameCache.TryGetValue(appKey, out var cached))
            return cached;

        var friendly = TryGetPackageDisplayName(appKey);
        var lastUnderscore = appKey.LastIndexOf('_');
        var fallback = lastUnderscore > 0 ? appKey[..lastUnderscore] : appKey;
        var result = !string.IsNullOrWhiteSpace(friendly) ? friendly : fallback;
        _nameCache[appKey] = result;
        return result;
    }

    private static string? TryGetPackageDisplayName(string packageFamilyName)
    {
        try
        {
            var pm = new PackageManager();
            var pkg = pm.FindPackagesForUser(string.Empty, packageFamilyName).FirstOrDefault();
            var name = pkg?.DisplayName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, bool> ReadCapabilityStore(string registryPath)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(registryPath);
            if (root == null) return result;

            // Global toggle: "Let apps access your microphone/camera/location"
            // If the root Value is "Deny", every per-app entry is effectively denied regardless
            // of its individual Value, which is exactly what Windows Settings shows.
            var globalValue = root.GetValue("Value") as string;
            bool globalAllow = !string.Equals(globalValue, "Deny", StringComparison.OrdinalIgnoreCase);

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
                            result[appKey] = globalAllow && allowed.Value;
                    }
                }
                else
                {
                    var allowed = IsAllowed(root, subKeyName);
                    if (allowed.HasValue)
                        result[subKeyName] = globalAllow && allowed.Value;
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
            if (key == null) return null;
            var val = key.GetValue("Value") as string;
            // No explicit Value = implicitly allowed (Windows tracks the access but user
            // hasn't explicitly managed it; effective permission follows the global toggle).
            if (val == null) return true;
            return val.Equals("Allow", StringComparison.OrdinalIgnoreCase);
        }
        catch { return null; }
    }
}
