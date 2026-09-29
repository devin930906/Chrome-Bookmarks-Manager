using System.IO;

namespace ChromeBookmarksManager.Application.Saving;

internal static class ChromeBookmarksSourceSafetyPolicy
{
    private static readonly string[] KnownChromeUserDataRelativePaths =
    [
        Path.Combine("Google", "Chrome", "User Data"),
        Path.Combine("Google", "Chrome Beta", "User Data"),
        Path.Combine("Google", "Chrome Dev", "User Data"),
        Path.Combine("Google", "Chrome SxS", "User Data")
    ];

    public static bool RequiresChromeClosed(string? sourcePath) =>
        RequiresChromeClosed(
            sourcePath,
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData));

    internal static bool RequiresChromeClosed(
        string? sourcePath,
        string? localApplicationData)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) ||
            string.IsNullOrWhiteSpace(localApplicationData))
        {
            return false;
        }

        string normalizedSource;
        string normalizedLocalApplicationData;

        try
        {
            normalizedSource = Path.GetFullPath(sourcePath);
            normalizedLocalApplicationData =
                Path.GetFullPath(localApplicationData);
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return false;
        }

        if (!string.Equals(
                Path.GetFileName(normalizedSource),
                "Bookmarks",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var relativeUserDataPath in
                 KnownChromeUserDataRelativePaths)
        {
            var userDataRoot = Path.GetFullPath(
                Path.Combine(
                    normalizedLocalApplicationData,
                    relativeUserDataPath));

            var relativePath =
                Path.GetRelativePath(userDataRoot, normalizedSource);

            if (Path.IsPathRooted(relativePath) ||
                relativePath.Equals(
                    "..",
                    StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith(
                    $"..{Path.AltDirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var segments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 2 &&
                string.Equals(
                    segments[1],
                    "Bookmarks",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
