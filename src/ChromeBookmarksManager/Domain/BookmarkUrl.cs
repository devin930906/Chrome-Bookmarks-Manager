using System.Text.Json;

namespace ChromeBookmarksManager.Domain;

public sealed class BookmarkUrl : BookmarkNode
{
    public BookmarkUrl(string id, Guid guid, string name, string url, string? dateAddedRaw, string? dateModifiedRaw, string? dateLastUsedRaw, JsonElement? metaInfo, IReadOnlyDictionary<string, JsonElement> extensionData)
        : base(id, guid, name, BookmarkNodeKind.Url, dateAddedRaw, dateModifiedRaw, dateLastUsedRaw, metaInfo, extensionData)
    {
        Url = url;
    }

    public string Url { get; private set; }

    internal bool SetUrl(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.Equals(Url, value, StringComparison.Ordinal))
        {
            return false;
        }

        Url = value;
        return true;
    }
}
