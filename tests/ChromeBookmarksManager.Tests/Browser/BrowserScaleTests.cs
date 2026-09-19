using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;
using Xunit.Abstractions;

namespace ChromeBookmarksManager.Tests.Browser;

public sealed class BrowserScaleTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "BrowserScale")]
    public async Task BrowserState_UsesDomainReferencesAtScale()
    {
        var urlCount = int.TryParse(
            Environment.GetEnvironmentVariable("CBM_BROWSER_URL_COUNT"),
            out var configured)
            ? configured
            : 10_000;

        Assert.InRange(urlCount, 1, 1_000_000);

        var urls = new BookmarkUrl[urlCount];
        for (var index = 0; index < urlCount; index++)
        {
            urls[index] = new BookmarkUrl(
                (index + 4L).ToString(CultureInfo.InvariantCulture),
                CreateGuid(index + 1000),
                $"Synthetic Bookmark {index}",
                $"https://example.com/bookmark/{index}",
                null,
                null,
                null,
                null,
                EmptyProperties);
        }

        var bookmarkBar = new BookmarkFolder(
            "1",
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Bookmarks bar",
            null,
            null,
            null,
            null,
            EmptyProperties,
            urls);
        var other = EmptyFolder(
            "2",
            "22222222-2222-4222-8222-222222222222",
            "Other bookmarks");
        var synced = EmptyFolder(
            "3",
            "33333333-3333-4333-8333-333333333333",
            "Mobile bookmarks");
        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(bookmarkBar, other, synced, EmptyProperties),
            EmptyProperties);

        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(document)));
        var before = Process.GetCurrentProcess().WorkingSet64;
        var stopwatch = Stopwatch.StartNew();

        await viewModel.LoadBookmarksAsync(@"C:\Synthetic\Bookmarks");

        stopwatch.Stop();
        var after = Process.GetCurrentProcess().WorkingSet64;

        Assert.Equal(urlCount, viewModel.CurrentBookmarks.Count);
        Assert.Same(urls[0], viewModel.CurrentBookmarks[0]);
        Assert.Same(urls[^1], viewModel.CurrentBookmarks[^1]);
        Assert.True(
            urls.Zip(
                    viewModel.CurrentBookmarks,
                    (expected, actual) => ReferenceEquals(expected, actual))
                .All(same => same));
        Assert.Equal(
            document.FolderCount,
            CountFolderWrappers(viewModel.FolderRoots));
        Assert.Equal(3, CountFolderWrappers(viewModel.FolderRoots));

        output.WriteLine(
            $"URLs={urlCount:N0}; " +
            $"Elapsed={stopwatch.Elapsed}; " +
            $"WorkingSetDelta={after - before:N0} bytes; " +
            $"FolderWrappers={CountFolderWrappers(viewModel.FolderRoots):N0}");
    }

    private static int CountFolderWrappers(
        IEnumerable<FolderTreeItemViewModel> roots)
    {
        var count = 0;
        var stack = new Stack<FolderTreeItemViewModel>(roots.Reverse());

        while (stack.TryPop(out var item))
        {
            count++;
            for (var index = item.Children.Count - 1; index >= 0; index--)
            {
                stack.Push(item.Children[index]);
            }
        }

        return count;
    }

    private static BookmarkFolder EmptyFolder(
        string id,
        string guid,
        string name) =>
        new(
            id,
            Guid.Parse(guid),
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

    private sealed class StubReader(
        Func<string, CancellationToken, Task<BookmarkDocument>> read)
        : IChromeBookmarksReader
    {
        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            read(path, cancellationToken);
    }
}
