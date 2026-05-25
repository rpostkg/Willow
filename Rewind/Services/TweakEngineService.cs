using Microsoft.Win32;
using Microsoft.Windows.ApplicationModel.Resources;
using Rewind.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Rewind.Services;

public class TweakEngineService
{
    private readonly PreferencesService _prefsService = new();

    public async Task<List<ChangeItem>> GenerateChangeReportAsync(List<Tweak> tweaks, bool isRevert)
    {
        var changes = new List<ChangeItem>();
        var prefs = _prefsService.LoadPreferences();

        // Load display strings on the calling (UI) thread before entering Task.Run
        var res = new ResourceLoader();
        string scriptTargetFmt  = res.GetString("Engine_ScriptTargetFormat");
        string scriptNA         = res.GetString("Engine_ScriptNA");
        string runScript        = res.GetString("Engine_RunScript");
        string runRevertScript  = res.GetString("Engine_RunRevertScript");
        string deleteKey        = res.GetString("Engine_DeleteKey");
        string newKeyDisplay    = res.GetString("Engine_NewKey");
        string notFoundDisplay  = res.GetString("Engine_NotFound");
        string svcPrefixDisplay = res.GetString("Engine_ServicePrefix");

        await Task.Run(() =>
        {
            foreach (var tweak in tweaks)
            {
                var actions = isRevert ? tweak.RevertActions : tweak.Actions;
                if (actions == null) continue;

                List<BackedUpState>? backups = null;
                bool hasBackup = isRevert && prefs.OldRegistryData.TryGetValue(tweak.Id, out backups) && backups != null;

                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    var item = new ChangeItem
                    {
                        TweakId   = tweak.Id,
                        Type      = action.Type,
                        NewValue  = action.Type == ActionType.Registry ? action.Value :
                                    (action.Type == ActionType.Service ? action.TargetState : runScript),
                        Hive      = action.Hive,
                        Path      = action.Path,
                        Key       = action.Key,
                        ValueType = action.ValueType
                    };

                    if (action.Type == ActionType.Registry)
                    {
                        item.Target        = $"{action.Hive}\\{action.Path}\\{action.Key}";
                        item.DisplayTarget = item.Target;

                        if (hasBackup && backups != null)
                        {
                            var matchingBackup = backups.Find(b => b.Target == item.Target);
                            if (matchingBackup != null)
                                item.NewValue = matchingBackup.OldValue == TweakSentinels.NewKey ? deleteKey : matchingBackup.OldValue;
                        }

                        item.OldValue        = RegistryService.ReadValue(action.Hive, action.Path, action.Key);
                        item.DisplayOldValue = item.OldValue == TweakSentinels.NewKey ? newKeyDisplay : item.OldValue;
                    }
                    else if (action.Type == ActionType.Service)
                    {
                        item.Target        = $"{TweakSentinels.ServicePrefix}{action.Name}";
                        item.DisplayTarget = $"{svcPrefixDisplay}{action.Name}";

                        if (hasBackup && backups != null)
                        {
                            var matchingBackup = backups.Find(b => b.Target == item.Target);
                            if (matchingBackup != null)
                                item.NewValue = matchingBackup.OldValue;
                        }

                        item.OldValue        = WindowsServiceManager.GetStartupType(action.Name);
                        item.DisplayOldValue = item.OldValue == TweakSentinels.NotFound ? notFoundDisplay : item.OldValue;
                    }
                    else if (action.Type == ActionType.Script)
                    {
                        item.Target        = string.Format(scriptTargetFmt, tweak.Name);
                        item.DisplayTarget = item.Target;
                        item.OldValue        = scriptNA;
                        item.DisplayOldValue = scriptNA;
                        item.NewValue        = isRevert ? runRevertScript : runScript;
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

        if (!prefs.DisableBackups)
        {
            CreateRestorePoint();
        }

        bool allSuccess = true;

        foreach (var tweak in tweaks)
        {
            if (tweak.Actions == null) continue;
            
            foreach (var action in tweak.Actions)
            {
                if (action.Type == ActionType.Registry)
                {
                    allSuccess &= RegistryService.WriteValue(action.Hive, action.Path, action.Key, action.Value, action.ValueType);
                }
                else if (action.Type == ActionType.Service)
                {
                    allSuccess &= WindowsServiceManager.SetStartupType(action.Name, action.TargetState);
                }
                else if (action.Type == ActionType.Script)
                {
                    allSuccess &= ShellService.RunInlinePowerShell(action.Script);
                }
            }
        }

        return allSuccess;
    }

    public bool RevertTweaks(List<Tweak> tweaks)
    {
        var prefs = _prefsService.LoadPreferences();
        bool allSuccess = true;

        foreach (var tweak in tweaks)
        {
            if (prefs.OldRegistryData.TryGetValue(tweak.Id, out var backups) && backups.Count > 0)
            {
                foreach (var backup in backups)
                {
                    if (backup.Type == ActionType.Registry)
                    {
                        if (backup.OldValue == TweakSentinels.NewKey)
                        {
                            allSuccess &= RegistryService.DeleteValue(backup.Hive, backup.Path, backup.Key);
                        }
                        else if (backup.OldValue != TweakSentinels.UnknownError)
                        {
                            allSuccess &= RegistryService.WriteValue(backup.Hive, backup.Path, backup.Key, backup.OldValue, backup.ValueType);
                        }
                    }
                    else if (backup.Type == ActionType.Service)
                    {
                        if (backup.OldValue != TweakSentinels.NotFound)
                        {
                            string srvName = backup.Target.Replace(TweakSentinels.ServicePrefix, "");
                            allSuccess &= WindowsServiceManager.SetStartupType(srvName, backup.OldValue);
                        }
                    }
                }

                if (tweak.RevertActions != null)
                {
                    foreach (var action in tweak.RevertActions)
                    {
                        if (action.Type == ActionType.Script) 
                        {
                            allSuccess &= ShellService.RunInlinePowerShell(action.Script);
                        }
                    }
                }
            }
            else
            {
                if (tweak.RevertActions != null)
                {
                    foreach (var action in tweak.RevertActions)
                    {
                        if (action.Type == ActionType.Registry)
                        {
                            allSuccess &= RegistryService.WriteValue(action.Hive, action.Path, action.Key, action.Value, action.ValueType);
                        }
                        else if (action.Type == ActionType.Service)
                        {
                            allSuccess &= WindowsServiceManager.SetStartupType(action.Name, action.TargetState);
                        }
                        else if (action.Type == ActionType.Script)
                        {
                            allSuccess &= ShellService.RunInlinePowerShell(action.Script);
                        }
                    }
                }
            }
        }

        if (allSuccess)
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

        return allSuccess;
    }

    private void CreateRestorePoint()
    {
        string script = @"
            Set-ItemProperty -Path ""HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore"" -Name ""SystemRestorePointCreationFrequency"" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
            Start-Process powershell.exe -ArgumentList ""-NoProfile -Command `""Checkpoint-Computer -Description 'System Restore Point created by Rewind' -RestorePointType MODIFY_SETTINGS -ErrorAction SilentlyContinue`"""" -WindowStyle Hidden
        ";
        ShellService.RunInlinePowerShell(script);
    }
}
