using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.History;

internal static class BookmarkHistoryMutation
{
    internal sealed record RawMove(
        BookmarkNode Node,
        BookmarkFolder FromParent,
        int FromIndex,
        BookmarkFolder ToParent,
        int ToIndex);

    internal static void EnsureBelongsToDocument(
        BookmarkDocument document,
        BookmarkNode node)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(node);

        BookmarkNode current = node;
        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        if (!ReferenceEquals(current, document.Roots.BookmarkBar) &&
            !ReferenceEquals(current, document.Roots.Other) &&
            !ReferenceEquals(current, document.Roots.Synced))
        {
            throw new InvalidOperationException(
                "The history entry no longer belongs to the active bookmark document.");
        }
    }

    internal static void EnsureNodeAt(
        BookmarkNode node,
        BookmarkFolder parent,
        int index)
    {
        if (!ReferenceEquals(node.Parent, parent) ||
            index < 0 ||
            index >= parent.Children.Count ||
            !ReferenceEquals(parent.Children[index], node))
        {
            throw new InvalidOperationException(
                "The bookmark graph no longer matches the recorded history placement.");
        }
    }

    internal static void DetachWithCounts(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkFolder parent,
        int index,
        int urlCount,
        int folderCount)
    {
        EnsureBelongsToDocument(document, parent);
        EnsureNodeAt(node, parent, index);

        var removed = parent.RemoveChildAt(index);
        try
        {
            document.RecordRemovedSubtree(urlCount, folderCount);
        }
        catch
        {
            parent.InsertChild(index, removed);
            throw;
        }
    }

    internal static void RestoreDetachedWithCounts(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkFolder parent,
        int index,
        int urlCount,
        int folderCount)
    {
        EnsureBelongsToDocument(document, parent);

        if (node.Parent is not null)
        {
            throw new InvalidOperationException(
                "The history node is already attached and cannot be restored.");
        }

        if (index < 0 || index > parent.Children.Count)
        {
            throw new InvalidOperationException(
                "The recorded restore index is no longer valid.");
        }

        parent.InsertChild(index, node);
        try
        {
            document.RecordRestoredSubtree(urlCount, folderCount);
        }
        catch
        {
            var currentIndex = parent.IndexOfChild(node);
            if (currentIndex >= 0)
            {
                parent.RemoveChildAt(currentIndex);
            }

            throw;
        }
    }

    internal static void ApplyRawMove(
        BookmarkDocument document,
        RawMove move)
    {
        EnsureBelongsToDocument(document, move.FromParent);
        EnsureBelongsToDocument(document, move.ToParent);
        EnsureNodeAt(move.Node, move.FromParent, move.FromIndex);

        var sameParent = ReferenceEquals(move.FromParent, move.ToParent);
        var targetCountAfterDetach =
            move.ToParent.Children.Count - (sameParent ? 1 : 0);

        if (move.ToIndex < 0 || move.ToIndex > targetCountAfterDetach)
        {
            throw new InvalidOperationException(
                "The recorded move target index is no longer valid.");
        }

        var removed = move.FromParent.RemoveChildAt(move.FromIndex);
        try
        {
            move.ToParent.InsertChild(move.ToIndex, removed);
        }
        catch
        {
            move.FromParent.InsertChild(move.FromIndex, removed);
            throw;
        }
    }

    internal static void ReorderSameKind(
        BookmarkDocument document,
        BookmarkNode node,
        BookmarkFolder parent,
        int expectedVisibleIndex,
        int desiredVisibleIndex)
    {
        EnsureBelongsToDocument(document, parent);

        if (!ReferenceEquals(node.Parent, parent))
        {
            throw new InvalidOperationException(
                "The bookmark node is not in the recorded reorder parent.");
        }

        var slotIndexes = new List<int>();
        var visibleNodes = new List<BookmarkNode>();

        for (var index = 0; index < parent.Children.Count; index++)
        {
            var child = parent.Children[index];
            if (child.Kind != node.Kind)
            {
                continue;
            }

            slotIndexes.Add(index);
            visibleNodes.Add(child);
        }

        if (expectedVisibleIndex < 0 ||
            expectedVisibleIndex >= visibleNodes.Count ||
            !ReferenceEquals(visibleNodes[expectedVisibleIndex], node))
        {
            throw new InvalidOperationException(
                "The bookmark graph no longer matches the recorded visible reorder position.");
        }

        if (desiredVisibleIndex < 0 ||
            desiredVisibleIndex >= visibleNodes.Count)
        {
            throw new InvalidOperationException(
                "The recorded visible reorder target is invalid.");
        }

        if (expectedVisibleIndex == desiredVisibleIndex)
        {
            return;
        }

        var desiredNodes = visibleNodes.ToList();
        desiredNodes.RemoveAt(expectedVisibleIndex);
        desiredNodes.Insert(desiredVisibleIndex, node);

        RewriteSameKindSlots(
            parent,
            slotIndexes,
            visibleNodes,
            desiredNodes);
    }

    internal static void ApplyBatchRaw(
        BookmarkDocument document,
        IReadOnlyList<RawMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        var seen = new HashSet<BookmarkNode>(
            ReferenceEqualityComparer.Instance);

        foreach (var move in moves)
        {
            if (!seen.Add(move.Node))
            {
                throw new InvalidOperationException(
                    "A history batch cannot contain the same node more than once.");
            }

            EnsureBelongsToDocument(document, move.FromParent);
            EnsureBelongsToDocument(document, move.ToParent);
            EnsureNodeAt(move.Node, move.FromParent, move.FromIndex);
        }

        var inserted = new List<BookmarkNode>(moves.Count);

        try
        {
            foreach (var group in moves.GroupBy(move => move.FromParent))
            {
                foreach (var move in group.OrderByDescending(move => move.FromIndex))
                {
                    group.Key.RemoveChildAt(move.FromIndex);
                }
            }

            foreach (var group in moves.GroupBy(move => move.ToParent))
            {
                foreach (var move in group.OrderBy(move => move.ToIndex))
                {
                    if (move.ToIndex < 0 ||
                        move.ToIndex > group.Key.Children.Count)
                    {
                        throw new InvalidOperationException(
                            "A recorded batch move target index is no longer valid.");
                    }

                    group.Key.InsertChild(move.ToIndex, move.Node);
                    inserted.Add(move.Node);
                }
            }
        }
        catch
        {
            for (var index = inserted.Count - 1; index >= 0; index--)
            {
                var node = inserted[index];
                var parent = node.Parent;
                if (parent is null)
                {
                    continue;
                }

                var currentIndex = parent.IndexOfChild(node);
                if (currentIndex >= 0)
                {
                    parent.RemoveChildAt(currentIndex);
                }
            }

            RestoreRawSources(moves);
            throw;
        }
    }

    internal static void RestoreDeletedBatch(
        BookmarkDocument document,
        IReadOnlyList<BookmarkDeleteResult> items,
        int urlCount,
        int folderCount)
    {
        foreach (var item in items)
        {
            EnsureBelongsToDocument(document, item.SourceParent);

            if (item.Node.Parent is not null)
            {
                throw new InvalidOperationException(
                    "A deleted history node is unexpectedly attached.");
            }
        }

        var inserted = new List<BookmarkNode>(items.Count);

        try
        {
            foreach (var group in items.GroupBy(item => item.SourceParent))
            {
                foreach (var item in group.OrderBy(item => item.SourceIndex))
                {
                    if (item.SourceIndex < 0 ||
                        item.SourceIndex > group.Key.Children.Count)
                    {
                        throw new InvalidOperationException(
                            "A recorded deleted-node restore index is no longer valid.");
                    }

                    group.Key.InsertChild(item.SourceIndex, item.Node);
                    inserted.Add(item.Node);
                }
            }

            document.RecordRestoredSubtree(urlCount, folderCount);
        }
        catch
        {
            for (var index = inserted.Count - 1; index >= 0; index--)
            {
                var node = inserted[index];
                var parent = node.Parent;
                if (parent is null)
                {
                    continue;
                }

                var currentIndex = parent.IndexOfChild(node);
                if (currentIndex >= 0)
                {
                    parent.RemoveChildAt(currentIndex);
                }
            }

            throw;
        }
    }

    internal static void DetachDeletedBatch(
        BookmarkDocument document,
        IReadOnlyList<BookmarkDeleteResult> items,
        int urlCount,
        int folderCount)
    {
        foreach (var item in items)
        {
            EnsureBelongsToDocument(document, item.SourceParent);
            EnsureNodeAt(item.Node, item.SourceParent, item.SourceIndex);
        }

        try
        {
            foreach (var group in items.GroupBy(item => item.SourceParent))
            {
                foreach (var item in group.OrderByDescending(item => item.SourceIndex))
                {
                    group.Key.RemoveChildAt(item.SourceIndex);
                }
            }

            document.RecordRemovedSubtree(urlCount, folderCount);
        }
        catch
        {
            RestoreDeletedSources(items);
            throw;
        }
    }

    private static void RewriteSameKindSlots(
        BookmarkFolder parent,
        IReadOnlyList<int> slotIndexes,
        IReadOnlyList<BookmarkNode> originalNodes,
        IReadOnlyList<BookmarkNode> desiredNodes)
    {
        for (var index = slotIndexes.Count - 1; index >= 0; index--)
        {
            parent.RemoveChildAt(slotIndexes[index]);
        }

        var inserted = new List<BookmarkNode>(desiredNodes.Count);
        try
        {
            for (var index = 0; index < slotIndexes.Count; index++)
            {
                parent.InsertChild(slotIndexes[index], desiredNodes[index]);
                inserted.Add(desiredNodes[index]);
            }
        }
        catch
        {
            for (var index = inserted.Count - 1; index >= 0; index--)
            {
                var currentIndex = parent.IndexOfChild(inserted[index]);
                if (currentIndex >= 0)
                {
                    parent.RemoveChildAt(currentIndex);
                }
            }

            for (var index = 0; index < slotIndexes.Count; index++)
            {
                var node = originalNodes[index];
                if (node.Parent is null)
                {
                    parent.InsertChild(slotIndexes[index], node);
                }
            }

            throw;
        }
    }

    private static void RestoreRawSources(
        IReadOnlyList<RawMove> moves)
    {
        foreach (var group in moves.GroupBy(move => move.FromParent))
        {
            foreach (var move in group.OrderBy(move => move.FromIndex))
            {
                if (move.Node.Parent is null)
                {
                    group.Key.InsertChild(move.FromIndex, move.Node);
                }
            }
        }
    }

    private static void RestoreDeletedSources(
        IReadOnlyList<BookmarkDeleteResult> items)
    {
        foreach (var group in items.GroupBy(item => item.SourceParent))
        {
            foreach (var item in group.OrderBy(item => item.SourceIndex))
            {
                if (item.Node.Parent is null)
                {
                    group.Key.InsertChild(item.SourceIndex, item.Node);
                }
            }
        }
    }
}
