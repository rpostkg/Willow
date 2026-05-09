using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Rewind.ViewModels;

public partial class CleanerViewModel : ObservableObject
{
    [ObservableProperty]
    private string tempSize = "Calculating...";

    [ObservableProperty]
    private string winSxSSize = "Unknown";

    [ObservableProperty]
    private string status = "Ready to clean.";

    public CleanerViewModel()
    {
        _ = CalculateSizesAsync();
    }

    private async Task CalculateSizesAsync()
    {
        await Task.Run(() =>
        {
            long size = 0;
            size += GetDirectorySize(Path.GetTempPath());
            size += GetDirectorySize(@"C:\Windows\Temp");
            TempSize = $"{size / 1024 / 1024} MB";
        });
    }

    private long GetDirectorySize(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath)) return 0;
            return Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories).Sum(t => (new FileInfo(t).Length));
        }
        catch { return 0; }
    }

    private void CleanDirectory(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;
        foreach (var file in Directory.GetFiles(folderPath))
        {
            try { File.Delete(file); } catch { }
        }
        foreach (var dir in Directory.GetDirectories(folderPath))
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [RelayCommand]
    private async Task CleanAsync()
    {
        Status = "Cleaning temporary files...";
        await Task.Run(() =>
        {
            CleanDirectory(Path.GetTempPath());
            CleanDirectory(@"C:\Windows\Temp");
        });
        await CalculateSizesAsync();
        Status = "Cleanup complete.";
    }

    [RelayCommand]
    private async Task CleanWinSxSAsync()
    {
        Status = "Cleaning WinSxS (Requires Elevation)...";
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = "/online /Cleanup-Image /StartComponentCleanup",
                UseShellExecute = true,
                Verb = "runas"
            };
            var process = Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
            Status = "WinSxS Cleanup complete.";
        }
        catch (Exception ex)
        {
            Status = $"Failed: {ex.Message}";
        }
    }
}
