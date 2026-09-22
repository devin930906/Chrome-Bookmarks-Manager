using System.Diagnostics;
using ChromeBookmarksManager.Application.Launching;

namespace ChromeBookmarksManager.Infrastructure.Processes;

public sealed class WindowsExternalUrlLauncher : IExternalUrlLauncher
{
    private readonly Action<ProcessStartInfo> _startProcess;

    public WindowsExternalUrlLauncher()
        : this(
            startInfo =>
            {
                _ = Process.Start(startInfo);
            })
    {
    }

    internal WindowsExternalUrlLauncher(
        Action<ProcessStartInfo> startProcess)
    {
        _startProcess = startProcess
            ?? throw new ArgumentNullException(nameof(startProcess));
    }

    public Task LaunchAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri))
        {
            throw new ExternalUrlLaunchException(
                ExternalUrlLaunchError.InvalidUrl,
                "The bookmark URL is not a valid absolute URL.");
        }

        if (!string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ExternalUrlLaunchException(
                ExternalUrlLaunchError.UnsupportedScheme,
                $"The bookmark URL scheme '{uri.Scheme}' is not supported. Only http and https URLs can be opened.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };

        try
        {
            _startProcess(startInfo);
        }
        catch (Exception exception)
        {
            throw new ExternalUrlLaunchException(
                ExternalUrlLaunchError.LaunchFailed,
                "Windows could not open the bookmark URL with the default handler.",
                exception);
        }

        return Task.CompletedTask;
    }
}
