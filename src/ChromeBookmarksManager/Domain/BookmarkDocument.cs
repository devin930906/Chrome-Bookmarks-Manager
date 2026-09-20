using System.Globalization;
using System.Text.Json;

namespace ChromeBookmarksManager.Domain;

public sealed class BookmarkDocument
{
    private readonly HashSet<Guid> _knownGuids = new();
    private long _maximumNodeId;

    public BookmarkDocument(int version, string? checksum, string? checksumSha256, BookmarkRoots roots, IReadOnlyDictionary<string, JsonElement> extensionData)
    {
        Version = version;
        Checksum = checksum;
        ChecksumSha256 = checksumSha256;
        Roots = roots;
        ExtensionData = extensionData;

        var folders = 0;
        var urls = 0;
        var maximumNodeId = 0L;
        var stack = new Stack<BookmarkNode>(new BookmarkNode[] { roots.Synced, roots.Other, roots.BookmarkBar });
        while (stack.TryPop(out var node))
        {
            _knownGuids.Add(node.Guid);

            if (long.TryParse(
                    node.Id,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var numericId) &&
                numericId > maximumNodeId)
            {
                maximumNodeId = numericId;
            }

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

        _maximumNodeId = maximumNodeId;
        FolderCount = folders;
        UrlCount = urls;
    }

    public int Version { get; }
    public string? Checksum { get; }
    public string? ChecksumSha256 { get; }
    public BookmarkRoots Roots { get; }
    public IReadOnlyDictionary<string, JsonElement> ExtensionData { get; }
    public int FolderCount { get; private set; }
    public int UrlCount { get; private set; }
    public int TotalNodeCount => checked(FolderCount + UrlCount);

    internal string AllocateNextNodeId()
    {
        if (_maximumNodeId == long.MaxValue)
        {
            throw new InvalidOperationException(
                "No further Chrome bookmark node IDs can be allocated.");
        }

        _maximumNodeId++;
        return _maximumNodeId.ToString(CultureInfo.InvariantCulture);
    }

    internal Guid AllocateUniqueGuid()
    {
        Guid guid;
        do
        {
            guid = Guid.NewGuid();
        }
        while (guid == Guid.Empty || !_knownGuids.Add(guid));

        return guid;
    }

    internal void RecordAddedNode(BookmarkNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node is BookmarkUrl)
        {
            UrlCount = checked(UrlCount + 1);
        }
        else if (node is BookmarkFolder)
        {
            FolderCount = checked(FolderCount + 1);
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(node),
                node.Kind,
                "Unsupported bookmark node kind.");
        }
    }
}
