using System.Diagnostics;
using ChromeBookmarksManager.Application.Launching;
using ChromeBookmarksManager.Infrastructure.Processes;

namespace ChromeBookmarksManager.Tests.Opening;

public sealed class WindowsExternalUrlLauncherTests
{
    [Theory]
    [InlineData("https://example.com/path?q=1")]
    [InlineData("http://example.com/")]
    public async Task LaunchAsync_HttpAndHttpsUseShellHandler(
        string url)
    {
        var started = new List<ProcessStartInfo>();
        var launcher = new WindowsExternalUrlLauncher(
            startInfo => started.Add(startInfo));

        await launcher.LaunchAsync(url);

        var info = Assert.Single(started);
        Assert.True(info.UseShellExecute);
        Assert.Equal(url, info.FileName);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:test@example.com")]
    [InlineData("custom-protocol://example")]
    public async Task LaunchAsync_UnsupportedSchemeRejectsBeforeShellExecution(
        string url)
    {
        var shellCalls = 0;
        var launcher = new WindowsExternalUrlLauncher(
            _ => shellCalls++);

        var exception =
            await Assert.ThrowsAsync<ExternalUrlLaunchException>(
                () => launcher.LaunchAsync(url));

        Assert.Equal(
            ExternalUrlLaunchError.UnsupportedScheme,
            exception.Error);
        Assert.Equal(0, shellCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    public async Task LaunchAsync_InvalidUrlRejectsBeforeShellExecution(
        string url)
    {
        var shellCalls = 0;
        var launcher = new WindowsExternalUrlLauncher(
            _ => shellCalls++);

        var exception =
            await Assert.ThrowsAsync<ExternalUrlLaunchException>(
                () => launcher.LaunchAsync(url));

        Assert.Equal(
            ExternalUrlLaunchError.InvalidUrl,
            exception.Error);
        Assert.Equal(0, shellCalls);
    }
}
