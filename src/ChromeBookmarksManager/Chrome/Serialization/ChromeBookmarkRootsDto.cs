using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromeBookmarksManager.Chrome.Serialization;

internal sealed class ChromeBookmarkRootsDto
{
    [JsonPropertyName("bookmark_bar")]
    public ChromeBookmarkNodeDto? BookmarkBar { get; init; }

    [JsonPropertyName("other")]
    public ChromeBookmarkNodeDto? Other { get; init; }

    [JsonPropertyName("synced")]
    public ChromeBookmarkNodeDto? Synced { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
