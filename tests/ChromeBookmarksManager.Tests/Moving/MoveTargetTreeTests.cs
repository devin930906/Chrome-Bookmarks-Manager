using System.Text.Json;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Moving;

public sealed class MoveTargetTreeTests
{
    [Fact]
    public void BuildRoots_ForBookmark_AllFoldersAreValidTargets()
    {
        var fixture = CreateFixture();

        var roots = MoveTargetTreeItemViewModel.BuildRoots(
            fixture.Document,
            fixture.Bookmark);

        Assert.Equal(3, roots.Count);
        Assert.All(Flatten(roots), item => Assert.True(item.IsValidTarget));
        Assert.Contains(
            Flatten(roots),
            item => ReferenceEquals(item.Folder, fixture.DeepFolder));
    }

    [Fact]
    public void BuildRoots_ForFolder_DisablesSelfAndEveryDescendant()
    {
        var fixture = CreateFixture();

        var roots = MoveTargetTreeItemViewModel.BuildRoots(
            fixture.Document,
            fixture.MovingFolder);
        var items = Flatten(roots).ToArray();

        Assert.False(Find(items, fixture.MovingFolder).IsValidTarget);
        Assert.False(Find(items, fixture.ChildFolder).IsValidTarget);
        Assert.False(Find(items, fixture.DeepFolder).IsValidTarget);

        Assert.True(Find(items, fixture.BookmarkBar).IsValidTarget);
        Assert.True(Find(items, fixture.Other).IsValidTarget);
        Assert.True(Find(items, fixture.Synced).IsValidTarget);
        Assert.True(Find(items, fixture.UnrelatedFolder).IsValidTarget);
    }

    [Fact]
    public void BuildRoots_PreservesDocumentOrderAndHierarchy()
    {
        var fixture = CreateFixture();

        var roots = MoveTargetTreeItemViewModel.BuildRoots(
            fixture.Document,
            fixture.Bookmark);

        Assert.Same(fixture.BookmarkBar, roots[0].Folder);
        Assert.Same(fixture.Other, roots[1].Folder);
        Assert.Same(fixture.Synced, roots[2].Folder);

        var moving = Assert.Single(
            roots[0].Children.Where(
                child => ReferenceEquals(child.Folder, fixture.MovingFolder)));
        var child = Assert.Single(moving.Children);
        var deep = Assert.Single(child.Children);

        Assert.Same(fixture.ChildFolder, child.Folder);
        Assert.Same(fixture.DeepFolder, deep.Folder);
    }

    [Fact]
    public void SelectionAndExpansion_StateRaisesProperties()
    {
        var fixture = CreateFixture();
        var root = MoveTargetTreeItemViewModel.BuildRoots(
            fixture.Document,
            fixture.Bookmark)[0];
        var changed = new List<string?>();
        root.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        root.IsExpanded = true;
        root.IsSelected = true;

        Assert.True(root.IsExpanded);
        Assert.True(root.IsSelected);
        Assert.Contains(nameof(root.IsExpanded), changed);
        Assert.Contains(nameof(root.IsSelected), changed);
    }

    private static MoveTargetTreeItemViewModel Find(
        IEnumerable<MoveTargetTreeItemViewModel> items,
        BookmarkFolder folder) =>
        Assert.Single(items.Where(item => ReferenceEquals(item.Folder, folder)));

    private static IEnumerable<MoveTargetTreeItemViewModel> Flatten(
        IEnumerable<MoveTargetTreeItemViewModel> roots)
    {
        var stack = new Stack<MoveTargetTreeItemViewModel>(
            roots.Reverse());

        while (stack.TryPop(out var item))
        {
            yield return item;

            for (var index = item.Children.Count - 1; index >= 0; index--)
            {
                stack.Push(item.Children[index]);
            }
        }
    }

    private static Fixture CreateFixture()
    {
        var bookmark = Url("10", "Bookmark");
        var deepFolder = Folder("23", "Deep");
        var childFolder = Folder("22", "Child", deepFolder);
        var movingFolder = Folder("20", "Moving", childFolder);
        var unrelatedFolder = Folder("21", "Unrelated");
        var bookmarkBar = Folder(
            "1",
            "Bookmarks bar",
            bookmark,
            movingFolder,
            unrelatedFolder);
        var other = Folder("2", "Other bookmarks");
        var synced = Folder("3", "Mobile bookmarks");

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
            synced,
            movingFolder,
            childFolder,
            deepFolder,
            unrelatedFolder,
            bookmark);
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

    private static BookmarkUrl Url(string id, string name) =>
        new(
            id,
            GuidFor(int.Parse(id)),
            name,
            $"https://example.com/{id}",
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
        BookmarkFolder Synced,
        BookmarkFolder MovingFolder,
        BookmarkFolder ChildFolder,
        BookmarkFolder DeepFolder,
        BookmarkFolder UnrelatedFolder,
        BookmarkUrl Bookmark);

    private static readonly IReadOnlyDictionary<string, JsonElement>
        EmptyProperties = new Dictionary<string, JsonElement>();
}
