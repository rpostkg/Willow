using Microsoft.Win32;
using Rewind.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Rewind.Services;

public class TweakEngineService
{
    public async Task<List<ChangeItem>> GenerateChangeReportAsync(List<Tweak> tweaks, bool isRevert)
    {
        var changes = new List<ChangeItem>();
        
        await Task.Run(() => 
        {
            foreach (var tweak in tweaks)
            {
                var actions = isRevert ? tweak.RevertActions : tweak.Actions;
                if (actions == null) continue;

                foreach (var action in actions)
                {
                    var item = new ChangeItem
                    {
                        Type = action.Type,
                        NewValue = action.Type == ActionType.Registry ? action.Value : action.TargetState
                    };

                    if (action.Type == ActionType.Registry)
                    {
                        item.Target = $"{action.Hive}\\{action.Path}\\{action.Key}";
                        RegistryKey root = action.Hive == "CurrentUser" ? Registry.CurrentUser : Registry.LocalMachine;
                        try
                        {
                            using (var key = root.OpenSubKey(action.Path))
                            {
                                if (key != null)
                                {
                                    var val = key.GetValue(action.Key);
                                    item.OldValue = val != null ? val.ToString() : "New Key";
                                }
                                else
                                {
                                    item.OldValue = "New Key";
                                }
                            }
                        }
                        catch
                        {
                            item.OldValue = "Unknown/Error";
                        }
                    }
                    else if (action.Type == ActionType.Service)
                    {
                        item.Target = $"Service: {action.Name}";
                        try
                        {
                            using (var sc = new ServiceController(action.Name))
                            {
                                item.OldValue = sc.StartType.ToString();
                            }
                        }
                        catch
                        {
                            item.OldValue = "Not Found";
                        }
                    }
                    
                    changes.Add(item);
                }
            }
        });

        return changes;
    }

    public bool ApplyTweaks(List<Tweak> tweaks)
    {
        return ExecuteActions(tweaks, false);
    }
    
    public bool RevertTweaks(List<Tweak> tweaks)
    {
        return ExecuteActions(tweaks, true);
    }

    // The main meat of the optimization tab
    private bool ExecuteActions(List<Tweak> tweaks, bool revert)
    {
        var sb = new StringBuilder();
        
        foreach (var tweak in tweaks)
        {
            var actions = revert ? tweak.RevertActions : tweak.Actions;
            if (actions == null) continue;

            foreach (var action in actions)
            {
                if (action.Type == ActionType.Registry)
                {
                    string root = action.Hive == "CurrentUser" ? "HKCU:" : "HKLM:";
                    string fullPath = $"{root}\\{action.Path}";
                    sb.AppendLine($"if (!(Test-Path '{fullPath}')) {{ New-Item -Path '{fullPath}' -Force | Out-Null }}");
                    sb.AppendLine($"Set-ItemProperty -Path '{fullPath}' -Name '{action.Key}' -Value {action.Value} -Type {action.ValueType} -Force");
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
            }
        }

        if (sb.Length == 0) return true;

        string scriptPath = Path.Combine(Path.GetTempPath(), "RewindTweak.ps1");
        File.WriteAllText(scriptPath, sb.ToString());

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
