namespace ChromeBookmarksManager.Application.Launching;

public interface IExternalUrlLauncher
{
    Task LaunchAsync(
        string url,
        CancellationToken cancellationToken = default);
}
