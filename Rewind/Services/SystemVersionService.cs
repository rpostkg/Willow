using Microsoft.Win32;
using System;

namespace Rewind.Services;

public static class SystemVersionService
{
    public static int GetCurrentBuildNumber()
    {
        try
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                if (key != null)
                {
                    var buildStr = key.GetValue("CurrentBuildNumber")?.ToString();
                    if (int.TryParse(buildStr, out int buildNumber))
                    {
                        return buildNumber;
                    }
                }
            }
        }
        catch { }
        
        return Environment.OSVersion.Version.Build;
    }
}
