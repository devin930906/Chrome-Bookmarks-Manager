using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using Xunit.Abstractions;

namespace ChromeBookmarksManager.Tests.Saving;

public sealed class BookmarkWriteScaleTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "WriteScale")]
    public async Task Writer_RoundTripsLargeSyntheticDocumentWithExactIdentityAndChecksums()
    {
        var urlCount = ReadCount(
            "CBM_WRITE_URL_COUNT",
            10_000,
            100,
            250_000);
        var path = Path.Combine(
            Path.GetTempPath(),
            $"cbm-write-scale-{Guid.NewGuid():N}.json");

        var urls = new BookmarkUrl[urlCount];
        for (var index = 0; index < urlCount; index++)
        {
            var id = index + 10;
            urls[index] = new BookmarkUrl(
                id.ToString(CultureInfo.InvariantCulture),
                GuidFor(id),
                $"Synthetic Write Bookmark {index} 中文",
                $"https://write-{index % 37}.example/item/{index}?q=%E6%B5%8B%E8%AF%95",
                "13370000000000000",
                null,
                "0",
                null,
                EmptyProperties);
        }

        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                Folder(1, "Bookmarks bar", urls),
                Folder(2, "Other bookmarks"),
                Folder(3, "Mobile bookmarks"),
                EmptyProperties),
            EmptyProperties);

        try
        {
            var writer = new ChromeBookmarksWriter();
            var expectedChecksums = ChromeBookmarksChecksum.Compute(document);
            var before = Process.GetCurrentProcess().WorkingSet64;
            var writeStopwatch = Stopwatch.StartNew();

            await using (var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var writtenChecksums = await writer.WriteAsync(
                    document,
                    stream,
                    CancellationToken.None);

                Assert.Equal(expectedChecksums, writtenChecksums);
                await stream.FlushAsync();
            }

            writeStopwatch.Stop();

            var readStopwatch = Stopwatch.StartNew();
            var reloaded = await new ChromeBookmarksReader()
                .ReadFileAsync(path);
            readStopwatch.Stop();
            var after = Process.GetCurrentProcess().WorkingSet64;

            Assert.Equal(urlCount, reloaded.UrlCount);
            Assert.Equal(3, reloaded.FolderCount);
            Assert.Equal(expectedChecksums, ChromeBookmarksChecksum.Compute(reloaded));
            Assert.Equal(urlCount, reloaded.Roots.BookmarkBar.Children.Count);

            for (var index = 0; index < urlCount; index++)
            {
                var expected = urls[index];
                var actual = Assert.IsType<BookmarkUrl>(
                    reloaded.Roots.BookmarkBar.Children[index]);

                Assert.Equal(expected.Id, actual.Id);
                Assert.Equal(expected.Guid, actual.Guid);
                Assert.Equal(expected.Name, actual.Name);
                Assert.Equal(expected.Url, actual.Url);
            }

            output.WriteLine(
                $"URLs={urlCount:N0}; " +
                $"WriteElapsed={writeStopwatch.Elapsed}; " +
                $"ReadElapsed={readStopwatch.Elapsed}; " +
                $"WorkingSetDelta={after - before:N0} bytes; " +
                $"File={new FileInfo(path).Length:N0} bytes");
        }
        finally
        {
            File.Delete(path);
        }
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

    private static BookmarkFolder Folder(
        int id,
        string name,
        params BookmarkNode[] children) =>
        new(
            id.ToString(CultureInfo.InvariantCulture),
            GuidFor(id),
            name,
            "13370000000000000",
            "13370000000000000",
            "0",
            null,
            EmptyProperties,
            children);

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
