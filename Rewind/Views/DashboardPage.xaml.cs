using Microsoft.UI.Xaml;
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

    private void CleanUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mw)
            mw.NavigateTo("Cleaner");
    }
}
