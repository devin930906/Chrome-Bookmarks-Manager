using System.Text;
using ChromeBookmarksManager.Chrome;

namespace ChromeBookmarksManager.Tests.Chrome;

public sealed class ChromeBookmarksReaderStructureTests
{
    [Fact]
    public async Task ReadAsync_ValidVersionAndRoots_LoadsDocumentMetadata()
    {
        const string json = """
        {"version":1,"checksum":"md5-value","checksum_sha256":"sha-value","roots":{"bookmark_bar":{"id":"1","guid":"11111111-1111-4111-8111-111111111111","name":"Bar","type":"folder","children":[]},"other":{"id":"2","guid":"22222222-2222-4222-8222-222222222222","name":"Other","type":"folder","children":[]},"synced":{"id":"3","guid":"33333333-3333-4333-8333-333333333333","name":"Mobile","type":"folder","children":[]}},"future_document_field":{"enabled":true}}
        """;

        var document = await ReadJsonAsync(json);

        Assert.Equal(1, document.Version);
        Assert.Equal("md5-value", document.Checksum);
        Assert.Equal("sha-value", document.ChecksumSha256);
        Assert.True(document.ExtensionData.ContainsKey("future_document_field"));
    }

    [Theory]
    [InlineData("{}", ChromeBookmarksReadError.UnsupportedVersion)]
    [InlineData("{\"version\":2,\"roots\":{}}", ChromeBookmarksReadError.UnsupportedVersion)]
    [InlineData("{\"version\":1}", ChromeBookmarksReadError.MissingProperty)]
    [InlineData("{\"version\":1,\"roots\":{}}", ChromeBookmarksReadError.MissingProperty)]
    public async Task ReadAsync_InvalidTopLevel_FailsAtomically(string json, ChromeBookmarksReadError expected)
    {
        var error = await Assert.ThrowsAsync<ChromeBookmarksReadException>(() => ReadJsonAsync(json));
        Assert.Equal(expected, error.Error);
        Assert.Null(error.PartialDocument);
    }

    [Fact]
    public async Task ReadAsync_TruncatedJson_ReportsMalformedJson()
    {
        var error = await Assert.ThrowsAsync<ChromeBookmarksReadException>(() => ReadJsonAsync("{\"version\":1,"));
        Assert.Equal(ChromeBookmarksReadError.MalformedJson, error.Error);
    }

    private static async Task<ChromeBookmarksManager.Domain.BookmarkDocument> ReadJsonAsync(string json)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await new ChromeBookmarksReader().ReadAsync(stream);
    }
}
