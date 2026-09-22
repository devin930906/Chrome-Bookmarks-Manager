using System.IO;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Exporting;

public sealed class BookmarkHtmlExportService
{
    public const int MaxExportNodes = 1_000_000;
    public const int MaxExportDepth = 4_096;

    public async Task ExportAsync(
        BookmarkDocument document,
        TextWriter writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writer);

        ValidateBounds(document, cancellationToken);

        await WriteLineAsync(
            writer,
            "<!DOCTYPE NETSCAPE-Bookmark-file-1>",
            cancellationToken).ConfigureAwait(false);
        await WriteLineAsync(
            writer,
            "<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">",
            cancellationToken).ConfigureAwait(false);
        await WriteLineAsync(
            writer,
            "<TITLE>Bookmarks</TITLE>",
            cancellationToken).ConfigureAwait(false);
        await WriteLineAsync(
            writer,
            "<H1>Bookmarks</H1>",
            cancellationToken).ConfigureAwait(false);
        await WriteLineAsync(
            writer,
            "<DL><p>",
            cancellationToken).ConfigureAwait(false);

        var stack = new Stack<ExportFrame>();
        stack.Push(new ExportFrame(document.Roots.Synced, 1, CloseFolder: false));
        stack.Push(new ExportFrame(document.Roots.Other, 1, CloseFolder: false));
        stack.Push(new ExportFrame(document.Roots.BookmarkBar, 1, CloseFolder: false));

        while (stack.TryPop(out var frame))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (frame.CloseFolder)
            {
                await WriteLineAsync(
                    writer,
                    $"{Indent(frame.Depth)}</DL><p>",
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (frame.Node is BookmarkUrl bookmark)
            {
                await WriteLineAsync(
                    writer,
                    $"{Indent(frame.Depth)}<DT><A HREF=\"{EscapeHtml(bookmark.Url)}\">{EscapeHtml(bookmark.Name)}</A>",
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (frame.Node is not BookmarkFolder folder)
            {
                throw new InvalidOperationException(
                    "Unsupported bookmark node type encountered during HTML export.");
            }

            var toolbarAttribute =
                ReferenceEquals(folder, document.Roots.BookmarkBar)
                    ? " PERSONAL_TOOLBAR_FOLDER=\"true\""
                    : string.Empty;

            await WriteLineAsync(
                writer,
                $"{Indent(frame.Depth)}<DT><H3{toolbarAttribute}>{EscapeHtml(folder.Name)}</H3>",
                cancellationToken).ConfigureAwait(false);
            await WriteLineAsync(
                writer,
                $"{Indent(frame.Depth)}<DL><p>",
                cancellationToken).ConfigureAwait(false);

            stack.Push(
                new ExportFrame(
                    folder,
                    frame.Depth,
                    CloseFolder: true));

            for (var index = folder.Children.Count - 1; index >= 0; index--)
            {
                stack.Push(
                    new ExportFrame(
                        folder.Children[index],
                        frame.Depth + 1,
                        CloseFolder: false));
            }
        }

        await WriteLineAsync(
            writer,
            "</DL><p>",
            cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private static void ValidateBounds(
        BookmarkDocument document,
        CancellationToken cancellationToken)
    {
        if (document.TotalNodeCount > MaxExportNodes)
        {
            throw new InvalidOperationException(
                $"Bookmark HTML export supports at most {MaxExportNodes:N0} nodes.");
        }

        var seen = 0;
        var stack = new Stack<(BookmarkNode Node, int Depth)>();
        stack.Push((document.Roots.Synced, 1));
        stack.Push((document.Roots.Other, 1));
        stack.Push((document.Roots.BookmarkBar, 1));

        while (stack.TryPop(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();

            seen++;
            if (seen > MaxExportNodes)
            {
                throw new InvalidOperationException(
                    $"Bookmark HTML export supports at most {MaxExportNodes:N0} nodes.");
            }

            if (item.Depth > MaxExportDepth)
            {
                throw new InvalidOperationException(
                    $"Bookmark HTML export supports a maximum folder depth of {MaxExportDepth:N0}.");
            }

            if (item.Node is not BookmarkFolder folder)
            {
                continue;
            }

            for (var index = folder.Children.Count - 1; index >= 0; index--)
            {
                stack.Push(
                    (folder.Children[index], item.Depth + 1));
            }
        }
    }

    private static async Task WriteLineAsync(
        TextWriter writer,
        string value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await writer.WriteAsync(value).ConfigureAwait(false);
        await writer.WriteAsync("\n").ConfigureAwait(false);
    }

    private static string Indent(int depth) =>
        new(' ', checked(depth * 4));

    private static string EscapeHtml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private readonly record struct ExportFrame(
        BookmarkNode Node,
        int Depth,
        bool CloseFolder);
}
