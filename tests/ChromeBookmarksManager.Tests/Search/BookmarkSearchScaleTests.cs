using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Domain;
using Xunit.Abstractions;

namespace ChromeBookmarksManager.Tests.Search;

public sealed class BookmarkSearchScaleTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "SearchScale")]
    public async Task SearchIndexAndQueries_UseOriginalReferencesAtScale()
    {
        var urlCount = int.TryParse(
            Environment.GetEnvironmentVariable("CBM_SEARCH_URL_COUNT"),
            out var configured)
            ? configured
            : 10_000;

        Assert.InRange(urlCount, 1, 1_000_000);

        var currentFolderCount = Math.Min(urlCount, 100);
        var urls = new BookmarkUrl[urlCount];

        for (var index = 0; index < urlCount; index++)
        {
            var name = index switch
            {
                0 => "Needle Name common-scale-match current-folder-only",
                _ => $"Synthetic Bookmark {index} common-scale-match"
            };

            var url = index switch
            {
                1 => "https://special-domain.example/path/common-scale-match",
                _ => $"https://domain-{index % 17}.example/search/{index}/common-scale-match"
            };

            urls[index] = new BookmarkUrl(
                (index + 10L).ToString(CultureInfo.InvariantCulture),
                CreateGuid(index + 1000),
                name,
                url,
                null,
                null,
                null,
                null,
                EmptyProperties);
        }

        var currentFolder = new BookmarkFolder(
            "4",
            CreateGuid(4),
            "Current folder",
            null,
            null,
            null,
            null,
            EmptyProperties,
            urls.Take(currentFolderCount));

        var bookmarkBarChildren = new List<BookmarkNode> { currentFolder };
        bookmarkBarChildren.AddRange(urls.Skip(currentFolderCount));

        var bookmarkBar = new BookmarkFolder(
            "1",
            CreateGuid(1),
            "Bookmarks bar",
            null,
            null,
            null,
            null,
            EmptyProperties,
            bookmarkBarChildren);

        var other = EmptyFolder("2", "Other bookmarks");
        var synced = EmptyFolder("3", "Mobile bookmarks");
        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(bookmarkBar, other, synced, EmptyProperties),
            EmptyProperties);

        var service = new BookmarkSearchService();

        var before = Process.GetCurrentProcess().WorkingSet64;
        var indexStopwatch = Stopwatch.StartNew();
        var indexModel = await service.BuildIndexAsync(
            document,
            CancellationToken.None);
        indexStopwatch.Stop();

        Assert.Equal(urlCount, indexModel.Count);
        Assert.Same(urls[0], indexModel.Bookmarks[0]);
        Assert.Same(urls[^1], indexModel.Bookmarks[^1]);
        Assert.True(
            urls.Zip(
                    indexModel.Bookmarks,
                    (expected, actual) => ReferenceEquals(expected, actual))
                .All(same => same));

        var searchStopwatch = Stopwatch.StartNew();
        var commonResults = await service.SearchAsync(
            indexModel,
            "common-scale-match",
            BookmarkSearchScope.AllBookmarks,
            currentFolder,
            CancellationToken.None);
        searchStopwatch.Stop();
        var after = Process.GetCurrentProcess().WorkingSet64;

        Assert.Equal(urlCount, commonResults.Count);
        Assert.Same(urls[0], commonResults[0]);
        Assert.Same(urls[^1], commonResults[^1]);
        Assert.True(
            urls.Zip(
                    commonResults,
                    (expected, actual) => ReferenceEquals(expected, actual))
                .All(same => same));

        var nameResults = await service.SearchAsync(
            indexModel,
            "needle name",
            BookmarkSearchScope.AllBookmarks,
            currentFolder,
            CancellationToken.None);
        Assert.Same(urls[0], Assert.Single(nameResults));

        if (urlCount > 1)
        {
            var domainResults = await service.SearchAsync(
                indexModel,
                "SPECIAL-DOMAIN.EXAMPLE",
                BookmarkSearchScope.AllBookmarks,
                currentFolder,
                CancellationToken.None);
            Assert.Same(urls[1], Assert.Single(domainResults));
        }

        var folderResults = await service.SearchAsync(
            indexModel,
            "current-folder-only",
            BookmarkSearchScope.CurrentFolder,
            currentFolder,
            CancellationToken.None);
        Assert.Same(urls[0], Assert.Single(folderResults));

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SearchAsync(
                indexModel,
                "common-scale-match",
                BookmarkSearchScope.AllBookmarks,
                currentFolder,
                canceled.Token));

        output.WriteLine(
            $"URLs={urlCount:N0}; " +
            $"Results={commonResults.Count:N0}; " +
            $"IndexElapsed={indexStopwatch.Elapsed}; " +
            $"SearchElapsed={searchStopwatch.Elapsed}; " +
            $"WorkingSetDelta={after - before:N0} bytes");
    }

    private static BookmarkFolder EmptyFolder(string id, string name) =>
        new(
            id,
            CreateGuid(int.Parse(id, CultureInfo.InvariantCulture)),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            Array.Empty<BookmarkNode>());

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
