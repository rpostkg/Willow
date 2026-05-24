using Microsoft.UI.Xaml.Controls;
using Rewind.ViewModels;

namespace Rewind.Views;

public sealed partial class StartupPage : Page
{
    public StartupViewModel ViewModel { get; } = new();

    public StartupPage()
    {
        this.InitializeComponent();
    }
}
