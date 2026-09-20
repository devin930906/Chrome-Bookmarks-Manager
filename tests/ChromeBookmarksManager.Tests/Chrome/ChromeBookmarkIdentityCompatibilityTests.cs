using System.Text.Json;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Application.History;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Chrome;

public sealed class ChromeBookmarkIdentityCompatibilityTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddAfterDeleteUndoRedo_DoesNotReusePreviouslyAllocatedNodeId()
    {
        var (document, target) = CreateDocument();
        var editing = new BookmarkEditingService(new FixedTimeProvider(FixedNow));
        var deleting = new BookmarkDeleteService();
        var history = new BookmarkHistoryManager();

        var first = editing.AddBookmark(
            document,
            target,
            "First",
            "https://example.test/first");

        Assert.Equal("1000", first.Id);

        var deleted = deleting.DeleteNode(document, first);
        history.Record(new BookmarkDeleteHistoryEntry(deleted));

        Assert.True(history.Undo(document).Changed);
        Assert.True(history.Redo(document).Changed);

        var second = editing.AddBookmark(
            document,
            target,
            "Second",
            "https://example.test/second");

        Assert.Equal("1001", second.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Guid, second.Guid);
    }

    [Fact]
    public void AddBookmark_GeneratesLowercaseRfc4122Version4Identity()
    {
        var (document, target) = CreateDocument();
        var editing = new BookmarkEditingService(new FixedTimeProvider(FixedNow));

        var bookmark = editing.AddBookmark(
            document,
            target,
            "Identity",
            "https://example.test/identity");

        var text = bookmark.Guid.ToString("D");

        Assert.Equal(text.ToLowerInvariant(), text);
        Assert.Equal('4', text[14]);
        Assert.Contains(text[19], new[] { '8', '9', 'a', 'b' });
        Assert.NotEqual(Guid.Empty, bookmark.Guid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Writer_RejectsDuplicateIdentityBeforeWritingAnything(
        bool duplicateId)
    {
        var document = CreateDocumentWithDuplicateIdentity(duplicateId);
        var writer = new ChromeBookmarksWriter();
        await using var output = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(document, output));

        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task WriterRoundTrip_PreservesExistingRootAndNodeIdsAndGuids()
    {
        var (document, target) = CreateDocument();
        var originalRootIdentity = new[]
        {
            (document.Roots.BookmarkBar.Id, document.Roots.BookmarkBar.Guid),
            (document.Roots.Other.Id, document.Roots.Other.Guid),
            (document.Roots.Synced.Id, document.Roots.Synced.Guid)
        };
        var originalTargetIdentity = (target.Id, target.Guid);

        var writer = new ChromeBookmarksWriter();
        var reader = new ChromeBookmarksReader();
        await using var output = new MemoryStream();

        await writer.WriteAsync(document, output);
        output.Position = 0;
        var reloaded = await reader.ReadAsync(output);

        Assert.Equal(
            originalRootIdentity[0],
            (reloaded.Roots.BookmarkBar.Id, reloaded.Roots.BookmarkBar.Guid));
        Assert.Equal(
            originalRootIdentity[1],
            (reloaded.Roots.Other.Id, reloaded.Roots.Other.Guid));
        Assert.Equal(
            originalRootIdentity[2],
            (reloaded.Roots.Synced.Id, reloaded.Roots.Synced.Guid));

        var reloadedTarget =
            Assert.IsType<BookmarkFolder>(reloaded.Roots.BookmarkBar.Children.Single());

        Assert.Equal(
            originalTargetIdentity,
            (reloadedTarget.Id, reloadedTarget.Guid));
    }

    private static (BookmarkDocument Document, BookmarkFolder Target)
        CreateDocument()
    {
        var existing = new BookmarkUrl(
            "999",
            Guid.Parse("99999999-9999-4999-8999-999999999999"),
            "Existing",
            "https://example.test/existing",
            "100",
            null,
            "0",
            null,
            EmptyProperties());

        var target = new BookmarkFolder(
            "10",
            Guid.Parse("10101010-1010-4010-8010-101010101010"),
            "Target",
            "90",
            "95",
            "0",
            null,
            EmptyProperties(),
            new BookmarkNode[] { existing });

        var bar = Folder(
            "1",
            "11111111-1111-4111-8111-111111111111",
            "Bookmarks bar",
            target);
        var other = Folder(
            "2",
            "22222222-2222-4222-8222-222222222222",
            "Other bookmarks");
        var synced = Folder(
            "3",
            "33333333-3333-4333-8333-333333333333",
            "Mobile bookmarks");

        return (
            new BookmarkDocument(
                1,
                null,
                null,
                new BookmarkRoots(
                    bar,
                    other,
                    synced,
                    EmptyProperties()),
                EmptyProperties()),
            target);
    }

    private static BookmarkDocument CreateDocumentWithDuplicateIdentity(
        bool duplicateId)
    {
        var sharedGuid =
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

        var first = new BookmarkUrl(
            "10",
            sharedGuid,
            "First",
            "https://example.test/first",
            null,
            null,
            null,
            null,
            EmptyProperties());

        var second = new BookmarkUrl(
            duplicateId ? "10" : "11",
            duplicateId
                ? Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb")
                : sharedGuid,
            "Second",
            "https://example.test/second",
            null,
            null,
            null,
            null,
            EmptyProperties());

        var bar = Folder(
            "1",
            "11111111-1111-4111-8111-111111111111",
            "Bookmarks bar",
            first,
            second);
        var other = Folder(
            "2",
            "22222222-2222-4222-8222-222222222222",
            "Other bookmarks");
        var synced = Folder(
            "3",
            "33333333-3333-4333-8333-333333333333",
            "Mobile bookmarks");

        return new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bar,
                other,
                synced,
                EmptyProperties()),
            EmptyProperties());
    }

    private static BookmarkFolder Folder(
        string id,
        string guid,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            Guid.Parse(guid),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties(),
            children);

    private static IReadOnlyDictionary<string, JsonElement>
        EmptyProperties() =>
            new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
