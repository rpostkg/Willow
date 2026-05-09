using Microsoft.UI.Xaml.Controls;
using Rewind.ViewModels;

namespace Rewind.Views;

public sealed partial class OptimizerPage : Page
{
    public OptimizerViewModel ViewModel { get; } = new();

    public OptimizerPage()
    {
        this.InitializeComponent();
    }
}
