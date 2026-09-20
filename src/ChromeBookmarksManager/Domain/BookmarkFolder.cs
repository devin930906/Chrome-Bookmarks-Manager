using System.Collections.ObjectModel;
using System.Text.Json;

namespace ChromeBookmarksManager.Domain;

public sealed class BookmarkFolder : BookmarkNode
{
    private readonly List<BookmarkNode> _children;

    public BookmarkFolder(string id, Guid guid, string name, string? dateAddedRaw, string? dateModifiedRaw, string? dateLastUsedRaw, JsonElement? metaInfo, IReadOnlyDictionary<string, JsonElement> extensionData, IEnumerable<BookmarkNode> children)
        : base(id, guid, name, BookmarkNodeKind.Folder, dateAddedRaw, dateModifiedRaw, dateLastUsedRaw, metaInfo, extensionData)
    {
        _children = children.ToList();
        foreach (var child in _children)
        {
            child.Parent = this;
        }

        Children = new ReadOnlyCollection<BookmarkNode>(_children);
    }

    public IReadOnlyList<BookmarkNode> Children { get; }

    internal void AddChild(BookmarkNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.Parent is not null)
        {
            throw new InvalidOperationException(
                "A bookmark node with an existing parent cannot be inserted as a new child.");
        }

        _children.Add(child);
        child.Parent = this;
    }
}
