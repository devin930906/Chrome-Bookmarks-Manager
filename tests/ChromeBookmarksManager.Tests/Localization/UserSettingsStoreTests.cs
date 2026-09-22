using System.Reflection;
using System.Text.Json;

namespace ChromeBookmarksManager.Tests.Localization;

public sealed class UserSettingsStoreTests
{
    private static Assembly ApplicationAssembly =>
        typeof(ChromeBookmarksManager.MainWindow).Assembly;

    [Fact]
    public void Missing_settings_default_to_simplified_chinese()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateStore(Path.Combine(temp.Path, "settings.json"));

        Assert.Equal("zh-CN", LoadLanguage(store));
    }

    [Fact]
    public void Saved_english_language_is_remembered()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");

        var store = CreateStore(path);
        SaveLanguage(store, "en-US");

        var reopened = CreateStore(path);
        Assert.Equal("en-US", LoadLanguage(reopened));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"language\":\"fr-FR\"}")]
    [InlineData("{\"language\":\"\"}")]
    public void Invalid_settings_fall_back_to_simplified_chinese(string json)
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, json);

        var store = CreateStore(path);

        Assert.Equal("zh-CN", LoadLanguage(store));
    }

    private static object CreateStore(string path)
    {
        var type = ApplicationAssembly.GetType(
            "ChromeBookmarksManager.Infrastructure.UserSettingsStore");

        Assert.True(
            type is not null,
            "Expected UserSettingsStore to exist for persistent language preferences.");

        return Activator.CreateInstance(type!, path)
            ?? throw new InvalidOperationException(
                "UserSettingsStore could not be constructed.");
    }

    private static string LoadLanguage(object store)
    {
        var method = store.GetType().GetMethod(
            "LoadLanguage",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.True(method is not null, "LoadLanguage() is required.");

        return Assert.IsType<string>(method!.Invoke(store, null));
    }

    private static void SaveLanguage(object store, string languageCode)
    {
        var method = store.GetType().GetMethod(
            "SaveLanguage",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.True(method is not null, "SaveLanguage(string) is required.");
        method!.Invoke(store, [languageCode]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "cbm-localization-tests",
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
