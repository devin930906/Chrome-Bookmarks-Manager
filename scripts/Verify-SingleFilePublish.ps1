param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PublishDirectory -PathType Container)) {
    throw "Publish directory does not exist: $PublishDirectory"
}

$files = @(Get-ChildItem -Path $PublishDirectory -File)
$expectedName = "ChromeBookmarksManager.exe"

if ($files.Count -ne 1) {
    $found = if ($files.Count -eq 0) { "<none>" } else { $files.Name -join ", " }
    throw "Expected exactly one published file ($expectedName), but found: $found"
}

if ($files[0].Name -ne $expectedName) {
    throw "Expected $expectedName, but found $($files[0].Name)."
}

if ($files[0].Length -le 0) {
    throw "$expectedName exists but is empty."
}

Write-Host "Single-file publish verified: $($files[0].FullName) ($($files[0].Length) bytes)"
