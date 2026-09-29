using System.Text.Json;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Chrome;

public sealed class ChromeBookmarksWriterTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Bookmarks.sample.json");

    [Fact]
    public async Task WriteAsync_SampleFixture_RegeneratesChecksumsAndPreservesUnknownData()
    {
        var reader = new ChromeBookmarksReader();
        var document = await reader.ReadFileAsync(FixturePath);
        var expectedChecksums = ChromeBookmarksChecksum.Compute(document);
        var writer = new ChromeBookmarksWriter();
        await using var output = new MemoryStream();

        var result = await writer.WriteAsync(document, output);

        Assert.Equal(expectedChecksums, result);
        output.Position = 0;

        using var json = await JsonDocument.ParseAsync(output);
        var root = json.RootElement;
        var roots = root.GetProperty("roots");
        var documentation = roots
            .GetProperty("bookmark_bar")
            .GetProperty("children")[0]
            .GetProperty("children")[0];
        var network = roots
            .GetProperty("other")
            .GetProperty("children")[0];

        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal(expectedChecksums.Md5, root.GetProperty("checksum").GetString());
        Assert.Equal(expectedChecksums.Sha256, root.GetProperty("checksum_sha256").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("future_document").ValueKind);
        Assert.Equal("synthetic-only", root.GetProperty("v0_1_fixture_marker").GetString());
        Assert.Equal(JsonValueKind.Object, roots.GetProperty("future_root").ValueKind);
        Assert.Equal(JsonValueKind.Object, documentation.GetProperty("future_node").ValueKind);
        Assert.Equal(
            "fixture",
            network.GetProperty("meta_info")
                .GetProperty("nested")
                .GetProperty("source")
                .GetString());
    }

    [Fact]
    public async Task WriteAsync_SampleFixture_RoundTripsLogicalDocument()
    {
        var reader = new ChromeBookmarksReader();
        var original = await reader.ReadFileAsync(FixturePath);
        var writer = new ChromeBookmarksWriter();
        await using var output = new MemoryStream();

        await writer.WriteAsync(original, output);

        output.Position = 0;
        var reloaded = await reader.ReadAsync(output);

        Assert.Equal(original.Version, reloaded.Version);
        Assert.Equal(original.UrlCount, reloaded.UrlCount);
        Assert.Equal(original.FolderCount, reloaded.FolderCount);
        Assert.Equal(ChromeBookmarksChecksum.Compute(original), ChromeBookmarksChecksum.Compute(reloaded));
        AssertRootsEquivalent(original.Roots, reloaded.Roots);
        AssertJsonDictionaryEquivalent(original.ExtensionData, reloaded.ExtensionData);
        AssertJsonDictionaryEquivalent(original.Roots.ExtensionData, reloaded.Roots.ExtensionData);
    }

    [Fact]
    public async Task WriteAsync_ChromeNativeShape_UsesChromiumFormattingAndPreservesMissingSha256()
    {
        var document = CreateNativeShapeDocument();
        var expectedChecksums = ChromeBookmarksChecksum.Compute(document);
        var writer = new ChromeBookmarksWriter();
        await using var output = new MemoryStream();

        await writer.WriteAsync(document, output);

        var text = System.Text.Encoding.UTF8.GetString(output.ToArray());
        var expected =
            "{\r\n" +
            $"   \"checksum\": \"{expectedChecksums.Md5}\",\r\n" +
            "   \"roots\": {\r\n" +
            "      \"bookmark_bar\": {\r\n" +
            "         \"children\": [ {\r\n" +
            "            \"date_added\": \"40\",\r\n" +
            "            \"date_last_used\": \"0\",\r\n" +
            "            \"guid\": \"44444444-4444-4444-8444-444444444444\",\r\n" +
            "            \"id\": \"4\",\r\n" +
            "            \"name\": \"中文\\u003C测试>\",\r\n" +
            "            \"type\": \"url\",\r\n" +
            "            \"url\": \"https://example.com/?q=中文\"\r\n" +
            "         } ],\r\n" +
            "         \"date_added\": \"10\",\r\n" +
            "         \"date_last_used\": \"0\",\r\n" +
            "         \"date_modified\": \"20\",\r\n" +
            "         \"guid\": \"11111111-1111-4111-8111-111111111111\",\r\n" +
            "         \"id\": \"1\",\r\n" +
            "         \"name\": \"书签栏\",\r\n" +
            "         \"type\": \"folder\"\r\n" +
            "      },\r\n" +
            "      \"other\": {\r\n" +
            "         \"children\": [  ],\r\n" +
            "         \"date_added\": \"11\",\r\n" +
            "         \"date_last_used\": \"0\",\r\n" +
            "         \"date_modified\": \"21\",\r\n" +
            "         \"guid\": \"22222222-2222-4222-8222-222222222222\",\r\n" +
            "         \"id\": \"2\",\r\n" +
            "         \"name\": \"其他书签\",\r\n" +
            "         \"type\": \"folder\"\r\n" +
            "      },\r\n" +
            "      \"synced\": {\r\n" +
            "         \"children\": [  ],\r\n" +
            "         \"date_added\": \"12\",\r\n" +
            "         \"date_last_used\": \"0\",\r\n" +
            "         \"date_modified\": \"22\",\r\n" +
            "         \"guid\": \"33333333-3333-4333-8333-333333333333\",\r\n" +
            "         \"id\": \"3\",\r\n" +
            "         \"name\": \"移动设备书签\",\r\n" +
            "         \"type\": \"folder\"\r\n" +
            "      }\r\n" +
            "   },\r\n" +
            "   \"version\": 1\r\n" +
            "}\r\n";

        Assert.Equal(expected, text);
        Assert.Contains("中文", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u4E2D", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"checksum_sha256\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_CanceledToken_ThrowsBeforeCompletingOutput()
    {
        var reader = new ChromeBookmarksReader();
        var document = await reader.ReadFileAsync(FixturePath);
        var writer = new ChromeBookmarksWriter();
        await using var output = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => writer.WriteAsync(document, output, cancellation.Token));

        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task WriteAsync_ReservedExtensionProperties_DoNotDuplicateWriterOwnedFields()
    {
        var document = CreateDocumentWithReservedExtensionCollisions();
        var writer = new ChromeBookmarksWriter();
        await using var output = new MemoryStream();

        await writer.WriteAsync(document, output);

        output.Position = 0;
        using var json = await JsonDocument.ParseAsync(output);
        var rootNames = json.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        var bookmarkBarNames = json.RootElement
            .GetProperty("roots")
            .GetProperty("bookmark_bar")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(1, rootNames.Count(name => name == "version"));
        Assert.Equal(1, rootNames.Count(name => name == "checksum"));
        Assert.Equal(1, rootNames.Count(name => name == "checksum_sha256"));
        Assert.Equal(1, rootNames.Count(name => name == "roots"));
        Assert.Equal(1, bookmarkBarNames.Count(name => name == "id"));
        Assert.Equal("1", json.RootElement
            .GetProperty("roots")
            .GetProperty("bookmark_bar")
            .GetProperty("id")
            .GetString());
    }

    private static BookmarkDocument CreateNativeShapeDocument()
    {
        var empty =
            new Dictionary<string, JsonElement>(
                StringComparer.Ordinal);

        var child = new BookmarkUrl(
            "4",
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "中文<测试>",
            "https://example.com/?q=中文",
            "40",
            null,
            "0",
            null,
            empty);

        var bookmarkBar = new BookmarkFolder(
            "1",
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "书签栏",
            "10",
            "20",
            "0",
            null,
            empty,
            new BookmarkNode[] { child });

        var other = new BookmarkFolder(
            "2",
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            "其他书签",
            "11",
            "21",
            "0",
            null,
            empty,
            Array.Empty<BookmarkNode>());

        var synced = new BookmarkFolder(
            "3",
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            "移动设备书签",
            "12",
            "22",
            "0",
            null,
            empty,
            Array.Empty<BookmarkNode>());

        return new BookmarkDocument(
            1,
            checksum: null,
            checksumSha256: null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                empty),
            empty);
    }

    private static BookmarkDocument CreateDocumentWithReservedExtensionCollisions()
    {
        var topExtensions = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["version"] = JsonSerializer.SerializeToElement(999),
            ["checksum"] = JsonSerializer.SerializeToElement("wrong"),
            ["roots"] = JsonSerializer.SerializeToElement("wrong"),
            ["custom_top"] = JsonSerializer.SerializeToElement("preserve")
        };
        var rootNodeExtensions = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["id"] = JsonSerializer.SerializeToElement("999"),
            ["name"] = JsonSerializer.SerializeToElement("wrong"),
            ["custom_node"] = JsonSerializer.SerializeToElement(true)
        };

        var bookmarkBar = new BookmarkFolder(
            "1",
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "Bookmarks bar",
            "10",
            "20",
            "0",
            null,
            rootNodeExtensions,
            Array.Empty<BookmarkNode>());
        var other = EmptyFolder("2", "22222222-2222-4222-8222-222222222222", "Other bookmarks");
        var synced = EmptyFolder("3", "33333333-3333-4333-8333-333333333333", "Mobile bookmarks");

        return new BookmarkDocument(
            1,
            "old",
            "old-sha",
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal)),
            topExtensions);
    }

    private static BookmarkFolder EmptyFolder(string id, string guid, string name) =>
        new(
            id,
            Guid.Parse(guid),
            name,
            null,
            null,
            null,
            null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            Array.Empty<BookmarkNode>());

    private static void AssertRootsEquivalent(BookmarkRoots expected, BookmarkRoots actual)
    {
        AssertNodeEquivalent(expected.BookmarkBar, actual.BookmarkBar);
        AssertNodeEquivalent(expected.Other, actual.Other);
        AssertNodeEquivalent(expected.Synced, actual.Synced);
    }

    private static void AssertNodeEquivalent(BookmarkNode expected, BookmarkNode actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Guid, actual.Guid);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.DateAddedRaw, actual.DateAddedRaw);
        Assert.Equal(expected.DateModifiedRaw, actual.DateModifiedRaw);
        Assert.Equal(expected.DateLastUsedRaw, actual.DateLastUsedRaw);
        AssertJsonEquivalent(expected.MetaInfo, actual.MetaInfo);
        AssertJsonDictionaryEquivalent(expected.ExtensionData, actual.ExtensionData);

        if (expected is BookmarkUrl expectedUrl)
        {
            var actualUrl = Assert.IsType<BookmarkUrl>(actual);
            Assert.Equal(expectedUrl.Url, actualUrl.Url);
            return;
        }

        var expectedFolder = Assert.IsType<BookmarkFolder>(expected);
        var actualFolder = Assert.IsType<BookmarkFolder>(actual);
        Assert.Equal(expectedFolder.Children.Count, actualFolder.Children.Count);

        for (var index = 0; index < expectedFolder.Children.Count; index++)
        {
            AssertNodeEquivalent(expectedFolder.Children[index], actualFolder.Children[index]);
        }
    }

    private static void AssertJsonDictionaryEquivalent(
        IReadOnlyDictionary<string, JsonElement> expected,
        IReadOnlyDictionary<string, JsonElement> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(key => key), actual.Keys.OrderBy(key => key));
        foreach (var key in expected.Keys)
        {
            AssertJsonEquivalent(expected[key], actual[key]);
        }
    }

    private static void AssertJsonEquivalent(JsonElement? expected, JsonElement? actual)
    {
        Assert.Equal(expected.HasValue, actual.HasValue);
        if (!expected.HasValue)
        {
            return;
        }

        Assert.True(JsonElement.DeepEquals(expected.Value, actual!.Value));
    }
}
