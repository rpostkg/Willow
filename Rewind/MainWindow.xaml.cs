using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rewind.Views;
using System.Linq;

namespace Rewind
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
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
