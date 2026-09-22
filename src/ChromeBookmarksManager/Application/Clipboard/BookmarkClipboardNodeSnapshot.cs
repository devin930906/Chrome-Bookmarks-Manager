using System.Text.Json;
using ChromeBookmarksManager.Domain;

namespace ChromeBookmarksManager.Application.Clipboard;

public sealed record BookmarkClipboardNodeSnapshot(
    BookmarkNodeKind Kind,
    string Name,
    string? Url,
    string? DateAddedRaw,
    string? DateModifiedRaw,
    string? DateLastUsedRaw,
    JsonElement? MetaInfo,
    IReadOnlyDictionary<string, JsonElement> ExtensionData,
    IReadOnlyList<BookmarkClipboardNodeSnapshot> Children);
