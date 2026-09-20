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
    "File.WriteAllLines(",
    "File.AppendAllText(",
    "File.AppendAllLines(",
    "File.Replace(",
    "File.Move(",
    "File.Copy(",
    "File.Delete(",
    "File.Create(",
    "File.OpenWrite(",
    "new StreamWriter(",
    "new BinaryWriter(",
    "FileMode.Create",
    "FileMode.CreateNew",
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
            $violations.Add("$relative contains forbidden V0.7 file mutation primitive: $pattern")
        }
    }
}

if ($violations.Count -gt 0) {
    foreach ($violation in $violations) {
        Write-Error $violation
    }

    exit 1
}

Write-Host "V0.7 delete safety contract verified: production source has no file-write or file-delete primitives."
