using CommunityToolkit.Mvvm.ComponentModel;
using Rewind.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml;

namespace Rewind.ViewModels;

public class LanguageOption
{
    public string Code { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public override string ToString() => DisplayName;
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly PreferencesService _prefsService = new();

    [ObservableProperty]
    private bool enableBackups;

    [ObservableProperty]
    private bool resolveShortcuts;

    [ObservableProperty]
    private LanguageOption? selectedLanguage;

    public List<LanguageOption> AvailableLanguages { get; } = new()
    {
        new() { Code = "uk-UA", DisplayName = "Українська" },
        new() { Code = "en-US", DisplayName = "English" },
    };

    public SettingsViewModel()
    {
        var prefs = _prefsService.LoadPreferences();
        enableBackups = !prefs.DisableBackups;
        resolveShortcuts = prefs.ResolveShortcuts;
        selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == prefs.Language)
                           ?? AvailableLanguages[0];
    }

    partial void OnEnableBackupsChanged(bool value)
    {
        var prefs = _prefsService.LoadPreferences();
        prefs.DisableBackups = !value;
        _prefsService.SavePreferences(prefs);
    }

    partial void OnResolveShortcutsChanged(bool value)
    {
        var prefs = _prefsService.LoadPreferences();
        prefs.ResolveShortcuts = value;
        _prefsService.SavePreferences(prefs);
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null) return;
        var prefs = _prefsService.LoadPreferences();
        if (prefs.Language == value.Code) return;
        prefs.Language = value.Code;
        _prefsService.SavePreferences(prefs);

        var exe = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(exe))
        {
            Process.Start(exe);
            Application.Current.Exit();
        }
    }
}
