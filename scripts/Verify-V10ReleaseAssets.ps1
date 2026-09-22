param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDirectory
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ReleaseDirectory -PathType Container)) {
    throw "Release directory not found: $ReleaseDirectory"
}

$exe = Join-Path $ReleaseDirectory "ChromeBookmarksManager.exe"
$checksum = Join-Path $ReleaseDirectory "ChromeBookmarksManager.exe.sha256"
$files = @(Get-ChildItem -LiteralPath $ReleaseDirectory -File)

if ($files.Count -ne 2) {
    throw "Release directory must contain exactly two files."
}

$names = @($files.Name | Sort-Object)
$expected = @(
    "ChromeBookmarksManager.exe",
    "ChromeBookmarksManager.exe.sha256"
)

if (Compare-Object $names $expected) {
    throw "Release directory contains an unexpected asset set."
}

if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "Release executable is missing."
}

if (-not (Test-Path -LiteralPath $checksum -PathType Leaf)) {
    throw "Release checksum file is missing."
}

$line = (Get-Content -LiteralPath $checksum -Raw -Encoding ascii).Trim()
if ($line -notmatch '^([0-9a-f]{64})  ChromeBookmarksManager\.exe$') {
    throw "Checksum file format is invalid."
}

$expectedHash = $Matches[1]
$actualHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()

if ($actualHash -cne $expectedHash) {
    throw "Release EXE SHA256 does not match the checksum file."
}

Write-Host "V1.0 release asset contract verified."
