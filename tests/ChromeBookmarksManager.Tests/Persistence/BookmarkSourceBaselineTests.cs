using System.Security.Cryptography;
using System.Text;
using ChromeBookmarksManager.Infrastructure.Persistence;

namespace ChromeBookmarksManager.Tests.Persistence;

public sealed class BookmarkSourceBaselineTests
{
    [Fact]
    public async Task CaptureAsync_ReturnsCanonicalSha256LengthAndTimestamp()
    {
        using var fixture = await TempFileFixture.CreateAsync("alpha");
        var service = new BookmarkSourceBaselineService();

        var baseline = await service.CaptureAsync(fixture.Path);

        Assert.Equal(Path.GetFullPath(fixture.Path), baseline.FullPath);
        Assert.Equal(5, baseline.Length);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("alpha")))
                .ToLowerInvariant(),
            baseline.Sha256);
        Assert.Equal(File.GetLastWriteTimeUtc(fixture.Path), baseline.LastWriteTimeUtc);
    }

    [Fact]
    public async Task VerifyUnchangedAsync_UnchangedFile_ReturnsCurrentBaseline()
    {
        using var fixture = await TempFileFixture.CreateAsync("stable");
        var service = new BookmarkSourceBaselineService();
        var baseline = await service.CaptureAsync(fixture.Path);

        var current = await service.VerifyUnchangedAsync(baseline);

        Assert.Equal(baseline, current);
    }

    [Fact]
    public async Task VerifyUnchangedAsync_SameLengthDifferentContent_ThrowsSourceChanged()
    {
        using var fixture = await TempFileFixture.CreateAsync("alpha");
        var service = new BookmarkSourceBaselineService();
        var baseline = await service.CaptureAsync(fixture.Path);

        await File.WriteAllTextAsync(fixture.Path, "bravo");
        File.SetLastWriteTimeUtc(fixture.Path, baseline.LastWriteTimeUtc);

        var error = await Assert.ThrowsAsync<BookmarkSourceBaselineException>(
            () => service.VerifyUnchangedAsync(baseline));

        Assert.Equal(BookmarkSourceBaselineError.SourceChanged, error.Error);
    }

    [Fact]
    public async Task VerifyUnchangedAsync_TimestampOnlyChange_IsConservativelyRejected()
    {
        using var fixture = await TempFileFixture.CreateAsync("same-content");
        var service = new BookmarkSourceBaselineService();
        var baseline = await service.CaptureAsync(fixture.Path);

        File.SetLastWriteTimeUtc(
            fixture.Path,
            baseline.LastWriteTimeUtc.AddMinutes(5));

        var error = await Assert.ThrowsAsync<BookmarkSourceBaselineException>(
            () => service.VerifyUnchangedAsync(baseline));

        Assert.Equal(BookmarkSourceBaselineError.SourceChanged, error.Error);
    }

    [Fact]
    public async Task VerifyUnchangedAsync_MissingSource_ReturnsTypedFailure()
    {
        using var fixture = await TempFileFixture.CreateAsync("gone");
        var service = new BookmarkSourceBaselineService();
        var baseline = await service.CaptureAsync(fixture.Path);

        File.Delete(fixture.Path);

        var error = await Assert.ThrowsAsync<BookmarkSourceBaselineException>(
            () => service.VerifyUnchangedAsync(baseline));

        Assert.Equal(BookmarkSourceBaselineError.FileNotFound, error.Error);
    }

    [Fact]
    public async Task VerifyUnchangedAsync_PathComparisonUsesWindowsCaseInsensitivity()
    {
        using var fixture = await TempFileFixture.CreateAsync("case");
        var service = new BookmarkSourceBaselineService();
        var baseline = await service.CaptureAsync(fixture.Path);
        var differentlyCased = baseline with
        {
            FullPath = baseline.FullPath.ToUpperInvariant()
        };

        var current = await service.VerifyUnchangedAsync(differentlyCased);

        Assert.Equal(baseline.Sha256, current.Sha256);
        Assert.Equal(baseline.Length, current.Length);
    }

    [Fact]
    public async Task CaptureAsync_PreCanceledToken_DoesNotReadFile()
    {
        using var fixture = await TempFileFixture.CreateAsync("cancel");
        var service = new BookmarkSourceBaselineService();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CaptureAsync(fixture.Path, cancellation.Token));
    }

    private sealed class TempFileFixture : IDisposable
    {
        private TempFileFixture(string directory, string path)
        {
            Directory = directory;
            Path = path;
        }

        private string Directory { get; }
        public string Path { get; }

        public static async Task<TempFileFixture> CreateAsync(string content)
        {
            var directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ChromeBookmarksManager-V09-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "Bookmarks");
            await File.WriteAllTextAsync(path, content);
            return new TempFileFixture(directory, path);
        }

        public void Dispose()
        {
            try
            {
                if (System.IO.Directory.Exists(Directory))
                {
                    System.IO.Directory.Delete(Directory, recursive: true);
                }
            }
            catch
            {
                // Test cleanup is best effort.
            }
        }
    }
}
