using Microsoft.UI.Xaml;
using Rewind.Services;
using Microsoft.Windows.Globalization;

namespace Rewind
{
    public partial class App : Application
    {
        public static Window? MainWindow { get; private set; }

        public App()
        {
            var prefsService = new PreferencesService();
            var prefs = prefsService.LoadPreferences();
            if (!string.IsNullOrEmpty(prefs.Language))
                ApplicationLanguages.PrimaryLanguageOverride = prefs.Language;

            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}
