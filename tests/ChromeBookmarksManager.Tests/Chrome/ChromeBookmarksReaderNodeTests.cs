using System.Text;
using System.Text.Json;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Chrome;

public sealed class ChromeBookmarksReaderNodeTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Bookmarks.sample.json");

    [Fact]
    public async Task ReadAsync_SampleFixture_BuildsOrderedTreeAndCounts()
    {
        var document = await ReadFixtureAsync();

        Assert.Equal(3, document.FolderCount);
        Assert.Equal(2, document.UrlCount);
        Assert.Equal("Examples", document.Roots.BookmarkBar.Children[0].Name);
        var examples = Assert.IsType<BookmarkFolder>(document.Roots.BookmarkBar.Children[0]);
        var documentation = Assert.IsType<BookmarkUrl>(examples.Children[0]);
        Assert.Same(examples, documentation.Parent);
        Assert.Equal("https://www.example.com/docs", documentation.Url);
    }

    [Fact]
    public async Task ReadAsync_PreservesUnknownFieldsAndBothMetaInfoShapes()
    {
        const string json = """
        {"version":1,"roots":{"bookmark_bar":{"id":"1","guid":"11111111-1111-4111-8111-111111111111","name":"Bar","type":"folder","children":[{"id":"4","guid":"44444444-4444-4444-8444-444444444444","name":"Opaque","type":"url","url":"chrome://bookmarks/","meta_info":{"future":{"value":"x"}},"future_node":[1,2]}]},"other":{"id":"2","guid":"22222222-2222-4222-8222-222222222222","name":"Other","type":"folder","children":[{"id":"5","guid":"55555555-5555-4555-8555-555555555555","name":"Legacy","type":"url","url":"javascript:void(0)","meta_info":"{\"legacy\":\"yes\"}"}]},"synced":{"id":"3","guid":"33333333-3333-4333-8333-333333333333","name":"Mobile","type":"folder","children":[]},"future_root":{"shape":"retained"}}}
        """;

        var document = await ReadJsonAsync(json);
        var opaque = Assert.IsType<BookmarkUrl>(document.Roots.BookmarkBar.Children[0]);
        var legacy = Assert.IsType<BookmarkUrl>(document.Roots.Other.Children[0]);

        Assert.Equal(JsonValueKind.Object, opaque.MetaInfo?.ValueKind);
        Assert.Equal(JsonValueKind.Array, opaque.ExtensionData["future_node"].ValueKind);
        Assert.Equal(JsonValueKind.String, legacy.MetaInfo?.ValueKind);
        Assert.True(document.Roots.ExtensionData.ContainsKey("future_root"));
        Assert.Equal("javascript:void(0)", legacy.Url);
    }

    [Theory]
    [MemberData(nameof(InvalidNodeCases))]
    public async Task ReadAsync_InvalidNode_ReportsTypedErrorAndExactPath(
        string json,
        ChromeBookmarksReadError expectedError,
        string expectedPath)
    {
        var error = await Assert.ThrowsAsync<ChromeBookmarksReadException>(() => ReadJsonAsync(json));

        Assert.Equal(expectedError, error.Error);
        Assert.Equal(expectedPath, error.JsonPath);
        Assert.Null(error.PartialDocument);
    }

    public static IEnumerable<object[]> InvalidNodeCases()
    {
        yield return Case(
            Child("""{"id":"4","guid":"44444444-4444-4444-8444-444444444444","name":"Unknown","type":"future","url":"https://example.com/"}"""),
            ChromeBookmarksReadError.InvalidNodeType,
            "$.roots.bookmark_bar.children[0].type");

        yield return Case(
            Child("""{"id":"4","guid":"44444444-4444-4444-8444-444444444444","name":"Missing URL","type":"url"}"""),
            ChromeBookmarksReadError.MissingProperty,
            "$.roots.bookmark_bar.children[0].url");

        yield return Case(
            Child("""{"id":"4","guid":"44444444-4444-4444-8444-444444444444","name":"Missing Children","type":"folder"}"""),
            ChromeBookmarksReadError.MissingProperty,
            "$.roots.bookmark_bar.children[0].children");

        yield return Case(
            Children(
                """{"id":"4","guid":"44444444-4444-4444-8444-444444444444","name":"One","type":"url","url":"https://example.com/one"}""",
                """{"id":"4","guid":"55555555-5555-4555-8555-555555555555","name":"Two","type":"url","url":"https://example.com/two"}"""),
            ChromeBookmarksReadError.DuplicateId,
            "$.roots.bookmark_bar.children[1].id");

        yield return Case(
            Children(
                """{"id":"4","guid":"44444444-4444-4444-8444-444444444444","name":"One","type":"url","url":"https://example.com/one"}""",
                """{"id":"5","guid":"44444444-4444-4444-8444-444444444444","name":"Two","type":"url","url":"https://example.com/two"}"""),
            ChromeBookmarksReadError.DuplicateGuid,
            "$.roots.bookmark_bar.children[1].guid");

        yield return Case(
            Child("""{"id":"0","guid":"44444444-4444-4444-8444-444444444444","name":"Zero","type":"url","url":"https://example.com/zero"}"""),
            ChromeBookmarksReadError.InvalidId,
            "$.roots.bookmark_bar.children[0].id");

        yield return Case(
            Child("""{"id":"4","guid":"not-a-guid","name":"Bad GUID","type":"url","url":"https://example.com/bad"}"""),
            ChromeBookmarksReadError.InvalidGuid,
            "$.roots.bookmark_bar.children[0].guid");
    }

    private static object[] Case(string json, ChromeBookmarksReadError error, string path) =>
        new object[] { json, error, path };

    private static string Child(string child) => Children(child);

    private static string Children(params string[] children) =>
        "{\"version\":1,\"roots\":{" +
        "\"bookmark_bar\":{\"id\":\"1\",\"guid\":\"11111111-1111-4111-8111-111111111111\",\"name\":\"Bar\",\"type\":\"folder\",\"children\":[" +
        string.Join(",", children) +
        "]}," +
        "\"other\":{\"id\":\"2\",\"guid\":\"22222222-2222-4222-8222-222222222222\",\"name\":\"Other\",\"type\":\"folder\",\"children\":[]}," +
        "\"synced\":{\"id\":\"3\",\"guid\":\"33333333-3333-4333-8333-333333333333\",\"name\":\"Mobile\",\"type\":\"folder\",\"children\":[]}}}";

    private static Task<BookmarkDocument> ReadFixtureAsync() =>
        new ChromeBookmarksReader().ReadFileAsync(FixturePath);

    private static async Task<BookmarkDocument> ReadJsonAsync(string json)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await new ChromeBookmarksReader().ReadAsync(stream);
    }
}
