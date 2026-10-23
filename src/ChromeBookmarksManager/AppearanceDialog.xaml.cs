using System.IO;
using System.Windows;
using ChromeBookmarksManager.Infrastructure;
using ChromeBookmarksManager.Localization;
using ChromeBookmarksManager.Theming;

namespace ChromeBookmarksManager;

public partial class AppearanceDialog : Window
{
    private readonly UserSettingsStore _settings;
    private bool _synchronizing = true;

    public AppearanceDialog(UserSettingsStore settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        InitializeComponent();
        SynchronizeSelection();
        ThemeService.ThemeChanged += ThemeChanged;
        Closed += (_, _) => ThemeService.ThemeChanged -= ThemeChanged;
    }

    private void ThemeChanged(object? sender, EventArgs e) => SynchronizeSelection();

    private void SynchronizeSelection()
    {
        _synchronizing = true;
        LightChoice.IsChecked = ThemeService.CurrentTheme == UserSettingsStore.LightTheme;
        DarkChoice.IsChecked = !LightChoice.IsChecked;
        _synchronizing = false;
    }

    private void LightChoice_Checked(object sender, RoutedEventArgs e) => ChangeTheme("light");
    private void DarkChoice_Checked(object sender, RoutedEventArgs e) => ChangeTheme("dark");

    private void ChangeTheme(string theme)
    {
        if (_synchronizing) return;
        SaveStatus.Text = string.Empty;
        ThemeService.SetTheme(theme);
        try { _settings.SaveTheme(theme); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SaveStatus.Text = LocalizationService.GetString("ThemeSaveFailed");
        }
    }
}
