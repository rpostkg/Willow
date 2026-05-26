using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Willow.Models;
using Willow.ViewModels;
using System;

namespace Willow.Views;

public sealed partial class StartupPage : Page
{
    public StartupViewModel ViewModel { get; } = new();

    public StartupPage()
    {
        this.InitializeComponent();
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ViewModel.NameQuery = sender.Text;
    }

    private void OpenLocationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is StartupItem item)
            ViewModel.OpenLocation(item);
    }
}
