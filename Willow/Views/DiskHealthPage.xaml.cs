using Microsoft.UI.Xaml.Controls;
using Willow.ViewModels;

namespace Willow.Views;

public sealed partial class DiskHealthPage : Page
{
    public DiskHealthViewModel ViewModel { get; } = new();
    public DiskHealthPage() => InitializeComponent();
}
