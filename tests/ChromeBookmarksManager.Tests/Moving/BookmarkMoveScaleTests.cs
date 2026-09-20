using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;
using Xunit.Abstractions;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class BookmarkMoveScaleTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "MoveScale")]
    public void MoveOperations_PreserveIdentityCountsAndHierarchyAtScale()
    {
        var urlCount = int.TryParse(
            Environment.GetEnvironmentVariable("CBM_MOVE_URL_COUNT"),
            out var configured)
            ? configured
            : 10_000;

        Assert.InRange(urlCount, 4, 250_000);

        var sourceUrlCount = Math.Max(2, urlCount * 3 / 4);
        var targetUrlCount = urlCount - sourceUrlCount;

        var sourceUrls = Enumerable
            .Range(0, sourceUrlCount)
            .Select(index => Url(
                10_000 + index,
                $"Source Bookmark {index}",
                $"https://source-{index % 17}.example/item/{index}"))
            .ToArray();

        var targetUrls = Enumerable
            .Range(0, targetUrlCount)
            .Select(index => Url(
                100_000 + index,
                $"Target Bookmark {index}",
                $"https://target-{index % 11}.example/item/{index}"))
            .ToArray();

        var nestedLeaf = Folder(7004, "Nested leaf");
        var movingFolder = Folder(7001, "Moving folder", nestedLeaf);
        var siblingFolder = Folder(7002, "Sibling folder");
        var anchorFolder = Folder(7003, "Anchor folder");

        var split = sourceUrls.Length / 2;
        var sourceChildren = new List<BookmarkNode>
        {
            movingFolder
        };
        sourceChildren.AddRange(sourceUrls.Take(split));
        sourceChildren.Add(siblingFolder);
        sourceChildren.AddRange(sourceUrls.Skip(split));
        sourceChildren.Add(anchorFolder);

        var source = Folder(4, "Source folder", sourceChildren.ToArray());
        var target = Folder(5, "Target folder", targetUrls);
        var bookmarkBar = Folder(1, "Bookmarks bar", source, target);
        var other = Folder(2, "Other bookmarks");
        var synced = Folder(3, "Mobile bookmarks");

        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);

        var originalUrlCount = document.UrlCount;
        var originalFolderCount = document.FolderCount;
        var originalTotalNodeCount = document.TotalNodeCount;
        var service = new BookmarkMoveService();

        var reorderBookmark = sourceUrls[^1];
        var bookmarkTarget = sourceUrls[0];

        var stopwatch = Stopwatch.StartNew();

        var bookmarkReorder = service.MoveBookmarkBefore(
            document,
            reorderBookmark,
            bookmarkTarget);
        var bookmarkCrossFolder = service.MoveToEnd(
            document,
            bookmarkTarget,
            target);
        var folderReorder = service.MoveFolderAfter(
            document,
            movingFolder,
            siblingFolder);
        var folderCrossParent = service.MoveToEnd(
            document,
            movingFolder,
            target);

        stopwatch.Stop();

        Assert.True(bookmarkReorder.Changed);
        Assert.True(bookmarkCrossFolder.Changed);
        Assert.True(folderReorder.Changed);
        Assert.True(folderCrossParent.Changed);

        Assert.Same(source, reorderBookmark.Parent);
        Assert.Same(target, bookmarkTarget.Parent);
        Assert.Same(target, movingFolder.Parent);
        Assert.Same(movingFolder, nestedLeaf.Parent);

        Assert.Contains(
            source.Children,
            node => ReferenceEquals(node, reorderBookmark));
        Assert.Contains(
            target.Children,
            node => ReferenceEquals(node, bookmarkTarget));
        Assert.Contains(
            target.Children,
            node => ReferenceEquals(node, movingFolder));

        Assert.Equal(originalUrlCount, document.UrlCount);
        Assert.Equal(originalFolderCount, document.FolderCount);
        Assert.Equal(originalTotalNodeCount, document.TotalNodeCount);

        var traversed = AssertHierarchyIsCoherent(document);
        Assert.Equal(document.TotalNodeCount, traversed);

        output.WriteLine(
            $"URLs={urlCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"Folders={document.FolderCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"Elapsed={stopwatch.Elapsed}; " +
            $"SourceChildren={source.Children.Count.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"TargetChildren={target.Children.Count.ToString("N0", CultureInfo.InvariantCulture)}");
    }

    private static int AssertHierarchyIsCoherent(BookmarkDocument document)
    {
        var seen = new HashSet<BookmarkNode>(
            ReferenceEqualityComparer.Instance);
        var stack = new Stack<BookmarkNode>(
            new BookmarkNode[]
            {
                document.Roots.Synced,
                document.Roots.Other,
                document.Roots.BookmarkBar
            });

        while (stack.TryPop(out var node))
        {
            Assert.True(
                seen.Add(node),
                $"Duplicate reference or hierarchy cycle detected at node {node.Id}.");

            if (node is not BookmarkFolder folder)
            {
                continue;
            }

            for (var index = folder.Children.Count - 1; index >= 0; index--)
            {
                var child = folder.Children[index];
                Assert.Same(folder, child.Parent);
                stack.Push(child);
            }
        }

        return seen.Count;
    }

    private static BookmarkFolder Folder(
        int id,
        string name,
        params BookmarkNode[] children) =>
        new(
            id.ToString(CultureInfo.InvariantCulture),
            GuidFor(id),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            children);

    private static BookmarkUrl Url(
        int id,
        string name,
        string url) =>
        new(
            id.ToString(CultureInfo.InvariantCulture),
            GuidFor(id),
            name,
            url,
            null,
            null,
            null,
            null,
            EmptyProperties);

    private static Guid GuidFor(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        bytes[7] = 0x40;
        bytes[8] = 0x80;
        return new Guid(bytes);
    }

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
