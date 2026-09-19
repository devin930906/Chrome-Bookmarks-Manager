param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$mainWindowXaml = Join-Path $ProjectRoot "src/ChromeBookmarksManager/MainWindow.xaml"
$mainWindowCode = Join-Path $ProjectRoot "src/ChromeBookmarksManager/MainWindow.xaml.cs"
$dialogXaml = Join-Path $ProjectRoot "src/ChromeBookmarksManager/BookmarkEditDialog.xaml"
$dialogCode = Join-Path $ProjectRoot "src/ChromeBookmarksManager/BookmarkEditDialog.xaml.cs"

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

    if ($Content -notmatch $Pattern) {
        $errors.Add($Message)
    }
}

if (Require-File $mainWindowXaml) {
    $xaml = Get-Content -LiteralPath $mainWindowXaml -Raw

    Require-Text $xaml 'x:Name="FolderTree"' "Folder TreeView must have a stable FolderTree name for keyboard routing."
    Require-Text $xaml 'Header="_Edit"' "Edit menu must exist and be enabled by per-action state."
    Require-Text $xaml 'Header="Add _Bookmark..."' "Edit menu must expose Add Bookmark."
    Require-Text $xaml 'Header="Add _Folder..."' "Edit menu must expose Add Folder."
    Require-Text $xaml 'Header="_Rename Folder..."' "Edit menu must expose Rename Folder."
    Require-Text $xaml 'Header="Rename _Bookmark..."' "Edit menu must expose Rename Bookmark."
    Require-Text $xaml 'Header="Edit _URL..."' "Edit menu must expose Edit URL."
    Require-Text $xaml 'InputGestureText="F2"' "Rename UI must advertise the F2 gesture."
    Require-Text $xaml '<TreeView.ContextMenu>' "Folder tree must expose an editing context menu."
    Require-Text $xaml '<ListView.ContextMenu>' "Bookmark list must expose an editing context menu."
    Require-Text $xaml 'IsEnabled="{Binding CanRenameSelectedFolder}"' "Root rename protection must flow from CanRenameSelectedFolder."
    Require-Text $xaml 'IsEnabled="{Binding CanRenameSelectedBookmark}"' "Bookmark rename enablement must flow from MainViewModel."
    Require-Text $xaml 'IsEnabled="{Binding CanEditSelectedBookmarkUrl}"' "URL edit enablement must flow from MainViewModel."
    Require-Text $xaml 'VirtualizingPanel.IsVirtualizing="True"' "Editing UI must preserve virtualization."
    Require-Text $xaml 'VirtualizingPanel.VirtualizationMode="Recycling"' "Editing UI must preserve recycling virtualization."
    Require-Text $xaml 'in-memory only' "UI must explicitly state that V0.5 edits are in-memory only."
    Require-Text $xaml 'Save is not available' "UI must explicitly state that Save is not available in V0.5."
}

if (Require-File $mainWindowCode) {
    $code = Get-Content -LiteralPath $mainWindowCode -Raw

    foreach ($api in @(
        "AddBookmarkAsync",
        "AddFolderAsync",
        "RenameSelectedFolderAsync",
        "RenameSelectedBookmarkAsync",
        "EditSelectedBookmarkUrlAsync"
    )) {
        Require-Text $code ([regex]::Escape($api)) "MainWindow must bridge UI actions through MainViewModel.$api."
    }

    Require-Text $code 'Key.F2' "MainWindow must route F2 rename."
    Require-Text $code 'BookmarkEditDialog' "MainWindow must use the shared compact editing dialog."
    Require-Text $code 'BookmarkEditException' "MainWindow must surface editing validation failures."

    foreach ($forbidden in @(
        ".SetName(",
        ".SetUrl(",
        ".AddChild(",
        ".RecordAddedNode("
    )) {
        if ($code.Contains($forbidden, [System.StringComparison]::Ordinal)) {
            $errors.Add("MainWindow code-behind must not mutate the domain directly: $forbidden")
        }
    }
}

if (Require-File $dialogXaml) {
    $dialog = Get-Content -LiteralPath $dialogXaml -Raw
    Require-Text $dialog 'x:Name="NameBox"' "Shared edit dialog must contain NameBox."
    Require-Text $dialog 'x:Name="UrlBox"' "Shared edit dialog must contain UrlBox."
    Require-Text $dialog 'x:Name="ValidationText"' "Shared edit dialog must expose validation feedback."
    Require-Text $dialog 'IsDefault="True"' "Shared edit dialog must provide an Enter/default action."
    Require-Text $dialog 'IsCancel="True"' "Shared edit dialog must provide an Escape/cancel action."
}

if (Require-File $dialogCode) {
    $dialogCodeContent = Get-Content -LiteralPath $dialogCode -Raw
    Require-Text $dialogCodeContent 'string.IsNullOrWhiteSpace(UrlBox.Text)' "Dialog must reject an empty URL when URL input is required."
    Require-Text $dialogCodeContent 'ValidationText' "Dialog validation must produce visible feedback."
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "V0.5 editing UI contract verified."
