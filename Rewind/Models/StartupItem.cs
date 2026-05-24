using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace Rewind.Models;

public partial class StartupItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string Hive { get; init; } = string.Empty;
    public string HiveLabel { get; init; } = string.Empty;
    public bool RequiresAdmin { get; init; }
    public bool CanToggle => !RequiresAdmin;
    public Visibility AdminIconVisibility => RequiresAdmin ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private bool isEnabled;
}
