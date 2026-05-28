using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using System.Linq;
using Windows.UI;
using Willow.Services;

namespace Willow.ViewModels;

public enum PrivacyScore { Low, Fair, Good }

public partial class PrivacyViewModel : ObservableObject
{
    private bool _loading;
    private string _privacyCategoryFilter = string.Empty;
    public string PrivacyCategoryFilter => _privacyCategoryFilter;

    [ObservableProperty]
    private string scoreLabel = string.Empty;

    [ObservableProperty]
    private SolidColorBrush scoreBackground =
        new(Color.FromArgb(255, 136, 136, 136));

    [ObservableProperty]
    private bool cameraEnabled;

    [ObservableProperty]
    private bool micEnabled;

    [ObservableProperty]
    private bool locationEnabled;

    public PrivacyViewModel()
    {
        var res = new ResourceLoader();
        LoadScore(res);
        LoadToggles();
    }

    private void LoadScore(ResourceLoader res)
    {
        var tweaks = new TweakLoaderService().LoadTweaksFromFile("privacy.yaml");
        var prefs = new PreferencesService().LoadPreferences();
        var applied = tweaks.Count(t => prefs.OldRegistryData.ContainsKey(t.Id));

        _privacyCategoryFilter = tweaks.FirstOrDefault()?.Category ?? string.Empty;
        var score = CalculateScore(applied, tweaks.Count);
        ScoreLabel = LabelForScore(score, res);
        ScoreBackground = BrushForScore(score);
    }

    private void LoadToggles()
    {
        _loading = true;
        CameraEnabled   = AppPermissionsService.GetGlobalToggle("webcam");
        MicEnabled      = AppPermissionsService.GetGlobalToggle("microphone");
        LocationEnabled = AppPermissionsService.GetGlobalToggle("location");
        _loading = false;
    }

    partial void OnCameraEnabledChanged(bool value)
    {
        if (!_loading) AppPermissionsService.SetGlobalToggle("webcam", value);
    }

    partial void OnMicEnabledChanged(bool value)
    {
        if (!_loading) AppPermissionsService.SetGlobalToggle("microphone", value);
    }

    partial void OnLocationEnabledChanged(bool value)
    {
        if (!_loading) AppPermissionsService.SetGlobalToggle("location", value);
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
        PrivacyScore.Good => new SolidColorBrush(Color.FromArgb(255, 0,  128,  0)),
        PrivacyScore.Fair => new SolidColorBrush(Color.FromArgb(255, 202,  80,  16)),
        _                 => new SolidColorBrush(Color.FromArgb(255, 196,  43,  28)),
    };
}
