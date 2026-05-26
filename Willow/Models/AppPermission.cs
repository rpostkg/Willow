using CommunityToolkit.Mvvm.ComponentModel;

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
}
