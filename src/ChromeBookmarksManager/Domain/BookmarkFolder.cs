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

    internal int IndexOfChild(BookmarkNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return _children.IndexOf(child);
    }

    internal BookmarkNode RemoveChildAt(int index)
    {
        var child = _children[index];
        _children.RemoveAt(index);
        child.Parent = null;
        return child;
    }

    internal void InsertChild(int index, BookmarkNode child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.Parent is not null)
        {
            throw new InvalidOperationException(
                "A bookmark node with an existing parent cannot be inserted as a child.");
        }

        _children.Insert(index, child);
        child.Parent = this;
    }

    internal void AddChild(BookmarkNode child)
    {
        InsertChild(_children.Count, child);
    }
}
