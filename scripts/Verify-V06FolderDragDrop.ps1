param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$mainWindowXaml = Join-Path $ProjectRoot "src/ChromeBookmarksManager/MainWindow.xaml"
$mainWindowCode = Join-Path $ProjectRoot "src/ChromeBookmarksManager/MainWindow.xaml.cs"

$errors = [System.Collections.Generic.List[string]]::new()

function Require-File {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        $errors.Add("Missing required file: $Path")
        return $false
    }

    return $true
}

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

if (Require-File $mainWindowXaml) {
    $xaml = Get-Content -LiteralPath $mainWindowXaml -Raw

    Require-Text $xaml 'PreviewMouseLeftButtonDown="FolderTree_PreviewMouseLeftButtonDown"' "Folder tree must capture a folder drag source candidate."
    Require-Text $xaml 'PreviewMouseMove="FolderTree_PreviewMouseMove"' "Folder tree must start folder dragging from pointer movement."
    Require-Text $xaml 'DragOver="FolderTree_DragOver"' "Folder tree must validate folder drop targets."
    Require-Text $xaml 'DragLeave="FolderTree_DragLeave"' "Folder tree must clear folder drop feedback."
    Require-Text $xaml 'Drop="FolderTree_Drop"' "Folder tree must execute folder drops."
    Require-Text $xaml 'VirtualizingPanel.IsVirtualizing="True"' "Folder drag/drop must preserve tree virtualization."
    Require-Text $xaml 'VirtualizingPanel.VirtualizationMode="Recycling"' "Folder drag/drop must preserve recycling virtualization."
}

if (Require-File $mainWindowCode) {
    $code = Get-Content -LiteralPath $mainWindowCode -Raw

    Require-Text $code 'DragDropRules.CanStartFolderDrag' "Permanent roots must be blocked as folder drag sources."
    Require-Text $code 'DragDropRules.GetFolderRowPlacement' "Folder drops must use the tested thirds placement rule."
    Require-Text $code 'DragDropRules.CanMoveFolderInto' "Folder Into drops must validate self/descendant protection."
    Require-Text $code 'DragDropRules.CanMoveFolderRelativeTo' "Folder Before/After drops must validate relative targets."
    Require-Text $code 'MoveFolderBeforeAsync' "Folder Before drops must route through MainViewModel."
    Require-Text $code 'MoveFolderAfterAsync' "Folder After drops must route through MainViewModel."
    Require-Text $code 'MoveFolderToEndAsync' "Folder Into drops must route through MainViewModel."
    Require-Text $code 'SetFolderDropIndicator' "Folder drops must expose placement feedback."
    Require-Text $code 'BookmarkDragPayload' "Folder drag source must carry the original bookmark-domain object."

    foreach ($forbidden in @(
        ".RemoveChildAt(",
        ".InsertChild(",
        ".AddChild("
    )) {
        if ($code.Contains($forbidden, [System.StringComparison]::Ordinal)) {
            $errors.Add("Folder drag/drop code-behind must not mutate bookmark hierarchy directly: $forbidden")
        }
    }
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "V0.6 folder Drag & Drop UI contract verified."
