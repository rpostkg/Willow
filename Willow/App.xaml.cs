using Microsoft.UI.Xaml;
using Willow.Services;
using Microsoft.Windows.Globalization;

namespace Willow
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

            var prefs = new PreferencesService().LoadPreferences();
            ApplyTheme(prefs.Theme);
        }

        public static void ApplyTheme(string theme)
        {
            if (MainWindow?.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark"  => ElementTheme.Dark,
                    _       => ElementTheme.Default
                };
            }
        }
    }
}
