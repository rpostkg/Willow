using Microsoft.Win32;
using Rewind.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Rewind.Services;

public class TweakEngineService
{
    private readonly PreferencesService _prefsService = new();

    public async Task<List<ChangeItem>> GenerateChangeReportAsync(List<Tweak> tweaks, bool isRevert)
    {
        var changes = new List<ChangeItem>();
        var prefs = _prefsService.LoadPreferences();

        await Task.Run(() =>
        {
            foreach (var tweak in tweaks)
            {
                var actions = isRevert ? tweak.RevertActions : tweak.Actions;
                if (actions == null) continue;

                // Check if we have backup data for this tweak (from userpreferences.yaml)
                List<BackedUpState> backups = null;
                bool hasBackup = isRevert && prefs.OldRegistryData.TryGetValue(tweak.Id, out backups);

                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    var item = new ChangeItem
                    {
                        TweakId = tweak.Id,
                        Type = action.Type,
                        NewValue = action.Type == ActionType.Registry ? action.Value :
                                   (action.Type == ActionType.Service ? action.TargetState : "Виконати скрипт"),
                        Hive = action.Hive,
                        Path = action.Path,
                        Key = action.Key,
                        ValueType = action.ValueType
                    };

                    if (action.Type == ActionType.Registry)
                    {
                        item.Target = $"{action.Hive}\\{action.Path}\\{action.Key}";

                        // If we have a backup for this specific target, override the NewValue shown in the UI
                        if (hasBackup)
                        {
                            var matchingBackup = backups.Find(b => b.Target == item.Target);
                            if (matchingBackup != null)
                            {
                                // If the backed up state was "new key", it means we should delete it on revert.
                                item.NewValue = matchingBackup.OldValue == "Новий ключ" ? "Видалення ключа" : matchingBackup.OldValue;
                            }
                        }

                        RegistryKey root = action.Hive == "CurrentUser" ? Registry.CurrentUser : Registry.LocalMachine;
                        try
                        {
                            using (var key = root.OpenSubKey(action.Path))
                            {
                                if (key != null)
                                {
                                    var val = key.GetValue(action.Key);
                                    item.OldValue = val != null ? val.ToString() : "Новий ключ";
                                }
                                else
                                {
                                    item.OldValue = "Новий ключ";
                                }
                            }
                        }
                        catch
                        {
                            item.OldValue = "Невідомо/Помилка";
                        }
                    }
                    else if (action.Type == ActionType.Service)
                    {
                        item.Target = $"Сервіс: {action.Name}";

                        if (hasBackup)
                        {
                            var matchingBackup = backups.Find(b => b.Target == item.Target);
                            if (matchingBackup != null)
                            {
                                item.NewValue = matchingBackup.OldValue;
                            }
                        }

                        try
                        {
                            using (var sc = new ServiceController(action.Name))
                            {
                                item.OldValue = sc.StartType.ToString();
                            }
                        }
                        catch
                        {
                            item.OldValue = "Не знайдено";
                        }
                    }
                    else if (action.Type == ActionType.Script)
                    {
                        item.Target = $"Скрипт Powershell ({tweak.Name})";
                        item.OldValue = "Н/Д";
                        item.NewValue = isRevert ? "Виконання скрипту скасування дії" : "Виконання скрипту";
                    }

                    changes.Add(item);
                }
            }
        });

