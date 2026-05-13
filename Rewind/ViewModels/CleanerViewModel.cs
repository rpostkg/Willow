using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Rewind.ViewModels;

public partial class CleanerViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty]
    private string tempSize = "Calculating...";

    [ObservableProperty]
    private string userTempSize = "Calculating...";

    [ObservableProperty]
    private string windowsTempSize = "Calculating...";

    [ObservableProperty]
    private string winSxSSize = "Unknown";

    [ObservableProperty]
    private string status = "Ready to clean.";

    public CleanerViewModel()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _ = CalculateSizesAsync();
    }

    private async Task CalculateSizesAsync()
    {
        await Task.Run(() =>
        {
            Debug.Print("Async calculation task running");

            string userTempPath = Path.GetTempPath();
            long userSize = GetDirectorySize(userTempPath);
            long winSize = GetDirectorySize(@"C:\Windows\Temp");
            long size = userSize + winSize;

            string result = $"{size / 1024 / 1024} MB";
            string userResult = $"{userSize / 1024 / 1024} MB";
            string windowsResult = $"{winSize / 1024 / 1024} MB";

            _dispatcher?.TryEnqueue(() =>
            {
                TempSize = result;
                UserTempSize = userResult;
                WindowsTempSize = windowsResult;
            });
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

    // TODO: Separate clean-ups into elevated and non-elevated states. We can clean User temp just fine but not Windows temp without elevation.
    private void CleanDirectory(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;
        try
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                try { File.Delete(file); } catch { }
            }
        } catch { }
        try
        {
            foreach (var dir in Directory.GetDirectories(folderPath))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        } catch { }
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
