using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Clipboard;

public sealed class BookmarkClipboardService : IBookmarkClipboardService
{
    private readonly IBookmarkMoveService _moveService;

    public BookmarkClipboardService()
        : this(new BookmarkMoveService())
    {
    }

    internal BookmarkClipboardService(
        IBookmarkMoveService moveService)
    {
        _moveService = moveService
            ?? throw new ArgumentNullException(nameof(moveService));
    }

    public BookmarkClipboardPayload Capture(
        BookmarkDocument document,
        IReadOnlyList<BookmarkNode> nodes,
        BookmarkClipboardMode mode)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(nodes);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Unsupported bookmark clipboard mode.");
        }

        var unique = new List<BookmarkNode>(nodes.Count);
        var seen = new HashSet<BookmarkNode>(
            ReferenceEqualityComparer.Instance);

        foreach (var node in nodes)
        {
            ArgumentNullException.ThrowIfNull(node);
            EnsureBelongsToDocument(document, node);

            if (mode == BookmarkClipboardMode.Cut &&
                IsPermanentRoot(document, node))
            {
                throw new BookmarkClipboardException(
                    BookmarkClipboardError.ProtectedRoot,
                    "Permanent Chrome bookmark roots cannot be cut.");
            }

            if (seen.Add(node))
            {
                unique.Add(node);
            }
        }

        var selected = new HashSet<BookmarkNode>(
            unique,
            ReferenceEqualityComparer.Instance);
        var topLevel = unique
            .Where(node => !HasSelectedAncestor(node, selected))
            .ToArray();

        return new BookmarkClipboardPayload(
            mode,
            GetDocumentIdentity(document),
            topLevel.Select(CreateSnapshot).ToArray(),
            topLevel.Select(node => node.Guid).ToArray());
    }

    public BookmarkClipboardPasteResult Paste(
        BookmarkDocument document,
        BookmarkClipboardPayload payload,
        BookmarkFolder targetParent,
        int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(targetParent);

        EnsureBelongsToDocument(document, targetParent);

        if (targetIndex < 0 ||
            targetIndex > targetParent.Children.Count)
        {
            throw new BookmarkClipboardException(
                BookmarkClipboardError.InvalidTargetIndex,
                "The requested paste insertion position is invalid.");
        }

        if (payload.Items.Count == 0)
        {
            return new BookmarkClipboardPasteResult(
                false,
                payload.Mode == BookmarkClipboardMode.Cut,
                Array.Empty<BookmarkNode>(),
                targetParent,
                targetIndex,
                0,
                0,
                null);
        }

        return payload.Mode switch
        {
            BookmarkClipboardMode.Copy =>
                PasteCopy(
                    document,
                    payload,
                    targetParent,
                    targetIndex),
            BookmarkClipboardMode.Cut =>
                PasteCut(
                    document,
                    payload,
                    targetParent,
                    targetIndex),
            _ => throw new BookmarkClipboardException(
                BookmarkClipboardError.InvalidPayload,
                "The bookmark clipboard payload mode is invalid.")
        };
    }

    private BookmarkClipboardPasteResult PasteCut(
        BookmarkDocument document,
        BookmarkClipboardPayload payload,
        BookmarkFolder targetParent,
        int targetIndex)
    {
        if (payload.SourceDocument != GetDocumentIdentity(document))
        {
            throw new BookmarkClipboardException(
                BookmarkClipboardError.SourceDocumentMismatch,
                "Cut bookmark items can only be pasted back into their source document.");
        }

        if (payload.SourceNodeGuids.Count != payload.Items.Count)
        {
            throw new BookmarkClipboardException(
                BookmarkClipboardError.InvalidPayload,
                "The cut clipboard payload does not match its source-node identity list.");
        }

        var nodes = new BookmarkNode[payload.SourceNodeGuids.Count];

        for (var index = 0;
             index < payload.SourceNodeGuids.Count;
             index++)
        {
            var node = FindNodeByGuid(
                document,
                payload.SourceNodeGuids[index]);

            if (node is null)
            {
                throw new BookmarkClipboardException(
                    BookmarkClipboardError.SourceNodeMissing,
                    "A cut bookmark item is no longer present in the source document.");
            }

            nodes[index] = node;
        }

        var move = _moveService.MoveNodes(
            document,
            nodes,
            targetParent,
            targetIndex);

        return new BookmarkClipboardPasteResult(
            move.Changed,
            true,
            nodes,
            targetParent,
            targetIndex,
            0,
            0,
            move);
    }

    private static BookmarkClipboardPasteResult PasteCopy(
        BookmarkDocument document,
        BookmarkClipboardPayload payload,
        BookmarkFolder targetParent,
        int targetIndex)
    {
        var addedUrlCount = 0;
        var addedFolderCount = 0;

        foreach (var item in payload.Items)
        {
            var (urls, folders) = CountSnapshot(item);
            addedUrlCount = checked(addedUrlCount + urls);
            addedFolderCount = checked(
                addedFolderCount + folders);
        }

        var clones = payload.Items
            .Select(item => CloneSnapshot(document, item))
            .ToArray();
        var inserted = new List<BookmarkNode>(clones.Length);

        try
        {
            for (var index = 0; index < clones.Length; index++)
            {
                targetParent.InsertChild(
                    targetIndex + index,
                    clones[index]);
                inserted.Add(clones[index]);
            }

            document.RecordRestoredSubtree(
                addedUrlCount,
                addedFolderCount);
        }
        catch
        {
            for (var index = inserted.Count - 1;
                 index >= 0;
                 index--)
            {
                var node = inserted[index];
                var currentIndex =
                    targetParent.IndexOfChild(node);

                if (currentIndex >= 0)
                {
                    targetParent.RemoveChildAt(
                        currentIndex);
                }
            }

            throw;
        }

        return new BookmarkClipboardPasteResult(
            true,
            false,
            clones,
            targetParent,
            targetIndex,
            addedUrlCount,
            addedFolderCount,
            null);
    }

    private static BookmarkClipboardNodeSnapshot CreateSnapshot(
        BookmarkNode node)
    {
        var children = node is BookmarkFolder folder
            ? folder.Children
                .Select(CreateSnapshot)
                .ToArray()
            : Array.Empty<BookmarkClipboardNodeSnapshot>();

        return new BookmarkClipboardNodeSnapshot(
            node.Kind,
            node.Name,
            node is BookmarkUrl bookmark
                ? bookmark.Url
                : null,
            node.DateAddedRaw,
            node.DateModifiedRaw,
            node.DateLastUsedRaw,
            node.MetaInfo,
            node.ExtensionData,
            children);
    }

    private static BookmarkNode CloneSnapshot(
        BookmarkDocument document,
        BookmarkClipboardNodeSnapshot snapshot)
    {
        return snapshot.Kind switch
        {
            BookmarkNodeKind.Url =>
                new BookmarkUrl(
                    document.AllocateNextNodeId(),
                    document.AllocateUniqueGuid(),
                    snapshot.Name,
                    snapshot.Url
                        ?? throw new BookmarkClipboardException(
                            BookmarkClipboardError.InvalidPayload,
                            "A bookmark clipboard URL item is missing its URL."),
                    snapshot.DateAddedRaw,
                    snapshot.DateModifiedRaw,
                    snapshot.DateLastUsedRaw,
                    snapshot.MetaInfo,
                    snapshot.ExtensionData),

            BookmarkNodeKind.Folder =>
                new BookmarkFolder(
                    document.AllocateNextNodeId(),
                    document.AllocateUniqueGuid(),
                    snapshot.Name,
                    snapshot.DateAddedRaw,
                    snapshot.DateModifiedRaw,
                    snapshot.DateLastUsedRaw,
                    snapshot.MetaInfo,
                    snapshot.ExtensionData,
                    snapshot.Children
                        .Select(
                            child => CloneSnapshot(
                                document,
                                child))
                        .ToArray()),

            _ => throw new BookmarkClipboardException(
                BookmarkClipboardError.InvalidPayload,
                "The bookmark clipboard payload contains an unsupported node kind.")
        };
    }

    private static (int UrlCount, int FolderCount)
        CountSnapshot(BookmarkClipboardNodeSnapshot snapshot)
    {
        var urls =
            snapshot.Kind == BookmarkNodeKind.Url
                ? 1
                : 0;
        var folders =
            snapshot.Kind == BookmarkNodeKind.Folder
                ? 1
                : 0;

        foreach (var child in snapshot.Children)
        {
            var (childUrls, childFolders) =
                CountSnapshot(child);
            urls = checked(urls + childUrls);
            folders = checked(
                folders + childFolders);
        }

        return (urls, folders);
    }

    private static BookmarkClipboardDocumentIdentity
        GetDocumentIdentity(BookmarkDocument document) =>
        new(
            document.Roots.BookmarkBar.Guid,
            document.Roots.Other.Guid,
            document.Roots.Synced.Guid);

    private static BookmarkNode? FindNodeByGuid(
        BookmarkDocument document,
        Guid guid)
    {
        var stack = new Stack<BookmarkNode>(
            new BookmarkNode[]
            {
                document.Roots.Synced,
                document.Roots.Other,
                document.Roots.BookmarkBar
            });

        while (stack.TryPop(out var node))
        {
            if (node.Guid == guid)
            {
                return node;
            }

            if (node is not BookmarkFolder folder)
            {
                continue;
            }

            for (var index = folder.Children.Count - 1;
                 index >= 0;
                 index--)
            {
                stack.Push(folder.Children[index]);
            }
        }

        return null;
    }

    private static bool HasSelectedAncestor(
        BookmarkNode node,
        IReadOnlySet<BookmarkNode> selected)
    {
        var current = node.Parent;

        while (current is not null)
        {
            if (selected.Contains(current))
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static void EnsureBelongsToDocument(
        BookmarkDocument document,
        BookmarkNode node)
    {
        BookmarkNode current = node;

        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        if (!IsPermanentRoot(document, current))
        {
            throw new BookmarkClipboardException(
                BookmarkClipboardError.NodeNotInDocument,
                "The bookmark node does not belong to the active document.");
        }
    }

    private static bool IsPermanentRoot(
        BookmarkDocument document,
        BookmarkNode node) =>
        ReferenceEquals(
            node,
            document.Roots.BookmarkBar) ||
        ReferenceEquals(
            node,
            document.Roots.Other) ||
        ReferenceEquals(
            node,
            document.Roots.Synced);
}
