using System.Windows;
using ChromeBookmarksManager.Infrastructure;
using ChromeBookmarksManager.Localization;

namespace ChromeBookmarksManager;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var settings = new UserSettingsStore();
        LocalizationService.Initialize(settings.LoadLanguage());
        base.OnStartup(e);
    }
}
