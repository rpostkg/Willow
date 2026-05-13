using CommunityToolkit.Mvvm.ComponentModel;
using Rewind.Services;

namespace Rewind.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly PreferencesService _prefsService = new();

    [ObservableProperty]
    private bool enableBackups;

    public SettingsViewModel()
    {
        var prefs = _prefsService.LoadPreferences();
        enableBackups = !prefs.DisableBackups;
    }

    partial void OnEnableBackupsChanged(bool value)
    {
        var prefs = _prefsService.LoadPreferences();
        prefs.DisableBackups = !value;
        _prefsService.SavePreferences(prefs);
    }
}
