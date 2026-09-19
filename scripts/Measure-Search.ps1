param(
    [ValidateRange(1, 1000000)]
    [int]$UrlCount = 250000
)

$ErrorActionPreference = "Stop"
$previous = $env:CBM_SEARCH_URL_COUNT

try {
    $env:CBM_SEARCH_URL_COUNT = $UrlCount.ToString(
        [System.Globalization.CultureInfo]::InvariantCulture)

    dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~BookmarkSearchScaleTests.SearchIndexAndQueries_UseOriginalReferencesAtScale" --logger "console;verbosity=detailed"

    if ($LASTEXITCODE -ne 0) {
        throw "Search scale verification failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:CBM_SEARCH_URL_COUNT = $previous
}
