using Microsoft.UI.Xaml.Controls;
using Willow.ViewModels;

namespace Willow.Views;

public sealed partial class CleanerPage : Page
{
    public CleanerViewModel ViewModel { get; } = new();

    public CleanerPage()
    {
        this.InitializeComponent();
    }
}
