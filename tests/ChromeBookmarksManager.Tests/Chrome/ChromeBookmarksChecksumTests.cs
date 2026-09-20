using System.Text.Json;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Chrome;

public sealed class ChromeBookmarksChecksumTests
{
    [Fact]
    public void Compute_KnownChromiumCompatibleVector_MatchesMd5AndSha256()
    {
        var document = CreateKnownVectorDocument();

        var checksums = ChromeBookmarksChecksum.Compute(document);

        Assert.Equal("58bc0222a8144cbdd631624c9c57e96b", checksums.Md5);
        Assert.Equal(
            "9942446cbc90a3f27764bf19cb40f26f8cf733d58c7e81a3251664d30fc3dc9b",
            checksums.Sha256);
    }

    [Fact]
    public void Compute_WhenChildOrderChanges_ChangesBothChecksums()
    {
        var first = CreateOrderDocument(reverseChildren: false);
        var second = CreateOrderDocument(reverseChildren: true);

        var firstChecksums = ChromeBookmarksChecksum.Compute(first);
        var secondChecksums = ChromeBookmarksChecksum.Compute(second);

        Assert.NotEqual(firstChecksums.Md5, secondChecksums.Md5);
        Assert.NotEqual(firstChecksums.Sha256, secondChecksums.Sha256);
    }

    [Fact]
    public void Compute_WhenOnlyMetadataChanges_DoesNotChangeChecksums()
    {
        var first = CreateMetadataDocument("""{"source":"one"}""");
        var second = CreateMetadataDocument("""{"source":"two","nested":{"value":1}}""");

        Assert.Equal(
            ChromeBookmarksChecksum.Compute(first),
            ChromeBookmarksChecksum.Compute(second));
    }

    private static BookmarkDocument CreateKnownVectorDocument()
    {
        var bookmark = new BookmarkUrl(
            "4",
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "例子😀",
            "https://example.com/路径?q=测试",
            "10",
            null,
            "0",
            null,
            EmptyExtensionData());

        var bookmarkBar = Folder("1", "Bookmarks bar", bookmark);
        var other = Folder("2", "Other bookmarks");
        var synced = Folder("3", "Mobile bookmarks");

        return Document(bookmarkBar, other, synced);
    }

    private static BookmarkDocument CreateOrderDocument(bool reverseChildren)
    {
        var first = new BookmarkUrl(
            "4",
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "First",
            "https://example.com/first",
            null,
            null,
            null,
            null,
            EmptyExtensionData());
        var second = new BookmarkUrl(
            "5",
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            "Second",
            "https://example.com/second",
            null,
            null,
            null,
            null,
            EmptyExtensionData());

        var children = reverseChildren
            ? new BookmarkNode[] { second, first }
            : new BookmarkNode[] { first, second };

        return Document(
            Folder("1", "Bookmarks bar", children),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
    }

    private static BookmarkDocument CreateMetadataDocument(string metaInfoJson)
    {
        using var json = JsonDocument.Parse(metaInfoJson);
        var metaInfo = json.RootElement.Clone();

        var bookmark = new BookmarkUrl(
            "4",
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "Same",
            "https://example.com/same",
            "10",
            "20",
            "30",
            metaInfo,
            new Dictionary<string, JsonElement>
            {
                ["future_field"] = JsonDocument.Parse(""preserved"").RootElement.Clone()
            });

        return Document(
            Folder("1", "Bookmarks bar", bookmark),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
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
                EmptyExtensionData()),
            EmptyExtensionData());

    private static BookmarkFolder Folder(
        string id,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            Guid.Parse($"{int.Parse(id):x8}-0000-4000-8000-{int.Parse(id):x12}"),
            name,
            null,
            null,
            null,
            null,
            EmptyExtensionData(),
            children);

    private static IReadOnlyDictionary<string, JsonElement> EmptyExtensionData() =>
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}
