using System.Text.Json;
using ChromeBookmarksManager.Application.Importing;
using ChromeBookmarksManager.Application.Search;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Importing;

public sealed class BookmarkHtmlImportServiceTests
{
    [Fact]
    public async Task ImportAsync_NestedExportFixture_AppendsDedicatedImportedFolderInExactOrder()
    {
        var existing = Url(
            "10",
            "Existing",
            "https://existing.example/");
        var document = Document(
            Folder("1", "Bookmarks bar", existing),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var target = document.Roots.BookmarkBar;
        var service = new BookmarkHtmlImportService();

        using var reader = new StringReader(NestedFixture);
        var result = await service.ImportAsync(
            document,
            target,
            reader,
            CancellationToken.None);

        Assert.True(result.Changed);
        Assert.Equal("Imported bookmarks", result.ImportedFolder.Name);
        Assert.Same(target, result.ImportedFolder.Parent);
        Assert.Equal(2, target.Children.Count);
        Assert.Same(existing, target.Children[0]);
        Assert.Same(result.ImportedFolder, target.Children[1]);

        var importedRoots = result.ImportedFolder.Children;
        Assert.Equal(3, importedRoots.Count);

        var bar = Assert.IsType<BookmarkFolder>(importedRoots[0]);
        Assert.Equal("Bookmarks bar", bar.Name);
        Assert.Equal(3, bar.Children.Count);

        var first = Assert.IsType<BookmarkUrl>(bar.Children[0]);
        Assert.Equal("First", first.Name);
        Assert.Equal("https://example.com/first", first.Url);

        var child = Assert.IsType<BookmarkFolder>(bar.Children[1]);
        Assert.Equal("Child & 电影", child.Name);
        var nested = Assert.IsType<BookmarkUrl>(
            Assert.Single(child.Children));
        Assert.Equal("Nested & Unicode 标题", nested.Name);
        Assert.Equal(
            "https://example.com/nested?a=1&b=2",
            nested.Url);

        var last = Assert.IsType<BookmarkUrl>(bar.Children[2]);
        Assert.Equal("Last", last.Name);

        var other = Assert.IsType<BookmarkFolder>(importedRoots[1]);
        Assert.Equal("Other bookmarks", other.Name);
        var otherUrl = Assert.IsType<BookmarkUrl>(
            Assert.Single(other.Children));
        Assert.Equal("Other", otherUrl.Name);

        var mobile = Assert.IsType<BookmarkFolder>(importedRoots[2]);
        Assert.Equal("Mobile bookmarks", mobile.Name);
        Assert.Empty(mobile.Children);

        Assert.Equal(4, result.AddedUrlCount);
        Assert.Equal(5, result.AddedFolderCount);
    }

    [Fact]
    public async Task ImportAsync_AssignsFreshUniqueIdsAndGuidsAcrossEntireDocument()
    {
        var document = Document(
            Folder(
                "1",
                "Bookmarks bar",
                Url("10", "Existing", "https://existing.example/")),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var service = new BookmarkHtmlImportService();

        using var firstReader = new StringReader(SimpleFixture);
        var first = await service.ImportAsync(
            document,
            document.Roots.BookmarkBar,
            firstReader,
            CancellationToken.None);

        using var secondReader = new StringReader(SimpleFixture);
        var second = await service.ImportAsync(
            document,
            document.Roots.BookmarkBar,
            secondReader,
            CancellationToken.None);

        var nodes = EnumerateAllNodes(document).ToArray();

        Assert.Equal(
            nodes.Length,
            nodes.Select(node => node.Id).Distinct().Count());
        Assert.Equal(
            nodes.Length,
            nodes.Select(node => node.Guid).Distinct().Count());

        Assert.NotEqual(first.ImportedFolder.Id, second.ImportedFolder.Id);
        Assert.NotEqual(first.ImportedFolder.Guid, second.ImportedFolder.Guid);
        Assert.All(
            first.ImportedFolder.Children,
            node => Assert.DoesNotContain(
                node.Guid,
                new[]
                {
                    document.Roots.BookmarkBar.Guid,
                    document.Roots.Other.Guid,
                    document.Roots.Synced.Guid
                }));
    }

    [Fact]
    public async Task ImportAsync_MalformedHtml_FailsWithoutPartialMutation()
    {
        var document = CreateAtomicityDocument();
        var target = document.Roots.BookmarkBar;
        var beforeChildren = target.Children.ToArray();
        var beforeUrls = document.UrlCount;
        var beforeFolders = document.FolderCount;
        var service = new BookmarkHtmlImportService();

        const string malformed =
            "<DL><p><DT><A HREF=\"https://example.com\">Broken</DL><p>";

        using var reader = new StringReader(malformed);
        var error = await Assert.ThrowsAsync<BookmarkHtmlImportException>(
            () => service.ImportAsync(
                document,
                target,
                reader,
                CancellationToken.None));

        Assert.Equal(
            BookmarkHtmlImportError.MalformedHtml,
            error.Error);
        Assert.Equal(beforeChildren, target.Children);
        Assert.Equal(beforeUrls, document.UrlCount);
        Assert.Equal(beforeFolders, document.FolderCount);
    }

    [Fact]
    public async Task ImportAsync_DepthLimitExceeded_FailsWithoutPartialMutation()
    {
        var document = CreateAtomicityDocument();
        var target = document.Roots.BookmarkBar;
        var beforeChildren = target.Children.ToArray();
        var beforeUrls = document.UrlCount;
        var beforeFolders = document.FolderCount;
        var service = new BookmarkHtmlImportService(
            maxNodes: 100,
            maxDepth: 2,
            maxCharacters: 1_000_000);

        const string tooDeep =
            "<DL><p>" +
            "<DT><H3>Level 1</H3><DL><p>" +
            "<DT><H3>Level 2</H3><DL><p>" +
            "<DT><H3>Level 3</H3><DL><p>" +
            "<DT><A HREF=\"https://example.com\">Leaf</A>" +
            "</DL><p></DL><p></DL><p></DL><p>";

        using var reader = new StringReader(tooDeep);
        var error = await Assert.ThrowsAsync<BookmarkHtmlImportException>(
            () => service.ImportAsync(
                document,
                target,
                reader,
                CancellationToken.None));

        Assert.Equal(
            BookmarkHtmlImportError.DepthLimitExceeded,
            error.Error);
        Assert.Equal(beforeChildren, target.Children);
        Assert.Equal(beforeUrls, document.UrlCount);
        Assert.Equal(beforeFolders, document.FolderCount);
    }

    [Fact]
    public async Task ImportAsync_NodeLimitExceeded_FailsWithoutPartialMutation()
    {
        var document = CreateAtomicityDocument();
        var target = document.Roots.BookmarkBar;
        var beforeChildren = target.Children.ToArray();
        var beforeUrls = document.UrlCount;
        var beforeFolders = document.FolderCount;
        var service = new BookmarkHtmlImportService(
            maxNodes: 2,
            maxDepth: 10,
            maxCharacters: 1_000_000);

        const string tooMany =
            "<DL><p>" +
            "<DT><A HREF=\"https://example.com/1\">One</A>" +
            "<DT><A HREF=\"https://example.com/2\">Two</A>" +
            "<DT><A HREF=\"https://example.com/3\">Three</A>" +
            "</DL><p>";

        using var reader = new StringReader(tooMany);
        var error = await Assert.ThrowsAsync<BookmarkHtmlImportException>(
            () => service.ImportAsync(
                document,
                target,
                reader,
                CancellationToken.None));

        Assert.Equal(
            BookmarkHtmlImportError.NodeLimitExceeded,
            error.Error);
        Assert.Equal(beforeChildren, target.Children);
        Assert.Equal(beforeUrls, document.UrlCount);
        Assert.Equal(beforeFolders, document.FolderCount);
    }

    [Fact]
    public async Task ImportBookmarksHtmlAsync_IsOneUndoableHistoryOperation()
    {
        var document = Document(
            Folder("1", "Bookmarks bar"),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));
        var viewModel = new MainViewModel(
            new StubReader(document),
            new BookmarkSearchService(),
            TimeSpan.Zero);

        await viewModel.LoadBookmarksAsync(
            @"C:\Synthetic\Bookmarks");

        var target = Assert.IsType<BookmarkFolder>(
            viewModel.SelectedFolder);
        var beforeUrls = document.UrlCount;
        var beforeFolders = document.FolderCount;

        using var reader = new StringReader(SimpleFixture);
        var result = await viewModel.ImportBookmarksHtmlAsync(
            reader,
            CancellationToken.None);

        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Same(
            result.ImportedFolder,
            target.Children[^1]);
        Assert.Equal(
            beforeUrls + result.AddedUrlCount,
            document.UrlCount);
        Assert.Equal(
            beforeFolders + result.AddedFolderCount,
            document.FolderCount);

        Assert.True(await viewModel.UndoAsync());
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.CanRedo);
        Assert.DoesNotContain(
            result.ImportedFolder,
            target.Children);
        Assert.Equal(beforeUrls, document.UrlCount);
        Assert.Equal(beforeFolders, document.FolderCount);

        Assert.True(await viewModel.RedoAsync());
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Same(
            result.ImportedFolder,
            target.Children[^1]);
        Assert.Equal(
            beforeUrls + result.AddedUrlCount,
            document.UrlCount);
        Assert.Equal(
            beforeFolders + result.AddedFolderCount,
            document.FolderCount);
    }

