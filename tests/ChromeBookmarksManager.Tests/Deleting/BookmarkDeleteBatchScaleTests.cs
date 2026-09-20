using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;
using Xunit.Abstractions;

namespace ChromeBookmarksManager.Tests.Deleting;

public sealed class BookmarkDeleteBatchScaleTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "DeleteBatchScale")]
    public void DeleteBookmarkBatch_PreservesIdentityOrderCountsAndHierarchyAtScale()
    {
        var urlCount = ReadCount("CBM_DELETE_URL_COUNT", 10_000, 4, 250_000);
        var folderCount = ReadCount("CBM_DELETE_FOLDER_COUNT", 1_000, 1, 10_000);
        var folderChildren = Enumerable
            .Range(0, folderCount)
            .Select(_ => new List<BookmarkNode>())
            .ToArray();
        var bookmarks = new BookmarkUrl[urlCount];

        for (var index = 0; index < urlCount; index++)
        {
            var bookmark = Url(
                1_000_000 + index,
                $"Synthetic Delete Bookmark {index}",
                $"https://delete-{index % 19}.example/bookmark/{index}");

            bookmarks[index] = bookmark;
            folderChildren[index % folderCount].Add(bookmark);
        }

        var scaleFolders = Enumerable
            .Range(0, folderCount)
            .Select(index => Folder(
                100_000 + index,
                $"Synthetic Delete Folder {index}",
                folderChildren[index].ToArray()))
            .ToArray();
        var bookmarkBar = Folder(1, "Bookmarks bar", scaleFolders);
        var other = Folder(2, "Other bookmarks");
        var synced = Folder(3, "Mobile bookmarks");
        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(bookmarkBar, other, synced, EmptyProperties),
            EmptyProperties);

        var originalChildren = scaleFolders
            .Select(folder => folder.Children.ToArray())
            .ToArray();
        var originalUrlCount = document.UrlCount;
        var originalFolderCount = document.FolderCount;
        var selectedBookmarks = bookmarks
            .Where((_, index) => index % 2 == 0)
            .ToArray();
        var selectedSet = new HashSet<BookmarkUrl>(
            selectedBookmarks,
            ReferenceEqualityComparer.Instance);
        var service = new BookmarkDeleteService();

        Assert.Equal(urlCount, document.UrlCount);
        Assert.Equal(folderCount + 3, document.FolderCount);

        var stopwatch = Stopwatch.StartNew();
        var result = service.DeleteBookmarks(document, selectedBookmarks);
        stopwatch.Stop();

        Assert.True(result.Changed);
        Assert.Equal(selectedBookmarks.Length, result.RemovedUrlCount);
        Assert.Equal(selectedBookmarks.Length, result.RemovedItems.Count);
        Assert.Equal(
            originalUrlCount - selectedBookmarks.Length,
            document.UrlCount);
        Assert.Equal(originalFolderCount, document.FolderCount);

        var snapshotsByNode = result.RemovedItems.ToDictionary(
            item => item.Node,
            ReferenceEqualityComparer.Instance);

        for (var index = 0; index < bookmarks.Length; index++)
        {
            var bookmark = bookmarks[index];
            if (index % 2 == 0)
            {
                Assert.Null(bookmark.Parent);
                var snapshot = snapshotsByNode[bookmark];
                Assert.Same(scaleFolders[index % folderCount], snapshot.SourceParent);
                Assert.Equal(index / folderCount, snapshot.SourceIndex);
                continue;
            }

            Assert.Same(scaleFolders[index % folderCount], bookmark.Parent);
        }

        for (var folderIndex = 0; folderIndex < scaleFolders.Length; folderIndex++)
        {
            var expectedChildren = originalChildren[folderIndex]
                .Where(node => !selectedSet.Contains((BookmarkUrl)node));
            Assert.Equal(expectedChildren, scaleFolders[folderIndex].Children);
        }

        var traversedCount = AssertHierarchyIsCoherent(document);
        Assert.Equal(document.TotalNodeCount, traversedCount);

        output.WriteLine(
            $"URLs={urlCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"Folders={folderCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"DeletedBookmarks={result.RemovedUrlCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"SurvivingBookmarks={document.UrlCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"Elapsed={stopwatch.Elapsed}");
    }

    [Fact]
    [Trait("Category", "DeleteBatchScale")]
    public async Task DeleteLargeFolderSubtree_RebuildsActiveSearchIndexAtScale()
    {
        var urlCount = ReadCount("CBM_DELETE_URL_COUNT", 10_000, 4, 250_000);
        var folderCount = ReadCount("CBM_DELETE_FOLDER_COUNT", 1_000, 1, 10_000);
        var folderChildren = Enumerable
            .Range(0, folderCount)
            .Select(_ => new List<BookmarkNode>())
            .ToArray();
        var bookmarks = new BookmarkUrl[urlCount];

        for (var index = 0; index < urlCount; index++)
        {
            var bookmark = Url(
                1_000_000 + index,
                $"Delete Scale Match {index}",
                $"https://delete-scale-{index % 19}.example/bookmark/{index}");

            bookmarks[index] = bookmark;
            folderChildren[index % folderCount].Add(bookmark);
        }

        var descendantFolders = Enumerable
            .Range(0, folderCount)
            .Select(index => Folder(
                100_000 + index,
                $"Subtree Folder {index}",
                folderChildren[index].ToArray()))
            .ToArray();
        var subtree = Folder(
            50_000,
            "Large delete scale subtree",
            descendantFolders);
        var survivor = Url(
            2_000_000,
            "Synthetic survivor",
            "https://survivor.example/keep");
        var bookmarkBar = Folder(
            1,
            "Bookmarks bar",
            subtree,
            survivor);
        var other = Folder(2, "Other bookmarks");
        var synced = Folder(3, "Mobile bookmarks");
        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(bookmarkBar, other, synced, EmptyProperties),
            EmptyProperties);
        var viewModel = new MainViewModel(
            new DelegateReader(document),
            new BookmarkSearchService(),
            TimeSpan.Zero);

        Assert.Equal(urlCount + 1, document.UrlCount);
        Assert.Equal(folderCount + 4, document.FolderCount);
        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");
        viewModel.SearchText = "delete scale match";
        await viewModel.WaitForPendingSearchAsync();
        Assert.Equal(urlCount, viewModel.SearchResults.Count);

        var subtreeItem = Assert.Single(viewModel.FolderRoots[0].Children);
        viewModel.SelectFolder(subtreeItem);
        await viewModel.WaitForPendingSearchAsync();
        Assert.Same(subtree, viewModel.SelectedFolder);
        Assert.Equal(urlCount, viewModel.SearchResults.Count);

        var stopwatch = Stopwatch.StartNew();
        var changed = await viewModel.DeleteSelectedFolderAsync();
        stopwatch.Stop();
        await viewModel.WaitForPendingSearchAsync();

        Assert.True(changed);
        Assert.Null(subtree.Parent);
        Assert.Same(bookmarkBar, viewModel.SelectedFolder);
        Assert.Same(bookmarkBar, survivor.Parent);
        Assert.Equal(new[] { survivor }, viewModel.CurrentBookmarks);
        Assert.Empty(viewModel.SearchResults);
        Assert.Equal(1, document.UrlCount);
        Assert.Equal(3, document.FolderCount);
        Assert.All(
            descendantFolders,
            folder => Assert.Same(subtree, folder.Parent));

        for (var index = 0; index < bookmarks.Length; index++)
        {
            Assert.Same(
                descendantFolders[index % folderCount],
                bookmarks[index].Parent);
        }

        output.WriteLine(
            $"SubtreeURLs={urlCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"SubtreeFolders={folderCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"RemainingURLs={document.UrlCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"ActiveSearchResults={viewModel.SearchResults.Count.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"Elapsed={stopwatch.Elapsed}");
    }

    private static int ReadCount(
        string variableName,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var value = int.TryParse(
            Environment.GetEnvironmentVariable(variableName),
            out var configured)
            ? configured
            : defaultValue;

        Assert.InRange(value, minimum, maximum);
        return value;
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

    private sealed class DelegateReader(BookmarkDocument document)
        : IChromeBookmarksReader
    {
        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(document);
    }

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
