param(
    [ValidateSet("Prepare", "CapturePreSave", "VerifyAppSave", "LaunchChrome", "CapturePostChrome", "VerifySecondSave", "Status", "Cleanup")]
    [string]$Mode = "Status",

    [string]$TestRoot = (Join-Path $env:TEMP "ChromeBookmarksManager-V09-Task12"),

    [string]$ChromePath,

    [switch]$IUnderstandThisDeletesTheDisposableProfile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$MarkerName = ".cbm-v09-disposable-profile"
$PreSaveSnapshotName = ".cbm-v09-presave.json"
$PostChromeSnapshotName = ".cbm-v09-postchrome.json"

function Get-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path)
}

function Test-IsSameOrChildPath([string]$Candidate, [string]$Parent) {
    $candidateFull = (Get-FullPath $Candidate).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $parentFull = (Get-FullPath $Parent).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)

    if ($candidateFull.Equals($parentFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $prefix = $parentFull + [System.IO.Path]::DirectorySeparatorChar
    return $candidateFull.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-DisposableRoot([string]$Root) {
    $rootFull = Get-FullPath $Root
    $forbiddenRoots = @(
        (Join-Path $env:LOCALAPPDATA "Google\Chrome\User Data"),
        (Join-Path $env:LOCALAPPDATA "Google\Chrome Beta\User Data"),
        (Join-Path $env:LOCALAPPDATA "Google\Chrome Dev\User Data"),
        (Join-Path $env:LOCALAPPDATA "Google\Chrome SxS\User Data")
    )

    foreach ($forbidden in $forbiddenRoots) {
        if (Test-IsSameOrChildPath $rootFull $forbidden) {
            throw "Refusing to use a real Chrome profile path: $rootFull"
        }
    }

    if ($rootFull -match '(?i)\\Google\\Chrome(?: Beta| Dev| SxS)?\\User Data(?:\\|$)') {
        throw "Refusing to use a path that looks like a real Chrome User Data directory: $rootFull"
    }

    return $rootFull
}

function Assert-NoChromeProcesses {
    $chrome = @(Get-Process -Name chrome -ErrorAction SilentlyContinue)
    if ($chrome.Count -gt 0) {
        throw "Chrome is still running ($($chrome.Count) process(es)). Close every Chrome window/process before this phase."
    }
}

function Resolve-ChromeExecutable([string]$RequestedPath) {
    if ($RequestedPath) {
        $full = Get-FullPath $RequestedPath
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
            throw "Chrome executable not found: $full"
        }
        return $full
    }

    $programFilesX86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)")
    $candidates = @(
        (Join-Path $env:ProgramFiles "Google\Chrome\Application\chrome.exe"),
        $(if ($programFilesX86) { Join-Path $programFilesX86 "Google\Chrome\Application\chrome.exe" }),
        (Join-Path $env:LOCALAPPDATA "Google\Chrome\Application\chrome.exe")
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }

    if ($candidates.Count -eq 0) {
        throw "Google Chrome was not found automatically. Re-run with -ChromePath 'C:\Path\To\chrome.exe'."
    }

    return (Get-FullPath $candidates[0])
}

function Get-BookmarksPath([string]$Root) {
    return Join-Path $Root "Default\Bookmarks"
}

function Get-MarkerPath([string]$Root) {
    return Join-Path $Root $MarkerName
}

function Assert-MarkedDisposableRoot([string]$Root) {
    $marker = Get-MarkerPath $Root
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        throw "Disposable-profile marker is missing. Refusing to continue: $marker"
    }
}

function Get-FileState([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Bookmarks file not found: $Path"
    }

    $item = Get-Item -LiteralPath $Path
    $hash = Get-FileHash -LiteralPath $Path -Algorithm SHA256

    return [ordered]@{
        Path = $item.FullName
        Sha256 = $hash.Hash.ToLowerInvariant()
        Length = [int64]$item.Length
        LastWriteTimeUtc = $item.LastWriteTimeUtc.ToString("o")
        CapturedAtUtc = [DateTime]::UtcNow.ToString("o")
    }
}

