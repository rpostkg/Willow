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
    private string tempSize = "Розрахунок...";

    [ObservableProperty]
    private string userTempSize = "Розрахунок...";

    [ObservableProperty]
    private string windowsTempSize = "Розрахунок...";

    [ObservableProperty]
    private string status = "Готово до очищення.";

    public CleanerViewModel()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _ = CalculateSizesAsync();
    }

    private async Task CalculateSizesAsync()
    {
        await Task.Run(() =>
        {
            string userTempPath = Path.GetTempPath();
            long userSize = GetDirectorySize(userTempPath);
            long winSize = GetDirectorySize(@"C:\Windows\Temp");
            long size = userSize + winSize;

            _dispatcher?.TryEnqueue(() =>
            {
                UserTempSize = FormatSize(userSize);
                WindowsTempSize = FormatSize(winSize);
                TempSize = FormatSize(size);
            });
        });
    }

    private string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:F2} {units[unitIndex]}";
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
        Status = "Очищення тимчасових файлів...";
        await Task.Run(() =>
        {
            CleanDirectory(Path.GetTempPath());
            CleanDirectory(@"C:\Windows\Temp");
        });
        await CalculateSizesAsync();
        Status = "Очищення завершено.";
    }

    [RelayCommand]
    private async Task CleanWinSxSAsync()
    {
        Status = "Очищення WinSxS (Потрібні права адміністратора)...";
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
            Status = "Очищення WinSxS завершено.";
        }
        catch (Exception ex)
        {
            Status = $"Помилка: {ex.Message}";
        }
    }
}
