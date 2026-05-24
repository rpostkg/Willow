using Microsoft.Win32;
using Microsoft.Windows.ApplicationModel.Resources;
using Rewind.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Rewind.Services;

public class StartupService
{
    private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private readonly string _hkcuLabel;
    private readonly string _hklmLabel;

    public StartupService()
    {
        var res = new ResourceLoader();
        _hkcuLabel = res.GetString("StartupPage_HkcuBadge");
        _hklmLabel = res.GetString("StartupPage_HklmBadge");
    }

    public List<StartupItem> GetStartupItems()
    {
        var items = new List<StartupItem>();
        items.AddRange(ReadFromHive(Registry.CurrentUser, "HKCU", _hkcuLabel));
        items.AddRange(ReadFromHive(Registry.LocalMachine, "HKLM", _hklmLabel));
        return items;
    }

    private static List<StartupItem> ReadFromHive(RegistryKey rootKey, string hive, string hiveLabel)
    {
        var items = new List<StartupItem>();
        try
        {
            using var run = rootKey.OpenSubKey(RunPath, false);
            if (run == null) return items;

            using var approved = rootKey.OpenSubKey(ApprovedPath, false);

            foreach (var name in run.GetValueNames())
            {
                var command = run.GetValue(name)?.ToString() ?? string.Empty;
                items.Add(new StartupItem
                {
                    Name = name,
                    Command = command,
                    Hive = hive,
                    HiveLabel = hiveLabel,
                    RequiresAdmin = hive == "HKLM",
                    IsEnabled = IsApproved(approved, name),
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupService] Failed to read {hive}\\{RunPath}: {ex.Message}");
        }
        return items;
    }

    private static bool IsApproved(RegistryKey? approved, string name)
    {
        if (approved == null) return true;
        var raw = approved.GetValue(name);
        if (raw is byte[] bytes && bytes.Length >= 1)
            return bytes[0] != 0x03;
        return true; // absent entry means enabled
    }

    public void SetEnabled(StartupItem item, bool enabled)
    {
        var rootKey = item.Hive == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
        try
        {
            if (enabled)
            {
                using var approved = rootKey.OpenSubKey(ApprovedPath, true);
                approved?.DeleteValue(item.Name, false);
            }
            else
            {
                using var approved = rootKey.CreateSubKey(ApprovedPath, true);
                var disabled = new byte[12];
                disabled[0] = 0x03;
                approved?.SetValue(item.Name, disabled, RegistryValueKind.Binary);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"[StartupService] Access denied for {item.Name}: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupService] Toggle failed for {item.Name}: {ex.Message}");
            throw;
        }
    }
}
