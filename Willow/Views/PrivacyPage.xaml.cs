using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Willow.ViewModels;

namespace Willow.Views;

public sealed partial class PrivacyPage : Page
{
    public PrivacyViewModel ViewModel { get; } = new();

    public PrivacyPage()
    {
        this.InitializeComponent();
    }

    private void ViewTweaksButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mw)
            mw.NavigateTo("Optimizer", ViewModel.PrivacyCategoryFilter);
    }
}
