using System.Collections.ObjectModel;
using System.Text.Json;

namespace ChromeBookmarksManager.Domain;

public sealed class BookmarkFolder : BookmarkNode
{
    public BookmarkFolder(string id, Guid guid, string name, string? dateAddedRaw, string? dateModifiedRaw, string? dateLastUsedRaw, JsonElement? metaInfo, IReadOnlyDictionary<string, JsonElement> extensionData, IEnumerable<BookmarkNode> children)
        : base(id, guid, name, BookmarkNodeKind.Folder, dateAddedRaw, dateModifiedRaw, dateLastUsedRaw, metaInfo, extensionData)
    {
        var ordered = children.ToList();
        foreach (var child in ordered)
        {
            child.Parent = this;
        }

        Children = new ReadOnlyCollection<BookmarkNode>(ordered);
    }

    public IReadOnlyList<BookmarkNode> Children { get; }
}