    private static BookmarkDocument CreateAtomicityDocument() =>
        Document(
            Folder(
                "1",
                "Bookmarks bar",
                Url("10", "Existing", "https://existing.example/")),
            Folder("2", "Other bookmarks"),
            Folder("3", "Mobile bookmarks"));

    private static IEnumerable<BookmarkNode> EnumerateAllNodes(
        BookmarkDocument document)
    {
        var stack = new Stack<BookmarkNode>(
            new BookmarkNode[]
            {
                document.Roots.Synced,
                document.Roots.Other,
                document.Roots.BookmarkBar
            });

        while (stack.TryPop(out var node))
        {
            yield return node;

            if (node is not BookmarkFolder folder)
            {
                continue;
            }

            for (var index = folder.Children.Count - 1;
                 index >= 0;
                 index--)
            {
                stack.Push(folder.Children[index]);
            }
        }
    }

    private sealed class StubReader(BookmarkDocument document)
        : IChromeBookmarksReader
    {
        public Task<BookmarkDocument> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(document);
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

    private const string SimpleFixture =
        "<!DOCTYPE NETSCAPE-Bookmark-file-1>\n" +
        "<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">\n" +
        "<TITLE>Bookmarks</TITLE>\n" +
        "<H1>Bookmarks</H1>\n" +
        "<DL><p>\n" +
        "    <DT><H3>Imported source</H3>\n" +
        "    <DL><p>\n" +
        "        <DT><A HREF=\"https://example.com/simple\">Simple</A>\n" +
        "    </DL><p>\n" +
        "</DL><p>\n";

    private const string NestedFixture =
        "<!DOCTYPE NETSCAPE-Bookmark-file-1>\n" +
        "<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">\n" +
        "<TITLE>Bookmarks</TITLE>\n" +
        "<H1>Bookmarks</H1>\n" +
        "<DL><p>\n" +
        "    <DT><H3 PERSONAL_TOOLBAR_FOLDER=\"true\">Bookmarks bar</H3>\n" +
        "    <DL><p>\n" +
        "        <DT><A HREF=\"https://example.com/first\">First</A>\n" +
        "        <DT><H3>Child &amp; 电影</H3>\n" +
        "        <DL><p>\n" +
        "            <DT><A HREF=\"https://example.com/nested?a=1&amp;b=2\">Nested &amp; Unicode 标题</A>\n" +
        "        </DL><p>\n" +
        "        <DT><A HREF=\"https://example.com/last\">Last</A>\n" +
        "    </DL><p>\n" +
        "    <DT><H3>Other bookmarks</H3>\n" +
        "    <DL><p>\n" +
        "        <DT><A HREF=\"https://example.com/other\">Other</A>\n" +
        "    </DL><p>\n" +
        "    <DT><H3>Mobile bookmarks</H3>\n" +
        "    <DL><p>\n" +
        "    </DL><p>\n" +
        "</DL><p>\n";

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
