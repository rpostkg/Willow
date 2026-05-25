using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Rewind.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace Rewind.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;

    private static double _lastCpu = 0;
    private static double _lastRam = 0;
    private static double _lastDisk = 0;
    private static double _lastDiskTotalGb = 0;
    private static double _lastDiskUsedGb = 0;
    private static string _freeableText = string.Empty;
    private static bool _freeableScanDone = false;
    private static bool _hasValues = false;
    private static bool _isPolling = false;
    private static DashboardViewModel? _currentActive;

    [ObservableProperty] private double cpuUsage;
    [ObservableProperty] private double ramUsage;
    [ObservableProperty] private double diskUsage;
    [ObservableProperty] private string diskSizeText = string.Empty;
    [ObservableProperty] private string freeableText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    private bool isLoaded;

    public bool IsIndeterminate => !IsLoaded;

    public string CpuUsageText  => $"{CpuUsage:F0}%";
    public string RamUsageText  => $"{RamUsage:F0}%";
    public string DiskUsageText => $"{DiskUsage:F0}%";

    partial void OnCpuUsageChanged(double value)  => OnPropertyChanged(nameof(CpuUsageText));
    partial void OnRamUsageChanged(double value)  => OnPropertyChanged(nameof(RamUsageText));
    partial void OnDiskUsageChanged(double value) => OnPropertyChanged(nameof(DiskUsageText));

    public DashboardViewModel()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        CpuUsage     = _lastCpu;
        RamUsage     = _lastRam;
        DiskUsage    = _lastDisk;
        DiskSizeText = FormatDiskSize(_lastDiskUsedGb, _lastDiskTotalGb);
        FreeableText = _freeableText;
        IsLoaded     = _hasValues;

        _currentActive = this;

        if (!_isPolling)
        {
            StartUpdating();
        }
        else
        {
            _ = Task.Run(() =>
            {
                var (cpu, ram, disk, totalGb, usedGb) = GetMetrics();
                _lastCpu = cpu; _lastRam = ram; _lastDisk = disk;
                _lastDiskTotalGb = totalGb; _lastDiskUsedGb = usedGb;
                _hasValues = true;
                _dispatcher?.TryEnqueue(() =>
                {
                    CpuUsage     = cpu;
                    RamUsage     = ram;
                    DiskUsage    = disk;
                    DiskSizeText = FormatDiskSize(usedGb, totalGb);
                    IsLoaded     = true;
                });
            });
        }

        if (!_freeableScanDone)
        {
            var cleanerService = new CleanerService();
            var customPaths    = new PreferencesService().LoadPreferences().CustomCleanerPaths;
            _ = ScanFreeableAsync(cleanerService, customPaths);
        }
    }

    private async void StartUpdating()
    {
        _isPolling = true;
        while (_isPolling)
        {
            var (cpu, ram, disk, totalGb, usedGb) = await Task.Run(() => GetMetrics());

            _lastCpu         = cpu;
            _lastRam         = ram;
            _lastDisk        = disk;
            _lastDiskTotalGb = totalGb;
            _lastDiskUsedGb  = usedGb;
            _hasValues       = true;

            _currentActive?._dispatcher?.TryEnqueue(() =>
            {
                _currentActive.CpuUsage     = cpu;
                _currentActive.RamUsage     = ram;
                _currentActive.DiskUsage    = disk;
                _currentActive.DiskSizeText = FormatDiskSize(usedGb, totalGb);
                _currentActive.IsLoaded     = true;
            });

            await Task.Delay(2000);
        }
    }

    private (double cpu, double ram, double disk, double totalGb, double usedGb) GetMetrics()
    {
        double cpu     = _lastCpu;
        double ram     = _lastRam;
        double disk    = _lastDisk;
        double totalGb = _lastDiskTotalGb;
        double usedGb  = _lastDiskUsedGb;

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
                    disk    = Math.Round(((total - free) / total) * 100, 1);
                    totalGb = total / (1024.0 * 1024 * 1024);
                    usedGb  = (total - free) / (1024.0 * 1024 * 1024);
                }
        }
        catch { /* silently use last-known values on WMI failure */ }

        return (cpu, ram, disk, totalGb, usedGb);
    }

    private static async Task ScanFreeableAsync(CleanerService service, List<string> customPaths)
    {
        try
        {
            var cats  = service.GetCategories(customPaths);
            var sizes = await Task.WhenAll(cats.Select(c => service.ScanAsync(c)));
            long total = sizes.Sum();
            _freeableText     = FormatFreeableBytes(total);
            _freeableScanDone = true;
            _currentActive?._dispatcher?.TryEnqueue(() =>
            {
                if (_currentActive != null)
                    _currentActive.FreeableText = _freeableText;
            });
        }
        catch { }
    }

    private static string FormatFreeableBytes(long bytes)
    {
        if (bytes >= 1L << 30) return $"~{bytes / (1024.0 * 1024 * 1024):F1} GB freeable";
        if (bytes >= 1L << 20) return $"~{bytes / (1024.0 * 1024):F0} MB freeable";
        return $"~{bytes / 1024:F0} KB freeable";
    }

    private static string FormatDiskSize(double usedGb, double totalGb) =>
        totalGb > 0 ? $"{usedGb:F0} GB / {totalGb:F0} GB" : string.Empty;
}
