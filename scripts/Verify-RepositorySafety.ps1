$ErrorActionPreference = "Stop"

$trackedFiles = @(git ls-files)

if ($LASTEXITCODE -ne 0) {
    throw "git ls-files failed."
}

$forbidden = foreach ($path in $trackedFiles) {
    $normalized = $path -replace "\\", "/"
    $leaf = [System.IO.Path]::GetFileName($normalized)
    $isApprovedFixture = $normalized -eq "samples/Bookmarks.sample.json"

    $looksLikeProductionBookmark =
        $leaf -eq "Bookmarks" -or
        $leaf -eq "Bookmarks.bak" -or
        $leaf -like "Bookmarks.backup-*" -or
        $leaf -eq "Bookmarks.tmp" -or
        ($leaf -like "Bookmarks.*" -and -not $isApprovedFixture)

    $isPrivatePath =
        $normalized -match '(^|/)(UserData|PrivateData)(/|$)' -or
        $normalized -match '(^|/)TestData/Private(/|$)'

    if ($looksLikeProductionBookmark -or $isPrivatePath) {
        $path
    }
}

if ($forbidden.Count -gt 0) {
    $separator = [Environment]::NewLine
    Write-Error ("Forbidden private bookmark paths are tracked:" + $separator + ($forbidden -join $separator))
    exit 1
}

Write-Host "Repository safety check passed."
