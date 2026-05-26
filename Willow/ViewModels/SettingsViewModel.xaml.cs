using CommunityToolkit.Mvvm.ComponentModel;

namespace Willow.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string message = "Settings skeleton";
}
