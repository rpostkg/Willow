using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace Willow.Models;

public partial class AppPermission : ObservableObject
{
    public string AppKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    [ObservableProperty]
    private bool hasCamera;

    [ObservableProperty]
    private bool hasMic;

    [ObservableProperty]
    private bool hasLocation;

    public bool HasCameraEntry { get; init; }
    public bool HasMicEntry { get; init; }
    public bool HasLocationEntry { get; init; }

    public Visibility CameraVisibility   => HasCameraEntry   ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MicVisibility      => HasMicEntry      ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LocationVisibility => HasLocationEntry ? Visibility.Visible : Visibility.Collapsed;
}
