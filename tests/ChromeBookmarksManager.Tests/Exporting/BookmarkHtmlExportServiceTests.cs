using System.Text.Json;
using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.Application.Exporting;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Exporting;

public sealed class BookmarkHtmlExportServiceTests
{
    [Fact]
    public async Task ExportAsync_NestedMixedTree_WritesDeterministicNetscapeHtmlInCurrentOrder()
    {
        var nested = Url(
            "12",
            "Nested",
            "https://example.com/nested");
        var child = Folder(
            "20",
            "Child",
            nested);
        var document = Document(
            Folder(
                "1",
                "Bookmarks bar",
                Url("10", "First", "https://example.com/first"),
                child,
                Url("11", "Last", "https://example.com/last")),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var service = new BookmarkHtmlExportService();
        using var writer = new StringWriter();

        await service.ExportAsync(
            document,
            writer,
            CancellationToken.None);

        const string expected =
            "<!DOCTYPE NETSCAPE-Bookmark-file-1>\n" +
            "<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">\n" +
            "<TITLE>Bookmarks</TITLE>\n" +
            "<H1>Bookmarks</H1>\n" +
            "<DL><p>\n" +
            "    <DT><H3 PERSONAL_TOOLBAR_FOLDER=\"true\">Bookmarks bar</H3>\n" +
            "    <DL><p>\n" +
            "        <DT><A HREF=\"https://example.com/first\">First</A>\n" +
            "        <DT><H3>Child</H3>\n" +
            "        <DL><p>\n" +
            "            <DT><A HREF=\"https://example.com/nested\">Nested</A>\n" +
            "        </DL><p>\n" +
            "        <DT><A HREF=\"https://example.com/last\">Last</A>\n" +
            "    </DL><p>\n" +
            "    <DT><H3>Other bookmarks</H3>\n" +
            "    <DL><p>\n" +
            "    </DL><p>\n" +
            "    <DT><H3>Mobile bookmarks</H3>\n" +
            "    <DL><p>\n" +
            "    </DL><p>\n" +
            "</DL><p>\n";

        Assert.Equal(expected, writer.ToString());
    }

    [Fact]
    public async Task ExportAsync_EscapesHtmlAndPreservesUnicode()
    {
        var bookmark = Url(
            "10",
            "标题 & <测试> \"电影\"",
            "https://example.com/?a=1&b=\"x\"<y>");
        var folder = Folder(
            "20",
            "研究 & \"电影\" <资料>",
            bookmark);
        var document = Document(
            Folder("1", "Bookmarks bar", folder),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var service = new BookmarkHtmlExportService();
        using var writer = new StringWriter();

        await service.ExportAsync(
            document,
            writer,
            CancellationToken.None);

        var html = writer.ToString();

        Assert.Contains(
            "研究 &amp; &quot;电影&quot; &lt;资料&gt;",
            html);
        Assert.Contains(
            "HREF=\"https://example.com/?a=1&amp;b=&quot;x&quot;&lt;y&gt;\"",
            html);
        Assert.Contains(
            "标题 &amp; &lt;测试&gt; &quot;电影&quot;",
            html);
        Assert.DoesNotContain(
            "研究 & \"电影\" <资料>",
            html);
    }

    [Fact]
    public async Task ExportFileAsync_WritesUtf8WithoutBomAndUsesStreamingExporter()
    {
        var document = Document(
            Folder(
                "1",
                "Bookmarks bar",
                Url(
                    "10",
                    "Unicode 标题",
                    "https://example.com/export")),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var service = new BookmarkHtmlExportService();
        var path = Path.Combine(
            Path.GetTempPath(),
            $"cbm-export-{Guid.NewGuid():N}.html");

        try
        {
            await service.ExportFileAsync(
                document,
                path,
                CancellationToken.None);

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.False(
                bytes.Length >= 3 &&
                bytes[0] == 0xEF &&
                bytes[1] == 0xBB &&
                bytes[2] == 0xBF);

            var html = await File.ReadAllTextAsync(path);
            Assert.Contains(
                "<!DOCTYPE NETSCAPE-Bookmark-file-1>",
                html);
            Assert.Contains(
                "<DT><A HREF=\"https://example.com/export\">Unicode 标题</A>",
                html);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportBookmarksHtmlAsync_DoesNotDirtyOrMutateLoadedDocument()
    {
        var first = Url(
            "10",
            "First",
            "https://example.com/first");
        var nested = Url(
            "11",
            "Nested",
            "https://example.com/nested");
        var child = Folder(
            "20",
            "Child",
            nested);
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            first,
            child);
        var document = Document(
            bookmarkBar,
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var originalChildren = bookmarkBar.Children.ToArray();
        var originalChildChildren = child.Children.ToArray();
        var originalUrlCount = document.UrlCount;
        var originalFolderCount = document.FolderCount;
        var viewModel = new MainViewModel(
            new StubReader((_, _) => Task.FromResult(document)),
            new BookmarkSearchService(),
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks");

        using var writer = new StringWriter();
        await viewModel.ExportBookmarksHtmlAsync(
            writer,
            CancellationToken.None);

        Assert.Equal(
            DocumentState.LoadedClean,
            viewModel.State);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanUndo);
        Assert.Equal(originalUrlCount, document.UrlCount);
        Assert.Equal(originalFolderCount, document.FolderCount);
        Assert.Equal(originalChildren, bookmarkBar.Children);
        Assert.Equal(originalChildChildren, child.Children);
        Assert.Same(bookmarkBar, first.Parent);
        Assert.Same(bookmarkBar, child.Parent);
        Assert.Same(child, nested.Parent);
    }

    private sealed class StubReader(
        Func<string, CancellationToken, Task<BookmarkDocument>> read)
        : IChromeBookmarksReader
    {
        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            read(path, cancellationToken);
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
        string name,
        string url) =>
        new(
            id,
            GuidFor(int.Parse(id)),
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
