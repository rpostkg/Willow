using Microsoft.UI.Xaml.Controls;
using Rewind.ViewModels;

namespace Rewind.Views;

public sealed partial class DiskHealthPage : Page
{
    public DiskHealthViewModel ViewModel { get; } = new();
    public DiskHealthPage() => InitializeComponent();
}
