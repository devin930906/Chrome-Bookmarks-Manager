using System.Windows;
using System.Windows.Media;
using ChromeBookmarksManager.Localization;
using ChromeBookmarksManager.Tests.Ui;
using ChromeBookmarksManager.Theming;
using WpfApplication = System.Windows.Application;

namespace ChromeBookmarksManager.Tests.Theming;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfTestCollection
{
    public const string Name = "WPF resource tests";
}

[Collection(WpfTestCollection.Name)]
public sealed class ThemeServiceTests
{
    [Theory]
    [InlineData("Themes/Dark.xaml", true)]
    [InlineData("pack://application:,,,/ChromeBookmarksManager;component/Themes/Dark.xaml", true)]
    [InlineData("Themes/Light.xaml", true)]
    [InlineData("pack://application:,,,/ChromeBookmarksManager;component/Themes/Light.xaml", true)]
    [InlineData("Themes/Controls.xaml", false)]
    [InlineData("pack://application:,,,/ChromeBookmarksManager;component/Themes/Controls.xaml", false)]
    [InlineData("SomeThemes/Dark.xaml", false)]
    public void Only_the_dark_and_light_palette_paths_are_theme_dictionaries(
        string source,
        bool expected)
    {
        Assert.Equal(expected, ThemeService.IsThemeDictionarySource(source));
    }

    [Fact]
    public void Initialization_and_switching_replace_only_the_current_theme_palette()
    {
        WpfTestHost.Run(() =>
        {
            var app = Assert.IsAssignableFrom<WpfApplication>(WpfApplication.Current);
            ThemeService.Initialize("dark");
            var localizationDictionary = new ResourceDictionary
            {
                ["ThemeServiceLocalizationSentinel"] = "preserved"
            };
            app.Resources.MergedDictionaries.Add(localizationDictionary);
            LocalizationService.Initialize("en-US");
            var englishDictionary = app.Resources.MergedDictionaries.Single(
                dictionary => IsLocalizationDictionary(dictionary, "en-US"));

            ThemeService.Initialize("light");
            Assert.Equal("light", ThemeService.CurrentTheme);
            AssertPaletteColor(app, "AppBackgroundBrush", "#FFFFFFFF");
            AssertPaletteColor(app, "SidebarBackgroundBrush", "#FFF5F6FA");
            AssertPaletteColor(app, "AccentBrush", "#FF5364D7");
            AssertPaletteColor(app, "AccentForegroundBrush", "#FFFFFFFF");
            AssertPaletteColor(app, "SelectionBrush", "#FFE9EDFF");

            var themeChangedCount = 0;
            EventHandler themeChangedHandler = (_, _) => themeChangedCount++;
            ThemeService.ThemeChanged += themeChangedHandler;
            try
            {
                ThemeService.SetTheme("dark");
            }
            finally
            {
                ThemeService.ThemeChanged -= themeChangedHandler;
            }

            Assert.Equal("dark", ThemeService.CurrentTheme);
            AssertPaletteColor(app, "AppBackgroundBrush", "#FF191C23");
            AssertPaletteColor(app, "SidebarBackgroundBrush", "#FF13161C");
            AssertPaletteColor(app, "SurfaceBrush", "#FF20242D");
            AssertPaletteColor(app, "PrimaryTextBrush", "#FFE4E8F0");
            AssertPaletteColor(app, "SecondaryTextBrush", "#FF9BA5B6");
            AssertPaletteColor(app, "BorderBrush", "#FF303641");
            AssertPaletteColor(app, "AccentBrush", "#FFABB6FF");
            AssertPaletteColor(app, "AccentForegroundBrush", "#FF151A35");
            AssertPaletteColor(app, "SelectionBrush", "#FF2D344F");
            Assert.Equal(1, themeChangedCount);
            AssertPaletteBrushes(app.Resources.MergedDictionaries
                .Single(IsThemeDictionary));

            for (var index = 0; index < 3; index++)
            {
                ThemeService.Initialize(index % 2 == 0 ? "light" : "dark");
            }

            var themeDictionaries = app.Resources.MergedDictionaries
                .Where(IsThemeDictionary)
                .ToArray();
            Assert.Single(themeDictionaries);
            Assert.Same(localizationDictionary,
                app.Resources.MergedDictionaries.Single(
                    dictionary => dictionary.Contains("ThemeServiceLocalizationSentinel")));
            Assert.Same(englishDictionary,
                app.Resources.MergedDictionaries.Single(
                    dictionary => IsLocalizationDictionary(dictionary, "en-US")));
            Assert.Equal(
                "Chrome Bookmarks Manager {0}",
                LocalizationService.GetString("ApplicationTitle"));

            var lightPalette = themeDictionaries[0];
            AssertPaletteBrushes(lightPalette);
            LocalizationService.SetLanguage("zh-CN");
            Assert.Same(lightPalette,
                app.Resources.MergedDictionaries.Single(IsThemeDictionary));
            Assert.Equal(
                "Chrome 书签管理器 {0}",
                LocalizationService.GetString("ApplicationTitle"));
            AssertPaletteColor(app, "AppBackgroundBrush", "#FFFFFFFF");
        });
    }

    private static void AssertPaletteColor(
        WpfApplication app,
        string key,
        string expectedColor)
    {
        var brush = Assert.IsType<SolidColorBrush>(app.TryFindResource(key));
        Assert.Equal(ColorConverter.ConvertFromString(expectedColor), brush.Color);
    }

    private static void AssertPaletteBrushes(ResourceDictionary palette)
    {
        foreach (var key in new[]
                 {
                     "AppBackgroundBrush", "SidebarBackgroundBrush", "SurfaceBrush",
                     "HoverBrush", "PrimaryTextBrush", "SecondaryTextBrush",
                     "MutedTextBrush", "BorderBrush", "AccentBrush",
                     "AccentForegroundBrush", "SelectionBrush", "FocusBrush",
                     "SuccessBrush", "WarningBrush", "DangerBrush", "DisabledTextBrush"
                 })
        {
            Assert.IsType<SolidColorBrush>(palette[key]);
        }
    }

    private static bool IsLocalizationDictionary(
        ResourceDictionary dictionary,
        string language) =>
        dictionary.Source?.OriginalString.Contains(
            $"Localization/Strings.{language}.xaml",
            StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsThemeDictionary(ResourceDictionary dictionary) =>
        ThemeService.IsThemeDictionarySource(dictionary.Source?.OriginalString);
}
