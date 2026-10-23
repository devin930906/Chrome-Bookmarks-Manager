using System.Windows;
using ChromeBookmarksManager.Infrastructure;
using ChromeBookmarksManager.Localization;
using ChromeBookmarksManager.Theming;

namespace ChromeBookmarksManager;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var settings = new UserSettingsStore();
        ThemeService.Initialize(settings.LoadTheme());
        LocalizationService.Initialize(settings.LoadLanguage());
        base.OnStartup(e);
    }
}
