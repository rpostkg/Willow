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
}
