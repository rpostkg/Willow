using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace Rewind.Models;

public enum StartupSource { Registry, StartupFolder, TaskScheduler }

public partial class StartupItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string HiveLabel { get; init; } = string.Empty;
    public bool RequiresAdmin { get; init; }
    public bool CanToggle => true;
    public Visibility AdminIconVisibility => RequiresAdmin ? Visibility.Visible : Visibility.Collapsed;

    public StartupSource Source { get; init; }
    public string Hive { get; init; } = string.Empty;      // "HKCU" / "HKLM" for Registry
    public string? ShortcutName { get; init; }              // filename for StartupFolder (e.g. "Tailscale.lnk")
    public string? LnkPath { get; init; }                  // canonical .lnk path (without .disabled suffix)
    public string? TaskPath { get; init; }                  // full path for TaskScheduler

    [ObservableProperty]
    private bool isEnabled;
}
