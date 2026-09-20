using System.Text.Json;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Application.History;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.History;

public sealed class BookmarkMutationHistoryEntryTests
{
    [Fact]
    public void AddBookmark_UndoRedo_PreservesIdentityPlacementAndCounts()
    {
        var fixture = CreateFixture();
        var editing = new BookmarkEditingService();
        var beforeUrls = fixture.Document.UrlCount;
        var index = fixture.BookmarkBar.Children.Count;

        var added = editing.AddBookmark(
            fixture.Document,
            fixture.BookmarkBar,
            "Added",
            "https://example.com/added");
        var entry = new BookmarkAddHistoryEntry(
            added,
            fixture.BookmarkBar,
            index);

        Assert.Equal(BookmarkHistoryImpact.SearchRelevant, entry.Impact);
        Assert.Equal(beforeUrls + 1, fixture.Document.UrlCount);

        entry.Undo(fixture.Document);

        Assert.Null(added.Parent);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);
        Assert.DoesNotContain(added, fixture.BookmarkBar.Children);

        entry.Redo(fixture.Document);

        Assert.Same(
            added,
            fixture.BookmarkBar.Children[index]);
        Assert.Same(fixture.BookmarkBar, added.Parent);
        Assert.Equal(beforeUrls + 1, fixture.Document.UrlCount);
    }

    [Fact]
    public void AddFolder_UndoRedo_PreservesIdentityPlacementAndCounts()
    {
        var fixture = CreateFixture();
        var editing = new BookmarkEditingService();
        var beforeFolders = fixture.Document.FolderCount;
        var index = fixture.BookmarkBar.Children.Count;

        var added = editing.AddFolder(
            fixture.Document,
            fixture.BookmarkBar,
            "Added folder");
        var entry = new BookmarkAddHistoryEntry(
            added,
            fixture.BookmarkBar,
            index);

        entry.Undo(fixture.Document);

        Assert.Null(added.Parent);
        Assert.Equal(beforeFolders, fixture.Document.FolderCount);

        entry.Redo(fixture.Document);

        Assert.Same(
            added,
            fixture.BookmarkBar.Children[index]);
        Assert.Equal(beforeFolders + 1, fixture.Document.FolderCount);
    }

    [Fact]
    public void Rename_UndoRedo_UsesSameNodeAndExactSanitizedValues()
    {
        var fixture = CreateFixture();
        var editing = new BookmarkEditingService();
        var bookmark = fixture.BarFirst;
        var oldName = bookmark.Name;

        Assert.True(editing.RenameNode(
            fixture.Document,
            bookmark,
            "Renamed\nbookmark"));

        var newName = bookmark.Name;
        var entry = new BookmarkRenameHistoryEntry(
            bookmark,
            oldName,
            newName);

        Assert.Equal("Renamed bookmark", newName);

        entry.Undo(fixture.Document);
        Assert.Equal(oldName, bookmark.Name);

        entry.Redo(fixture.Document);
        Assert.Equal(newName, bookmark.Name);
    }

    [Fact]
    public void EditUrl_UndoRedo_UsesSameBookmarkAndExactValues()
    {
        var fixture = CreateFixture();
        var editing = new BookmarkEditingService();
        var bookmark = fixture.BarFirst;
        var oldUrl = bookmark.Url;
        const string newUrl = "https://changed.example/path";

        Assert.True(editing.EditUrl(
            fixture.Document,
            bookmark,
            newUrl));

        var entry = new BookmarkUrlEditHistoryEntry(
            bookmark,
            oldUrl,
            newUrl);

        entry.Undo(fixture.Document);
        Assert.Equal(oldUrl, bookmark.Url);

        entry.Redo(fixture.Document);
        Assert.Equal(newUrl, bookmark.Url);
    }

    [Fact]
    public void SameParentSameKindReorder_UndoRedo_PreservesMixedChildSlotsExactly()
    {
        var fixture = CreateFixture();
        var moving = new BookmarkMoveService();
        var before = fixture.BookmarkBar.Children.ToArray();

        var result = moving.MoveBookmarkAfter(
            fixture.Document,
            fixture.BarFirst,
            fixture.BarSecond);

        Assert.True(result.Changed);
        Assert.Equal(
            BookmarkMoveSemantics.SameKindSlotReorder,
            result.Semantics);
        Assert.Equal(0, result.SourceVisibleIndex);
        Assert.Equal(1, result.TargetVisibleIndex);

        var after = fixture.BookmarkBar.Children.ToArray();
        var entry = new BookmarkMoveHistoryEntry(
            fixture.BarFirst,
            result);

        entry.Undo(fixture.Document);

        Assert.Equal(before, fixture.BookmarkBar.Children);
        Assert.Same(
            fixture.BarFirst,
            fixture.BookmarkBar.Children[0]);
        Assert.Same(
            fixture.ChildFolder,
            fixture.BookmarkBar.Children[1]);
        Assert.Same(
            fixture.BarSecond,
            fixture.BookmarkBar.Children[2]);

        entry.Redo(fixture.Document);

        Assert.Equal(after, fixture.BookmarkBar.Children);
    }

    [Fact]
    public void CrossParentMove_UndoRedo_RestoresExactRawPlacement()
    {
        var fixture = CreateFixture();
        var moving = new BookmarkMoveService();
        var beforeBar = fixture.BookmarkBar.Children.ToArray();
        var beforeOther = fixture.Other.Children.ToArray();

        var result = moving.MoveToEnd(
            fixture.Document,
            fixture.BarFirst,
            fixture.Other);
        var entry = new BookmarkMoveHistoryEntry(
            fixture.BarFirst,
            result);
        var afterBar = fixture.BookmarkBar.Children.ToArray();
        var afterOther = fixture.Other.Children.ToArray();

        Assert.Equal(
            BookmarkMoveSemantics.Direct,
            result.Semantics);

        entry.Undo(fixture.Document);

        Assert.Equal(beforeBar, fixture.BookmarkBar.Children);
        Assert.Equal(beforeOther, fixture.Other.Children);

        entry.Redo(fixture.Document);

        Assert.Equal(afterBar, fixture.BookmarkBar.Children);
        Assert.Equal(afterOther, fixture.Other.Children);
    }

    [Fact]
    public void BatchMove_UndoRedo_RestoresEveryParentAndGroupOrder()
    {
        var fixture = CreateFixture();
        var moving = new BookmarkMoveService();
        var selected = new[]
        {
            fixture.BarSecond,
            fixture.OtherFirst,
            fixture.BarFirst
        };
        var beforeBar = fixture.BookmarkBar.Children.ToArray();
        var beforeOther = fixture.Other.Children.ToArray();
        var beforeSynced = fixture.Synced.Children.ToArray();

        var result = moving.MoveBookmarksToEnd(
            fixture.Document,
            selected,
            fixture.Synced);
        var entry = new BookmarkBatchMoveHistoryEntry(
            selected,
            result);
        var afterBar = fixture.BookmarkBar.Children.ToArray();
        var afterOther = fixture.Other.Children.ToArray();
        var afterSynced = fixture.Synced.Children.ToArray();

        entry.Undo(fixture.Document);

        Assert.Equal(beforeBar, fixture.BookmarkBar.Children);
        Assert.Equal(beforeOther, fixture.Other.Children);
        Assert.Equal(beforeSynced, fixture.Synced.Children);

        entry.Redo(fixture.Document);

        Assert.Equal(afterBar, fixture.BookmarkBar.Children);
        Assert.Equal(afterOther, fixture.Other.Children);
        Assert.Equal(afterSynced, fixture.Synced.Children);
        Assert.Equal(
            selected,
            fixture.Synced.Children);
    }

    [Fact]
    public void DeleteBookmark_UndoRedo_RestoresIdentityIndexAndCount()
    {
        var fixture = CreateFixture();
        var deleting = new BookmarkDeleteService();
        var before = fixture.BookmarkBar.Children.ToArray();
        var beforeUrls = fixture.Document.UrlCount;

        var result = deleting.DeleteNode(
            fixture.Document,
            fixture.BarFirst);
        var entry = new BookmarkDeleteHistoryEntry(result);

        Assert.Null(fixture.BarFirst.Parent);
        Assert.Equal(beforeUrls - 1, fixture.Document.UrlCount);

        entry.Undo(fixture.Document);

        Assert.Equal(before, fixture.BookmarkBar.Children);
        Assert.Same(fixture.BookmarkBar, fixture.BarFirst.Parent);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);

        entry.Redo(fixture.Document);

        Assert.Null(fixture.BarFirst.Parent);
        Assert.Equal(beforeUrls - 1, fixture.Document.UrlCount);
    }

    [Fact]
    public void DeleteFolderSubtree_UndoRedo_RestoresSubtreeAndExactCounts()
    {
        var fixture = CreateFixture();
        var deleting = new BookmarkDeleteService();
        var before = fixture.BookmarkBar.Children.ToArray();
        var beforeUrls = fixture.Document.UrlCount;
        var beforeFolders = fixture.Document.FolderCount;

        var result = deleting.DeleteNode(
            fixture.Document,
            fixture.ChildFolder);
        var entry = new BookmarkDeleteHistoryEntry(result);

        Assert.Equal(beforeUrls - 1, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders - 1, fixture.Document.FolderCount);
        Assert.Same(
            fixture.Nested,
            fixture.ChildFolder.Children[0]);
        Assert.Same(
            fixture.ChildFolder,
            fixture.Nested.Parent);

        entry.Undo(fixture.Document);

        Assert.Equal(before, fixture.BookmarkBar.Children);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders, fixture.Document.FolderCount);
        Assert.Same(fixture.BookmarkBar, fixture.ChildFolder.Parent);
        Assert.Same(fixture.ChildFolder, fixture.Nested.Parent);

        entry.Redo(fixture.Document);

        Assert.Null(fixture.ChildFolder.Parent);
        Assert.Equal(beforeUrls - 1, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders - 1, fixture.Document.FolderCount);
    }

    [Fact]
    public void BatchDelete_UndoRedo_RestoresMultipleParentsAndOrder()
    {
        var fixture = CreateFixture();
        var deleting = new BookmarkDeleteService();
        var beforeBar = fixture.BookmarkBar.Children.ToArray();
        var beforeOther = fixture.Other.Children.ToArray();
        var beforeUrls = fixture.Document.UrlCount;

        var result = deleting.DeleteBookmarks(
            fixture.Document,
            new[]
            {
                fixture.BarFirst,
                fixture.OtherSecond
            });
        var entry = new BookmarkBatchDeleteHistoryEntry(result);

        entry.Undo(fixture.Document);

        Assert.Equal(beforeBar, fixture.BookmarkBar.Children);
        Assert.Equal(beforeOther, fixture.Other.Children);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);

        entry.Redo(fixture.Document);

        Assert.Null(fixture.BarFirst.Parent);
        Assert.Null(fixture.OtherSecond.Parent);
        Assert.Equal(beforeUrls - 2, fixture.Document.UrlCount);
    }

    [Fact]
    public void MoveEntry_RejectsUnexpectedCurrentPlacementWithoutMutation()
    {
        var fixture = CreateFixture();
        var moving = new BookmarkMoveService();

        var result = moving.MoveToEnd(
            fixture.Document,
            fixture.BarFirst,
            fixture.Other);
        var entry = new BookmarkMoveHistoryEntry(
            fixture.BarFirst,
            result);

        var movedIndex = fixture.Other.IndexOfChild(
            fixture.BarFirst);
        var detached = fixture.Other.RemoveChildAt(movedIndex);
        fixture.Synced.AddChild(detached);
        var beforeBar = fixture.BookmarkBar.Children.ToArray();
        var beforeOther = fixture.Other.Children.ToArray();
        var beforeSynced = fixture.Synced.Children.ToArray();

        Assert.Throws<InvalidOperationException>(
            () => entry.Undo(fixture.Document));

        Assert.Equal(beforeBar, fixture.BookmarkBar.Children);
        Assert.Equal(beforeOther, fixture.Other.Children);
        Assert.Equal(beforeSynced, fixture.Synced.Children);
    }

    private static Fixture CreateFixture()
    {
        var barFirst = Url("10", "First");
        var nested = Url("12", "Nested");
        var childFolder = Folder(
            "20",
            "Child folder",
            nested);
        var barSecond = Url("11", "Second");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            barFirst,
            childFolder,
            barSecond);

        var otherFirst = Url("13", "Other first");
        var otherSecond = Url("14", "Other second");
        var other = Folder(
            "2",
            "Other bookmarks",
            otherFirst,
            otherSecond);

        var synced = Folder(
            "3",
            "Mobile bookmarks");

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

        return new Fixture(
            document,
            bookmarkBar,
            other,
            synced,
            childFolder,
            barFirst,
            barSecond,
            nested,
            otherFirst,
            otherSecond);
    }

    private static BookmarkFolder Folder(
        string id,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            children);

    private static BookmarkUrl Url(
        string id,
        string name) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            $"https://example.com/{id}",
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

    private sealed record Fixture(
        BookmarkDocument Document,
        BookmarkFolder BookmarkBar,
        BookmarkFolder Other,
        BookmarkFolder Synced,
        BookmarkFolder ChildFolder,
        BookmarkUrl BarFirst,
        BookmarkUrl BarSecond,
        BookmarkUrl Nested,
        BookmarkUrl OtherFirst,
        BookmarkUrl OtherSecond);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
