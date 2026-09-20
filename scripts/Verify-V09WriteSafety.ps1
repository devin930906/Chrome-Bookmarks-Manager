param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$sourceRoot = Join-Path $ProjectRoot "src/ChromeBookmarksManager"
$writerPath = Join-Path $sourceRoot "Chrome/ChromeBookmarksWriter.cs"
$fileSystemPath = Join-Path $sourceRoot "Infrastructure/Persistence/BookmarkFileSystem.cs"
$transactionPath = Join-Path $sourceRoot "Infrastructure/Persistence/BookmarkFileTransaction.cs"
$baselinePath = Join-Path $sourceRoot "Infrastructure/Persistence/BookmarkSourceBaselineService.cs"
$saveServicePath = Join-Path $sourceRoot "Application/Saving/ChromeBookmarksSaveService.cs"
$viewModelPath = Join-Path $sourceRoot "ViewModels/MainViewModel.cs"
$mainWindowPath = Join-Path $sourceRoot "MainWindow.xaml.cs"

$required = @(
    $writerPath,
    $fileSystemPath,
    $transactionPath,
    $baselinePath,
    $saveServicePath,
    $viewModelPath,
    $mainWindowPath
)

foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required V0.9 persistence file was not found: $path"
    }
}

$errors = [System.Collections.Generic.List[string]]::new()

function Require-Text {
    param(
        [string]$Content,
        [string]$Pattern,
        [string]$Message
    )

    if (-not $Content.Contains($Pattern, [System.StringComparison]::Ordinal)) {
        $errors.Add($Message)
    }
}

function Forbid-Text {
    param(
        [string]$Content,
        [string]$Pattern,
        [string]$Message
    )

    if ($Content.Contains($Pattern, [System.StringComparison]::Ordinal)) {
        $errors.Add($Message)
    }
}

$writer = Get-Content -LiteralPath $writerPath -Raw
$fileSystem = Get-Content -LiteralPath $fileSystemPath -Raw
$transaction = Get-Content -LiteralPath $transactionPath -Raw
$baseline = Get-Content -LiteralPath $baselinePath -Raw
$saveService = Get-Content -LiteralPath $saveServicePath -Raw
$viewModel = Get-Content -LiteralPath $viewModelPath -Raw
$mainWindow = Get-Content -LiteralPath $mainWindowPath -Raw

# Serialization must remain stream-only and must not know the source path.
Require-Text $writer 'Utf8JsonWriter' "ChromeBookmarksWriter must serialize through Utf8JsonWriter."
Require-Text $writer 'ChromeBookmarksChecksum.Compute' "Writer must regenerate Chrome checksums."
Forbid-Text $writer 'File.' "ChromeBookmarksWriter must not perform direct filesystem operations."
Forbid-Text $writer 'FileStream' "ChromeBookmarksWriter must remain stream-based."

# The filesystem boundary is the only production layer allowed to call File.Replace.
Require-Text $fileSystem 'File.Replace(' "Persistence filesystem boundary must use File.Replace for atomic replacement."
Require-Text $fileSystem 'File.Copy(' "Persistence filesystem boundary must support backup copying."
Require-Text $fileSystem 'overwrite: false' "Safety backups must never overwrite an existing file."
Require-Text $fileSystem 'FileMode.CreateNew' "Temporary output must use create-new semantics."
Require-Text $fileSystem 'FileAccess.Write' "Temporary output stream must be explicitly write-only."
Forbid-Text $fileSystem 'File.Move(' "File.Move must not be used as the V0.9 replacement primitive."
Forbid-Text $fileSystem 'File.WriteAllText(' "Direct whole-source text writes are forbidden."
Forbid-Text $fileSystem 'File.WriteAllBytes(' "Direct whole-source byte writes are forbidden."

