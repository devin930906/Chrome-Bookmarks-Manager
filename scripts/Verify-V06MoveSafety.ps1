param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$sourceRoot = Join-Path $ProjectRoot "src/ChromeBookmarksManager"

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Production source directory was not found: $sourceRoot"
}

# V0.6 originally used a repository-wide "no write-back primitives anywhere"
# rule because the application had no persistence feature yet. V0.9 introduces
# an intentionally isolated Chrome writer/persistence layer, so this gate now
# protects the V0.6 ownership boundary: moving and UI/view-model code must still
# never perform direct filesystem/serialization write-back itself.
$protectedLocations = @(
    (Join-Path $sourceRoot "Application/Moving"),
    (Join-Path $sourceRoot "ViewModels"),
    (Join-Path $sourceRoot "MainWindow.xaml"),
    (Join-Path $sourceRoot "MainWindow.xaml.cs")
)

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
$productionFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()

foreach ($location in $protectedLocations) {
    if (-not (Test-Path -LiteralPath $location)) {
        continue
    }

    $item = Get-Item -LiteralPath $location
    if ($item.PSIsContainer) {
        Get-ChildItem -LiteralPath $location -Recurse -File |
            Where-Object { $_.Extension -in @(".cs", ".xaml") } |
            ForEach-Object { $productionFiles.Add($_) }
    }
    elseif ($item.Extension -in @(".cs", ".xaml")) {
        $productionFiles.Add($item)
    }
}

foreach ($file in $productionFiles | Sort-Object FullName -Unique) {
    $content = Get-Content -LiteralPath $file.FullName -Raw

    foreach ($pattern in $forbiddenPatterns) {
        if ($content.Contains($pattern, [System.StringComparison]::Ordinal)) {
            $relative = [System.IO.Path]::GetRelativePath(
                $ProjectRoot,
                $file.FullName)
            $violations.Add(
                "$relative contains forbidden V0.6 direct write primitive: $pattern")
        }
    }
}

if ($violations.Count -gt 0) {
    foreach ($violation in $violations) {
        Write-Error $violation
    }

    exit 1
}

Write-Host "V0.6 move safety contract verified: move/UI layers contain no direct write-back primitives."
