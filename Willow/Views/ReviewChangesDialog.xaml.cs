using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Willow.Models;
using System.Collections.Generic;

namespace Willow.Views;

public sealed partial class ReviewChangesDialog : ContentDialog
{
    public ReviewChangesDialog(List<ChangeItem> changes)
    {
        this.InitializeComponent();

        ChangesRepeater.ItemsSource = changes;

    }
}