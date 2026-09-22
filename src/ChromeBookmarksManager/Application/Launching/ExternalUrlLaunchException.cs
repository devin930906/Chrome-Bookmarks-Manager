namespace ChromeBookmarksManager.Application.Launching;

public sealed class ExternalUrlLaunchException : Exception
{
    public ExternalUrlLaunchException(
        ExternalUrlLaunchError error,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public ExternalUrlLaunchError Error { get; }
}
