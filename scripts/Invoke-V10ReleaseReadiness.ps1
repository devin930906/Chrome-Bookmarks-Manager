param(
    [ValidateRange(100, 250000)]
    [int]$UrlCount = 250000,

    [ValidateRange(1, 10000)]
    [int]$FolderCount = 4000,

    [ValidateRange(101, 100000)]
    [int]$HistoryEditCount = 100000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    Write-Host ""
    Write-Host "=== $Name ==="
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

Invoke-Checked -Name "Repository safety" -Command {
    pwsh -NoProfile -File scripts/Verify-RepositorySafety.ps1
}

Invoke-Checked -Name "V1.0 project version" -Command {
    pwsh -NoProfile -File scripts/Verify-V10Version.ps1
}

Invoke-Checked -Name "V1.0 production UI contract" -Command {
    node scripts/Verify-V10UiContract.mjs
}

Invoke-Checked -Name "V1.0 release measurement contract" -Command {
    node scripts/Verify-V10ReleaseMeasurementsContract.mjs
}

if (Test-Path -LiteralPath "scripts/Verify-V10ReleaseWorkflow.mjs" -PathType Leaf) {
    Invoke-Checked -Name "V1.0 release workflow contract" -Command {
        node scripts/Verify-V10ReleaseWorkflow.mjs
    }
}
else {
    Write-Host "V1.0 release workflow contract is not present yet; Task 6 has not created the formal workflow."
}

Invoke-Checked -Name "Restore" -Command {
    dotnet restore ChromeBookmarksManager.slnx
}

Invoke-Checked -Name "Build Release" -Command {
    dotnet build ChromeBookmarksManager.slnx --configuration Release --no-restore
}

Invoke-Checked -Name "ReaderScale" -Command {
    dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --no-build --filter "Category=ReaderScale" --logger "console;verbosity=normal"
}

Invoke-Checked -Name "Browser virtualization contract" -Command {
    pwsh -NoProfile -File scripts/Verify-BrowserVirtualization.ps1
}

Invoke-Checked -Name "Search UI contract" -Command {
    pwsh -NoProfile -File scripts/Verify-SearchUi.ps1
}

Invoke-Checked -Name "V0.5 editing UI contract" -Command {
    pwsh -NoProfile -File scripts/Verify-V05EditingUi.ps1
}

Invoke-Checked -Name "V0.6 Move UI contract" -Command {
    pwsh -NoProfile -File scripts/Verify-V06MoveUi.ps1
}

Invoke-Checked -Name "V0.6 bookmark Drag & Drop contract" -Command {
    pwsh -NoProfile -File scripts/Verify-V06BookmarkDragDrop.ps1
}

Invoke-Checked -Name "V0.6 folder Drag & Drop contract" -Command {
    pwsh -NoProfile -File scripts/Verify-V06FolderDragDrop.ps1
}

Invoke-Checked -Name "V0.6 move safety" -Command {
    pwsh -NoProfile -File scripts/Verify-V06MoveSafety.ps1
}

Invoke-Checked -Name "V0.7 Delete/Batch UI contract" -Command {
    node scripts/Verify-V07DeleteUi.mjs
}

Invoke-Checked -Name "V0.7 delete safety" -Command {
    pwsh -NoProfile -File scripts/Verify-V07DeleteSafety.ps1
}

Invoke-Checked -Name "V0.8 Undo/Redo UI contract" -Command {
    node scripts/Verify-V08UndoRedoUi.mjs
}

Invoke-Checked -Name "V0.8 Undo/Redo safety" -Command {
    pwsh -NoProfile -File scripts/Verify-V08UndoRedoSafety.ps1
}

Invoke-Checked -Name "V0.9 Save UI contract" -Command {
    node scripts/Verify-V09SaveUi.mjs
}

Invoke-Checked -Name "V0.9 persistence safety" -Command {
    pwsh -NoProfile -File scripts/Verify-V09WriteSafety.ps1
}

foreach ($category in @(
    "BrowserScale",
    "SearchScale",
    "MoveScale",
    "DeleteBatchScale",
    "UndoRedoScale",
    "WriteScale"
)) {
    $capturedCategory = $category
    Invoke-Checked -Name $capturedCategory -Command {
        dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --no-build --filter "Category=$capturedCategory" --logger "console;verbosity=normal"
    }
}

Invoke-Checked -Name "Full test suite" -Command {
    dotnet test ChromeBookmarksManager.slnx --configuration Release --no-build --logger "console;verbosity=normal"
}

Invoke-Checked -Name "V1.0 release measurements" -Command {
    pwsh -NoProfile -File scripts/Invoke-V10ReleaseMeasurements.ps1 -UrlCount $UrlCount -FolderCount $FolderCount -HistoryEditCount $HistoryEditCount
}

Invoke-Checked -Name "Publish Windows x64" -Command {
    pwsh -NoProfile -File scripts/Publish-Windows.ps1
}

Invoke-Checked -Name "Verify single-file output" -Command {
    pwsh -NoProfile -File scripts/Verify-SingleFilePublish.ps1 -PublishDirectory artifacts/win-x64
}

Invoke-Checked -Name "Smoke test executable" -Command {
    pwsh -NoProfile -File scripts/SmokeTest-Windows.ps1 -ExecutablePath artifacts/win-x64/ChromeBookmarksManager.exe
}

Invoke-Checked -Name "Verify V1.0 EXE version" -Command {
    pwsh -NoProfile -File scripts/Verify-V10Version.ps1 -ExecutablePath artifacts/win-x64/ChromeBookmarksManager.exe
}

Invoke-Checked -Name "Create V1.0 SHA256" -Command {
    pwsh -NoProfile -File scripts/New-V10ReleaseChecksum.ps1 -ExecutablePath artifacts/win-x64/ChromeBookmarksManager.exe
}

Invoke-Checked -Name "Verify V1.0 release assets" -Command {
    pwsh -NoProfile -File scripts/Verify-V10ReleaseAssets.ps1 -ReleaseDirectory artifacts/win-x64
}

Write-Host ""
Write-Host "V1.0 release readiness PASSED."
Write-Host "UrlCount=$UrlCount"
Write-Host "FolderCount=$FolderCount"
Write-Host "HistoryEditCount=$HistoryEditCount"
