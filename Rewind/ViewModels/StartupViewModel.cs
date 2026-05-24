using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rewind.Models;
using Rewind.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

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
}
