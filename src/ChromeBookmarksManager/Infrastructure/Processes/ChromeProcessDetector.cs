using System.Diagnostics;

namespace ChromeBookmarksManager.Infrastructure.Processes;

public sealed class ChromeProcessDetector : IChromeProcessDetector
{
    private const string ChromeProcessName = "chrome";
    private readonly Func<string, int> _processCountProvider;

    public ChromeProcessDetector()
        : this(CountProcessesByName)
    {
    }

    public ChromeProcessDetector(
        Func<string, int> processCountProvider)
    {
        _processCountProvider = processCountProvider
            ?? throw new ArgumentNullException(
                nameof(processCountProvider));
    }

    public bool IsChromeRunning()
    {
        try
        {
            return _processCountProvider(ChromeProcessName) > 0;
        }
        catch (ChromeProcessDetectionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ChromeProcessDetectionException(
                ChromeProcessDetectionError.EnumerationFailed,
                "Chrome process state could not be checked safely.",
                exception);
        }
    }

    private static int CountProcessesByName(string processName)
    {
        var processes = Process.GetProcessesByName(processName);

        try
        {
            return processes.Length;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
