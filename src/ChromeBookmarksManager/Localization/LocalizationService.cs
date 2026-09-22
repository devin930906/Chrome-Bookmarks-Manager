using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using ChromeBookmarksManager.Infrastructure;

namespace ChromeBookmarksManager.Localization;

public static partial class LocalizationService
{
    public const string SimplifiedChinese = "zh-CN";
    public const string English = "en-US";

    private const string DictionaryPrefix =
        "Localization/Strings.";

    private static readonly IReadOnlyDictionary<string, string>
        EnglishFallback = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["ApplicationTitle"] = "Chrome Bookmarks Manager {0}",
            ["StatusNoFileOpen"] = "No Bookmarks file is open.",
            ["StatusReading"] = "Reading Bookmarks...",
            ["StatusVerifyingSource"] = "Verifying Bookmarks source...",
            ["StatusBuildingSearch"] = "Building search index...",
            ["StatusLoaded"] = "Loaded {0:N0} URLs and {1:N0} folders in {2:F1}s.",
            ["StatusReplacementCanceled"] =
                "Replacement loading was canceled. The current document was kept unchanged.",
            ["StatusLoadingCanceled"] = "Loading was canceled.",
            ["StatusCurrentDocumentKept"] =
                " The current document was kept unchanged.",
            ["StatusReplacementSearchFailed"] =
                "Replacement loading failed while preparing search. The current document was kept unchanged.",
            ["StatusSearchPreparationFailed"] =
                "Loading failed while preparing search.",
            ["StatusSaving"] = "Saving Bookmarks safely...",
            ["StatusSavedVerified"] =
                "Saved and verified. Verified safety backup: {0}",
            ["StatusVerifiedRecoveryBackup"] =
                " Verified recovery backup: {0}",
            ["StatusReloadBeforeSave"] =
                " Reload or recover the Bookmarks source before saving again.",
            ["StatusSaveCanceled"] =
                "Saving was canceled before replacement.",
            ["StatusSaveUnexpected"] =
                "Saving failed unexpectedly. The document remains unsaved and retryable.",
            ["StatusNoUnsavedChanges"] =
                "No unsaved in-memory changes. Source file has not been modified.",
            ["StatusUnsavedChanges"] =
                "Unsaved in-memory changes. Changes are not saved to disk.",
            ["SearchRefreshingIndex"] = "Refreshing search index...",
            ["SearchRefreshing"] = "Refreshing search...",
            ["SearchSearching"] = "Searching...",
            ["SearchNoMatches"] = "No matches",
            ["SearchMatches"] = "{0:N0} matches",
            ["SearchFailed"] = "Search failed.",
            ["DocumentSummary"] = "{0:N0} URLs | {1:N0} folders",
            ["SelectionSummary"] = "{0} | {1} | {2}",
            ["CountFolderOne"] = "{0:N0} folder",
            ["CountFolderMany"] = "{0:N0} folders",
            ["CountBookmarkOne"] = "{0:N0} bookmark",
            ["CountBookmarkMany"] = "{0:N0} bookmarks",
            ["HistoryAddBookmark"] = "Add bookmark",
            ["HistoryAddFolder"] = "Add folder",
            ["HistoryDeleteFolder"] = "Delete folder",
            ["HistoryDeleteBookmark"] = "Delete bookmark",
            ["HistoryImportHtml"] = "Import bookmarks HTML",
            ["HistoryMoveFolder"] = "Move folder",
            ["HistoryMoveBookmark"] = "Move bookmark",
            ["HistoryRenameFolder"] = "Rename folder",
            ["HistoryRenameBookmark"] = "Rename bookmark",
            ["HistorySortByName"] = "Sort by name",
            ["HistoryEditUrl"] = "Edit bookmark URL",
            ["HistoryDeleteBookmarks"] = "Delete {0:N0} bookmarks",
            ["HistoryMoveBookmarks"] = "Move {0:N0} bookmarks",
            ["HistoryMoveItems"] = "Move {0:N0} items",
            ["HistoryPasteItemsOne"] = "Paste {0:N0} item",
            ["HistoryPasteItemsMany"] = "Paste {0:N0} items"
        };

    private static string _currentLanguage = English;

    public static event EventHandler? LanguageChanged;

    public static string CurrentLanguage => _currentLanguage;

    public static void Initialize(string? languageCode)
    {
        var normalized = UserSettingsStore.NormalizeLanguage(languageCode);
        _currentLanguage = normalized;
        ApplyLanguageResources(normalized);
    }

    public static void SetLanguage(string? languageCode)
    {
        var normalized = UserSettingsStore.NormalizeLanguage(languageCode);

        if (string.Equals(
                _currentLanguage,
                normalized,
                StringComparison.Ordinal))
        {
            ApplyLanguageResources(normalized);
            return;
        }

        _currentLanguage = normalized;
        ApplyLanguageResources(normalized);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string GetString(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (System.Windows.Application.Current?.TryFindResource(key)
            is string localized)
        {
            return localized;
        }

        return EnglishFallback.TryGetValue(key, out var fallback)
            ? fallback
            : key;
    }

    public static string Format(
        string key,
        params object?[] arguments) =>
        string.Format(
            CultureInfo.CurrentCulture,
            GetString(key),
            arguments);

    public static string? LocalizeHistoryDescription(
        string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        var key = description switch
        {
            "Add bookmark" => "HistoryAddBookmark",
            "Add folder" => "HistoryAddFolder",
            "Delete folder" => "HistoryDeleteFolder",
            "Delete bookmark" => "HistoryDeleteBookmark",
            "Import bookmarks HTML" => "HistoryImportHtml",
            "Move folder" => "HistoryMoveFolder",
            "Move bookmark" => "HistoryMoveBookmark",
            "Rename folder" => "HistoryRenameFolder",
            "Rename bookmark" => "HistoryRenameBookmark",
            "Sort by name" => "HistorySortByName",
            "Edit bookmark URL" => "HistoryEditUrl",
            _ => null
        };

        if (key is not null)
        {
            return GetString(key);
        }

        var match = HistoryCountPattern().Match(description);
        if (!match.Success ||
            !int.TryParse(
                match.Groups["count"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var count))
        {
            return description;
        }

        return match.Groups["verb"].Value switch
        {
            "Delete" when match.Groups["noun"].Value == "bookmarks" =>
                Format("HistoryDeleteBookmarks", count),
            "Move" when match.Groups["noun"].Value == "bookmarks" =>
                Format("HistoryMoveBookmarks", count),
            "Move" when match.Groups["noun"].Value == "items" =>
                Format("HistoryMoveItems", count),
            "Paste" =>
                Format(
                    count == 1
                        ? "HistoryPasteItemsOne"
                        : "HistoryPasteItemsMany",
                    count),
            _ => description
        };
    }

    private static void ApplyLanguageResources(string languageCode)
    {
        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        var existing = resources.MergedDictionaries
            .Where(dictionary =>
                dictionary.Source?.OriginalString.Contains(
                    DictionaryPrefix,
                    StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        foreach (var dictionary in existing)
        {
            resources.MergedDictionaries.Remove(dictionary);
        }

        resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/ChromeBookmarksManager;component/" +
                    $"{DictionaryPrefix}{languageCode}.xaml",
                    UriKind.Absolute)
            });
    }

    [GeneratedRegex(
        @"^(?<verb>Delete|Move|Paste) (?<count>\d+) (?<noun>bookmarks|items?)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex HistoryCountPattern();
}
