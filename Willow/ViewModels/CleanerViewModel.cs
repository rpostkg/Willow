using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using Willow.Models;
using Willow.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Willow.ViewModels;

public partial class CleanerViewModel : ObservableObject
{
    private readonly CleanerService _service = new();
    private readonly ResourceLoader _res = new();

    public ObservableCollection<CleanerCategory> Categories { get; } = new();

    [ObservableProperty] private bool isScanning;
    [ObservableProperty] private string totalSelectedText = string.Empty;
    [ObservableProperty] private string status = string.Empty;

    public CleanerViewModel()
    {
        LoadCategories();
        _ = ScanAll();
    }

    private void LoadCategories()
    {
        var customPaths = new PreferencesService().LoadPreferences().CustomCleanerPaths;
        foreach (var cat in _service.GetCategories(customPaths))
        {
            cat.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CleanerCategory.IsSelected) or nameof(CleanerCategory.SizeBytes))
                    UpdateTotal();
            };
            Categories.Add(cat);
        }
        UpdateTotal();
    }

    [RelayCommand]
    private async Task ScanAll()
    {
        IsScanning = true;
        Status = _res.GetString("Cleaner_StatusCalculating");
        await Task.WhenAll(Categories.Select(async c => c.SizeBytes = await _service.ScanAsync(c)));
        IsScanning = false;
        Status = _res.GetString("Cleaner_StatusReady");
        UpdateTotal();
    }

    [RelayCommand]
    private async Task CleanSelected()
    {
        var targets = Categories.Where(c => c.IsSelected).ToList();
        if (targets.Count == 0) return;
        Status = _res.GetString("Cleaner_StatusCleaning");
        await Task.Run(() => { foreach (var cat in targets) _service.CleanCategory(cat); });
        await ScanAll();
        Status = _res.GetString("Cleaner_StatusDone");
    }

    [RelayCommand]
    private void SelectAll() { foreach (var c in Categories) c.IsSelected = true; }

    [RelayCommand]
    private void SelectNone() { foreach (var c in Categories) c.IsSelected = false; }

    private void UpdateTotal()
    {
        long total = Categories.Where(c => c.IsSelected).Sum(c => c.SizeBytes);
        TotalSelectedText = string.Format(_res.GetString("Cleaner_TotalSelectedFormat"), FormatSize(total));
    }

    internal static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int i = 0;
        while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
        return $"{size:F2} {units[i]}";
    }
}
