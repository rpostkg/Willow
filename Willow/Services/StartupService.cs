using Microsoft.Win32;
using Microsoft.Windows.ApplicationModel.Resources;
using Willow.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace Willow.Services;

public class StartupService
{
    private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private static readonly XNamespace TaskNs = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    private readonly string _hkcuLabel;
    private readonly string _hklmLabel;
    private readonly string _folderLabel;
    private readonly string _schedulerLabel;

    public StartupService()
    {
        var res = new ResourceLoader();
        _hkcuLabel = res.GetString("StartupPage_HkcuBadge");
        _hklmLabel = res.GetString("StartupPage_HklmBadge");
        _folderLabel = res.GetString("StartupPage_FolderBadge");
        _schedulerLabel = res.GetString("StartupPage_SchedulerBadge");
    }

    public List<StartupItem> GetStartupItems(bool resolveShortcuts = false)
    {
        var items = new List<StartupItem>();
        items.AddRange(ReadRegistryHive(Registry.CurrentUser, "HKCU", _hkcuLabel));
        items.AddRange(ReadRegistryHive(Registry.LocalMachine, "HKLM", _hklmLabel));
        items.AddRange(ReadStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            requiresAdmin: false, resolveShortcuts));
        items.AddRange(ReadStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            requiresAdmin: true, resolveShortcuts));
        items.AddRange(ReadTaskScheduler());
        return items;
    }

    // ── Registry Run keys ─────────────────────────────────────────────────────

    private List<StartupItem> ReadRegistryHive(RegistryKey rootKey, string hive, string hiveLabel)
    {
        var items = new List<StartupItem>();
        try
        {
            using var run = rootKey.OpenSubKey(RunPath, false);
            if (run == null) return items;

            // Approval for registry Run items lives in HKCU (same as Task Manager)
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedRunPath, false);

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
                    Source = StartupSource.Registry,
                    IsEnabled = IsApprovedEnabled(approved, name),
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupService] Registry read failed ({hive}): {ex.Message}");
        }
        return items;
    }

    // ── Startup folders ───────────────────────────────────────────────────────
    // Enable/disable is done by renaming .lnk ↔ .lnk.disabled in-place.
    // This mirrors Autoruns and works without admin for the user's personal folder.
    // For the common folder (ProgramData) the rename requires admin — File.Move will
    // throw UnauthorizedAccessException, which the ViewModel catches and reverts.

    private List<StartupItem> ReadStartupFolder(string folderPath, bool requiresAdmin, bool resolveShortcuts)
    {
        var items = new List<StartupItem>();
        if (!Directory.Exists(folderPath)) return items;
        try
        {
            // Enabled items
            foreach (var lnk in Directory.GetFiles(folderPath, "*.lnk"))
                items.Add(BuildFolderItem(lnk, lnkPath: lnk, isEnabled: true, requiresAdmin, resolveShortcuts));

            // Disabled items (renamed by us to .lnk.disabled)
            foreach (var disabled in Directory.GetFiles(folderPath, "*.lnk.disabled"))
            {
                var basePath = disabled[..^".disabled".Length];
                items.Add(BuildFolderItem(disabled, lnkPath: basePath, isEnabled: false, requiresAdmin, resolveShortcuts: false));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupService] Startup folder read failed: {ex.Message}");
        }
        return items;
    }

    private StartupItem BuildFolderItem(string actualFile, string lnkPath, bool isEnabled,
                                        bool requiresAdmin, bool resolveShortcuts)
    {
        var displayName = Path.GetFileNameWithoutExtension(lnkPath);
        var command = lnkPath;

        if (resolveShortcuts && isEnabled)
        {
            var (resolvedName, resolvedPath) = ResolveLnk(actualFile);
            if (!string.IsNullOrEmpty(resolvedPath))
            {
                displayName = resolvedName;
                command = resolvedPath;
            }
        }

        return new StartupItem
        {
            Name = displayName,
            Command = command,
            HiveLabel = _folderLabel,
            RequiresAdmin = requiresAdmin,
            Source = StartupSource.StartupFolder,
            Hive = requiresAdmin ? "HKLM" : "HKCU",
            ShortcutName = Path.GetFileName(lnkPath),
            LnkPath = lnkPath,
            IsEnabled = isEnabled,
        };
    }

    // ── Task Scheduler (root-folder logon tasks via XML) ─────────────────────

    private List<StartupItem> ReadTaskScheduler()
    {
        var items = new List<StartupItem>();
        var tasksDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "Tasks");
        if (!Directory.Exists(tasksDir)) return items;

        try
        {
            // Root folder only — mirrors what Task Manager's Startup tab shows
            foreach (var file in Directory.GetFiles(tasksDir))
            {
                try
                {
                    var doc = XDocument.Load(file);
                    bool hasLogon = false;
                    foreach (var _ in doc.Descendants(TaskNs + "LogonTrigger"))
                    { hasLogon = true; break; }
                    if (!hasLogon) continue;

                    string command = string.Empty;
                    foreach (var el in doc.Descendants(TaskNs + "Command"))
                    { command = el.Value; break; }

                    bool enabled = !string.Equals(
                        doc.Root?.Element(TaskNs + "Settings")?.Element(TaskNs + "Enabled")?.Value,
                        "false", StringComparison.OrdinalIgnoreCase);

                    bool requiresAdmin = string.Equals(
                        doc.Root?.Element(TaskNs + "Principals")?
                            .Element(TaskNs + "Principal")?
                            .Element(TaskNs + "RunLevel")?.Value,
                        "HighestAvailable", StringComparison.OrdinalIgnoreCase);

                    items.Add(new StartupItem
                    {
                        Name = Path.GetFileName(file),
                        Command = command,
                        HiveLabel = _schedulerLabel,
                        RequiresAdmin = requiresAdmin,
                        Source = StartupSource.TaskScheduler,
                        Hive = "TaskScheduler",
                        TaskPath = file.Substring(tasksDir.Length),
                        IsEnabled = enabled,
                    });
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupService] Task Scheduler read failed: {ex.Message}");
        }
        return items;
    }

    // ── Enable / Disable ──────────────────────────────────────────────────────

    public void SetEnabled(StartupItem item, bool enabled)
    {
        switch (item.Source)
        {
            case StartupSource.Registry:
                SetRegistryApproved(item.Name, enabled);
                break;

            case StartupSource.StartupFolder:
                SetFolderEnabled(item, enabled);
                break;

            case StartupSource.TaskScheduler:
                SetTaskEnabled(item, enabled);
                break;
        }
    }

    private static void SetRegistryApproved(string name, bool enabled)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedRunPath, true);
            key?.DeleteValue(name, false);
        }
        else
        {
            using var key = Registry.CurrentUser.CreateSubKey(ApprovedRunPath, true)
                ?? throw new InvalidOperationException($"Cannot create registry key: {ApprovedRunPath}");
            var disabled = new byte[12];
            disabled[0] = 0x03;
            key.SetValue(name, disabled, RegistryValueKind.Binary);
        }
    }

    private static void SetFolderEnabled(StartupItem item, bool enabled)
    {
        if (item.LnkPath == null) return;

        if (enabled)
        {
            var disabledPath = item.LnkPath + ".disabled";
            if (File.Exists(disabledPath))
                File.Move(disabledPath, item.LnkPath);
        }
        else
        {
            if (File.Exists(item.LnkPath))
                File.Move(item.LnkPath, item.LnkPath + ".disabled");
        }
    }

    private static void SetTaskEnabled(StartupItem item, bool enabled)
    {
        if (item.TaskPath == null) return;
        var flag = enabled ? "/enable" : "/disable";
        var psi = new ProcessStartInfo("schtasks", $"/change /tn \"{item.TaskPath}\" {flag}")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start schtasks.exe");
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"schtasks failed (exit {proc.ExitCode})");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsApprovedEnabled(RegistryKey? approved, string name)
    {
        if (approved == null) return true;
        var raw = approved.GetValue(name);
        if (raw is byte[] bytes && bytes.Length >= 1)
            return bytes[0] != 0x03;
        return true;
    }

    private static (string name, string path) ResolveLnk(string lnkFilePath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return (Path.GetFileNameWithoutExtension(lnkFilePath), lnkFilePath);

            var shell = Activator.CreateInstance(shellType);
            var shortcut = shellType.InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell, [lnkFilePath]);
            if (shortcut == null) return (Path.GetFileNameWithoutExtension(lnkFilePath), lnkFilePath);

            var target = shortcut.GetType().InvokeMember("TargetPath",
                BindingFlags.GetProperty, null, shortcut, null) as string ?? string.Empty;

            if (string.IsNullOrWhiteSpace(target))
                return (Path.GetFileNameWithoutExtension(lnkFilePath), lnkFilePath);

            return (Path.GetFileNameWithoutExtension(target), target);
        }
        catch
        {
            return (Path.GetFileNameWithoutExtension(lnkFilePath), lnkFilePath);
        }
    }
}
