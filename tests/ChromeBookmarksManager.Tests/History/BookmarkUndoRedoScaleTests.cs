using System.Globalization;
using System.Text.Json;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Application.History;
using ChromeBookmarksManager.Domain;
using Xunit.Abstractions;

namespace ChromeBookmarksManager.Tests.History;

public sealed class BookmarkUndoRedoScaleTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "UndoRedoScale")]
    public void LargeBatchDelete_UndoRedo_RestoresExactIdentityOrderAndCounts()
    {
        var urlCount = ReadCount(
            "CBM_HISTORY_URL_COUNT",
            10_000,
            100,
            250_000);
        var bookmarks = Enumerable.Range(0, urlCount)
            .Select(index => Url(
                10_000 + index,
                $"Synthetic History Bookmark {index}",
                $"https://history-{index % 31}.example/item/{index}"))
            .ToArray();

        var bookmarkBar = Folder(
            1,
            "Bookmarks bar",
            bookmarks);
        var other = Folder(2, "Other bookmarks");
        var synced = Folder(3, "Mobile bookmarks");
        var document = Document(bookmarkBar, other, synced);
        var originalOrder = bookmarkBar.Children.ToArray();
        var history = new BookmarkHistoryManager();
        var deleteService = new BookmarkDeleteService();

        var selected = bookmarks
            .Where((_, index) => index % 2 == 0)
            .ToArray();

        var result = deleteService.DeleteBookmarks(document, selected);
        history.Record(new BookmarkBatchDeleteHistoryEntry(result));

        Assert.Equal(urlCount - selected.Length, document.UrlCount);
        Assert.True(history.CanUndo);

        var undo = history.Undo(document);

        Assert.True(undo.Changed);
        Assert.Equal(urlCount, document.UrlCount);
        Assert.Equal(originalOrder, bookmarkBar.Children);
        Assert.All(
            bookmarks,
            bookmark => Assert.Same(bookmarkBar, bookmark.Parent));

        var redo = history.Redo(document);

        Assert.True(redo.Changed);
        Assert.Equal(urlCount - selected.Length, document.UrlCount);
        Assert.All(
            selected,
            bookmark => Assert.Null(bookmark.Parent));

        output.WriteLine(
            $"URLs={urlCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"Batch={selected.Length.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"HistoryCount={history.Count}; Capacity={history.Capacity}");
    }

    [Fact]
    [Trait("Category", "UndoRedoScale")]
    public void ManySequentialEdits_RespectBoundedHistoryAndRedoBranchTruncation()
    {
        var editCount = ReadCount(
            "CBM_HISTORY_EDIT_COUNT",
            1_000,
            BookmarkHistoryManager.DefaultCapacity + 1,
            100_000);
        var bookmark = Url(
            10,
            "Initial",
            "https://history.example/original");
        var bookmarkBar = Folder(1, "Bookmarks bar", bookmark);
        var document = Document(
            bookmarkBar,
            Folder(2, "Other bookmarks"),
            Folder(3, "Mobile bookmarks"));
        var editing = new BookmarkEditingService();
        var history = new BookmarkHistoryManager();

        for (var index = 0; index < editCount; index++)
        {
            var oldName = bookmark.Name;
            var newName = $"History Rename {index}";
            Assert.True(editing.RenameNode(document, bookmark, newName));
            history.Record(
                new BookmarkRenameHistoryEntry(
                    bookmark,
                    oldName,
                    bookmark.Name));
        }

        Assert.Equal(
            BookmarkHistoryManager.DefaultCapacity,
            history.Count);
        Assert.Equal(
            BookmarkHistoryManager.DefaultCapacity,
            history.Position);
        Assert.False(history.IsCleanStateReachable);

        for (var index = 0;
             index < BookmarkHistoryManager.DefaultCapacity / 2;
             index++)
        {
            Assert.True(history.Undo(document).Changed);
        }

        Assert.True(history.CanRedo);

        var branchOldName = bookmark.Name;
        Assert.True(
            editing.RenameNode(
                document,
                bookmark,
                "Divergent History Edit"));
        history.Record(
            new BookmarkRenameHistoryEntry(
                bookmark,
                branchOldName,
                bookmark.Name));

        Assert.False(history.CanRedo);
        Assert.Equal(
            BookmarkHistoryManager.DefaultCapacity / 2 + 1,
            history.Position);
        Assert.False(history.IsAtCleanState);

        output.WriteLine(
            $"ForwardEdits={editCount.ToString("N0", CultureInfo.InvariantCulture)}; " +
            $"RetainedHistory={history.Count}; Position={history.Position}");
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

    private static BookmarkDocument Document(
        BookmarkFolder bookmarkBar,
        BookmarkFolder other,
        BookmarkFolder synced) =>
        new(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);

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
