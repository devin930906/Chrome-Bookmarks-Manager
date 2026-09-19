using System.Text.Json;

namespace ChromeBookmarksManager.Domain;

public sealed record BookmarkRoots(BookmarkFolder BookmarkBar, BookmarkFolder Other, BookmarkFolder Synced, IReadOnlyDictionary<string, JsonElement> ExtensionData);
