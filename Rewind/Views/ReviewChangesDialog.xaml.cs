using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Rewind.Models;
using System.Collections.Generic;

namespace Rewind.Views;

public sealed partial class ReviewChangesDialog : ContentDialog
{
    public ReviewChangesDialog(List<ChangeItem> changes)
    {
        this.InitializeComponent();

        ChangesRepeater.ItemsSource = changes;

        this.Opened += ReviewChangesDialog_Opened; // Refer to the comment below.
    }
    // The year is 2026 and you still can't easily modify ContentDialog controls.
    // I really have to do all of this just to add the shield icon to indicate that the action will require admin privileges.
    private void ReviewChangesDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        if (GetTemplateChild("PrimaryButton") is Button primaryButton)
        {
            primaryButton.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon
                    {
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        Glyph = "\uEA18"
                    },
                    new TextBlock
                    {
                        Text = "Confirm & Apply"
                    }
                }
            };
        }
    }
}