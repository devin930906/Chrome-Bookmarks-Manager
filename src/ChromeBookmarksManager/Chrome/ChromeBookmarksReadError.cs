namespace ChromeBookmarksManager.Chrome;

public enum ChromeBookmarksReadError
{
    FileNotFound,
    AccessDenied,
    IoFailure,
    MalformedJson,
    UnsupportedVersion,
    MissingProperty,
    InvalidPropertyType,
    InvalidNodeType,
    InvalidId,
    DuplicateId,
    InvalidGuid,
    DuplicateGuid,
    DepthLimitExceeded
}
