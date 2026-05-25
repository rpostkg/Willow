using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace Rewind.Models;

public partial class CleanerCategory : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string[] Paths { get; init; } = [];
    public bool RequiresAdmin { get; init; }
    public bool IsCustom { get; init; }

    [ObservableProperty] private bool isSelected = true;
    [ObservableProperty] private long sizeBytes;

    public string SizeText => FormatSize(SizeBytes);
    public Visibility AdminIconVisibility => RequiresAdmin ? Visibility.Visible : Visibility.Collapsed;

    partial void OnSizeBytesChanged(long value) => OnPropertyChanged(nameof(SizeText));

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int i = 0;
        while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
        return $"{size:F2} {units[i]}";
    }
}
