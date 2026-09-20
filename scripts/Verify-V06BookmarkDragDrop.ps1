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

    Require-Text $xaml 'PreviewMouseLeftButtonDown="BookmarksList_PreviewMouseLeftButtonDown"' "Bookmark list must capture a drag start candidate."
    Require-Text $xaml 'PreviewMouseMove="BookmarksList_PreviewMouseMove"' "Bookmark list must start dragging only after pointer movement."
    Require-Text $xaml 'DragOver="BookmarksList_DragOver"' "Bookmark list must validate Before/After drag targets."
    Require-Text $xaml 'DragLeave="BookmarksList_DragLeave"' "Bookmark list must clear positional drag feedback."
    Require-Text $xaml 'Drop="BookmarksList_Drop"' "Bookmark list must handle bookmark reorder drops."

    Require-Text $xaml 'DragOver="FolderTree_DragOver"' "Folder tree must accept bookmark Into drag targets."
    Require-Text $xaml 'DragLeave="FolderTree_DragLeave"' "Folder tree must clear bookmark Into feedback."
    Require-Text $xaml 'Drop="FolderTree_Drop"' "Folder tree must handle bookmark Into drops."

    $allowDropCount = ([regex]::Matches($xaml, 'AllowDrop="True"')).Count
    if ($allowDropCount -lt 2) {
        $errors.Add("Both bookmark list and folder tree must opt into drop handling.")
    }

    Require-Text $xaml 'VirtualizingPanel.IsVirtualizing="True"' "Bookmark drag/drop must preserve virtualization."
    Require-Text $xaml 'VirtualizingPanel.VirtualizationMode="Recycling"' "Bookmark drag/drop must preserve recycling virtualization."
}

if (Require-File $mainWindowCode) {
    $code = Get-Content -LiteralPath $mainWindowCode -Raw

    Require-Text $code 'BookmarkDragPayload' "Bookmark drag source must use the typed bookmark payload."
    Require-Text $code 'SystemParameters.MinimumHorizontalDragDistance' "Bookmark drag must respect the system horizontal drag threshold."
    Require-Text $code 'SystemParameters.MinimumVerticalDragDistance' "Bookmark drag must respect the system vertical drag threshold."
    Require-Text $code 'System.Windows.DragDrop.DoDragDrop' "Bookmark drag source must use WPF DragDrop.DoDragDrop."
    Require-Text $code 'DragDropRules.GetBookmarkRowPlacement' "Bookmark row drops must use the tested Before/After placement rule."
    Require-Text $code 'DragDropRules.CanPositionallyReorderBookmarks' "Search-result positional reorder must use the tested search guard."
    Require-Text $code 'MoveBookmarkBeforeAsync' "Before drops must route through MainViewModel."
    Require-Text $code 'MoveBookmarkAfterAsync' "After drops must route through MainViewModel."
    Require-Text $code 'MoveBookmarkToEndAsync' "Bookmark-to-folder drops must route through MainViewModel."
    Require-Text $code 'SetBookmarkDropIndicator' "Bookmark positional drops must expose visual feedback."
    Require-Text $code 'SetFolderDropIndicator' "Folder Into drops must expose visual feedback."

    foreach ($forbidden in @(
        ".RemoveChildAt(",
        ".InsertChild(",
        ".AddChild("
    )) {
        if ($code.Contains($forbidden, [System.StringComparison]::Ordinal)) {
            $errors.Add("Drag/drop code-behind must not mutate bookmark hierarchy directly: $forbidden")
        }
    }
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "V0.6 bookmark Drag & Drop UI contract verified."
