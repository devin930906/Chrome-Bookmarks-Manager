param(
    [ValidateSet("Discover", "Prepare", "VerifyFirstSave", "CapturePostChrome", "VerifySecondSave", "Status")]
    [string]$Mode = "Discover",

    [string]$BookmarksPath,

    [string]$SessionDirectory,

    [string]$BackupRoot = (Join-Path ([Environment]::GetFolderPath("MyDocuments")) "ChromeBookmarksManager-Backups")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$SessionFileName = "task13-session.json"
$MarkerFileName = ".cbm-v09-task13-production-session"

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

function Assert-NoChromeProcesses {
    $chrome = @(Get-Process -Name chrome -ErrorAction SilentlyContinue)
    if ($chrome.Count -gt 0) {
        throw "Chrome is still running ($($chrome.Count) process(es)). Close every Chrome window/process before this phase."
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

function Test-JsonBookmarks([string]$Path) {
    try {
        $doc = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        return [ordered]@{
            JsonValid = $true
            HasRoots = ($null -ne $doc.roots)
            HasChecksum = -not [string]::IsNullOrWhiteSpace([string]$doc.checksum)
            HasChecksumSha256 = -not [string]::IsNullOrWhiteSpace([string]$doc.checksum_sha256)
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

function Find-NewestAppBackup([string]$SourcePath, [DateTime]$NotBeforeUtc) {
    $directory = Split-Path -Parent $SourcePath
    $candidates = @(
        Get-ChildItem -LiteralPath $directory -File -Filter "Bookmarks.ChromeBookmarksManager.*.bak" -ErrorAction SilentlyContinue |
            Where-Object { $_.CreationTimeUtc -ge $NotBeforeUtc.AddSeconds(-2) } |
            Sort-Object CreationTimeUtc -Descending
    )

    if ($candidates.Count -eq 0) {
        return $null
    }

    return $candidates[0]
}

function Get-SessionFile([string]$Directory) {
    return Join-Path $Directory $SessionFileName
}

function Get-MarkerFile([string]$Directory) {
    return Join-Path $Directory $MarkerFileName
}

function Assert-Session([string]$Directory) {
    if ([string]::IsNullOrWhiteSpace($Directory)) {
        throw "SessionDirectory is required for this mode."
    }

    $full = Get-FullPath $Directory
    if (-not (Test-Path -LiteralPath (Get-MarkerFile $full) -PathType Leaf)) {
        throw "Task 13 session marker not found. Refusing to continue: $full"
    }
    if (-not (Test-Path -LiteralPath (Get-SessionFile $full) -PathType Leaf)) {
        throw "Task 13 session metadata not found: $full"
    }
    return $full
}

function Load-Session([string]$Directory) {
    $full = Assert-Session $Directory
    return Get-Content -LiteralPath (Get-SessionFile $full) -Raw -Encoding UTF8 | ConvertFrom-Json
}

function Save-Session($Session, [string]$Directory) {
    $Session | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Get-SessionFile $Directory) -Encoding UTF8
}

function Assert-ExternalBackupStillMatches($Session) {
    $backupState = Get-FileState ([string]$Session.ExternalBackupPath)
    $matches = (
        ([string]$Session.PreSaveSha256).Equals([string]$backupState.Sha256, [System.StringComparison]::OrdinalIgnoreCase) -and
        ([int64]$Session.PreSaveLength -eq [int64]$backupState.Length)
    )

    if (-not $matches) {
        throw "Independent external backup no longer matches the captured pre-V0.9 source. Stop production acceptance."
    }

    return $true
}

switch ($Mode) {
    "Discover" {
        $roots = @(
            (Join-Path $env:LOCALAPPDATA "Google\Chrome\User Data"),
            (Join-Path $env:LOCALAPPDATA "Google\Chrome Beta\User Data"),
            (Join-Path $env:LOCALAPPDATA "Google\Chrome Dev\User Data"),
            (Join-Path $env:LOCALAPPDATA "Google\Chrome SxS\User Data")
        ) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }

        $candidates = @()
        foreach ($root in $roots) {
            $profiles = @(
                Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
                    Where-Object { $_.Name -eq "Default" -or $_.Name -like "Profile *" }
            )

            foreach ($profile in $profiles) {
                $candidate = Join-Path $profile.FullName "Bookmarks"
                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    $item = Get-Item -LiteralPath $candidate
                    $candidates += [pscustomobject]@{
                        ProfileDirectory = $profile.Name
                        BookmarksPath = $item.FullName
                        Length = $item.Length
                        LastWriteTime = $item.LastWriteTime
                    }
                }
            }
        }

        if ($candidates.Count -eq 0) {
            Write-Host "No standard Chrome Bookmarks files were found."
            Write-Host "If Chrome uses a custom/portable profile, use -Mode Prepare -BookmarksPath '<exact path>'."
            break
        }

        Write-Host ""
        Write-Host "Chrome Bookmarks candidates (paths/metadata only; no bookmark content is read):"
        $candidates | Format-Table -AutoSize
        Write-Host ""
        Write-Host "If more than one profile appears, open chrome://version in your normal Chrome and compare the Profile Path."
        Write-Host "Then close ALL Chrome processes before running Prepare."
    }

    "Prepare" {
        Assert-NoChromeProcesses

        if ([string]::IsNullOrWhiteSpace($BookmarksPath)) {
            throw "BookmarksPath is required. Use -Mode Discover first if needed."
        }

        $source = Get-FullPath $BookmarksPath
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Production Bookmarks file not found: $source"
        }
        if (-not ([System.IO.Path]::GetFileName($source).Equals("Bookmarks", [System.StringComparison]::OrdinalIgnoreCase))) {
            throw "Refusing production acceptance because the selected file is not named exactly 'Bookmarks'."
        }

        $sourceDirectory = Split-Path -Parent $source
        $backupRootFull = Get-FullPath $BackupRoot

        if (Test-IsSameOrChildPath $backupRootFull $sourceDirectory) {
            throw "BackupRoot must be outside the Chrome profile directory."
        }

        if (-not (Test-Path -LiteralPath $backupRootFull)) {
            New-Item -ItemType Directory -Path $backupRootFull -Force | Out-Null
        }

        $sessionName = "V09-Task13-" + [DateTime]::Now.ToString("yyyyMMdd-HHmmss.fff")
        $sessionDir = Join-Path $backupRootFull $sessionName
        New-Item -ItemType Directory -Path $sessionDir -Force | Out-Null

        Set-Content -LiteralPath (Get-MarkerFile $sessionDir) -Encoding UTF8 -Value "ChromeBookmarksManager V0.9 Task 13 production owner acceptance session."

        $before = Get-FileState $source
        $externalBackup = Join-Path $sessionDir "Bookmarks.pre-v09.external.bak"
        Copy-Item -LiteralPath $source -Destination $externalBackup -ErrorAction Stop

        $backupState = Get-FileState $externalBackup
        $backupMatches = (
            $before.Sha256.Equals($backupState.Sha256, [System.StringComparison]::OrdinalIgnoreCase) -and
            ([int64]$before.Length -eq [int64]$backupState.Length)
        )

        if (-not $backupMatches) {
            throw "Independent external backup verification FAILED. Do not open the app against the production Bookmarks file."
        }

        $session = [ordered]@{
            SourcePath = $before.Path
            PreSaveSha256 = $before.Sha256
            PreSaveLength = $before.Length
            PreSaveLastWriteTimeUtc = $before.LastWriteTimeUtc
            PreSaveCapturedAtUtc = $before.CapturedAtUtc
            ExternalBackupPath = $backupState.Path
            ExternalBackupSha256 = $backupState.Sha256
            ExternalBackupLength = $backupState.Length
            FirstSaveVerified = $false
            ChromeRewriteCaptured = $false
            SecondSaveVerified = $false
        }
        Save-Session $session $sessionDir

        Write-Host ""
        Write-Host "Task 13 production preflight PASSED."
        Write-Host "SessionDirectory=$sessionDir"
        Write-Host "BookmarksPath=$($before.Path)"
        Write-Host "ExternalBackupPath=$($backupState.Path)"
        Write-Host "ExternalBackupMatchesSource=True"
        Write-Host "SourceSHA256=$($before.Sha256)"
        Write-Host "SourceLength=$($before.Length)"
        Write-Host ""
        Write-Host "KEEP this SessionDirectory. Do not delete the external backup until V0.9 production acceptance is complete."
    }

    "VerifyFirstSave" {
        Assert-NoChromeProcesses
        $sessionDir = Assert-Session $SessionDirectory
        $session = Load-Session $sessionDir
        [void](Assert-ExternalBackupStillMatches $session)

        $source = [string]$session.SourcePath
        $current = Get-FileState $source
        $json = Test-JsonBookmarks $source
        $capturedUtc = [DateTime]::Parse([string]$session.PreSaveCapturedAtUtc).ToUniversalTime()
        $appBackup = Find-NewestAppBackup $source $capturedUtc

        if ($null -eq $appBackup) {
            throw "No ChromeBookmarksManager application backup was found after the production pre-save baseline."
        }

        $appBackupState = Get-FileState $appBackup.FullName
        $appBackupMatches = (
            ([string]$session.PreSaveSha256).Equals([string]$appBackupState.Sha256, [System.StringComparison]::OrdinalIgnoreCase) -and
            ([int64]$session.PreSaveLength -eq [int64]$appBackupState.Length)
        )
        $sourceChanged = -not ([string]$session.PreSaveSha256).Equals([string]$current.Sha256, [System.StringComparison]::OrdinalIgnoreCase)

        Write-Host ""
        Write-Host "Task 13 - first production app save verification"
        Write-Host "ExternalBackupStillMatchesPreSave=True"
        Write-Host "AppBackupMatchesPreSave=$appBackupMatches"
        Write-Host "SavedFileChangedFromPreSave=$sourceChanged"
        Write-Host "SavedFileJsonValid=$($json.JsonValid)"
        Write-Host "HasChecksum=$($json.HasChecksum)"
        Write-Host "HasChecksumSha256=$($json.HasChecksumSha256)"
        Write-Host ""

        if (-not $appBackupMatches -or -not $json.JsonValid -or -not $json.HasChecksum -or -not $json.HasChecksumSha256) {
            throw "Task 13 first production Save integrity verification FAILED. Stop and keep the independent external backup."
        }

        $session.FirstSaveVerified = $true
        $session.FirstSaveVerifiedAtUtc = [DateTime]::UtcNow.ToString("o")
        $session.FirstAppBackupPath = $appBackupState.Path
        $session.FirstSavedSha256 = $current.Sha256
        $session.FirstSavedLength = $current.Length
        Save-Session $session $sessionDir

        Write-Host "First production Save integrity verification PASSED."
    }

    "CapturePostChrome" {
        Assert-NoChromeProcesses
        $sessionDir = Assert-Session $SessionDirectory
        $session = Load-Session $sessionDir
        [void](Assert-ExternalBackupStillMatches $session)

        if (-not [bool]$session.FirstSaveVerified) {
            throw "First production app Save has not been verified. Refusing to capture Chrome rewrite."
        }

        $source = [string]$session.SourcePath
        $state = Get-FileState $source
        $json = Test-JsonBookmarks $source

        if (-not $json.JsonValid -or -not $json.HasChecksum) {
            throw "Chrome-rewritten production Bookmarks file failed structural validation."
        }

        $session.ChromeRewriteCaptured = $true
        $session.ChromeRewriteSha256 = $state.Sha256
        $session.ChromeRewriteLength = $state.Length
        $session.ChromeRewriteLastWriteTimeUtc = $state.LastWriteTimeUtc
        $session.ChromeRewriteCapturedAtUtc = $state.CapturedAtUtc
        Save-Session $session $sessionDir

        Write-Host ""
        Write-Host "Production Chrome rewrite baseline captured."
        Write-Host "ChromeRewriteJsonValid=$($json.JsonValid)"
        Write-Host "HasChecksum=$($json.HasChecksum)"
        Write-Host "ChromeRewriteSHA256=$($state.Sha256)"
        Write-Host "ChromeRewriteLength=$($state.Length)"
    }

    "VerifySecondSave" {
        Assert-NoChromeProcesses
        $sessionDir = Assert-Session $SessionDirectory
        $session = Load-Session $sessionDir
        [void](Assert-ExternalBackupStillMatches $session)

        if (-not [bool]$session.ChromeRewriteCaptured) {
            throw "Chrome rewrite baseline has not been captured."
        }

        $source = [string]$session.SourcePath
        $current = Get-FileState $source
        $json = Test-JsonBookmarks $source
        $capturedUtc = [DateTime]::Parse([string]$session.ChromeRewriteCapturedAtUtc).ToUniversalTime()
        $appBackup = Find-NewestAppBackup $source $capturedUtc

        if ($null -eq $appBackup) {
            throw "No new application backup was found after the production Chrome rewrite baseline."
        }

        $appBackupState = Get-FileState $appBackup.FullName
        $appBackupMatches = (
            ([string]$session.ChromeRewriteSha256).Equals([string]$appBackupState.Sha256, [System.StringComparison]::OrdinalIgnoreCase) -and
            ([int64]$session.ChromeRewriteLength -eq [int64]$appBackupState.Length)
        )
        $sourceChanged = -not ([string]$session.ChromeRewriteSha256).Equals([string]$current.Sha256, [System.StringComparison]::OrdinalIgnoreCase)

        Write-Host ""
        Write-Host "Task 13 - second production app save verification"
        Write-Host "ExternalBackupStillMatchesPreSave=True"
        Write-Host "AppBackupMatchesChromeRewrite=$appBackupMatches"
        Write-Host "SavedFileChangedFromChromeRewrite=$sourceChanged"
        Write-Host "SavedFileJsonValid=$($json.JsonValid)"
        Write-Host "HasChecksum=$($json.HasChecksum)"
        Write-Host "HasChecksumSha256=$($json.HasChecksumSha256)"
        Write-Host ""

        if (-not $appBackupMatches -or -not $json.JsonValid -or -not $json.HasChecksum -or -not $json.HasChecksumSha256) {
            throw "Task 13 second production Save integrity verification FAILED. Keep the independent external backup and stop."
        }

        $session.SecondSaveVerified = $true
        $session.SecondSaveVerifiedAtUtc = [DateTime]::UtcNow.ToString("o")
        $session.SecondAppBackupPath = $appBackupState.Path
        $session.SecondSavedSha256 = $current.Sha256
        $session.SecondSavedLength = $current.Length
        Save-Session $session $sessionDir

        Write-Host "Second production Save integrity verification PASSED."
    }

    "Status" {
        $sessionDir = Assert-Session $SessionDirectory
        $session = Load-Session $sessionDir
        $chromeCount = @(Get-Process -Name chrome -ErrorAction SilentlyContinue).Count

        Write-Host "SessionDirectory=$sessionDir"
        Write-Host "SourcePath=$($session.SourcePath)"
        Write-Host "ExternalBackupPath=$($session.ExternalBackupPath)"
        Write-Host "FirstSaveVerified=$($session.FirstSaveVerified)"
        Write-Host "ChromeRewriteCaptured=$($session.ChromeRewriteCaptured)"
        Write-Host "SecondSaveVerified=$($session.SecondSaveVerified)"
        Write-Host "ChromeProcessCount=$chromeCount"
    }
}
