using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rewind.Services;
using Rewind.ViewModels;
using WinRT.Interop;

namespace Rewind.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = new();
    public SettingsPage() => InitializeComponent();

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        var path = FolderPickerService.PickFolder(hwnd);
        if (path is not null && Directory.Exists(path))
            ViewModel.AddCustomPath(path);
    }

    private void RemovePathButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
            ViewModel.RemoveCustomPath(path);
    }
}
