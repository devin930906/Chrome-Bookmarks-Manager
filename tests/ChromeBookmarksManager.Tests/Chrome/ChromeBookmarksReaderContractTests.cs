using ChromeBookmarksManager.Chrome;

namespace ChromeBookmarksManager.Tests.Chrome;

public sealed class ChromeBookmarksReaderContractTests
{
    [Fact]
    public async Task ReadFileAsync_MissingFile_ReturnsTypedFailure()
    {
        var reader = new ChromeBookmarksReader();
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}", "Bookmarks");

        var error = await Assert.ThrowsAsync<ChromeBookmarksReadException>(() => reader.ReadFileAsync(path));

        Assert.Equal(ChromeBookmarksReadError.FileNotFound, error.Error);
        Assert.Null(error.PartialDocument);
    }

    [Fact]
    public async Task ReadAsync_CanceledToken_DoesNotReturnPartialDocument()
    {
        var reader = new ChromeBookmarksReader();
        await using var stream = new MemoryStream("{}"u8.ToArray());
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(stream, source.Token));
    }
}
