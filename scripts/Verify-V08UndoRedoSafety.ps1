param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "Verify-V07DeleteSafety.ps1") -ProjectRoot $ProjectRoot
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$historyRoot = Join-Path $ProjectRoot "src/ChromeBookmarksManager/Application/History"
if (-not (Test-Path -LiteralPath $historyRoot)) {
    throw "V0.8 history source directory was not found: $historyRoot"
}

$forbiddenPatterns = @(
    '(?i)\bSourcePath\b',
    '(?i)\bChromeBookmarksReader\b',
    '(?i)\bIChromeBookmarksReader\b',
    '(?i)\bJsonSerializer\b',
    '(?i)\bUtf8JsonWriter\b',
    '(?i)\bSystem\.IO\b',
    '(?i)\bFileStream\b',
    '(?i)\bStreamWriter\b',
    '(?i)\bBinaryWriter\b',
    '(?i)\bFile\s*\.',
    '(?i)\bDirectory\s*\.'
)

$violations = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -LiteralPath $historyRoot -Recurse -Filter *.cs -File | ForEach-Object {
    $content = Get-Content -LiteralPath $_.FullName -Raw
    foreach ($pattern in $forbiddenPatterns) {
        if ($content -match $pattern) {
            $relative = [System.IO.Path]::GetRelativePath(
                $ProjectRoot,
                $_.FullName)
            $violations.Add(
                "$relative violates the V0.8 in-memory history boundary: $pattern")
        }
    }
}

if ($violations.Count -gt 0) {
    foreach ($violation in $violations) {
        Write-Error $violation
    }
    exit 1
}

Write-Host "V0.8 Undo/Redo safety contract verified: history is graph-local and introduces no source-file I/O or serialization snapshot path."
