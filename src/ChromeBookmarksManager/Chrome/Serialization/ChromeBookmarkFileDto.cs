using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChromeBookmarksManager.Chrome.Serialization;

internal sealed class ChromeBookmarkFileDto
{
    [JsonPropertyName("version")]
    public int? Version { get; init; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; init; }

    [JsonPropertyName("checksum_sha256")]
    public string? ChecksumSha256 { get; init; }

    [JsonPropertyName("roots")]
    public ChromeBookmarkRootsDto? Roots { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
