param(
    [ValidateRange(1, 1000000)]
    [int]$UrlCount = 250000
)

$ErrorActionPreference = "Stop"
$previous = $env:CBM_READER_URL_COUNT

try {
    $env:CBM_READER_URL_COUNT = $UrlCount.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "FullyQualifiedName~ChromeBookmarksReaderScaleTests.Reader_LoadsGeneratedDatasetWithExactCounts" --logger "console;verbosity=detailed"
    if ($LASTEXITCODE -ne 0) {
        throw "Reader scale verification failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:CBM_READER_URL_COUNT = $previous
}
