using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromeBookmarksManager.Chrome.Serialization;

internal sealed class ChromeBookmarkNodeDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("guid")]
    public string? Guid { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("children")]
    public List<ChromeBookmarkNodeDto>? Children { get; init; }

    [JsonPropertyName("date_added")]
    public string? DateAdded { get; init; }

    [JsonPropertyName("date_modified")]
    public string? DateModified { get; init; }

    [JsonPropertyName("date_last_used")]
    public string? DateLastUsed { get; init; }

    [JsonPropertyName("meta_info")]
    public JsonElement? MetaInfo { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
