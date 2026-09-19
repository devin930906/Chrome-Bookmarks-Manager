using System.Text.Json;

namespace ChromeBookmarksManager.Domain;

public abstract class BookmarkNode
{
    protected BookmarkNode(
        string id,
        Guid guid,
        string name,
        BookmarkNodeKind kind,
        string? dateAddedRaw,
        string? dateModifiedRaw,
        string? dateLastUsedRaw,
        JsonElement? metaInfo,
        IReadOnlyDictionary<string, JsonElement> extensionData)
    {
        Id = id;
        Guid = guid;
        Name = name;
        Kind = kind;
        DateAddedRaw = dateAddedRaw;
        DateModifiedRaw = dateModifiedRaw;
        DateLastUsedRaw = dateLastUsedRaw;
        MetaInfo = metaInfo;
        ExtensionData = extensionData;
    }

    public string Id { get; }
    public Guid Guid { get; }
    public string Name { get; }
    public BookmarkNodeKind Kind { get; }
    public string? DateAddedRaw { get; }
    public string? DateModifiedRaw { get; }
    public string? DateLastUsedRaw { get; }
    public JsonElement? MetaInfo { get; }
    public IReadOnlyDictionary<string, JsonElement> ExtensionData { get; }
    public BookmarkFolder? Parent { get; internal set; }
}
