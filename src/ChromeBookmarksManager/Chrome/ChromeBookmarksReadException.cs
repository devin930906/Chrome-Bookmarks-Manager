using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Chrome;

public sealed class ChromeBookmarksReadException : Exception
{
    public ChromeBookmarksReadException(ChromeBookmarksReadError error, string message, string? jsonPath = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
        JsonPath = jsonPath;
    }

    public ChromeBookmarksReadError Error { get; }
    public string? JsonPath { get; }
    public BookmarkDocument? PartialDocument => null;
}