$allProductionCs = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter *.cs -File
foreach ($file in $allProductionCs) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    $relative = [System.IO.Path]::GetRelativePath($ProjectRoot, $file.FullName)

    if ($file.FullName -ne $fileSystemPath -and
        $content.Contains('File.Replace(', [System.StringComparison]::Ordinal)) {
        $errors.Add("$relative bypasses BookmarkFileSystem with direct File.Replace.")
    }

    if ($content.Contains('"Bookmarks.bak"', [System.StringComparison]::Ordinal) -or
        $content.Contains("'Bookmarks.bak'", [System.StringComparison]::Ordinal)) {
        $errors.Add("$relative must not use Chrome's Bookmarks.bak as the application backup.")
    }
}

# Transaction order/safety invariants.
Require-Text $transaction 'WriteTempAsync(' "Transaction must write a separate temporary file first."
Require-Text $transaction 'ReadValidatedDocumentAsync(' "Transaction must re-open generated/final files for validation."
Require-Text $transaction 'EnsureEquivalent(' "Transaction must verify logical equivalence before and after replacement."
Require-Text $transaction 'CreateUniqueBackupPath(' "Transaction must allocate a unique application-owned backup path."
Require-Text $transaction '.ChromeBookmarksManager.' "Application backup naming must be distinguishable from Chrome's own backup."
Require-Text $transaction '_fileSystem.Copy(sourcePath, backupPath)' "Source must be copied to a backup before replacement."
Require-Text $transaction 'backupBaseline.Length != expectedBaseline.Length' "Backup length must be verified."
Require-Text $transaction 'backupBaseline.Sha256' "Backup SHA-256 must be verified."
Require-Text $transaction 'VerifyExpectedSourceAsync(' "Source baseline must be rechecked during the transaction."
Require-Text $transaction 'beforeReplaceGuard(CancellationToken.None)' "Critical pre-replace safety guard must run after the cancellation boundary."
Require-Text $transaction '_fileSystem.Replace(tempPath, sourcePath)' "Atomic replacement must replace source from the validated temp file."
Require-Text $transaction 'sourcePath,' "Final source path must participate in post-replace validation."
Require-Text $transaction '_fileSystem.Delete(tempPath)' "Cleanup may remove the temporary file."
Forbid-Text $transaction '_fileSystem.Delete(sourcePath)' "Transaction must never delete the source before replacement."
Forbid-Text $transaction 'File.Move(' "Transaction must not fall back to delete/move replacement."

# Optimistic concurrency must use content hash, length and timestamp.
Require-Text $baseline 'SHA256' "Source baseline must hash source bytes with SHA-256."
Require-Text $baseline 'LastWriteTimeUtc' "Source baseline must track LastWriteTimeUtc."
Require-Text $baseline 'Length' "Source baseline must track byte length."

# Save orchestration must protect both the preflight and critical replacement edge.
Require-Text $saveService 'EnsureChromeClosed();' "Save service must block saves while Chrome is running."
Require-Text $saveService 'VerifyUnchangedAsync' "Save service must verify the accepted source baseline before transaction."
Require-Text $saveService 'CriticalChromeRecheckAsync' "Save service must provide a critical Chrome-process recheck."
Require-Text $saveService 'SourceChangedExternally' "External source changes must map to a typed save failure."

# UI/ViewModel may orchestrate persistence but must not directly mutate files.
foreach ($pair in @(
    @{ Name = "MainViewModel"; Content = $viewModel },
    @{ Name = "MainWindow"; Content = $mainWindow }
)) {
    foreach ($primitive in @(
        'File.Replace(',
        'File.Move(',
        'File.Copy(',
        'File.Delete(',
        'File.WriteAllText(',
        'File.WriteAllBytes(',
        'new FileStream('
    )) {
        if ($pair.Content.Contains($primitive, [System.StringComparison]::Ordinal)) {
            $errors.Add("$($pair.Name) contains forbidden direct persistence primitive: $primitive")
        }
    }
}

Require-Text $mainWindow 'await ViewModel.SaveAsync' "WPF Save must flow through MainViewModel.SaveAsync."
Require-Text $viewModel '_saveService.SaveAsync' "MainViewModel Save must flow through IChromeBookmarksSaveService."

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "V0.9 persistence safety contract verified: backup-first, conflict-aware atomic replacement remains isolated to the persistence layer."
