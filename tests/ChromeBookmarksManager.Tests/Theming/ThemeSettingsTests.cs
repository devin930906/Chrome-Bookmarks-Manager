using ChromeBookmarksManager.Infrastructure;

namespace ChromeBookmarksManager.Tests.Theming;

public sealed class ThemeSettingsTests
{
    [Fact]
    public void Missing_settings_default_to_dark_theme_and_simplified_chinese()
    {
        using var temp = new TemporaryDirectory();
        var store = new UserSettingsStore(Path.Combine(temp.Path, "settings.json"));

        Assert.Equal("dark", store.LoadTheme());
        Assert.Equal("zh-CN", store.LoadLanguage());
    }

    [Fact]
    public void Legacy_language_setting_remains_compatible_and_defaults_to_dark()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, "{\"language\":\"en-US\"}");
        var store = new UserSettingsStore(path);

        Assert.Equal("en-US", store.LoadLanguage());
        Assert.Equal("dark", store.LoadTheme());
    }

    [Fact]
    public void Saving_language_and_theme_preserves_the_other_preference()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var store = new UserSettingsStore(path);

        store.SaveTheme("light");
        store.SaveLanguage("en-US");

        var afterLanguageSave = new UserSettingsStore(path);
        Assert.Equal("light", afterLanguageSave.LoadTheme());
        Assert.Equal("en-US", afterLanguageSave.LoadLanguage());

        afterLanguageSave.SaveTheme("dark");

        var afterThemeSave = new UserSettingsStore(path);
        Assert.Equal("dark", afterThemeSave.LoadTheme());
        Assert.Equal("en-US", afterThemeSave.LoadLanguage());
    }

    [Theory]
    [InlineData("light", "light")]
    [InlineData("LIGHT", "light")]
    [InlineData("  light  ", "light")]
    [InlineData("night", "dark")]
    [InlineData("", "dark")]
    [InlineData(null, "dark")]
    public void Theme_values_are_normalized(string? input, string expected)
    {
        Assert.Equal(expected, UserSettingsStore.NormalizeTheme(input));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"theme\":\"night\"}")]
    [InlineData("{\"theme\":\"\"}")]
    public void Invalid_or_corrupt_theme_settings_default_to_dark(string json)
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, json);

        Assert.Equal("dark", new UserSettingsStore(path).LoadTheme());
    }

    [Fact]
    public void Read_io_failure_uses_the_theme_default()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, "{\"theme\":\"light\"}");
        using var lockedFile = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        Assert.Equal("dark", new UserSettingsStore(path).LoadTheme());
    }

    [Fact]
    public void Write_io_failure_is_reported_to_the_caller()
    {
        using var temp = new TemporaryDirectory();
        var blockingFile = Path.Combine(temp.Path, "not-a-directory");
        File.WriteAllText(blockingFile, "block");
        var store = new UserSettingsStore(
            Path.Combine(blockingFile, "settings.json"));

        Assert.ThrowsAny<IOException>(() => store.SaveTheme("light"));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "cbm-theme-settings-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }
    }
}
