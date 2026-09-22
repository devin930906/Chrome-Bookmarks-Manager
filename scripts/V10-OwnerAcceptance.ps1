param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Prepare", "VerifyBackup", "VerifyFinal")]
    [string]$Phase,

    [Parameter(Mandatory = $true)]
    [string]$BookmarksPath,

    [string]$ExecutablePath,

    [string]$BaselinePath,

    [string]$BackupPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-BookmarksSource([string]$Path) {
    $fullPath = Get-FullPath $Path

    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Bookmarks file not found: $fullPath"
    }

    if (-not ([System.IO.Path]::GetFileName($fullPath).Equals(
        "Bookmarks",
        [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Expected a Chrome Bookmarks file named exactly 'Bookmarks': $fullPath"
    }

    return $fullPath
}

function Assert-NoChromeProcesses {
    $chrome = @(Get-Process -Name chrome -ErrorAction SilentlyContinue)
    if ($chrome.Count -gt 0) {
        throw "Chrome is still running ($($chrome.Count) process(es)). Close every Chrome window/process before this phase."
    }
}

function Get-FileState([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "File not found: $Path"
    }

    $item = Get-Item -LiteralPath $Path
    $hash = Get-FileHash -LiteralPath $Path -Algorithm SHA256

    return [ordered]@{
        Sha256 = $hash.Hash.ToLowerInvariant()
        Length = [int64]$item.Length
        LastWriteTimeUtc = $item.LastWriteTimeUtc.ToString("o")
    }
}

function Resolve-BaselineOutputPath([string]$RequestedPath) {
    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $full = Get-FullPath $RequestedPath
        $parent = Split-Path -Parent $full
        if (-not [string]::IsNullOrWhiteSpace($parent) -and
            -not (Test-Path -LiteralPath $parent -PathType Container)) {
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
        }

        return $full
    }

    $documents = [Environment]::GetFolderPath("MyDocuments")
    if ([string]::IsNullOrWhiteSpace($documents)) {
        throw "Unable to resolve the Documents directory. Supply -BaselinePath explicitly."
    }

    $directory = Join-Path $documents "ChromeBookmarksManager-Acceptance"
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $name = "V10-owner-baseline-" + [DateTime]::Now.ToString("yyyyMMdd-HHmmss.fff") + ".json"
    return Join-Path $directory $name
}

function Load-Baseline([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "BaselinePath is required for this phase."
    }

    $full = Get-FullPath $Path
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "Baseline file not found: $full"
    }

    $baseline = Get-Content -LiteralPath $full -Raw -Encoding UTF8 | ConvertFrom-Json

    if ([string]::IsNullOrWhiteSpace([string]$baseline.Sha256) -or
        $null -eq $baseline.Length -or
        [string]::IsNullOrWhiteSpace([string]$baseline.LastWriteTimeUtc)) {
        throw "Baseline file is missing required integrity metadata."
    }

    return $baseline
}

function Find-VersionVerifier {
    $candidates = @(
        (Join-Path $PSScriptRoot "Verify-V10Version.ps1"),
        (Join-Path (Get-Location) "scripts\Verify-V10Version.ps1"),
        (Join-Path (Get-Location) "Verify-V10Version.ps1")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Get-FullPath $candidate)
        }
    }

    throw "Verify-V10Version.ps1 was not found. Run this helper from the V1.0 repository/acceptance kit that contains the version verifier."
}

