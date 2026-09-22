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

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

$variables = @(
    "CBM_MOVE_URL_COUNT",
    "CBM_MOVE_FOLDER_COUNT",
    "CBM_DELETE_URL_COUNT",
    "CBM_DELETE_FOLDER_COUNT",
    "CBM_HISTORY_URL_COUNT",
    "CBM_HISTORY_EDIT_COUNT",
    "CBM_WRITE_URL_COUNT"
)

$previous = @{}
foreach ($name in $variables) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

try {
    Invoke-Checked -Name "Reader release measurement" -Command {
        pwsh -NoProfile -File scripts/Measure-Reader.ps1 -UrlCount $UrlCount
    }

    Invoke-Checked -Name "Browser release measurement" -Command {
        pwsh -NoProfile -File scripts/Measure-BrowserState.ps1 -UrlCount $UrlCount
    }

    Invoke-Checked -Name "Search release measurement" -Command {
        pwsh -NoProfile -File scripts/Measure-Search.ps1 -UrlCount $UrlCount
    }

    $env:CBM_MOVE_URL_COUNT = $UrlCount.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $env:CBM_MOVE_FOLDER_COUNT = $FolderCount.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    Invoke-Checked -Name "MoveScale" -Command {
        dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=MoveScale" --logger "console;verbosity=normal"
    }

    $env:CBM_DELETE_URL_COUNT = $UrlCount.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $env:CBM_DELETE_FOLDER_COUNT = $FolderCount.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    Invoke-Checked -Name "DeleteBatchScale" -Command {
        dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=DeleteBatchScale" --logger "console;verbosity=normal"
    }

    $env:CBM_HISTORY_URL_COUNT = $UrlCount.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $env:CBM_HISTORY_EDIT_COUNT = $HistoryEditCount.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    Invoke-Checked -Name "UndoRedoScale" -Command {
        dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=UndoRedoScale" --logger "console;verbosity=normal"
    }

    $env:CBM_WRITE_URL_COUNT = $UrlCount.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    Invoke-Checked -Name "WriteScale" -Command {
        dotnet test tests/ChromeBookmarksManager.Tests/ChromeBookmarksManager.Tests.csproj --configuration Release --filter "Category=WriteScale" --logger "console;verbosity=normal"
    }

    Write-Host "V1.0 release measurements PASSED."
    Write-Host "UrlCount=$UrlCount"
    Write-Host "FolderCount=$FolderCount"
    Write-Host "HistoryEditCount=$HistoryEditCount"
}
finally {
    foreach ($name in $variables) {
        $value = $previous[$name]
        if ($null -eq $value) {
            [Environment]::SetEnvironmentVariable($name, $null, "Process")
        }
        else {
            [Environment]::SetEnvironmentVariable($name, [string]$value, "Process")
        }
    }
}
