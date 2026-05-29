using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Willow.ViewModels;
using System;
using System.Linq;

namespace Willow.Views;

public sealed partial class CleanerPage : Page
{
    public CleanerViewModel ViewModel { get; } = new();

    private readonly ResourceLoader _res = new();

    public CleanerPage()
    {
        this.InitializeComponent();
    }

    private async void CleanSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var targets = ViewModel.Categories.Where(c => c.IsSelected).ToList();
        if (targets.Count == 0) return;

        long totalBytes = targets.Sum(c => c.SizeBytes);

        var list = new StackPanel { Spacing = 4 };
        foreach (var cat in targets)
            list.Children.Add(new TextBlock { Text = "• " + cat.Name, TextWrapping = TextWrapping.Wrap });

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = string.Format(
                _res.GetString("Cleaner_ConfirmMessageFormat"),
                CleanerViewModel.FormatSize(totalBytes)),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new ScrollViewer { MaxHeight = 240, Content = list });

        var dialog = new ContentDialog
        {
            XamlRoot = this.Content.XamlRoot,
            RequestedTheme = this.ActualTheme,
            Title = _res.GetString("Cleaner_ConfirmTitle"),
            PrimaryButtonText = _res.GetString("Cleaner_ConfirmPrimary"),
            CloseButtonText = _res.GetString("Cleaner_ConfirmCancel"),
            DefaultButton = ContentDialogButton.Primary,
            Content = content
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.CleanSelectedCommand.ExecuteAsync(null);
    }
}
