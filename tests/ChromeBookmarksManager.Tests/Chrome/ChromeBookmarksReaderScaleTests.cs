using System.Diagnostics;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Tests.Fixtures;
using Xunit.Abstractions;

namespace ChromeBookmarksManager.Tests.Chrome;

public sealed class ChromeBookmarksReaderScaleTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "ReaderScale")]
    public async Task Reader_LoadsGeneratedDatasetWithExactCounts()
    {
        var urlCount = int.TryParse(
            Environment.GetEnvironmentVariable("CBM_READER_URL_COUNT"),
            out var configured)
            ? configured
            : 10_000;
        var path = Path.Combine(
            Path.GetTempPath(),
            $"cbm-reader-{Guid.NewGuid():N}.json");

        try
        {
            await SyntheticBookmarksWriter.WriteAsync(
                path,
                urlCount,
                CancellationToken.None);
            var before = Process.GetCurrentProcess().WorkingSet64;
            var stopwatch = Stopwatch.StartNew();

            var document = await new ChromeBookmarksReader().ReadFileAsync(path);

            stopwatch.Stop();
            var after = Process.GetCurrentProcess().WorkingSet64;

            Assert.Equal(urlCount, document.UrlCount);
            Assert.Equal(3, document.FolderCount);
            output.WriteLine(
                $"URLs={urlCount:N0}; Elapsed={stopwatch.Elapsed}; " +
                $"WorkingSetDelta={after - before:N0} bytes; " +
                $"File={new FileInfo(path).Length:N0} bytes");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Category", "ReaderScale")]
    public async Task Reader_CancellationAfterMultipleReads_DoesNotReturnDocument()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"cbm-reader-cancel-{Guid.NewGuid():N}.json");

        try
        {
            await SyntheticBookmarksWriter.WriteAsync(
                path,
                10_000,
                CancellationToken.None);

            await using var file = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var cancellation = new CancellationTokenSource();
            await using var throttled = new CancelAfterReadsStream(
                file,
                cancellation,
                cancelOnRead: 4,
                maxBytesPerRead: 1024);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new ChromeBookmarksReader().ReadAsync(
                    throttled,
                    cancellation.Token));

            Assert.True(throttled.ReadCount >= 4);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class CancelAfterReadsStream(
        Stream inner,
        CancellationTokenSource cancellation,
        int cancelOnRead,
        int maxBytesPerRead) : Stream
    {
        public int ReadCount { get; private set; }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCount++;
            CancelIfNeeded();
            return inner.Read(
                buffer,
                offset,
                Math.Min(count, maxBytesPerRead));
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            CancelIfNeeded();

            var count = Math.Min(buffer.Length, maxBytesPerRead);
            return await inner
                .ReadAsync(buffer[..count], cancellationToken)
                .ConfigureAwait(false);
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            ReadCount++;
            CancelIfNeeded();

            return inner.ReadAsync(
                buffer,
                offset,
                Math.Min(count, maxBytesPerRead),
                cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            inner.Seek(offset, origin);

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        private void CancelIfNeeded()
        {
            if (ReadCount >= cancelOnRead)
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            }
        }
    }
}
