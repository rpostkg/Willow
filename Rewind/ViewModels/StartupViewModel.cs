using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rewind.Models;
using Rewind.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Rewind.ViewModels;

public partial class StartupViewModel : ObservableObject
{
    private readonly StartupService _service = new();
    private bool _applying;

    public ObservableCollection<StartupItem> StartupItems { get; } = new();

    public StartupViewModel() => LoadItems();

    [RelayCommand]
    private void Refresh() => LoadItems();

    private void LoadItems()
    {
        foreach (var item in StartupItems)
            item.PropertyChanged -= OnItemChanged;

        StartupItems.Clear();

        foreach (var item in _service.GetStartupItems())
        {
            item.PropertyChanged += OnItemChanged;
            StartupItems.Add(item);
        }
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
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StartupViewModel] OpenLocation failed: {ex.Message}");
        }
    }

    private static string ResolveExecutablePath(string command)
    {
        if (string.IsNullOrEmpty(command)) return string.Empty;

        // Quoted path: "C:\path\app.exe" --args
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end > 0) return command[1..end];
        }

        // Unquoted: split at first space (args follow)
        var spaceIdx = command.IndexOf(' ');
        var path = spaceIdx > 0 ? command[..spaceIdx] : command;

        return Path.IsPathRooted(path) ? path : string.Empty;
    }
}
