using System.Windows;
using ChromeBookmarksManager.Infrastructure;
using WpfApplication = System.Windows.Application;

namespace ChromeBookmarksManager.Theming;

public static class ThemeService
{
    private const string ThemeDictionaryPath = "/Themes/";
    private static string _currentTheme = UserSettingsStore.DefaultTheme;

    public static event EventHandler? ThemeChanged;

    public static string CurrentTheme => _currentTheme;

    public static void Initialize(string? theme)
    {
        _currentTheme = UserSettingsStore.NormalizeTheme(theme);
        ApplyThemeResources(_currentTheme);
    }

    public static void SetTheme(string? theme)
    {
        var normalized = UserSettingsStore.NormalizeTheme(theme);
        if (string.Equals(
                _currentTheme,
                normalized,
                StringComparison.Ordinal))
        {
            ApplyThemeResources(normalized);
            return;
        }

        _currentTheme = normalized;
        ApplyThemeResources(normalized);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void ApplyThemeResources(string theme)
    {
        var resources = WpfApplication.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        var existing = resources.MergedDictionaries
            .Where(IsThemeDictionary)
            .ToArray();

        foreach (var dictionary in existing)
        {
            resources.MergedDictionaries.Remove(dictionary);
        }

        var dictionaryName = theme == UserSettingsStore.LightTheme
            ? "Light.xaml"
            : "Dark.xaml";
        resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/ChromeBookmarksManager;component" +
                    $"{ThemeDictionaryPath}{dictionaryName}",
                    UriKind.Absolute)
            });
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        return IsThemeDictionarySource(dictionary.Source?.OriginalString);
    }

    internal static bool IsThemeDictionarySource(string? source) =>
        IsPaletteSource(source, "Dark.xaml") ||
        IsPaletteSource(source, "Light.xaml");

    private static bool IsPaletteSource(string? source, string fileName) =>
        source is not null &&
        (string.Equals(
             source,
             $"Themes/{fileName}",
             StringComparison.OrdinalIgnoreCase) ||
         source.EndsWith(
             $"/Themes/{fileName}",
             StringComparison.OrdinalIgnoreCase));
}
