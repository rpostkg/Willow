using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rewind.ViewModels;

namespace Rewind.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = new();

    public SettingsPage() => InitializeComponent();

    private async void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var textBox = new TextBox
        {
            PlaceholderText = @"Paste or type path, e.g. C:\MyFolder",
            MinWidth = 420,
        };

        var dialog = new ContentDialog
        {
            Title = "Add custom folder",
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = textBox,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var path = textBox.Text.Trim().Trim('"');
        if (Directory.Exists(path))
            ViewModel.AddCustomPath(path);
    }

    private void RemovePathButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
            ViewModel.RemoveCustomPath(path);
    }
}
