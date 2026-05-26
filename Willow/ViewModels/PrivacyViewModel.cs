using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Windows.UI;
using Willow.Models;
using Willow.Services;

namespace Willow.ViewModels;

public enum PrivacyScore { Low, Fair, Good }

public partial class PrivacyViewModel : ObservableObject
{
    private bool _applying;

    [ObservableProperty]
    private string scoreLabel = string.Empty;

    [ObservableProperty]
    private SolidColorBrush scoreBackground =
        new(Color.FromArgb(255, 136, 136, 136));

    [ObservableProperty]
    private Visibility emptyStateVisibility = Visibility.Collapsed;

    public ObservableCollection<AppPermission> AppPermissions { get; } = new();

    public PrivacyViewModel()
    {
        var res = new ResourceLoader();
        LoadScore(res);
        LoadPermissions();
    }

    private void LoadScore(ResourceLoader res)
    {
        var tweaks = new TweakLoaderService().LoadTweaksFromFile("privacy.yaml");
        var prefs = new PreferencesService().LoadPreferences();
        var applied = tweaks.Count(t => prefs.OldRegistryData.ContainsKey(t.Id));

        var score = CalculateScore(applied, tweaks.Count);
        ScoreLabel = LabelForScore(score, res);
        ScoreBackground = BrushForScore(score);
    }

    private void LoadPermissions()
    {
        foreach (var item in AppPermissions)
            item.PropertyChanged -= OnPermissionChanged;
        AppPermissions.Clear();

        foreach (var perm in AppPermissionsService.GetPermissions())
        {
            perm.PropertyChanged += OnPermissionChanged;
            AppPermissions.Add(perm);
        }

        EmptyStateVisibility = AppPermissions.Any()
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnPermissionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_applying || sender is not AppPermission perm) return;

        var capability = e.PropertyName switch
        {
            nameof(AppPermission.HasCamera)   => "webcam",
            nameof(AppPermission.HasMic)      => "microphone",
            nameof(AppPermission.HasLocation) => "location",
            _ => null
        };
        if (capability == null) return;

        var allow = e.PropertyName switch
        {
            nameof(AppPermission.HasCamera)   => perm.HasCamera,
            nameof(AppPermission.HasMic)      => perm.HasMic,
            _ => perm.HasLocation
        };

        _applying = true;
        try { AppPermissionsService.SetPermission(perm.AppKey, capability, allow); }
        finally { _applying = false; }
    }

    internal static PrivacyScore CalculateScore(int applied, int total)
    {
        if (total == 0) return PrivacyScore.Low;
        var ratio = (double)applied / total;
        if (ratio >= 0.75) return PrivacyScore.Good;
        if (ratio >= 0.25) return PrivacyScore.Fair;
        return PrivacyScore.Low;
    }

    private static string LabelForScore(PrivacyScore score, ResourceLoader res) => score switch
    {
        PrivacyScore.Good => res.GetString("PrivacyPage_ScoreGood"),
        PrivacyScore.Fair => res.GetString("PrivacyPage_ScoreFair"),
        _                 => res.GetString("PrivacyPage_ScoreLow"),
    };

    internal static SolidColorBrush BrushForScore(PrivacyScore score) => score switch
    {
        PrivacyScore.Good => new SolidColorBrush(Color.FromArgb(255, 22,  198,  12)),
        PrivacyScore.Fair => new SolidColorBrush(Color.FromArgb(255, 202,  80,  16)),
        _                 => new SolidColorBrush(Color.FromArgb(255, 196,  43,  28)),
    };
}
