using System.Text.Json;
using ChromeBookmarksManager.Application.Clipboard;
using ChromeBookmarksManager.Application.Moving;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Tests.Clipboard;

public sealed class BookmarkClipboardServiceTests
{
    [Fact]
    public void CaptureCopy_PreservesMixedNestedSnapshotOrder()
    {
        var fixture = CreateFixture();
        var service = new BookmarkClipboardService();

        var payload = service.Capture(
            fixture.Document,
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.Project
            },
            BookmarkClipboardMode.Copy);

        Assert.Equal(BookmarkClipboardMode.Copy, payload.Mode);
        Assert.Collection(
            payload.Items,
            first =>
            {
                Assert.Equal(BookmarkNodeKind.Url, first.Kind);
                Assert.Equal("First", first.Name);
                Assert.Equal("https://example.com/first", first.Url);
                Assert.Empty(first.Children);
            },
            project =>
            {
                Assert.Equal(BookmarkNodeKind.Folder, project.Kind);
                Assert.Equal("Project", project.Name);
                Assert.Null(project.Url);
                Assert.Collection(
                    project.Children,
                    nested =>
                    {
                        Assert.Equal(BookmarkNodeKind.Url, nested.Kind);
                        Assert.Equal("Nested", nested.Name);
                        Assert.Equal(
                            "https://example.com/nested",
                            nested.Url);
                    },
                    deep =>
                    {
                        Assert.Equal(BookmarkNodeKind.Folder, deep.Kind);
                        Assert.Equal("Deep", deep.Name);
                        var deepUrl = Assert.Single(deep.Children);
                        Assert.Equal(BookmarkNodeKind.Url, deepUrl.Kind);
                        Assert.Equal("Deep URL", deepUrl.Name);
                        Assert.Equal(
                            "https://example.com/deep",
                            deepUrl.Url);
                    });
            });
    }

    [Fact]
    public void PasteCopy_ClonesSubtreesWithFreshIdsGuidsAndCounts()
    {
        var fixture = CreateFixture();
        var service = new BookmarkClipboardService();
        var beforeUrls = fixture.Document.UrlCount;
        var beforeFolders = fixture.Document.FolderCount;
        var sourceIds = Enumerate(
                new BookmarkNode[]
                {
                    fixture.BarFirst,
                    fixture.Project
                })
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var sourceGuids = Enumerate(
                new BookmarkNode[]
                {
                    fixture.BarFirst,
                    fixture.Project
                })
            .Select(node => node.Guid)
            .ToHashSet();

        var payload = service.Capture(
            fixture.Document,
            new BookmarkNode[]
            {
                fixture.BarFirst,
                fixture.Project
            },
            BookmarkClipboardMode.Copy);

        var result = service.Paste(
            fixture.Document,
            payload,
            fixture.Other,
            0);

        Assert.True(result.Changed);
        Assert.False(result.MovedOriginalNodes);
        Assert.Equal(3, result.AddedUrlCount);
        Assert.Equal(2, result.AddedFolderCount);
        Assert.Equal(beforeUrls + 3, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders + 2, fixture.Document.FolderCount);

        Assert.Same(fixture.BookmarkBar, fixture.BarFirst.Parent);
        Assert.Same(fixture.BookmarkBar, fixture.Project.Parent);

        Assert.Collection(
            fixture.Other.Children,
            firstClone =>
            {
                Assert.IsType<BookmarkUrl>(firstClone);
                Assert.Equal(fixture.BarFirst.Name, firstClone.Name);
                Assert.NotSame(fixture.BarFirst, firstClone);
            },
            projectClone =>
            {
                var folder = Assert.IsType<BookmarkFolder>(projectClone);
                Assert.Equal(fixture.Project.Name, folder.Name);
                Assert.NotSame(fixture.Project, folder);
                Assert.Collection(
                    folder.Children,
                    nested =>
                    {
                        var url = Assert.IsType<BookmarkUrl>(nested);
                        Assert.Equal("Nested", url.Name);
                    },
                    deep =>
                    {
                        var deepFolder = Assert.IsType<BookmarkFolder>(deep);
                        Assert.Equal("Deep", deepFolder.Name);
                        Assert.IsType<BookmarkUrl>(
                            Assert.Single(deepFolder.Children));
                    });
            });

        Assert.Equal(
            fixture.Other.Children,
            result.Nodes);

        var clones = Enumerate(result.Nodes).ToArray();
        Assert.Equal(
            clones.Length,
            clones.Select(node => node.Id).Distinct().Count());
        Assert.Equal(
            clones.Length,
            clones.Select(node => node.Guid).Distinct().Count());
        Assert.DoesNotContain(
            clones,
            node => sourceIds.Contains(node.Id));
        Assert.DoesNotContain(
            clones,
            node => sourceGuids.Contains(node.Guid));
    }

    [Fact]
    public void PasteCut_SameDocumentMovesOriginalNodesAndPreservesIdentity()
    {
        var fixture = CreateFixture();
        var service = new BookmarkClipboardService();
        var beforeUrls = fixture.Document.UrlCount;
        var beforeFolders = fixture.Document.FolderCount;

        var payload = service.Capture(
            fixture.Document,
            new BookmarkNode[]
            {
                fixture.Project,
                fixture.BarLast
            },
            BookmarkClipboardMode.Cut);

        var result = service.Paste(
            fixture.Document,
            payload,
            fixture.Other,
            0);

        Assert.True(result.Changed);
        Assert.True(result.MovedOriginalNodes);
        Assert.Equal(0, result.AddedUrlCount);
        Assert.Equal(0, result.AddedFolderCount);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders, fixture.Document.FolderCount);

        Assert.Equal(
            new BookmarkNode[] { fixture.BarFirst },
            fixture.BookmarkBar.Children);
        Assert.Equal(
            new BookmarkNode[]
            {
                fixture.Project,
                fixture.BarLast
            },
            fixture.Other.Children);
        Assert.Same(fixture.Project, result.Nodes[0]);
        Assert.Same(fixture.BarLast, result.Nodes[1]);
        Assert.Same(fixture.Other, fixture.Project.Parent);
        Assert.Same(fixture.Project, fixture.Nested.Parent);
        Assert.Same(fixture.Deep, fixture.DeepUrl.Parent);
    }

    [Fact]
    public void PasteCut_DescendantTargetFailsWithoutMutation()
    {
        var fixture = CreateFixture();
        var service = new BookmarkClipboardService();
        var beforeBar = fixture.BookmarkBar.Children.ToArray();
        var beforeProject = fixture.Project.Children.ToArray();
        var beforeDeep = fixture.Deep.Children.ToArray();
        var beforeUrls = fixture.Document.UrlCount;
        var beforeFolders = fixture.Document.FolderCount;

        var payload = service.Capture(
            fixture.Document,
            new BookmarkNode[] { fixture.Project },
            BookmarkClipboardMode.Cut);

        var exception = Assert.Throws<BookmarkMoveException>(
            () => service.Paste(
                fixture.Document,
                payload,
                fixture.Deep,
                fixture.Deep.Children.Count));

        Assert.Equal(
            BookmarkMoveError.DescendantTarget,
            exception.Error);
        Assert.Equal(beforeBar, fixture.BookmarkBar.Children);
        Assert.Equal(beforeProject, fixture.Project.Children);
        Assert.Equal(beforeDeep, fixture.Deep.Children);
        Assert.Equal(beforeUrls, fixture.Document.UrlCount);
        Assert.Equal(beforeFolders, fixture.Document.FolderCount);
        Assert.Same(fixture.BookmarkBar, fixture.Project.Parent);
        Assert.Same(fixture.Project, fixture.Deep.Parent);
        Assert.Same(fixture.Deep, fixture.DeepUrl.Parent);
    }

    private static IEnumerable<BookmarkNode> Enumerate(
        IEnumerable<BookmarkNode> roots)
    {
        var stack = new Stack<BookmarkNode>(roots.Reverse());

        while (stack.TryPop(out var node))
        {
            yield return node;

            if (node is not BookmarkFolder folder)
            {
                continue;
            }

            for (var index = folder.Children.Count - 1;
                 index >= 0;
                 index--)
            {
                stack.Push(folder.Children[index]);
            }
        }
    }

    private static Fixture CreateFixture()
    {
        var barFirst = Url(
            "10",
            "First",
            "https://example.com/first");
        var nested = Url(
            "12",
            "Nested",
            "https://example.com/nested");
        var deepUrl = Url(
            "14",
            "Deep URL",
            "https://example.com/deep");
        var deep = Folder(
            "13",
            "Deep",
            deepUrl);
        var project = Folder(
            "20",
            "Project",
            nested,
            deep);
        var barLast = Url(
            "11",
            "Last",
            "https://example.com/last");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            barFirst,
            project,
            barLast);
        var other = Folder(
            "2",
            "Other bookmarks");
        var synced = Folder(
            "3",
            "Mobile bookmarks");

        var document = new BookmarkDocument(
            1,
            null,
            null,
            new BookmarkRoots(
                bookmarkBar,
                other,
                synced,
                EmptyProperties),
            EmptyProperties);

        return new Fixture(
            document,
            bookmarkBar,
            other,
            project,
            deep,
            barFirst,
            barLast,
            nested,
            deepUrl);
    }

    private static BookmarkFolder Folder(
        string id,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties,
            children);

    private static BookmarkUrl Url(
        string id,
        string name,
        string url) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            url,
            null,
            null,
            null,
            null,
            null,
            EmptyProperties);

    private static Guid GuidFor(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        bytes[7] = 0x40;
        bytes[8] = 0x80;
        return new Guid(bytes);
    }

    private sealed record Fixture(
        BookmarkDocument Document,
        BookmarkFolder BookmarkBar,
        BookmarkFolder Other,
        BookmarkFolder Project,
        BookmarkFolder Deep,
        BookmarkUrl BarFirst,
        BookmarkUrl BarLast,
        BookmarkUrl Nested,
        BookmarkUrl DeepUrl);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
