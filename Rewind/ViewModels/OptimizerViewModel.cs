using CommunityToolkit.Mvvm.ComponentModel;

namespace Rewind.ViewModels;

public partial class OptimizerViewModel : ObservableObject
{
    [ObservableProperty]
    private string message = "Optimizer module skeleton. Ready for future implementation.";
}
