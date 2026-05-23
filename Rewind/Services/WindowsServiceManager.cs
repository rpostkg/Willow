using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace Rewind.Services;

public static class WindowsServiceManager
{
    public static string GetStartupType(string serviceName)
    {
        try
        {
            // ServiceController in .NET Framework / Standard doesn't easily expose StartupType without WMI or Registry,
            // but we can read it from the registry since services are just keys in HKLM\System\CurrentControlSet\Services
            string path = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            string val = RegistryService.ReadValue("LocalMachine", path, "Start");
            
            if (int.TryParse(val, out int startVal))
            {
                return startVal switch
                {
                    2 => "Automatic",
                    3 => "Manual",
                    4 => "Disabled",
                    _ => "Unknown"
                };
            }
            return "Не знайдено";
        }
        catch
        {
            return "Не знайдено";
        }
    }

    public static bool SetStartupType(string serviceName, string targetState)
    {
        try
        {
            // Convert state to registry value
            int startVal = targetState.Equals("Automatic", StringComparison.OrdinalIgnoreCase) ? 2 :
                           targetState.Equals("Manual", StringComparison.OrdinalIgnoreCase) ? 3 :
                           targetState.Equals("Disabled", StringComparison.OrdinalIgnoreCase) ? 4 : 3;

            string path = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            bool regSuccess = RegistryService.WriteValue("LocalMachine", path, "Start", startVal.ToString(), "DWord");

            if (!regSuccess) return false;

            // Apply immediate state changes
            using var sc = new ServiceController(serviceName);
            
            if (targetState.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
            {
                if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                }
            }
            else if (targetState.Equals("Automatic", StringComparison.OrdinalIgnoreCase))
            {
                if (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.StopPending)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                }
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            // TODO: Bubble up errors to the UI
            Debug.WriteLine($"[WindowsServiceManager] Access Denied: {serviceName} - {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsServiceManager] Failed to modify service: {serviceName} - {ex.Message}");
            return false;
        }
    }
}
