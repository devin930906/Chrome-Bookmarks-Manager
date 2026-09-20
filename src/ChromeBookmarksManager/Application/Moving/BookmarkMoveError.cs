namespace ChromeBookmarksManager.Application.Moving;

public enum BookmarkMoveError
{
    ProtectedRoot,
    NodeNotInDocument,
    TargetNotInDocument,
    MissingParent,
    SelfTarget,
    DescendantTarget,
    InvalidIndex,
    InvalidDropTarget
}
