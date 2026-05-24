using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rewind.Models;
using Rewind.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Rewind.ViewModels;

public partial class DiskHealthViewModel : ObservableObject
{
    private readonly DiskHealthService _service = new();

    public ObservableCollection<DiskDriveInfo> Drives { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    public DiskHealthViewModel() => _ = LoadAsync();

    [RelayCommand]
    private async Task Refresh()
    {
        IsLoading = true;
        Drives.Clear();
        var list = await Task.Run(_service.GetDrives);
        foreach (var d in list) Drives.Add(d);
        IsLoading = false;
    }

    private async Task LoadAsync() => await Refresh();
}
