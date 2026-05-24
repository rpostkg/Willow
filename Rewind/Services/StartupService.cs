using Microsoft.Win32;
using Microsoft.Windows.ApplicationModel.Resources;
using Rewind.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

namespace Rewind.Services;

public class StartupService
{
    private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedFolderPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

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

    public List<StartupItem> GetStartupItems()
    {
        var items = new List<StartupItem>();
        items.AddRange(ReadRegistryHive(Registry.CurrentUser, "HKCU", _hkcuLabel));
        items.AddRange(ReadRegistryHive(Registry.LocalMachine, "HKLM", _hklmLabel));
        items.AddRange(ReadStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            requiresAdmin: false));
        items.AddRange(ReadStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            requiresAdmin: true));
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

            // Approval state for ALL startup items lives in HKCU (same as Task Manager)
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

    private List<StartupItem> ReadStartupFolder(string folderPath, bool requiresAdmin)
    {
        var items = new List<StartupItem>();
        if (!Directory.Exists(folderPath)) return items;
        try
        {
            // Approval for startup folder items is always in HKCU (both user and common folders)
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedFolderPath, false);

            foreach (var lnk in Directory.GetFiles(folderPath, "*.lnk"))
            {
                var fileName = Path.GetFileName(lnk);
                items.Add(new StartupItem
                {
                    Name = Path.GetFileNameWithoutExtension(lnk),
                    Command = lnk,
                    HiveLabel = _folderLabel,
                    RequiresAdmin = requiresAdmin,
                    Source = StartupSource.StartupFolder,
                    Hive = requiresAdmin ? "HKLM" : "HKCU",
                    ShortcutName = fileName,
                    IsEnabled = IsApprovedEnabled(approved, fileName),
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupService] Startup folder read failed: {ex.Message}");
        }
        return items;
    }

    // ── Task Scheduler (all folders, logon tasks via XML) ─────────────────────

    private List<StartupItem> ReadTaskScheduler()
    {
        var items = new List<StartupItem>();
        var tasksDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "Tasks");
        if (!Directory.Exists(tasksDir)) return items;

        try
        {
            // Search all subdirectories to catch tasks like \Microsoft\Windows Terminal\...
            foreach (var file in Directory.GetFiles(tasksDir, "*", SearchOption.AllDirectories))
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

                    // <Settings><Enabled> governs the whole task
                    bool enabled = !string.Equals(
                        doc.Root?.Element(TaskNs + "Settings")?.Element(TaskNs + "Enabled")?.Value,
                        "false", StringComparison.OrdinalIgnoreCase);

                    bool requiresAdmin = string.Equals(
                        doc.Root?.Element(TaskNs + "Principals")?
                            .Element(TaskNs + "Principal")?
                            .Element(TaskNs + "RunLevel")?.Value,
                        "HighestAvailable", StringComparison.OrdinalIgnoreCase);

                    // TaskPath relative to tasksDir, with leading backslash — used for schtasks /tn
                    var taskPath = file.Substring(tasksDir.Length);

                    items.Add(new StartupItem
                    {
                        Name = Path.GetFileName(file),
                        Command = command,
                        HiveLabel = _schedulerLabel,
                        RequiresAdmin = requiresAdmin,
                        Source = StartupSource.TaskScheduler,
                        Hive = "TaskScheduler",
                        TaskPath = taskPath,
                        IsEnabled = enabled,
                    });
                }
                catch { /* skip inaccessible or malformed task files */ }
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
                // Write approval to HKCU regardless of source hive — same as Task Manager
                SetApproved(Registry.CurrentUser, ApprovedRunPath, item.Name, enabled);
                break;
            case StartupSource.StartupFolder:
                if (item.ShortcutName == null) return;
                SetApproved(Registry.CurrentUser, ApprovedFolderPath, item.ShortcutName, enabled);
                break;
            case StartupSource.TaskScheduler:
                SetTaskEnabled(item, enabled);
                break;
        }
    }

    private static void SetApproved(RegistryKey rootKey, string path, string name, bool enabled)
    {
        if (enabled)
        {
            using var key = rootKey.OpenSubKey(path, true);
            key?.DeleteValue(name, false);
        }
        else
        {
            using var key = rootKey.CreateSubKey(path, true);
            var disabled = new byte[12];
            disabled[0] = 0x03;
            key?.SetValue(name, disabled, RegistryValueKind.Binary);
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
}
