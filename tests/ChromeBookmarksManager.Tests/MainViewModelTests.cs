using ChromeBookmarksManager.Application;
using ChromeBookmarksManager.ViewModels;

namespace ChromeBookmarksManager.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void Constructor_StartsWithoutDocument()
    {
        var viewModel = new MainViewModel();

        Assert.Equal(DocumentState.NoDocument, viewModel.State);
    }

    [Fact]
    public void Constructor_ExposesStableApplicationTitle()
    {
        var viewModel = new MainViewModel();

        Assert.Equal("Chrome Bookmarks Manager", viewModel.ApplicationTitle);
    }

    [Fact]
    public void Constructor_ExplainsThatNoFileIsOpen()
    {
        var viewModel = new MainViewModel();

        Assert.Equal("No Bookmarks file is open.", viewModel.StatusText);
    }
}
