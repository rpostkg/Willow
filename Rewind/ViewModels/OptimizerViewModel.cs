using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rewind.Models;
using Rewind.Services;
using System.Collections.Generic;
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
    private void SelectAll()
    {
        foreach(var t in Tweaks) t.IsEnabled = true;
    }

    public async Task<List<ChangeItem>> GenerateReportAsync(List<Tweak> tweaks, bool isRevert)
    {
        return await _engineService.GenerateChangeReportAsync(tweaks, isRevert);
    }

    public async Task ExecuteTweaksAsync(List<Tweak> tweaks, bool isRevert)
    {
        await Task.Run(() => 
        {
            if (isRevert) _engineService.RevertTweaks(tweaks);
            else _engineService.ApplyTweaks(tweaks);
        });
    }
}
