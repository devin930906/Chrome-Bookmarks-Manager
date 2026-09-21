namespace ChromeBookmarksManager.Infrastructure.Processes;

public sealed class ChromeProcessDetectionException : InvalidOperationException
{
    public ChromeProcessDetectionException(
        ChromeProcessDetectionError error,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public ChromeProcessDetectionError Error { get; }
}
