using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using ChromeBookmarksManager.Chrome;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Importing;

public enum BookmarkHtmlImportError
{
    MalformedHtml,
    DepthLimitExceeded,
    NodeLimitExceeded,
    InputTooLarge,
    InvalidTarget
}

public sealed class BookmarkHtmlImportException : Exception
{
    public BookmarkHtmlImportException(
        BookmarkHtmlImportError error,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public BookmarkHtmlImportError Error { get; }
}

public sealed record BookmarkHtmlImportResult(
    bool Changed,
    BookmarkFolder ImportedFolder,
    BookmarkFolder TargetParent,
    int TargetIndex,
    int AddedUrlCount,
    int AddedFolderCount);

public sealed class BookmarkHtmlImportService
{
    public const int DefaultMaxNodes = 1_000_000;
    public const int DefaultMaxDepth = 4_096;
    public const int DefaultMaxCharacters = 64 * 1024 * 1024;
    public const string ImportedFolderName = "Imported bookmarks";

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyExtensionData =
            new Dictionary<string, JsonElement>(
                StringComparer.Ordinal);

    private readonly int _maxNodes;
    private readonly int _maxDepth;
    private readonly int _maxCharacters;
    private readonly TimeProvider _timeProvider;

    public BookmarkHtmlImportService(
        int maxNodes = DefaultMaxNodes,
        int maxDepth = DefaultMaxDepth,
        int maxCharacters = DefaultMaxCharacters,
        TimeProvider? timeProvider = null)
    {
        if (maxNodes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxNodes));
        }

