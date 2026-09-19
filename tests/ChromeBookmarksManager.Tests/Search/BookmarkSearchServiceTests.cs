using System.Text.Json;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Search;

public sealed class BookmarkSearchServiceTests
{
    private readonly BookmarkSearchService _service = new();

    [Fact]
    public async Task BuildIndexAsync_UsesOriginalDocumentReferencesInStableOrder()
    {
        var first = Url("10", "First", "https://example.com/first");
        var second = Url("11", "Second", "https://example.com/second");
        var document = Document(
            Folder("1", "Bookmarks bar", first),
            Folder("2", "Other bookmarks", second),
            Folder("3", "Mobile bookmarks"));

        var index = await _service.BuildIndexAsync(document, CancellationToken.None);

        Assert.Equal(2, index.Count);
        Assert.Same(first, index.Bookmarks[0]);
        Assert.Same(second, index.Bookmarks[1]);
    }

    [Fact]
    public async Task SearchAsync_MatchesNameCaseInsensitively()
    {
        var target = Url("10", "OpenAI Docs", "https://example.com/reference");
        var index = Index(target);

        var results = await _service.SearchAsync(
            index,
            "openai",
            BookmarkSearchScope.AllBookmarks,
            null,
            CancellationToken.None);

        Assert.Single(results);
        Assert.Same(target, results[0]);
    }

    [Fact]
    public async Task SearchAsync_MatchesUrlAndDomainCaseInsensitively()
    {
        var target = Url("10", "Reference", "https://Docs.Example.COM/path");
        var index = Index(target);

        var results = await _service.SearchAsync(
            index,
            "example.com",
            BookmarkSearchScope.AllBookmarks,
            null,
            CancellationToken.None);

        Assert.Single(results);
        Assert.Same(target, results[0]);
    }

    [Fact]
    public async Task SearchAsync_MatchesChineseUnicodeSubstring()
    {
        var target = Url("10", "电影资料库", "https://example.com/cinema");
        var index = Index(target);

        var results = await _service.SearchAsync(
            index,
            "资料",
            BookmarkSearchScope.AllBookmarks,
            null,
            CancellationToken.None);

        Assert.Single(results);
        Assert.Same(target, results[0]);
    }

    [Fact]
    public async Task SearchAsync_MatchesOpaqueCustomUrlScheme()
    {
        var target = Url("10", "Internal", "custom-scheme:Workspace/Alpha");
        var index = Index(target);

        var results = await _service.SearchAsync(
            index,
            "workspace/alpha",
            BookmarkSearchScope.AllBookmarks,
            null,
            CancellationToken.None);

        Assert.Single(results);
        Assert.Same(target, results[0]);
    }

    [Fact]
    public async Task SearchAsync_TrimsQueryWhitespace()
    {
        var target = Url("10", "Trim Me", "https://example.com/");
        var index = Index(target);

        var results = await _service.SearchAsync(
            index,
            "   trim me   ",
            BookmarkSearchScope.AllBookmarks,
            null,
            CancellationToken.None);

        Assert.Single(results);
        Assert.Same(target, results[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  \t\r\n  ")]
    public async Task SearchAsync_WhitespaceOnlyQueryReturnsNoResults(string query)
    {
        var index = Index(Url("10", "Anything", "https://example.com/"));

        var results = await _service.SearchAsync(
            index,
            query,
            BookmarkSearchScope.AllBookmarks,
            null,
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_AllBookmarksPreservesDocumentOrderWithoutRanking()
    {
        var first = Url("10", "match third-looking", "https://example.com/1");
        var nested = Url("11", "match second-looking", "https://example.com/2");
        var other = Url("12", "match first-looking", "https://example.com/3");
        var synced = Url("13", "match zero-looking", "https://example.com/4");

        var document = Document(
            Folder("1", "Bookmarks bar", first, Folder("20", "Nested", nested)),
            Folder("2", "Other bookmarks", other),
            Folder("3", "Mobile bookmarks", synced));
        var index = new BookmarkSearchIndex(document);

        var results = await _service.SearchAsync(
            index,
            "match",
            BookmarkSearchScope.AllBookmarks,
            null,
            CancellationToken.None);

        Assert.Collection(
            results,
            item => Assert.Same(first, item),
            item => Assert.Same(nested, item),
            item => Assert.Same(other, item),
            item => Assert.Same(synced, item));
    }

    [Fact]
    public async Task SearchAsync_CurrentFolderUsesDirectUrlChildrenOnly()
    {
        var first = Url("10", "match first", "https://example.com/1");
        var descendant = Url("11", "match descendant", "https://example.com/2");
        var nested = Folder("20", "Nested", descendant);
        var second = Url("12", "match second", "https://example.com/3");
        var current = Folder("1", "Bookmarks bar", first, nested, second);
        var document = Document(
            current,
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var index = new BookmarkSearchIndex(document);

        var results = await _service.SearchAsync(
            index,
            "match",
            BookmarkSearchScope.CurrentFolder,
            current,
            CancellationToken.None);

        Assert.Collection(
            results,
            item => Assert.Same(first, item),
            item => Assert.Same(second, item));
        Assert.DoesNotContain(descendant, results);
    }

    [Fact]
    public async Task SearchAsync_CurrentFolderWithNullFolderReturnsNoResults()
    {
        var index = Index(Url("10", "match", "https://example.com/"));

        var results = await _service.SearchAsync(
            index,
            "match",
            BookmarkSearchScope.CurrentFolder,
            null,
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_NameAndUrlMatchAddsBookmarkOnlyOnce()
    {
        var target = Url("10", "alpha bookmark", "https://alpha.example/path");
        var index = Index(target);

        var results = await _service.SearchAsync(
            index,
            "alpha",
            BookmarkSearchScope.AllBookmarks,
            null,
            CancellationToken.None);

        Assert.Single(results);
        Assert.Same(target, results[0]);
    }

    [Fact]
    public async Task SearchAsync_CancellationIsObservedDuringLargeScan()
    {
        const int count = 200_000;
        var urls = new BookmarkUrl[count];
        for (var index = 0; index < count; index++)
        {
            urls[index] = Url(
                (index + 10).ToString(),
                $"Synthetic {index}",
                $"https://example.com/{index}");
        }

        var searchIndex = Index(urls);
        using var cts = new CancellationTokenSource();

        var searchTask = _service.SearchAsync(
            searchIndex,
            "definitely-not-present",
            BookmarkSearchScope.AllBookmarks,
            null,
            cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => searchTask);
    }

    [Fact]
    public async Task BuildIndexAsync_CanceledTokenPropagatesOperationCanceledException()
    {
        var document = Document(
            Folder("1", "Bookmarks bar", Url("10", "One", "https://example.com/")),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.BuildIndexAsync(document, cts.Token));
    }

    private static BookmarkSearchIndex Index(params BookmarkUrl[] urls)
    {
        var document = Document(
            Folder("1", "Bookmarks bar", urls),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));

        return new BookmarkSearchIndex(document);
    }

    private static BookmarkDocument Document(
        BookmarkFolder bookmarkBar,
        BookmarkFolder other,
        BookmarkFolder synced) =>
        new(
            1,
            null,
            null,
            new BookmarkRoots(bookmarkBar, other, synced, EmptyProperties),
            EmptyProperties);

    private static BookmarkFolder Folder(
        string id,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            CreateGuid(int.Parse(id)),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            children);

    private static BookmarkUrl Url(string id, string name, string url) =>
        new(
            id,
            CreateGuid(int.Parse(id)),
            name,
            url,
            null,
            null,
            null,
            null,
            EmptyProperties);

    private static Guid CreateGuid(int value)
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
