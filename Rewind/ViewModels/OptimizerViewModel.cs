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

    [ObservableProperty]
    private bool isInformedOfBackups;

    [ObservableProperty]
    private bool isInfoBarOpen;

    public bool AreTweaksSelected => Tweaks.Any(t => t.IsEnabled);
    public bool CanReviewOrRevert => AreTweaksSelected && IsInformedOfBackups;

    public OptimizerViewModel()
    {
        var prefs = new PreferencesService().LoadPreferences();
        isInformedOfBackups = prefs.InformedOfBackups;
        
        LoadTweaks();
    }

    private void LoadTweaks()
    {
        var loadedTweaks = _loaderService.LoadTweaks();
        foreach (var tweak in loadedTweaks)
        {
            tweak.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(Tweak.IsEnabled))
                {
                    OnPropertyChanged(nameof(AreTweaksSelected));
                    OnPropertyChanged(nameof(CanReviewOrRevert));
                    
                    if (AreTweaksSelected && !IsInformedOfBackups)
                    {
                        IsInfoBarOpen = true;
                    }
                }
            };
            Tweaks.Add(tweak);
        }
    }

    [RelayCommand]
    private void MarkAsInformed()
    {
        IsInformedOfBackups = true;
        IsInfoBarOpen = false;
        
        var prefsService = new PreferencesService();
        var prefs = prefsService.LoadPreferences();
        prefs.InformedOfBackups = true;
        prefsService.SavePreferences(prefs);
        
        OnPropertyChanged(nameof(CanReviewOrRevert));
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

    public async Task ExecuteTweaksAsync(List<Tweak> tweaks, List<ChangeItem> report, bool isRevert)
    {
        await Task.Run(() => 
        {
            if (isRevert) _engineService.RevertTweaks(tweaks);
            else _engineService.ApplyTweaks(tweaks, report);
        });
    }
}
