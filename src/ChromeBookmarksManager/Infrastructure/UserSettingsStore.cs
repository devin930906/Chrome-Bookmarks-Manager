using System.IO;
using System.Text.Json;

namespace ChromeBookmarksManager.Infrastructure;

public sealed class UserSettingsStore
{
    public const string DefaultLanguage = "zh-CN";
    public const string EnglishLanguage = "en-US";
    public const string DefaultTheme = "dark";
    public const string LightTheme = "light";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public UserSettingsStore(string? settingsPath = null)
    {
        _settingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? GetDefaultSettingsPath()
            : settingsPath;
    }

    public string LoadLanguage()
    {
        return ReadSettings().Language;
    }

    public string LoadTheme()
    {
        return ReadSettings().Theme;
    }

    public void SaveLanguage(string languageCode)
    {
        var settings = ReadSettings();
        WriteSettings(settings with
        {
            Language = NormalizeLanguage(languageCode)
        });
    }

    public void SaveTheme(string theme)
    {
        var settings = ReadSettings();
        WriteSettings(settings with
        {
            Theme = NormalizeTheme(theme)
        });
    }

    public static string NormalizeTheme(string? theme) =>
        string.Equals(
            theme?.Trim(),
            LightTheme,
            StringComparison.OrdinalIgnoreCase)
            ? LightTheme
            : DefaultTheme;

    public static string NormalizeLanguage(string? languageCode) =>
        string.Equals(
            languageCode,
            EnglishLanguage,
            StringComparison.OrdinalIgnoreCase)
            ? EnglishLanguage
            : DefaultLanguage;

    private UserSettings ReadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return UserSettings.Defaults;
            }

            var settings = JsonSerializer.Deserialize<UserSettings>(
                File.ReadAllText(_settingsPath),
                JsonOptions);

            return new UserSettings(
                NormalizeLanguage(settings?.Language),
                NormalizeTheme(settings?.Theme));
        }
        catch (JsonException)
        {
            return UserSettings.Defaults;
        }
        catch (IOException)
        {
            return UserSettings.Defaults;
        }
        catch (UnauthorizedAccessException)
        {
            return UserSettings.Defaults;
        }
    }

    private void WriteSettings(UserSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(
            settings,
            JsonOptions);

        File.WriteAllText(_settingsPath, json);
    }

    private static string GetDefaultSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localAppData,
            "ChromeBookmarksManager",
            "settings.json");
    }

    private sealed record UserSettings(string Language, string Theme)
    {
        public static UserSettings Defaults =>
            new(DefaultLanguage, DefaultTheme);
    }
}
