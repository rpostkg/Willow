using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Rewind.ViewModels;
using System;
using System.Linq;

namespace Rewind.Views;

public sealed partial class OptimizerPage : Page
{
    public OptimizerViewModel ViewModel { get; } = new();

    private readonly ResourceLoader _res = new ResourceLoader();

    public OptimizerPage()
    {
        this.InitializeComponent();
    }

    private void TweakSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ViewModel.NameQuery = sender.Text;
    }

    private async void ReviewButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var selected = ViewModel.Tweaks.Where(t => t.IsEnabled).ToList();
        if (!selected.Any()) return;

        var report = await ViewModel.GenerateReportAsync(selected, false);
        var dialog = new ReviewChangesDialog(report) { XamlRoot = this.Content.XamlRoot };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.ExecuteTweaksAsync(selected, report, false);
    }

    private async void RevertButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var selected = ViewModel.Tweaks.Where(t => t.IsEnabled).ToList();
        if (!selected.Any()) return;

        var report = await ViewModel.GenerateReportAsync(selected, true);
        var dialog = new ReviewChangesDialog(report)
        {
            XamlRoot = this.Content.XamlRoot,
            Title = _res.GetString("Optimizer_RevertDialogTitle")
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            await ViewModel.ExecuteTweaksAsync(selected, report, true);
    }

    private void InfoBar_CloseButtonClick(InfoBar sender, object args)
    {
        ViewModel.MarkAsInformedCommand.Execute(null);
    }
}
