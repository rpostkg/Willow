using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;

namespace Rewind.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private double cpuUsage;

    [ObservableProperty]
    private double ramUsage;

    [ObservableProperty]
    private double diskUsage;
    
    private bool _isUpdating;

    public DashboardViewModel()
    {
        StartUpdating();
    }

    private async void StartUpdating()
    {
        _isUpdating = true;
        while (_isUpdating)
        {
            UpdateMetrics();
            await Task.Delay(2000);
        }
    }

    private void UpdateMetrics()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("select LoadPercentage from Win32_Processor"))
            {
                foreach (var obj in searcher.Get())
                {
                    CpuUsage = Convert.ToDouble(obj["LoadPercentage"]);
                }
            }

            using (var searcher = new ManagementObjectSearcher("select FreePhysicalMemory, TotalVisibleMemorySize from Win32_OperatingSystem"))
            {
                foreach (var obj in searcher.Get())
                {
                    double free = Convert.ToDouble(obj["FreePhysicalMemory"]);
                    double total = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                    RamUsage = Math.Round(((total - free) / total) * 100, 1);
                }
            }

            using (var searcher = new ManagementObjectSearcher("select FreeSpace, Size from Win32_LogicalDisk where DeviceID='C:'"))
            {
                foreach (var obj in searcher.Get())
                {
                    double free = Convert.ToDouble(obj["FreeSpace"]);
                    double total = Convert.ToDouble(obj["Size"]);
                    DiskUsage = Math.Round(((total - free) / total) * 100, 1);
                }
            }
        }
        catch { }
    }
}
