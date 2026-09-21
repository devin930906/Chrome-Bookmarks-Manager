param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$sourceRoot = Join-Path $ProjectRoot "src/ChromeBookmarksManager"

if (-not (Test-Path -LiteralPath $sourceRoot)) {
    throw "Production source directory was not found: $sourceRoot"
}

$forbiddenTextPatterns = @(
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

$forbiddenRegexPatterns = @(
    '(?i)\b(?:System\.IO\.)?(?:File|FileInfo|Directory|DirectoryInfo)\s*\.\s*(?:WriteAllText|WriteAllBytes|WriteAllLines|AppendAllText|AppendAllLines|Replace|Move|Copy|Delete|Create|CreateText|AppendText|Open|OpenWrite|OpenHandle|MoveTo|CopyTo|CreateDirectory|SetAttributes|SetCreationTime|SetLastAccessTime|SetLastWriteTime|SetUnixFileMode|Encrypt|Decrypt)\s*\(',
    '(?im)^\s*using\s+(?:(?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*)?)(?:static\s+)?(?:(?:global::)?System\.IO\.)?(?:File|FileInfo|FileStream|Directory|DirectoryInfo|StreamWriter|BinaryWriter)\s*;',
    '(?i)\b(?:new\s+)?(?:global::)?(?:System\.IO\.)?(?:StreamWriter|BinaryWriter)\s*\(',
    '(?i)\.\s*(?:Write|WriteAsync|WriteByte|BeginWrite|EndWrite|CopyTo|CopyToAsync|SetLength|Flush|FlushAsync)\s*\('
)

$fileStreamCallPattern = '(?is)\b(?:new\s+)?(?:(?:global::)?System\.IO\.)?FileStream\s*\((?<Arguments>[^)]*)\)'

$violations = [System.Collections.Generic.List[string]]::new()

# V0.7 predates persistence and originally enforced a temporary global
# "production code cannot write files" rule. V0.9 intentionally adds a
# dedicated persistence layer. Keep the V0.7 contract strict where delete
# behavior is owned: deleting/batch operations and the UI/view-model boundary
# still may not perform direct file mutation.
$protectedLocations = @(
    (Join-Path $sourceRoot "Application/Deleting"),
    (Join-Path $sourceRoot "ViewModels"),
    (Join-Path $sourceRoot "MainWindow.xaml"),
    (Join-Path $sourceRoot "MainWindow.xaml.cs")
)

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

    foreach ($pattern in $forbiddenTextPatterns) {
        if ($content.Contains($pattern, [System.StringComparison]::Ordinal)) {
            $relative = [System.IO.Path]::GetRelativePath(
                $ProjectRoot,
                $file.FullName)
            $violations.Add("$relative contains forbidden V0.7 file mutation primitive: $pattern")
        }
    }

    foreach ($pattern in $forbiddenRegexPatterns) {
        if ($content -match $pattern) {
            $relative = [System.IO.Path]::GetRelativePath(
                $ProjectRoot,
                $file.FullName)
            $violations.Add("$relative matches forbidden V0.7 file mutation pattern: $pattern")
        }
    }

    $fileStreamCalls = [regex]::Matches(
        $content,
        $fileStreamCallPattern)
    $fileStreamReferenceCount = [regex]::Matches(
        $content,
        '(?i)\bFileStream\b').Count

    if ($fileStreamReferenceCount -gt $fileStreamCalls.Count) {
        $relative = [System.IO.Path]::GetRelativePath(
            $ProjectRoot,
            $file.FullName)
        $violations.Add(
            "$relative uses FileStream outside a checked constructor; use the explicit read-only FileMode.Open/FileAccess.Read form or remove it.")
    }

    foreach ($call in $fileStreamCalls) {
        $arguments = $call.Groups["Arguments"].Value
        $isExplicitReadOnlyOpen =
            $arguments -match '(?i)\bFileMode\s*\.\s*Open\b' -and
            $arguments -match '(?i)\bFileAccess\s*\.\s*Read\b' -and
            $arguments -notmatch '(?i)\bFileAccess\s*\.\s*(?:Write|ReadWrite)\b' -and
            $arguments -notmatch '(?i)\bFileMode\s*\.\s*(?:Append|Create|CreateNew|OpenOrCreate|Truncate)\b'

        if (-not $isExplicitReadOnlyOpen) {
            $relative = [System.IO.Path]::GetRelativePath(
                $ProjectRoot,
                $file.FullName)
            $violations.Add(
                "$relative constructs FileStream without explicit FileMode.Open and FileAccess.Read.")
        }
    }
}

if ($violations.Count -gt 0) {
    foreach ($violation in $violations) {
        Write-Error $violation
    }

    exit 1
}

Write-Host "V0.7 delete safety contract verified: delete/UI layers contain no direct file-write or file-delete primitives."
