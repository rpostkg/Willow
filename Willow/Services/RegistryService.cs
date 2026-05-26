using Microsoft.Win32;
using Willow.Models;
using System;
using System.Diagnostics;
using System.Security;

namespace Willow.Services;

public static class RegistryService
{
    internal static RegistryKey GetRootKey(string hive)
    {
        return hive.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase) || hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
            ? Registry.CurrentUser
            : Registry.LocalMachine;
    }

    internal static byte[]? ParseBinaryValue(string input)
    {
        if (!input.StartsWith("([byte[]]")) return null;
        string cleaned = input.Replace("([byte[]](", "").Replace("))", "");
        string[] parts = cleaned.Split(',');
        byte[] bytes = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (byte.TryParse(parts[i].Trim(), out byte b)) bytes[i] = b;
        }
        return bytes;
    }

    public static string ReadValue(string hive, string path, string keyName)
    {
        try
        {
            using var rootKey = GetRootKey(hive);
            using var subKey = rootKey.OpenSubKey(path, false);
            if (subKey != null)
            {
                var val = subKey.GetValue(keyName);
                if (val != null) return val.ToString() ?? TweakSentinels.NewKey;
            }
        }
        catch (SecurityException ex)
        {
            // TODO: Bubble up errors to the UI
            Debug.WriteLine($"[RegistryService] Read Access Denied: {hive}\\{path}\\{keyName} - {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            // TODO: Bubble up errors to the UI
            Debug.WriteLine($"[RegistryService] Read Unauthorized Access: {hive}\\{path}\\{keyName} - {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RegistryService] Read Failed: {hive}\\{path}\\{keyName} - {ex.Message}");
        }

        return TweakSentinels.NewKey;
    }

    public static bool WriteValue(string hive, string path, string keyName, string value, string valueType)
    {
        try
        {
            using var rootKey = GetRootKey(hive);
            
            // Create subkey if it doesn't exist
            using var subKey = rootKey.CreateSubKey(path, true);
            if (subKey == null) return false;

            RegistryValueKind kind = valueType.Equals("DWord", StringComparison.OrdinalIgnoreCase) ? RegistryValueKind.DWord :
                                     valueType.Equals("QWord", StringComparison.OrdinalIgnoreCase) ? RegistryValueKind.QWord :
                                     valueType.Equals("Binary", StringComparison.OrdinalIgnoreCase) ? RegistryValueKind.Binary :
                                     RegistryValueKind.String;

            object convertedValue = value;
            if (kind == RegistryValueKind.DWord)
            {
                if (int.TryParse(value, out int intVal)) convertedValue = intVal;
            }
            else if (kind == RegistryValueKind.QWord)
            {
                if (long.TryParse(value, out long longVal)) convertedValue = longVal;
            }
            else if (kind == RegistryValueKind.Binary)
            {
                var parsed = ParseBinaryValue(value);
                if (parsed != null) convertedValue = parsed;
            }

            subKey.SetValue(keyName, convertedValue, kind);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            // TODO: Bubble up errors to the UI
            Debug.WriteLine($"[RegistryService] Write Access Denied: {hive}\\{path}\\{keyName} - {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RegistryService] Write Failed: {hive}\\{path}\\{keyName} - {ex.Message}");
            return false;
        }
    }

    public static bool DeleteValue(string hive, string path, string keyName)
    {
        try
        {
            using var rootKey = GetRootKey(hive);
            using var subKey = rootKey.OpenSubKey(path, true);
            if (subKey == null) return true; // Already gone

            subKey.DeleteValue(keyName, false);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            // TODO: Bubble up errors to the UI
            Debug.WriteLine($"[RegistryService] Delete Access Denied: {hive}\\{path}\\{keyName} - {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RegistryService] Delete Failed: {hive}\\{path}\\{keyName} - {ex.Message}");
            return false;
        }
    }
}
