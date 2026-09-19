using System.ComponentModel;
using System.Text.Json;
using ChromeBookmarksManager.Domain;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests.Browser;

public sealed class FolderTreeItemViewModelTests
{
    [Fact]
    public void Constructor_ReferencesOriginalFolderAndExposesName()
    {
        var folder = Folder(
            "1",
            "11111111-1111-4111-8111-111111111111",
            "Bookmarks bar");

        var item = new FolderTreeItemViewModel(folder);

        Assert.Same(folder, item.Folder);
        Assert.Equal("Bookmarks bar", item.Name);
        Assert.False(item.IsExpanded);
        Assert.False(item.IsSelected);
    }

    [Fact]
    public void Children_ContainOnlyFoldersInOriginalDomainOrder()
    {
        var firstFolder = Folder(
            "2",
            "22222222-2222-4222-8222-222222222222",
            "First folder");
        var secondFolder = Folder(
            "4",
            "44444444-4444-4444-8444-444444444444",
            "Second folder");
        var root = Folder(
            "1",
            "11111111-1111-4111-8111-111111111111",
            "Root",
            Url(
                "3",
                "33333333-3333-4333-8333-333333333333",
                "Before",
                "https://example.com/before"),
            firstFolder,
            Url(
                "5",
                "55555555-5555-4555-8555-555555555555",
                "Between",
                "https://example.com/between"),
            secondFolder);

        var item = new FolderTreeItemViewModel(root);

        Assert.Equal(2, item.Children.Count);
        Assert.Same(firstFolder, item.Children[0].Folder);
        Assert.Same(secondFolder, item.Children[1].Folder);
    }

    [Fact]
    public void Children_RepresentNestedFoldersWithoutUrlWrappers()
    {
        var leaf = Folder(
            "3",
            "33333333-3333-4333-8333-333333333333",
            "Leaf",
            Url(
                "4",
                "44444444-4444-4444-8444-444444444444",
                "Leaf URL",
                "https://example.com/leaf"));
        var child = Folder(
            "2",
            "22222222-2222-4222-8222-222222222222",
            "Child",
            leaf);
        var root = Folder(
            "1",
            "11111111-1111-4111-8111-111111111111",
            "Root",
            child);

        var item = new FolderTreeItemViewModel(root);

        var childItem = Assert.Single(item.Children);
        var leafItem = Assert.Single(childItem.Children);
        Assert.Same(child, childItem.Folder);
        Assert.Same(leaf, leafItem.Folder);
        Assert.Empty(leafItem.Children);
    }

    [Fact]
    public void IsExpanded_RaisesPropertyChangedOnlyWhenValueChanges()
    {
        var item = new FolderTreeItemViewModel(
            Folder(
                "1",
                "11111111-1111-4111-8111-111111111111",
                "Root"));
        var notifications = new List<string?>();
        item.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        item.IsExpanded = true;
        item.IsExpanded = true;
        item.IsExpanded = false;

        Assert.Equal(
            new[] { nameof(FolderTreeItemViewModel.IsExpanded), nameof(FolderTreeItemViewModel.IsExpanded) },
            notifications);
    }

    [Fact]
    public void IsSelected_RaisesPropertyChangedOnlyWhenValueChanges()
    {
        var item = new FolderTreeItemViewModel(
            Folder(
                "1",
                "11111111-1111-4111-8111-111111111111",
                "Root"));
        var notifications = new List<string?>();
        item.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        item.IsSelected = true;
        item.IsSelected = true;
        item.IsSelected = false;

        Assert.Equal(
            new[] { nameof(FolderTreeItemViewModel.IsSelected), nameof(FolderTreeItemViewModel.IsSelected) },
            notifications);
    }

    private static BookmarkFolder Folder(
        string id,
        string guid,
        string name,
        params BookmarkNode[] children) =>
        new(
            id,
            Guid.Parse(guid),
            name,
            null,
            null,
            null,
            null,
            EmptyProperties(),
            children);

    private static BookmarkUrl Url(
        string id,
        string guid,
        string name,
        string url) =>
        new(
            id,
            Guid.Parse(guid),
            name,
            url,
            null,
            null,
            null,
            null,
            EmptyProperties());

    private static IReadOnlyDictionary<string, JsonElement> EmptyProperties() =>
        new Dictionary<string, JsonElement>();
}