        if (maxDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDepth));
        }

        if (maxCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCharacters));
        }

        _maxNodes = maxNodes;
        _maxDepth = maxDepth;
        _maxCharacters = maxCharacters;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BookmarkHtmlImportResult> ImportAsync(
        BookmarkDocument document,
        BookmarkFolder targetParent,
        TextReader reader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(targetParent);
        ArgumentNullException.ThrowIfNull(reader);

        EnsureBelongsToDocument(
            document,
            targetParent);

        var html = await ReadBoundedAsync(
                reader,
                cancellationToken)
            .ConfigureAwait(false);
        var parsed = Parse(
            html,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var nowRaw = ChromeBookmarkTime.ToRaw(
            _timeProvider.GetUtcNow());
        var importedChildren = MaterializeNodes(
            document,
            parsed.Roots,
            nowRaw,
            cancellationToken);
        var importedFolder = new BookmarkFolder(
            document.AllocateNextNodeId(),
            document.AllocateUniqueGuid(),
            ImportedFolderName,
            nowRaw,
            nowRaw,
            "0",
            null,
            EmptyExtensionData,
            importedChildren);

        var addedFolderCount = checked(
            parsed.FolderCount + 1);
        var targetIndex = targetParent.Children.Count;

        targetParent.AddChild(importedFolder);
        try
        {
            document.RecordRestoredSubtree(
                parsed.UrlCount,
                addedFolderCount);
        }
        catch
        {
            var currentIndex =
                targetParent.IndexOfChild(importedFolder);

            if (currentIndex >= 0)
            {
                targetParent.RemoveChildAt(currentIndex);
            }

            throw;
        }

        return new BookmarkHtmlImportResult(
            true,
            importedFolder,
            targetParent,
            targetIndex,
            parsed.UrlCount,
            addedFolderCount);
    }

    private async Task<string> ReadBoundedAsync(
        TextReader reader,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder(
            Math.Min(_maxCharacters, 64 * 1024));
        var buffer = new char[8 * 1024];

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read = await reader
                    .ReadAsync(
                        buffer.AsMemory(),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                if (builder.Length >
                    _maxCharacters - read)
                {
                    throw new BookmarkHtmlImportException(
                        BookmarkHtmlImportError.InputTooLarge,
                        $"Bookmark HTML import supports at most {_maxCharacters:N0} characters.");
                }

                builder.Append(
                    buffer,
                    0,
                    read);
            }
        }
        catch (DecoderFallbackException exception)
        {
            throw new BookmarkHtmlImportException(
                BookmarkHtmlImportError.MalformedHtml,
                "Bookmark HTML contains invalid text encoding.",
                exception);
        }

        return builder.ToString();
    }

    private ParseResult Parse(
        string html,
        CancellationToken cancellationToken)
    {
        var roots = new List<ParsedNode>();
        var containers =
            new Stack<List<ParsedNode>>();
        ParsedNode? pendingFolder = null;
        var rootOpened = false;
        var rootClosed = false;
        var nodeCount = 0;
        var urlCount = 0;
        var folderCount = 0;
        var index = 0;

        while (index < html.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tagStart = html.IndexOf(
                '<',
                index);

            if (tagStart < 0)
            {
                break;
            }

            if (StartsWithAt(
                html,
                tagStart,
                "<!--"))
            {
                var commentEnd = html.IndexOf(
                    "-->",
                    tagStart + 4,
                    StringComparison.Ordinal);

                if (commentEnd < 0)
                {
                    throw Malformed(
                        "Bookmark HTML contains an unterminated comment.");
                }

                index = commentEnd + 3;
                continue;
            }

            var tagEnd = html.IndexOf(
                '>',
                tagStart + 1);

            if (tagEnd < 0)
            {
                throw Malformed(
                    "Bookmark HTML contains an unterminated tag.");
            }

            var tag = html
                .Substring(
                    tagStart + 1,
                    tagEnd - tagStart - 1)
                .Trim();

            if (tag.Length == 0)
            {
                throw Malformed(
                    "Bookmark HTML contains an empty tag.");
            }

            var closing = tag[0] == '/';
            var name = ReadTagName(
                closing
                    ? tag.AsSpan(1)
                    : tag.AsSpan());

            if (name.Equals(
                "DL",
                StringComparison.OrdinalIgnoreCase))
            {
                if (closing)
                {
                    if (pendingFolder is not null ||
                        containers.Count == 0)
                    {
                        throw Malformed(
                            "Bookmark HTML contains an invalid folder-list close.");
                    }

                    containers.Pop();
                    if (containers.Count == 0)
                    {
                        rootClosed = true;
                    }

                    index = tagEnd + 1;
                    continue;
                }

                if (!rootOpened)
                {
                    rootOpened = true;
                    rootClosed = false;
                    containers.Push(roots);
                    index = tagEnd + 1;
                    continue;
                }

                if (rootClosed ||
                    pendingFolder is null)
                {
                    throw Malformed(
                        "Bookmark HTML contains a folder list without a folder.");
                }

                var folderDepth = containers.Count;
                if (folderDepth > _maxDepth)
                {
                    throw new BookmarkHtmlImportException(
                        BookmarkHtmlImportError.DepthLimitExceeded,
                        $"Bookmark HTML import supports a maximum folder depth of {_maxDepth:N0}.");
                }

                containers.Push(
                    pendingFolder.Children);
                pendingFolder = null;
                index = tagEnd + 1;
                continue;
            }

            if (!closing &&
                name.Equals(
                    "H3",
                    StringComparison.OrdinalIgnoreCase))
            {
                EnsureInsideRoot(
                    containers,
                    rootClosed);

                if (pendingFolder is not null)
                {
                    throw Malformed(
                        "Bookmark HTML contains a folder without its child list.");
                }

                var (text, nextIndex) =
                    ReadElementText(
                        html,
                        tagEnd + 1,
                        "H3");

                IncrementNodeCount(
                    ref nodeCount);
                folderCount = checked(
                    folderCount + 1);

                var folder = ParsedNode.Folder(
                    DecodeText(text));
                containers.Peek().Add(folder);
                pendingFolder = folder;
                index = nextIndex;
                continue;
            }

            if (!closing &&
                name.Equals(
                    "A",
                    StringComparison.OrdinalIgnoreCase))
            {
                EnsureInsideRoot(
                    containers,
                    rootClosed);

                if (pendingFolder is not null)
                {
                    throw Malformed(
                        "Bookmark HTML contains a folder without its child list.");
                }

                var href = ReadAttribute(
                    tag,
                    "HREF");

                if (string.IsNullOrWhiteSpace(href))
                {
                    throw Malformed(
                        "Bookmark HTML contains a bookmark without a valid HREF.");
                }

                var (text, nextIndex) =
                    ReadElementText(
                        html,
                        tagEnd + 1,
                        "A");

                IncrementNodeCount(
                    ref nodeCount);
                urlCount = checked(
                    urlCount + 1);

                containers.Peek().Add(
                    ParsedNode.Bookmark(
                        DecodeText(text),
                        WebUtility.HtmlDecode(href)));
                index = nextIndex;
                continue;
            }

            index = tagEnd + 1;
        }

        if (!rootOpened ||
            !rootClosed ||
            containers.Count != 0 ||
            pendingFolder is not null)
        {
            throw Malformed(
                "Bookmark HTML does not contain a complete Netscape bookmark list.");
        }

        return new ParseResult(
            roots,
            urlCount,
            folderCount);
    }

    private IReadOnlyList<BookmarkNode> MaterializeNodes(
        BookmarkDocument document,
        IReadOnlyList<ParsedNode> roots,
        string nowRaw,
        CancellationToken cancellationToken)
    {
        var created =
            new Dictionary<ParsedNode, BookmarkNode>(
                ReferenceEqualityComparer.Instance);
        var stack =
            new Stack<(ParsedNode Node, bool ChildrenReady)>();

        for (var index = roots.Count - 1;
             index >= 0;
             index--)
        {
            stack.Push(
                (roots[index], false));
        }

        while (stack.TryPop(out var frame))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (frame.Node.Kind ==
                    BookmarkNodeKind.Folder &&
                !frame.ChildrenReady)
            {
                stack.Push(
                    (frame.Node, true));

                for (var index =
                         frame.Node.Children.Count - 1;
                     index >= 0;
                     index--)
                {
                    stack.Push(
                        (frame.Node.Children[index], false));
                }

                continue;
            }

            BookmarkNode createdNode =
                frame.Node.Kind switch
                {
                    BookmarkNodeKind.Url =>
                        new BookmarkUrl(
                            document.AllocateNextNodeId(),
                            document.AllocateUniqueGuid(),
                            BookmarkNode.SanitizeTitleForChromium(
                                frame.Node.Name),
                            frame.Node.Url
                                ?? throw Malformed(
                                    "Bookmark HTML contains a URL node without an HREF."),
                            nowRaw,
                            null,
                            "0",
                            null,
                            EmptyExtensionData),

                    BookmarkNodeKind.Folder =>
                        new BookmarkFolder(
                            document.AllocateNextNodeId(),
                            document.AllocateUniqueGuid(),
                            BookmarkNode.SanitizeTitleForChromium(
                                frame.Node.Name),
                            nowRaw,
                            nowRaw,
                            "0",
                            null,
                            EmptyExtensionData,
                            frame.Node.Children
                                .Select(
                                    child => created[child])
                                .ToArray()),

                    _ => throw Malformed(
                        "Bookmark HTML contains an unsupported node type.")
                };

            created.Add(
                frame.Node,
                createdNode);
        }

        return roots
            .Select(root => created[root])
            .ToArray();
    }

    private void IncrementNodeCount(
        ref int nodeCount)
    {
        nodeCount = checked(nodeCount + 1);

        if (nodeCount > _maxNodes)
        {
            throw new BookmarkHtmlImportException(
                BookmarkHtmlImportError.NodeLimitExceeded,
                $"Bookmark HTML import supports at most {_maxNodes:N0} nodes.");
        }
    }

    private static void EnsureInsideRoot(
        Stack<List<ParsedNode>> containers,
        bool rootClosed)
    {
        if (containers.Count == 0 ||
            rootClosed)
        {
            throw Malformed(
                "Bookmark HTML contains bookmark nodes outside the root list.");
        }
    }

    private static (
        string Text,
        int NextIndex)
        ReadElementText(
            string html,
            int contentStart,
            string elementName)
    {
        var closeToken =
            $"</{elementName}";
        var closeStart = html.IndexOf(
            closeToken,
            contentStart,
            StringComparison.OrdinalIgnoreCase);

        if (closeStart < 0)
        {
            throw Malformed(
                $"Bookmark HTML contains an unterminated {elementName} element.");
        }

        var closeEnd = html.IndexOf(
            '>',
            closeStart + closeToken.Length);

        if (closeEnd < 0)
        {
            throw Malformed(
                $"Bookmark HTML contains an unterminated {elementName} close tag.");
        }

        var rawText = html.Substring(
            contentStart,
            closeStart - contentStart);

        if (rawText.Contains(
            '<',
            StringComparison.Ordinal))
        {
            throw Malformed(
                $"Bookmark HTML contains unsupported nested markup inside {elementName}.");
        }

        return (
            rawText,
            closeEnd + 1);
    }

    private static string ReadTagName(
        ReadOnlySpan<char> tag)
    {
        var end = 0;

        while (end < tag.Length &&
               !char.IsWhiteSpace(tag[end]) &&
               tag[end] != '/')
        {
            end++;
        }

        return tag[..end].ToString();
    }

    private static string? ReadAttribute(
        string tag,
        string requestedName)
    {
        var index = 0;

        while (index < tag.Length &&
               !char.IsWhiteSpace(tag[index]))
        {
            index++;
        }

        while (index < tag.Length)
        {
            while (index < tag.Length &&
                   char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            if (index >= tag.Length ||
                tag[index] == '/')
            {
                break;
            }

            var nameStart = index;
            while (index < tag.Length &&
                   !char.IsWhiteSpace(tag[index]) &&
                   tag[index] != '=' &&
                   tag[index] != '/')
            {
                index++;
            }

            var attributeName = tag.Substring(
                nameStart,
                index - nameStart);

            while (index < tag.Length &&
                   char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            string? value = null;
            if (index < tag.Length &&
                tag[index] == '=')
            {
                index++;

                while (index < tag.Length &&
                       char.IsWhiteSpace(tag[index]))
                {
                    index++;
                }

                if (index >= tag.Length)
                {
                    value = string.Empty;
                }
                else if (tag[index] is '"' or '\'')
                {
                    var quote = tag[index++];
                    var valueStart = index;
                    var valueEnd = tag.IndexOf(
                        quote,
                        valueStart);

                    if (valueEnd < 0)
                    {
                        throw Malformed(
                            "Bookmark HTML contains an unterminated attribute value.");
                    }

                    value = tag.Substring(
                        valueStart,
                        valueEnd - valueStart);
                    index = valueEnd + 1;
                }
                else
                {
                    var valueStart = index;
                    while (index < tag.Length &&
                           !char.IsWhiteSpace(tag[index]) &&
                           tag[index] != '/')
                    {
                        index++;
                    }

                    value = tag.Substring(
                        valueStart,
                        index - valueStart);
                }
            }

            if (attributeName.Equals(
                requestedName,
                StringComparison.OrdinalIgnoreCase))
            {
                return value is null
                    ? null
                    : WebUtility.HtmlDecode(value);
            }
        }

        return null;
    }

    private static string DecodeText(
        string value) =>
        WebUtility.HtmlDecode(value);

    private static bool StartsWithAt(
        string value,
        int index,
        string token) =>
        index >= 0 &&
        index + token.Length <= value.Length &&
        value.AsSpan(
                index,
                token.Length)
            .Equals(
                token.AsSpan(),
                StringComparison.Ordinal);

    private static BookmarkHtmlImportException Malformed(
        string message) =>
        new(
            BookmarkHtmlImportError.MalformedHtml,
            message);

    private static void EnsureBelongsToDocument(
        BookmarkDocument document,
        BookmarkNode node)
    {
        BookmarkNode current = node;

        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        if (!ReferenceEquals(
                current,
                document.Roots.BookmarkBar) &&
            !ReferenceEquals(
                current,
                document.Roots.Other) &&
            !ReferenceEquals(
                current,
                document.Roots.Synced))
        {
            throw new BookmarkHtmlImportException(
                BookmarkHtmlImportError.InvalidTarget,
                "The import destination does not belong to the active bookmark document.");
        }
    }

    private sealed class ParsedNode
    {
        private ParsedNode(
            BookmarkNodeKind kind,
            string name,
            string? url)
        {
            Kind = kind;
            Name = name;
            Url = url;
        }

        public BookmarkNodeKind Kind { get; }
        public string Name { get; }
        public string? Url { get; }
        public List<ParsedNode> Children { get; } = new();

        public static ParsedNode Folder(
            string name) =>
            new(
                BookmarkNodeKind.Folder,
                name,
                null);

        public static ParsedNode Bookmark(
            string name,
            string url) =>
            new(
                BookmarkNodeKind.Url,
                name,
                url);
    }

    private sealed record ParseResult(
        IReadOnlyList<ParsedNode> Roots,
        int UrlCount,
        int FolderCount);
}
