using Microsoft.UI.Xaml.Controls;
using Rewind.ViewModels;
using System;
using System.Linq;

namespace Rewind.Views;

public sealed partial class OptimizerPage : Page
{
    public OptimizerViewModel ViewModel { get; } = new();

    public OptimizerPage()
    {
        this.InitializeComponent();
    }

    private async void ReviewButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var selected = ViewModel.Tweaks.Where(t => t.IsEnabled).ToList();
        if (!selected.Any()) return;

        var report = await ViewModel.GenerateReportAsync(selected, false);
        var dialog = new ReviewChangesDialog(report) { XamlRoot = this.Content.XamlRoot };
        
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ExecuteTweaksAsync(selected, report, false);
        }
    }

    private async void RevertButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var selected = ViewModel.Tweaks.Where(t => t.IsEnabled).ToList();
        if (!selected.Any()) return;

        var report = await ViewModel.GenerateReportAsync(selected, true);
        var dialog = new ReviewChangesDialog(report) { XamlRoot = this.Content.XamlRoot, Title = "Перегляд скасованих змін" };
        
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ExecuteTweaksAsync(selected, report, true);
        }
    }

    private void InfoBar_CloseButtonClick(InfoBar sender, object args)
    {
        ViewModel.MarkAsInformedCommand.Execute(null);
    }
}
