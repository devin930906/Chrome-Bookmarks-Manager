namespace ChromeBookmarksManager.Application.Deleting;

public enum BookmarkDeleteError
{
    ProtectedRoot,
    NodeNotInDocument,
    MissingParent
}