function Assert-V10Executable([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "ExecutablePath is required for this phase."
    }

    $full = Get-FullPath $Path
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "Executable not found: $full"
    }

    if (-not ([System.IO.Path]::GetFileName($full).Equals(
        "ChromeBookmarksManager.exe",
        [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Expected executable named exactly 'ChromeBookmarksManager.exe': $full"
    }

    $verifier = Find-VersionVerifier
    & pwsh -NoProfile -File $verifier -ExecutablePath $full
    if ($LASTEXITCODE -ne 0) {
        throw "V1.0 executable version verification failed."
    }

    return $full
}

switch ($Phase) {
    "Prepare" {
        Assert-NoChromeProcesses
        $source = Assert-BookmarksSource $BookmarksPath

        $exeVerified = $false
        if (-not [string]::IsNullOrWhiteSpace($ExecutablePath)) {
            [void](Assert-V10Executable $ExecutablePath)
            $exeVerified = $true
        }

        $state = Get-FileState $source
        $baselineOutput = Resolve-BaselineOutputPath $BaselinePath

        $baseline = [ordered]@{
            Schema = "ChromeBookmarksManager.V10.OwnerAcceptanceBaseline"
            Version = 1
            Sha256 = $state.Sha256
            Length = $state.Length
            LastWriteTimeUtc = $state.LastWriteTimeUtc
            CapturedAtUtc = [DateTime]::UtcNow.ToString("o")
        }

        $baseline |
            ConvertTo-Json -Depth 4 |
            Set-Content -LiteralPath $baselineOutput -Encoding UTF8

        Write-Host ""
        Write-Host "V1.0 owner acceptance Prepare PASSED."
        Write-Host "BaselinePath=$baselineOutput"
        Write-Host "SourceSHA256=$($state.Sha256)"
        Write-Host "SourceLength=$($state.Length)"
        Write-Host "SourceLastWriteTimeUtc=$($state.LastWriteTimeUtc)"
        Write-Host "ExecutableVersionVerified=$exeVerified"
        Write-Host ""
        Write-Host "The baseline contains integrity metadata only; no bookmark titles, URLs, queries, or raw JSON are stored."
    }

    "VerifyBackup" {
        [void](Assert-BookmarksSource $BookmarksPath)
        $baseline = Load-Baseline $BaselinePath

        if ([string]::IsNullOrWhiteSpace($BackupPath)) {
            throw "BackupPath is required for VerifyBackup."
        }

        $backupFull = Get-FullPath $BackupPath
        $backup = Get-FileState $backupFull

        $hashMatches = ([string]$baseline.Sha256).Equals(
            [string]$backup.Sha256,
            [System.StringComparison]::OrdinalIgnoreCase)
        $lengthMatches = ([int64]$baseline.Length -eq [int64]$backup.Length)

        Write-Host ""
        Write-Host "V1.0 owner acceptance backup verification"
        Write-Host "BackupHashMatchesSource=$hashMatches"
        Write-Host "BackupLengthMatchesSource=$lengthMatches"
        Write-Host "BackupSHA256=$($backup.Sha256)"
        Write-Host "BackupLength=$($backup.Length)"
        Write-Host ""

        if (-not $hashMatches -or -not $lengthMatches) {
            throw "Backup verification FAILED. Keep the independent external backup and stop acceptance."
        }

        Write-Host "V1.0 backup verification PASSED."
    }

    "VerifyFinal" {
        Assert-NoChromeProcesses
        $source = Assert-BookmarksSource $BookmarksPath
        [void](Assert-V10Executable $ExecutablePath)

        $state = Get-FileState $source

        try {
            $doc = Get-Content -LiteralPath $source -Raw -Encoding UTF8 | ConvertFrom-Json
            $jsonValid = $true
            $hasRoots = ($null -ne $doc.roots)
            $hasChecksum = -not [string]::IsNullOrWhiteSpace([string]$doc.checksum)
            $hasChecksumSha256 = -not [string]::IsNullOrWhiteSpace([string]$doc.checksum_sha256)
        }
        catch {
            $jsonValid = $false
            $hasRoots = $false
            $hasChecksum = $false
            $hasChecksumSha256 = $false
        }

        Write-Host ""
        Write-Host "V1.0 owner acceptance final structural verification"
        Write-Host "ExecutableVersionVerified=True"
        Write-Host "JsonValid=$jsonValid"
        Write-Host "HasRoots=$hasRoots"
        Write-Host "HasChecksum=$hasChecksum"
        Write-Host "HasChecksumSha256=$hasChecksumSha256"
        Write-Host "SourceSHA256=$($state.Sha256)"
        Write-Host "SourceLength=$($state.Length)"
        Write-Host "SourceLastWriteTimeUtc=$($state.LastWriteTimeUtc)"
        Write-Host ""

        if (-not $jsonValid -or
            -not $hasRoots -or
            -not $hasChecksum -or
            -not $hasChecksumSha256) {
            throw "Final Bookmarks structural verification FAILED. Keep the independent external backup and stop acceptance."
        }

        Write-Host "V1.0 final structural verification PASSED."
    }
}