function Save-Snapshot($State, [string]$Path) {
    $State | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Load-Snapshot([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Snapshot not found: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Test-JsonBookmarks([string]$Path) {
    try {
        $doc = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        $hasRoots = $null -ne $doc.roots
        $hasChecksum = -not [string]::IsNullOrWhiteSpace([string]$doc.checksum)
        $hasSha256 = -not [string]::IsNullOrWhiteSpace([string]$doc.checksum_sha256)

        return [ordered]@{
            JsonValid = $true
            HasRoots = $hasRoots
            HasChecksum = $hasChecksum
            HasChecksumSha256 = $hasSha256
        }
    }
    catch {
        return [ordered]@{
            JsonValid = $false
            HasRoots = $false
            HasChecksum = $false
            HasChecksumSha256 = $false
            Error = $_.Exception.Message
        }
    }
}

function Find-NewestAppBackup([string]$BookmarksPath, [DateTime]$NotBeforeUtc) {
    $directory = Split-Path -Parent $BookmarksPath
    $candidates = @(
        Get-ChildItem -LiteralPath $directory -File -Filter "Bookmarks.ChromeBookmarksManager.*.bak" -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTimeUtc -ge $NotBeforeUtc.AddSeconds(-2) } |
            Sort-Object LastWriteTimeUtc -Descending
    )

    if ($candidates.Count -eq 0) {
        return $null
    }

    return $candidates[0]
}

function Start-DisposableChrome([string]$Root, [string]$Executable) {
    $arguments = @(
        "--user-data-dir=$Root",
        "--profile-directory=Default",
        "--no-first-run",
        "--no-default-browser-check",
        "chrome://bookmarks/"
    )

    Start-Process -FilePath $Executable -ArgumentList $arguments | Out-Null
}

$TestRoot = Assert-DisposableRoot $TestRoot
$BookmarksPath = Get-BookmarksPath $TestRoot
$PreSaveSnapshotPath = Join-Path $TestRoot $PreSaveSnapshotName
$PostChromeSnapshotPath = Join-Path $TestRoot $PostChromeSnapshotName

switch ($Mode) {
    "Prepare" {
        Assert-NoChromeProcesses

        if (-not (Test-Path -LiteralPath $TestRoot)) {
            New-Item -ItemType Directory -Path $TestRoot -Force | Out-Null
        }

        $marker = Get-MarkerPath $TestRoot
        if (-not (Test-Path -LiteralPath $marker)) {
            "ChromeBookmarksManager V0.9 Task 12 disposable test profile. Safe to delete only through the helper." |
                Set-Content -LiteralPath $marker -Encoding UTF8
        }

        $resolvedChrome = Resolve-ChromeExecutable $ChromePath
        Write-Host ""
        Write-Host "Disposable Chrome profile:"
        Write-Host "  $TestRoot"
        Write-Host ""
        Write-Host "Chrome will open using ONLY this user-data directory."
        Write-Host "Do not sign in or enable Sync. Create synthetic bookmarks/folders only."
        Write-Host "When finished, close every Chrome window, then run -Mode CapturePreSave."
        Write-Host ""

        Start-DisposableChrome $TestRoot $resolvedChrome
    }

    "CapturePreSave" {
        Assert-MarkedDisposableRoot $TestRoot
        Assert-NoChromeProcesses

        $state = Get-FileState $BookmarksPath
        Save-Snapshot $state $PreSaveSnapshotPath

        Write-Host ""
        Write-Host "Pre-save baseline captured."
        Write-Host "BookmarksPath=$($state.Path)"
        Write-Host "SHA256=$($state.Sha256)"
        Write-Host "Length=$($state.Length)"
        Write-Host "LastWriteTimeUtc=$($state.LastWriteTimeUtc)"
        Write-Host ""
        Write-Host "Now open ChromeBookmarksManager.exe and load exactly the BookmarksPath shown above."
    }

    "VerifyAppSave" {
        Assert-MarkedDisposableRoot $TestRoot
        Assert-NoChromeProcesses

        $before = Load-Snapshot $PreSaveSnapshotPath
        $current = Get-FileState $BookmarksPath
        $json = Test-JsonBookmarks $BookmarksPath
        $capturedUtc = [DateTime]::Parse([string]$before.CapturedAtUtc).ToUniversalTime()
        $backup = Find-NewestAppBackup $BookmarksPath $capturedUtc

        if ($null -eq $backup) {
            throw "No ChromeBookmarksManager application backup was found after the pre-save baseline."
        }

        $backupState = Get-FileState $backup.FullName
        $backupMatches = (
            ([string]$before.Sha256).Equals([string]$backupState.Sha256, [System.StringComparison]::OrdinalIgnoreCase) -and
            ([int64]$before.Length -eq [int64]$backupState.Length)
        )
        $sourceChanged = -not ([string]$before.Sha256).Equals([string]$current.Sha256, [System.StringComparison]::OrdinalIgnoreCase)

        Write-Host ""
        Write-Host "Task 12 - first app save verification"
        Write-Host "Backup=$($backup.FullName)"
        Write-Host "BackupMatchesPreSave=$backupMatches"
        Write-Host "SavedFileChangedFromBaseline=$sourceChanged"
        Write-Host "SavedFileJsonValid=$($json.JsonValid)"
        Write-Host "HasChecksum=$($json.HasChecksum)"
        Write-Host "HasChecksumSha256=$($json.HasChecksumSha256)"
        Write-Host ""

        if (-not $backupMatches -or -not $json.JsonValid -or -not $json.HasChecksum -or -not $json.HasChecksumSha256) {
            throw "Task 12 first-save integrity verification FAILED. Do not proceed to the production profile."
        }

        Write-Host "Integrity checks passed. Next run -Mode LaunchChrome and visually verify the expected changes in the disposable profile."
    }

    "LaunchChrome" {
        Assert-MarkedDisposableRoot $TestRoot
        Assert-NoChromeProcesses
        $resolvedChrome = Resolve-ChromeExecutable $ChromePath
        Start-DisposableChrome $TestRoot $resolvedChrome

        Write-Host ""
        Write-Host "Verify the app-saved changes in Chrome, including Chinese/Unicode text."
        Write-Host "Then make ONE additional synthetic bookmark change inside Chrome."
        Write-Host "Close every Chrome process and run -Mode CapturePostChrome."
    }

    "CapturePostChrome" {
        Assert-MarkedDisposableRoot $TestRoot
        Assert-NoChromeProcesses

        $state = Get-FileState $BookmarksPath
        $json = Test-JsonBookmarks $BookmarksPath

        if (-not $json.JsonValid -or -not $json.HasChecksum) {
            throw "Chrome-rewritten Bookmarks file is not structurally valid enough for the next gate."
        }

        Save-Snapshot $state $PostChromeSnapshotPath

        Write-Host ""
        Write-Host "Post-Chrome baseline captured."
        Write-Host "SHA256=$($state.Sha256)"
        Write-Host "Length=$($state.Length)"
        Write-Host "LastWriteTimeUtc=$($state.LastWriteTimeUtc)"
        Write-Host ""
        Write-Host "Reopen this Bookmarks file in ChromeBookmarksManager."
        Write-Host "Confirm it loads normally, make one small synthetic edit, Save again, then run -Mode VerifySecondSave."
    }

    "VerifySecondSave" {
        Assert-MarkedDisposableRoot $TestRoot
        Assert-NoChromeProcesses

        $before = Load-Snapshot $PostChromeSnapshotPath
        $current = Get-FileState $BookmarksPath
        $json = Test-JsonBookmarks $BookmarksPath
        $capturedUtc = [DateTime]::Parse([string]$before.CapturedAtUtc).ToUniversalTime()
        $backup = Find-NewestAppBackup $BookmarksPath $capturedUtc

        if ($null -eq $backup) {
            throw "No new application backup was found after Chrome rewrote the test profile."
        }

        $backupState = Get-FileState $backup.FullName
        $backupMatches = (
            ([string]$before.Sha256).Equals([string]$backupState.Sha256, [System.StringComparison]::OrdinalIgnoreCase) -and
            ([int64]$before.Length -eq [int64]$backupState.Length)
        )
        $sourceChanged = -not ([string]$before.Sha256).Equals([string]$current.Sha256, [System.StringComparison]::OrdinalIgnoreCase)

        Write-Host ""
        Write-Host "Task 12 - second app save verification"
        Write-Host "Backup=$($backup.FullName)"
        Write-Host "BackupMatchesChromeRewrite=$backupMatches"
        Write-Host "SavedFileChangedFromChromeBaseline=$sourceChanged"
        Write-Host "SavedFileJsonValid=$($json.JsonValid)"
        Write-Host "HasChecksum=$($json.HasChecksum)"
        Write-Host "HasChecksumSha256=$($json.HasChecksumSha256)"
        Write-Host ""

        if (-not $backupMatches -or -not $json.JsonValid -or -not $json.HasChecksum -or -not $json.HasChecksumSha256) {
            throw "Task 12 second-save integrity verification FAILED. Production owner acceptance remains blocked."
        }

        Write-Host "Automated Task 12 integrity checks PASSED."
        Write-Host "Report these True/False results together with your visual Chrome/app checks before Task 13 starts."
    }

    "Status" {
        Write-Host "TestRoot=$TestRoot"
        Write-Host "BookmarksPath=$BookmarksPath"
        Write-Host "MarkerExists=$(Test-Path -LiteralPath (Get-MarkerPath $TestRoot))"
        Write-Host "BookmarksExists=$(Test-Path -LiteralPath $BookmarksPath)"
        Write-Host "PreSaveSnapshotExists=$(Test-Path -LiteralPath $PreSaveSnapshotPath)"
        Write-Host "PostChromeSnapshotExists=$(Test-Path -LiteralPath $PostChromeSnapshotPath)"
        Write-Host "ChromeProcessCount=$(@(Get-Process -Name chrome -ErrorAction SilentlyContinue).Count)"
    }

    "Cleanup" {
        if (-not $IUnderstandThisDeletesTheDisposableProfile) {
            throw "Cleanup requires -IUnderstandThisDeletesTheDisposableProfile."
        }

        Assert-NoChromeProcesses
        Assert-MarkedDisposableRoot $TestRoot

        $tempRoot = Get-FullPath $env:TEMP
        if (-not (Test-IsSameOrChildPath $TestRoot $tempRoot)) {
            throw "Cleanup is restricted to disposable profiles located under TEMP. Refusing to delete: $TestRoot"
        }

        Remove-Item -LiteralPath $TestRoot -Recurse -Force
        Write-Host "Deleted disposable Task 12 profile: $TestRoot"
    }
}
