using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.ApplicationModel.Resources;
using Rewind.Models;
using Rewind.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Rewind.ViewModels;

public partial class StartupViewModel : ObservableObject
{
    private readonly StartupService _service = new();
    private readonly string _allLabel;
    private readonly List<StartupItem> _allItems = new();
    private bool _applying;

    public ObservableCollection<string> SourceFilters { get; } = new();

    [ObservableProperty]
    private string nameQuery = string.Empty;

    [ObservableProperty]
    private string selectedSource = string.Empty;

    partial void OnNameQueryChanged(string _) => OnPropertyChanged(nameof(FilteredItems));
    partial void OnSelectedSourceChanged(string? _) => OnPropertyChanged(nameof(FilteredItems));

    public IEnumerable<StartupItem> FilteredItems
    {
        get
        {
            IEnumerable<StartupItem> result = _allItems;

            if (!string.IsNullOrWhiteSpace(NameQuery))
                result = result.Where(i =>
                    i.Name.Contains(NameQuery, StringComparison.OrdinalIgnoreCase) ||
                    i.Command.Contains(NameQuery, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SelectedSource) && SelectedSource != _allLabel)
                result = result.Where(i => i.HiveLabel == SelectedSource);

            return result;
        }
    }

    public StartupViewModel()
    {
        var res = new ResourceLoader();
        _allLabel = res.GetString("StartupPage_FilterAll");
        LoadItems();
    }

    [RelayCommand]
    private void Refresh() => LoadItems();

    private void LoadItems()
    {
        foreach (var item in _allItems)
            item.PropertyChanged -= OnItemChanged;
        _allItems.Clear();

        var resolveShortcuts = new PreferencesService().LoadPreferences().ResolveShortcuts;
        foreach (var item in _service.GetStartupItems(resolveShortcuts))
        {
            item.PropertyChanged += OnItemChanged;
            _allItems.Add(item);
        }

        RebuildSourceFilters();
        OnPropertyChanged(nameof(FilteredItems));
    }

    private void RebuildSourceFilters()
    {
        var previous = SelectedSource;
        SourceFilters.Clear();
        SourceFilters.Add(_allLabel);
        foreach (var label in _allItems.Select(i => i.HiveLabel).Distinct())
            SourceFilters.Add(label);

        // Restore previous selection if it still exists, else default to "All"
        SelectedSource = SourceFilters.Contains(previous) ? previous : _allLabel;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_applying || e.PropertyName != nameof(StartupItem.IsEnabled) || sender is not StartupItem item) return;
        _applying = true;
        try
        {
            _service.SetEnabled(item, item.IsEnabled);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupViewModel] Reverting toggle: {ex.Message}");
            item.IsEnabled = !item.IsEnabled;
        }
        finally
        {
            _applying = false;
        }
    }

    public void OpenLocation(StartupItem item)
    {
        var path = ResolveExecutablePath(item.Command);
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupViewModel] OpenLocation failed: {ex.Message}");
        }
    }

    private static string ResolveExecutablePath(string command)
    {
        if (string.IsNullOrEmpty(command)) return string.Empty;

        // Full command is itself a file (startup folder .lnk, or bare exe with no args)
        if (File.Exists(command)) return command;

        // Quoted: "C:\path\to\app.exe" --args
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end > 0) return command[1..end];
        }

        // Unquoted: split at first space to separate path from args
        var spaceIdx = command.IndexOf(' ');
        var path = spaceIdx > 0 ? command[..spaceIdx] : command;
        return Path.IsPathRooted(path) ? path : string.Empty;
    }
}
