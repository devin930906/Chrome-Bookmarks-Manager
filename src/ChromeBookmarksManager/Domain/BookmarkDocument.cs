using System.Text.Json;

namespace ChromeBookmarksManager.Domain;

public sealed class BookmarkDocument
{
    public BookmarkDocument(int version, string? checksum, string? checksumSha256, BookmarkRoots roots, IReadOnlyDictionary<string, JsonElement> extensionData)
    {
        Version = version;
        Checksum = checksum;
        ChecksumSha256 = checksumSha256;
        Roots = roots;
        ExtensionData = extensionData;

        var folders = 0;
        var urls = 0;
        var stack = new Stack<BookmarkNode>(new BookmarkNode[] { roots.Synced, roots.Other, roots.BookmarkBar });
        while (stack.TryPop(out var node))
        {
            if (node is BookmarkFolder folder)
            {
                folders++;
                for (var index = folder.Children.Count - 1; index >= 0; index--)
                {
                    stack.Push(folder.Children[index]);
                }
            }
            else
            {
                urls++;
            }
        }

        FolderCount = folders;
        UrlCount = urls;
        TotalNodeCount = folders + urls;
    }

    public int Version { get; }
    public string? Checksum { get; }
    public string? ChecksumSha256 { get; }
    public BookmarkRoots Roots { get; }
    public IReadOnlyDictionary<string, JsonElement> ExtensionData { get; }
    public int FolderCount { get; }
    public int UrlCount { get; }
    public int TotalNodeCount { get; }
}
