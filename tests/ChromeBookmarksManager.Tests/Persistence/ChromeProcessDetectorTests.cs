using ChromeBookmarksManager.Infrastructure.Processes;

namespace ChromeBookmarksManager.Tests.Persistence;

public sealed class ChromeProcessDetectorTests
{
    [Fact]
    public void IsChromeRunning_NoChromeProcesses_ReturnsFalse()
    {
        var detector = new ChromeProcessDetector(
            processCountProvider: name =>
            {
                Assert.Equal("chrome", name);
                return 0;
            });

        Assert.False(detector.IsChromeRunning());
    }

    [Fact]
    public void IsChromeRunning_AnyChromeProcess_ReturnsTrue()
    {
        var detector = new ChromeProcessDetector(
            processCountProvider: name =>
            {
                Assert.Equal("chrome", name);
                return 3;
            });

        Assert.True(detector.IsChromeRunning());
    }

    [Fact]
    public void IsChromeRunning_ProcessEnumerationFails_ThrowsTypedFailure()
    {
        var detector = new ChromeProcessDetector(
            processCountProvider: _ =>
                throw new InvalidOperationException("synthetic enumeration failure"));

        var error = Assert.Throws<ChromeProcessDetectionException>(
            detector.IsChromeRunning);

        Assert.Equal(
            ChromeProcessDetectionError.EnumerationFailed,
            error.Error);
        Assert.IsType<InvalidOperationException>(
            error.InnerException);
    }

    [Fact]
    public void Constructor_NullProvider_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ChromeProcessDetector(null!));
    }
}
