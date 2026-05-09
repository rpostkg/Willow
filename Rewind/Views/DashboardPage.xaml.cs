using Microsoft.UI.Xaml.Controls;
using Rewind.ViewModels;

namespace Rewind.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; } = new();

    public DashboardPage()
    {
        this.InitializeComponent();
    }
}
