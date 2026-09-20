using System.Text.Json;
using ChromeBookmarksManager.Application.Deleting;
using ChromeBookmarksManager.Application.Editing;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Chrome;

public sealed class ChromeBookmarkPersistenceCompatibilityTests
{
    [Fact]
    public async Task WriteAsync_DoesNotRewriteExistingIdsOrGuids()
    {
        var document = CreateDocument(out var target, out var existing);
        var originalRootIds = new[]
        {
            document.Roots.BookmarkBar.Id,
            document.Roots.Other.Id,
            document.Roots.Synced.Id
        };
        var originalRootGuids = new[]
        {
            document.Roots.BookmarkBar.Guid,
            document.Roots.Other.Guid,
            document.Roots.Synced.Guid
        };
        var existingId = existing.Id;
        var existingGuid = existing.Guid;
        var writer = new ChromeBookmarksWriter();
        await using var output = new MemoryStream();

        await writer.WriteAsync(document, output);

        Assert.Equal(originalRootIds, new[]
        {
            document.Roots.BookmarkBar.Id,
            document.Roots.Other.Id,
            document.Roots.Synced.Id
        });
        Assert.Equal(originalRootGuids, new[]
        {
            document.Roots.BookmarkBar.Guid,
            document.Roots.Other.Guid,
            document.Roots.Synced.Guid
        });
        Assert.Equal(existingId, existing.Id);
        Assert.Equal(existingGuid, existing.Guid);
        Assert.Same(target, existing.Parent);
    }

    [Fact]
    public void DeleteHighestIdThenAdd_DoesNotReuseDeletedId()
    {
        var document = CreateDocument(out var target, out var existing);
        var deleteService = new BookmarkDeleteService();
        var editingService = new BookmarkEditingService();

        Assert.Equal("999", existing.Id);

        deleteService.DeleteNode(document, existing);
        var added = editingService.AddBookmark(
            document,
            target,
            "Replacement",
            "https://example.test/replacement");

        Assert.Equal("1000", added.Id);
        Assert.NotEqual(existing.Guid, added.Guid);
        Assert.NotEqual(Guid.Empty, added.Guid);
        Assert.Equal('4', added.Guid.ToString("D")[14]);
    }

    [Fact]
    public async Task WriteAsync_DuplicateNodeId_IsRejectedBeforeOutput()
    {
        var document = CreateDocumentWithDuplicateIds();
        var writer = new ChromeBookmarksWriter();
        await using var output = new MemoryStream();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(document, output));

        Assert.Contains("invalid or duplicated", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task WriteAsync_DuplicateGuid_IsRejectedBeforeOutput()
    {
        var document = CreateDocumentWithDuplicateGuids();
        var writer = new ChromeBookmarksWriter();
        await using var output = new MemoryStream();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(document, output));

        Assert.Contains("invalid or duplicated", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, output.Length);
    }

    private static BookmarkDocument CreateDocument(
        out BookmarkFolder target,
        out BookmarkUrl existing)
    {
        existing = new BookmarkUrl(
            "999",
            Guid.Parse("99999999-9999-4999-8999-999999999999"),
            "Existing",
            "https://example.test/existing",
            "100",
            null,
            "0",
            null,
            Empty());

        target = new BookmarkFolder(
            "10",
            Guid.Parse("10101010-1010-4010-8010-101010101010"),
            "Target",
            "90",
            "95",
            "0",
            null,
            Empty(),
            new BookmarkNode[] { existing });

        return new BookmarkDocument(
            1,
            "old-md5",
            "old-sha",
            new BookmarkRoots(
                Folder("1", "11111111-1111-4111-8111-111111111111", "Bookmarks bar", target),
                Folder("2", "22222222-2222-4222-8222-222222222222", "Other bookmarks"),
                Folder("3", "33333333-3333-4333-8333-333333333333", "Mobile bookmarks"),
                Empty()),
            Empty());
    }

    private static BookmarkDocument CreateDocumentWithDuplicateIds()
    {
        var duplicate = new BookmarkUrl(
            "2",
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "Duplicate ID",
            "https://example.test/id",
            null,
            null,
            null,
            null,
            Empty());

        return new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                Folder("1", "11111111-1111-4111-8111-111111111111", "Bookmarks bar", duplicate),
                Folder("2", "22222222-2222-4222-8222-222222222222", "Other bookmarks"),
                Folder("3", "33333333-3333-4333-8333-333333333333", "Mobile bookmarks"),
                Empty()),
            Empty());
    }

    private static BookmarkDocument CreateDocumentWithDuplicateGuids()
    {
        var duplicateGuid = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var duplicate = new BookmarkUrl(
            "4",
            duplicateGuid,
            "Duplicate GUID",
            "https://example.test/guid",
            null,
            null,
            null,
            null,
            Empty());

        return new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                Folder("1", "11111111-1111-4111-8111-111111111111", "Bookmarks bar", duplicate),
                Folder("2", duplicateGuid.ToString("D"), "Other bookmarks"),
                Folder("3", "33333333-3333-4333-8333-333333333333", "Mobile bookmarks"),
                Empty()),
            Empty());
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
            Empty(),
            children);

    private static IReadOnlyDictionary<string, JsonElement> Empty() =>
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}
