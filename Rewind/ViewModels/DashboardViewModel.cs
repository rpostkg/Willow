using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;

namespace Rewind.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty]
    private double cpuUsage;

    [ObservableProperty]
    private double ramUsage;

    [ObservableProperty]
    private double diskUsage;

    private bool _isUpdating;

    public DashboardViewModel()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        StartUpdating();
    }

    private async void StartUpdating()
    {
        _isUpdating = true;
        while (_isUpdating)
        {
            var (cpu, ram, disk) = await Task.Run(() => GetMetrics());
            _dispatcher?.TryEnqueue(() =>
            {
                CpuUsage = cpu;
                RamUsage = ram;
                DiskUsage = disk;
            });
            await Task.Delay(2000);
        }
    }

    private (double, double, double) GetMetrics()
    {
        double cpu = 0;
        double ram = 0;
        double disk = 0;
        try
        {
            using (var searcher = new ManagementObjectSearcher("select LoadPercentage from Win32_Processor"))
            {
                foreach (var obj in searcher.Get())
                {
                    cpu = Convert.ToDouble(obj["LoadPercentage"]);
                }
            }

            using (var searcher = new ManagementObjectSearcher("select FreePhysicalMemory, TotalVisibleMemorySize from Win32_OperatingSystem"))
            {
                foreach (var obj in searcher.Get())
                {
                    double free = Convert.ToDouble(obj["FreePhysicalMemory"]);
                    double total = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                    ram = Math.Round(((total - free) / total) * 100, 1);
                }
            }

            using (var searcher = new ManagementObjectSearcher("select FreeSpace, Size from Win32_LogicalDisk where DeviceID='C:'"))
            {
                foreach (var obj in searcher.Get())
                {
                    double free = Convert.ToDouble(obj["FreeSpace"]);
                    double total = Convert.ToDouble(obj["Size"]);
                    disk = Math.Round(((total - free) / total) * 100, 1);
                }
            }
        }
        catch { }
        return (cpu, ram, disk);
    }
}
