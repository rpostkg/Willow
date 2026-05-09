using Microsoft.UI.Xaml.Controls;
using Rewind.ViewModels;

namespace Rewind.Views;

public sealed partial class CleanerPage : Page
{
    public CleanerViewModel ViewModel { get; } = new();

    public CleanerPage()
    {
        this.InitializeComponent();
    }
}
