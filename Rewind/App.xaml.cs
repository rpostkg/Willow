using Microsoft.UI.Xaml;
using Rewind.Services;
using Microsoft.Windows.Globalization;

namespace Rewind
{
    public partial class App : Application
    {
        private Window? _window;

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
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
