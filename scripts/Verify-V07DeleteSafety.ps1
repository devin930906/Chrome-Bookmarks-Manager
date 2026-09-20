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

$productionFiles = Get-ChildItem -LiteralPath $sourceRoot -Recurse -File |
    Where-Object { $_.Extension -in @(".cs", ".xaml") }

foreach ($file in $productionFiles) {
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

Write-Host "V0.7 delete safety contract verified: production source has no file-write or file-delete primitives beyond the explicit read-only Chrome input stream."