        return changes;
    }

    public bool ApplyTweaks(List<Tweak> tweaks, List<ChangeItem> report)
    {
        var prefs = _prefsService.LoadPreferences();
        bool prefsChanged = false;

        // Group report by TweakId to process backups per tweak
        var tweakGroups = report.GroupBy(r => r.TweakId);

        foreach (var group in tweakGroups)
        {
            string tweakId = group.Key;

            // If we already have a backup for this Tweak ID, do not overwrite or append. We don't want duplicate data.
            if (prefs.OldRegistryData.ContainsKey(tweakId)) continue;

            prefsChanged = true;
            prefs.OldRegistryData[tweakId] = new List<BackedUpState>();

            foreach (var change in group)
            {
                if (change.Type == ActionType.Script) continue;

                prefs.OldRegistryData[tweakId].Add(new BackedUpState
                {
                    Type = change.Type,
                    Target = change.Target,
                    OldValue = change.OldValue,
                    Hive = change.Hive,
                    Path = change.Path,
                    Key = change.Key,
                    ValueType = change.ValueType
                });
            }
        }

        if (prefsChanged)
        {
            _prefsService.SavePreferences(prefs);
        }

        var sb = new StringBuilder();

        if (!prefs.DisableBackups)
        {
            sb.AppendLine("# Ensure restore points can be created frequently (set-and-forget)");
            sb.AppendLine("Set-ItemProperty -Path \"HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore\" -Name \"SystemRestorePointCreationFrequency\" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue");
            sb.AppendLine();
            
            sb.AppendLine("# Launch restore point creation in the background so it doesn't block tweaks");
            sb.AppendLine("# This avoids the 'first-time run' issue where Checkpoint-Computer blocks or fails due to registry latency");
            sb.AppendLine("Start-Process powershell.exe -ArgumentList \"-NoProfile -Command `\"Checkpoint-Computer -Description 'System Restore Point created by Rewind' -RestorePointType MODIFY_SETTINGS -ErrorAction SilentlyContinue`\"\" -WindowStyle Hidden");
            sb.AppendLine();
        }

        foreach (var tweak in tweaks)
        {
            if (tweak.Actions == null) continue;
            AppendActions(sb, tweak.Actions);
        }

        return ExecutePowerShell(sb.ToString());
    }

    public bool RevertTweaks(List<Tweak> tweaks)
    {
        var prefs = _prefsService.LoadPreferences();
        var sb = new StringBuilder();

        foreach (var tweak in tweaks)
        {
            if (prefs.OldRegistryData.TryGetValue(tweak.Id, out var backups) && backups.Count > 0)
            {
                foreach (var backup in backups)
                {
                    if (backup.Type == ActionType.Registry)
                    {
                        string root = backup.Hive == "CurrentUser" ? "HKCU:" : "HKLM:";
                        string fullPath = $"{root}\\{backup.Path}";

                        if (backup.OldValue == "Новий ключ")
                        {
                            sb.AppendLine($"Remove-ItemProperty -Path '{fullPath}' -Name '{backup.Key}' -Force -ErrorAction SilentlyContinue");
                        }
                        else if (backup.OldValue != "Невідомо/Помилка")
                        {
                            sb.AppendLine($"if (!(Test-Path '{fullPath}')) {{ New-Item -Path '{fullPath}' -Force | Out-Null }}");
                            sb.AppendLine($"Set-ItemProperty -Path '{fullPath}' -Name '{backup.Key}' -Value {backup.OldValue} -Type {backup.ValueType} -Force");
                        }
                    }
                    else if (backup.Type == ActionType.Service)
                    {
                        if (backup.OldValue != "Не знайдено")
                        {
                            sb.AppendLine($"Set-Service -Name '{backup.Target.Replace("Сервіс: ", "")}' -StartupType {backup.OldValue}");
                        }
                    }
                }

                if (tweak.RevertActions != null)
                {
                    foreach (var action in tweak.RevertActions)
                    {
                        if (action.Type == ActionType.Script) sb.AppendLine(action.Script);
                    }
                }
            }
            else
            {
                if (tweak.RevertActions != null) AppendActions(sb, tweak.RevertActions);
            }
        }

        bool success = ExecutePowerShell(sb.ToString());
        
        if (success)
        {
            bool prefsChanged = false;
            foreach (var tweak in tweaks)
            {
                if (prefs.OldRegistryData.Remove(tweak.Id))
                {
                    prefsChanged = true;
                }
            }

            if (prefsChanged)
            {
                _prefsService.SavePreferences(prefs);
            }
        }

        return success;
    }

    private void AppendActions(StringBuilder sb, List<TweakAction> actions)
    {
        foreach (var action in actions)
        {
            if (action.Type == ActionType.Registry)
            {
                string root = action.Hive == "CurrentUser" ? "HKCU:" : "HKLM:";
                string fullPath = $"{root}\\{action.Path}";
                string val = action.Value;
                // If it's not a pre-formatted byte array, wrap in quotes to handle spaces/strings
                if (!val.StartsWith("([byte[]]")) val = $"'{val}'";

                sb.AppendLine($"if (!(Test-Path '{fullPath}')) {{ New-Item -Path '{fullPath}' -Force | Out-Null }}");
                sb.AppendLine($"Set-ItemProperty -Path '{fullPath}' -Name '{action.Key}' -Value {val} -Type {action.ValueType} -Force");
            }
            else if (action.Type == ActionType.Service)
            {
                string startupType = action.TargetState == "Disabled" ? "Disabled" :
                                     (action.TargetState == "Automatic" ? "Automatic" : "Manual");
                sb.AppendLine($"Set-Service -Name '{action.Name}' -StartupType {startupType}");
                if (action.TargetState == "Disabled")
                {
                    sb.AppendLine($"Stop-Service -Name '{action.Name}' -Force -ErrorAction SilentlyContinue");
                }
                else if (action.TargetState == "Automatic")
                {
                    sb.AppendLine($"Start-Service -Name '{action.Name}' -ErrorAction SilentlyContinue");
                }
            }
            else if (action.Type == ActionType.Script)
            {
                sb.AppendLine(action.Script);
            }
        }
    }

    private bool ExecutePowerShell(string scriptContent)
    {
        if (string.IsNullOrWhiteSpace(scriptContent)) return true;

        string scriptPath = Path.Combine(Path.GetTempPath(), "RewindTweak.ps1");
        File.WriteAllText(scriptPath, scriptContent);

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = true,
                Verb = "runas"
            };
            var process = Process.Start(processInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { }
        }
    }
}
