using System.Globalization;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Sorting;

public sealed class BookmarkSortService
{
    public BookmarkSortResult SortByName(
        BookmarkDocument document,
        BookmarkFolder folder,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(culture);

        EnsureBelongsToDocument(document, folder);

        var original = folder.Children.ToArray();
        var comparer = StringComparer.Create(
            culture,
            ignoreCase: false);

        var sorted = original
            .OrderBy(node => node is BookmarkFolder ? 0 : 1)
            .ThenBy(node => node.Name, comparer)
            .ToArray();

        var changed = !ReferenceSequenceEqual(
            original,
            sorted);

        if (!changed)
        {
            return new BookmarkSortResult(
                false,
                folder,
                original,
                original);
        }

        ApplyOrder(folder, sorted);

        return new BookmarkSortResult(
            true,
            folder,
            original,
            folder.Children.ToArray());
    }

    internal static void ApplyOrder(
        BookmarkFolder folder,
        IReadOnlyList<BookmarkNode> order)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(order);

        if (order.Count != folder.Children.Count)
        {
            throw new InvalidOperationException(
                "The requested sort order does not contain the same number of children.");
        }

        var current = folder.Children.ToArray();
        var expected = new HashSet<BookmarkNode>(
            current,
            ReferenceEqualityComparer.Instance);

        if (order.Any(node => node is null) ||
            order.Distinct(ReferenceEqualityComparer.Instance).Count() != order.Count ||
            order.Any(node => !expected.Contains(node)))
        {
            throw new InvalidOperationException(
                "The requested sort order must contain each existing child exactly once.");
        }

        for (var index = current.Length - 1; index >= 0; index--)
        {
            folder.RemoveChildAt(index);
        }

        try
        {
            for (var index = 0; index < order.Count; index++)
            {
                folder.InsertChild(index, order[index]);
            }
        }
        catch
        {
            for (var index = folder.Children.Count - 1; index >= 0; index--)
            {
                folder.RemoveChildAt(index);
            }

            for (var index = 0; index < current.Length; index++)
            {
                folder.InsertChild(index, current[index]);
            }

            throw;
        }
    }

    private static void EnsureBelongsToDocument(
        BookmarkDocument document,
        BookmarkNode node)
    {
        BookmarkNode current = node;

        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        if (!ReferenceEquals(current, document.Roots.BookmarkBar) &&
            !ReferenceEquals(current, document.Roots.Other) &&
            !ReferenceEquals(current, document.Roots.Synced))
        {
            throw new InvalidOperationException(
                "The folder does not belong to the active bookmark document.");
        }
    }

    private static bool ReferenceSequenceEqual(
        IReadOnlyList<BookmarkNode> left,
        IReadOnlyList<BookmarkNode> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!ReferenceEquals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }
}
