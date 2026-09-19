using System.Text.Json;

namespace ChromeBookmarksManager.Chrome.Serialization;

internal static class ChromeBookmarksJson
{
    public static JsonSerializerOptions ReaderOptions { get; } = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 64
    };
}
