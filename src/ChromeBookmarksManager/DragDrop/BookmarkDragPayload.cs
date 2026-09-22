using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.DragDrop;

internal sealed class BookmarkDragPayload
{
    private readonly IReadOnlyList<BookmarkNode> _nodes;

    internal BookmarkDragPayload(
        IEnumerable<BookmarkNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var copy = nodes.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException(
                "A bookmark drag payload must contain at least one node.",
                nameof(nodes));
        }

        foreach (var node in copy)
        {
            ArgumentNullException.ThrowIfNull(node);
        }

        _nodes = Array.AsReadOnly(copy);
    }

    internal IReadOnlyList<BookmarkNode> Nodes => _nodes;

    internal static BookmarkDragPayload ForContentRow(
        BookmarkNode pointerNode,
        IReadOnlyList<BookmarkNode> orderedSelectedNodes)
    {
        ArgumentNullException.ThrowIfNull(pointerNode);
        ArgumentNullException.ThrowIfNull(orderedSelectedNodes);

        var pointerIsSelected = orderedSelectedNodes.Any(
            node => ReferenceEquals(node, pointerNode));

        return pointerIsSelected
            ? new BookmarkDragPayload(orderedSelectedNodes)
            : new BookmarkDragPayload(
                new[] { pointerNode });
    }
}
