using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Willow.ViewModels;
using Willow.Views;
using System;
using System.IO;
using System.Linq;

namespace Willow
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Icon.ico"));
            _ = new DashboardViewModel();
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
        }

        public void NavigateTo(string tag)
        {
            var item = NavView.MenuItems.OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag?.ToString() == tag);
            if (item != null) NavView.SelectedItem = item;
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var item = args.SelectedItem as NavigationViewItem;
            if (item != null)
            {
                switch (item.Tag)
                {
                    case "Dashboard":
                        ContentFrame.Navigate(typeof(DashboardPage));
                        break;
                    case "Cleaner":
                        ContentFrame.Navigate(typeof(CleanerPage));
                        break;
                    case "Startup":
                        ContentFrame.Navigate(typeof(StartupPage));
                        break;
                    case "DiskHealth":
                        ContentFrame.Navigate(typeof(DiskHealthPage));
                        break;
                    case "Optimizer":
                        ContentFrame.Navigate(typeof(OptimizerPage));
                        break;
                    case "Settings":
                        ContentFrame.Navigate(typeof(SettingsPage));
                        break;
                }
            }
        }
    }
}
