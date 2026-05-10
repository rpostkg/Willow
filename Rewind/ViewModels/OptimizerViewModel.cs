using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rewind.Models;
using Rewind.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Rewind.ViewModels;

public partial class OptimizerViewModel : ObservableObject
{
    private readonly TweakLoaderService _loaderService = new();
    private readonly TweakEngineService _engineService = new();

    [ObservableProperty]
    private ObservableCollection<Tweak> tweaks = new();

    public OptimizerViewModel()
    {
        LoadTweaks();
    }

    private void LoadTweaks()
    {
        var loadedTweaks = _loaderService.LoadTweaks();
        foreach (var tweak in loadedTweaks)
        {
            Tweaks.Add(tweak);
        }
    }

    [RelayCommand]
    private async Task ApplySelectedTweaksAsync()
    {
        var selectedTweaks = Tweaks.Where(t => t.IsEnabled).ToList();
        if (selectedTweaks.Any())
        {
            await Task.Run(() => _engineService.ApplyTweaks(selectedTweaks));
        }
    }

    [RelayCommand]
    private async Task RevertSelectedTweaksAsync()
    {
        var selectedTweaks = Tweaks.Where(t => t.IsEnabled).ToList();
        if (selectedTweaks.Any())
        {
            await Task.Run(() => _engineService.RevertTweaks(selectedTweaks));
        }
    }
}
