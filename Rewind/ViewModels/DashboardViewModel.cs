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
    private static double _lastCpuSpeedMhz = 0;
    private static double _lastRamUsedGb = 0;
    private static double _lastRamTotalGb = 0;
    private static double _lastDiskTotalGb = 0;
    private static double _lastDiskUsedGb = 0;
    private static string _freeableText = string.Empty;
    private static bool _freeableScanDone = false;
    private static bool _hasValues = false;
    private static bool _isPolling = false;
    private static DashboardViewModel? _currentActive;

    [ObservableProperty] private double cpuUsage;
    [ObservableProperty] private string cpuSpeedText = string.Empty;
    [ObservableProperty] private double ramUsage;
    [ObservableProperty] private string ramSizeText = string.Empty;
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
        CpuSpeedText = FormatCpuSpeed(_lastCpuSpeedMhz);
        RamUsage     = _lastRam;
        RamSizeText  = FormatRamSize(_lastRamUsedGb, _lastRamTotalGb);
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
                var (cpu, cpuMhz, ram, ramUsed, ramTotal, disk, diskTotal, diskUsed) = GetMetrics();
                _lastCpu = cpu; _lastCpuSpeedMhz = cpuMhz;
                _lastRam = ram; _lastRamUsedGb = ramUsed; _lastRamTotalGb = ramTotal;
                _lastDisk = disk; _lastDiskTotalGb = diskTotal; _lastDiskUsedGb = diskUsed;
                _hasValues = true;
                _dispatcher?.TryEnqueue(() =>
                {
                    CpuUsage     = cpu;
                    CpuSpeedText = FormatCpuSpeed(cpuMhz);
                    RamUsage     = ram;
                    RamSizeText  = FormatRamSize(ramUsed, ramTotal);
                    DiskUsage    = disk;
                    DiskSizeText = FormatDiskSize(diskUsed, diskTotal);
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
            var (cpu, cpuMhz, ram, ramUsed, ramTotal, disk, diskTotal, diskUsed) = await Task.Run(() => GetMetrics());

            _lastCpu          = cpu;
            _lastCpuSpeedMhz  = cpuMhz;
            _lastRam          = ram;
            _lastRamUsedGb    = ramUsed;
            _lastRamTotalGb   = ramTotal;
            _lastDisk         = disk;
            _lastDiskTotalGb  = diskTotal;
            _lastDiskUsedGb   = diskUsed;
            _hasValues        = true;

            _currentActive?._dispatcher?.TryEnqueue(() =>
            {
                _currentActive.CpuUsage     = cpu;
                _currentActive.CpuSpeedText = FormatCpuSpeed(cpuMhz);
                _currentActive.RamUsage     = ram;
                _currentActive.RamSizeText  = FormatRamSize(ramUsed, ramTotal);
                _currentActive.DiskUsage    = disk;
                _currentActive.DiskSizeText = FormatDiskSize(diskUsed, diskTotal);
                _currentActive.IsLoaded     = true;
            });

            await Task.Delay(2000);
        }
    }

    private (double cpu, double cpuMhz, double ram, double ramUsedGb, double ramTotalGb, double disk, double diskTotalGb, double diskUsedGb) GetMetrics()
    {
        double cpu        = _lastCpu;
        double cpuMhz     = _lastCpuSpeedMhz;
        double ram        = _lastRam;
        double ramUsedGb  = _lastRamUsedGb;
        double ramTotalGb = _lastRamTotalGb;
        double disk       = _lastDisk;
        double diskTotalGb = _lastDiskTotalGb;
        double diskUsedGb  = _lastDiskUsedGb;

        try
        {
            using (var s = new ManagementObjectSearcher("select LoadPercentage, CurrentClockSpeed from Win32_Processor"))
                foreach (var o in s.Get())
                {
                    cpu    = Convert.ToDouble(o["LoadPercentage"]);
                    cpuMhz = Convert.ToDouble(o["CurrentClockSpeed"]);
                }

            using (var s = new ManagementObjectSearcher(
                "select FreePhysicalMemory, TotalVisibleMemorySize from Win32_OperatingSystem"))
                foreach (var o in s.Get())
                {
                    double free  = Convert.ToDouble(o["FreePhysicalMemory"]);
                    double total = Convert.ToDouble(o["TotalVisibleMemorySize"]);
                    ram       = Math.Round(((total - free) / total) * 100, 1);
                    ramUsedGb  = (total - free) / (1024.0 * 1024);
                    ramTotalGb = total / (1024.0 * 1024);
                }

            using (var s = new ManagementObjectSearcher(
                "select FreeSpace, Size from Win32_LogicalDisk where DeviceID='C:'"))
                foreach (var o in s.Get())
                {
                    double free  = Convert.ToDouble(o["FreeSpace"]);
                    double total = Convert.ToDouble(o["Size"]);
                    disk       = Math.Round(((total - free) / total) * 100, 1);
                    diskTotalGb = total / (1024.0 * 1024 * 1024);
                    diskUsedGb  = (total - free) / (1024.0 * 1024 * 1024);
                }
        }
        catch { /* silently use last-known values on WMI failure */ }

        return (cpu, cpuMhz, ram, ramUsedGb, ramTotalGb, disk, diskTotalGb, diskUsedGb);
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

    private static string FormatCpuSpeed(double mhz) =>
        mhz > 0 ? $"{mhz / 1000.0:F1} GHz" : string.Empty;

    private static string FormatRamSize(double usedGb, double totalGb) =>
        totalGb > 0 ? $"{usedGb:F1} GB / {totalGb:F0} GB" : string.Empty;

    private static string FormatDiskSize(double usedGb, double totalGb) =>
        totalGb > 0 ? $"{usedGb:F0} GB / {totalGb:F0} GB" : string.Empty;
}
