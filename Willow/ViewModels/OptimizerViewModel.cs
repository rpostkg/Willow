using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Willow.Models;
using Willow.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Willow.ViewModels;

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

    // --- Search / filter ---

    /// <summary>Free-text filter applied against each tweak's Name.</summary>
    [ObservableProperty]
    private string nameQuery = string.Empty;

    /// <summary>The category string chosen in the ComboBox. "All" means all categories.</summary>
    [ObservableProperty]
    private string selectedCategory = "All";

    /// <summary>Distinct category list, prepended with an "All" sentinel, for the ComboBox.</summary>
    public ObservableCollection<string> Categories { get; } = new();

    public IEnumerable<Tweak> FilteredTweaks
    {
        get
        {
            var result = (IEnumerable<Tweak>)Tweaks;

            if (!string.IsNullOrWhiteSpace(NameQuery))
                result = result.Where(t =>
                    t.Name != null &&
                    t.Name.Contains(NameQuery, System.StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "All")
                result = result.Where(t =>
                    t.Category != null &&
                    t.Category.Equals(SelectedCategory, System.StringComparison.OrdinalIgnoreCase));

            return result;
        }
    }

    partial void OnNameQueryChanged(string value)     => OnPropertyChanged(nameof(FilteredTweaks));
    partial void OnSelectedCategoryChanged(string? value) => OnPropertyChanged(nameof(FilteredTweaks));

    // --- Selection state ---
    public bool AreTweaksSelected  => Tweaks.Any(t => t.IsEnabled);
    public bool CanReviewOrRevert  => AreTweaksSelected && IsInformedOfBackups;

    public OptimizerViewModel()
    {
        var prefs = new PreferencesService().LoadPreferences();
        isInformedOfBackups = prefs.InformedOfBackups;
        LoadTweaks();
    }

    private void LoadTweaks()
    {
        var prefs = new PreferencesService().LoadPreferences();
        var loadedTweaks = _loaderService.LoadTweaks();
        foreach (var tweak in loadedTweaks)
        {
            tweak.IsApplied = prefs.OldRegistryData.ContainsKey(tweak.Id);
            tweak.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Tweak.IsEnabled))
                {
                    OnPropertyChanged(nameof(AreTweaksSelected));
                    OnPropertyChanged(nameof(CanReviewOrRevert));

                    if (AreTweaksSelected && !IsInformedOfBackups)
                        IsInfoBarOpen = true;
                }
            };
            Tweaks.Add(tweak);
        }

        // Build the category list from whatever tweaks were loaded
        var distinctCategories = Tweaks
            .Select(t => t.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c);

        Categories.Add("All");
        foreach (var cat in distinctCategories)
            Categories.Add(cat);

        // Ensure default selection after categories are populated
        SelectedCategory = "All";
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
        foreach (var t in Tweaks) t.IsEnabled = true;
    }

    public async Task<List<ChangeItem>> GenerateReportAsync(List<Tweak> tweaks, bool isRevert)
        => await _engineService.GenerateChangeReportAsync(tweaks, isRevert);

    public async Task ExecuteTweaksAsync(List<Tweak> tweaks, List<ChangeItem> report, bool isRevert)
    {
        await Task.Run(() =>
        {
            if (isRevert) _engineService.RevertTweaks(tweaks);
            else          _engineService.ApplyTweaks(tweaks, report);
        });
        foreach (var tweak in tweaks)
            tweak.IsApplied = !isRevert;
    }
}
