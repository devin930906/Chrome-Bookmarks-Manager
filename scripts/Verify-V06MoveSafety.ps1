param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$sourceRoot = Join-Path $ProjectRoot "src/ChromeBookmarksManager"

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Production source directory was not found: $sourceRoot"
}

$forbiddenPatterns = @(
    "File.WriteAllText(",
    "File.WriteAllBytes(",
    "File.Replace(",
    "File.Move(",
    "File.Copy(",
    "File.Delete(",
    "File.Create(",
    "File.OpenWrite(",
    "new StreamWriter(",
    "FileMode.Create",
    "FileMode.Truncate",
    "FileAccess.Write",
    "JsonSerializer.Serialize(",
    "Utf8JsonWriter"
)

$violations = [System.Collections.Generic.List[string]]::new()

$productionFiles = Get-ChildItem -LiteralPath $sourceRoot -Recurse -File |
    Where-Object { $_.Extension -in @(".cs", ".xaml") }

foreach ($file in $productionFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw

    foreach ($pattern in $forbiddenPatterns) {
        if ($content.Contains($pattern, [System.StringComparison]::Ordinal)) {
            $relative = [System.IO.Path]::GetRelativePath(
                $ProjectRoot,
                $file.FullName)
            $violations.Add("$relative contains forbidden V0.6 write primitive: $pattern")
        }
    }
}

if ($violations.Count -gt 0) {
    foreach ($violation in $violations) {
        Write-Error $violation
    }

    exit 1
}

Write-Host "V0.6 move safety contract verified: no production write-back primitives found."
