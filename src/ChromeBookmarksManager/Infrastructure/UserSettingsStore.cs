using System.Text.Json;

namespace ChromeBookmarksManager.Infrastructure;

public sealed class UserSettingsStore
{
    public const string DefaultLanguage = "zh-CN";
    public const string EnglishLanguage = "en-US";

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
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return DefaultLanguage;
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<UserSettings>(
                json,
                JsonOptions);

            return NormalizeLanguage(settings?.Language);
        }
        catch (JsonException)
        {
            return DefaultLanguage;
        }
        catch (IOException)
        {
            return DefaultLanguage;
        }
        catch (UnauthorizedAccessException)
        {
            return DefaultLanguage;
        }
    }

    public void SaveLanguage(string languageCode)
    {
        var normalized = NormalizeLanguage(languageCode);
        var directory = Path.GetDirectoryName(_settingsPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(
            new UserSettings(normalized),
            JsonOptions);

        File.WriteAllText(_settingsPath, json);
    }

    public static string NormalizeLanguage(string? languageCode) =>
        string.Equals(
            languageCode,
            EnglishLanguage,
            StringComparison.OrdinalIgnoreCase)
            ? EnglishLanguage
            : DefaultLanguage;

    private static string GetDefaultSettingsPath()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localAppData,
            "ChromeBookmarksManager",
            "settings.json");
    }

    private sealed record UserSettings(string Language);
}
