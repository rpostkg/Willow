using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using System;
using System.Management;
using System.Threading.Tasks;

namespace Rewind.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;

    private static double _lastCpu = 0;
    private static double _lastRam = 0;
    private static double _lastDisk = 0;
    private static bool _isPolling = false;
    private static DashboardViewModel? _currentActive;

    [ObservableProperty]
    private double cpuUsage;

    [ObservableProperty]
    private double ramUsage;

    [ObservableProperty]
    private double diskUsage;

    // Formatted text shown beneath each ProgressRing
    public string CpuUsageText  => $"{CpuUsage:F0}%";
    public string RamUsageText  => $"{RamUsage:F0}%";
    public string DiskUsageText => $"{DiskUsage:F0}%";

    partial void OnCpuUsageChanged(double value)  => OnPropertyChanged(nameof(CpuUsageText));
    partial void OnRamUsageChanged(double value)  => OnPropertyChanged(nameof(RamUsageText));
    partial void OnDiskUsageChanged(double value) => OnPropertyChanged(nameof(DiskUsageText));

    public DashboardViewModel()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Show last-known values immediately so rings don't flash 0% on re-navigation
        CpuUsage  = _lastCpu;
        RamUsage  = _lastRam;
        DiskUsage = _lastDisk;

        _currentActive = this;

        if (!_isPolling)
        {
            // First time: start the background polling loop (it fetches immediately then every 2 s)
            StartUpdating();
        }
        else
        {
            // Already polling — trigger a one-off fresh fetch so the user sees
            // current values the moment they navigate to this tab
            _ = Task.Run(() =>
            {
                var (cpu, ram, disk) = GetMetrics();
                _lastCpu = cpu; _lastRam = ram; _lastDisk = disk;
                _dispatcher?.TryEnqueue(() =>
                {
                    CpuUsage  = cpu;
                    RamUsage  = ram;
                    DiskUsage = disk;
                });
            });
        }
    }

    private async void StartUpdating()
    {
        _isPolling = true;
        while (_isPolling)
        {
            var (cpu, ram, disk) = await Task.Run(() => GetMetrics());

            _lastCpu  = cpu;
            _lastRam  = ram;
            _lastDisk = disk;

            _currentActive?._dispatcher?.TryEnqueue(() =>
            {
                _currentActive.CpuUsage  = cpu;
                _currentActive.RamUsage  = ram;
                _currentActive.DiskUsage = disk;
            });

            await Task.Delay(2000);
        }
    }

    private (double cpu, double ram, double disk) GetMetrics()
    {
        double cpu  = _lastCpu;
        double ram  = _lastRam;
        double disk = _lastDisk;

        try
        {
            using (var s = new ManagementObjectSearcher("select LoadPercentage from Win32_Processor"))
                foreach (var o in s.Get())
                    cpu = Convert.ToDouble(o["LoadPercentage"]);

            using (var s = new ManagementObjectSearcher(
                "select FreePhysicalMemory, TotalVisibleMemorySize from Win32_OperatingSystem"))
                foreach (var o in s.Get())
                {
                    double free  = Convert.ToDouble(o["FreePhysicalMemory"]);
                    double total = Convert.ToDouble(o["TotalVisibleMemorySize"]);
                    ram = Math.Round(((total - free) / total) * 100, 1);
                }

            using (var s = new ManagementObjectSearcher(
                "select FreeSpace, Size from Win32_LogicalDisk where DeviceID='C:'"))
                foreach (var o in s.Get())
                {
                    double free  = Convert.ToDouble(o["FreeSpace"]);
                    double total = Convert.ToDouble(o["Size"]);
                    disk = Math.Round(((total - free) / total) * 100, 1);
                }
        }
        catch { /* silently use last-known values on WMI failure */ }

        return (cpu, ram, disk);
    }
}
