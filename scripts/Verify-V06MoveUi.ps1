param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$mainWindowXaml = Join-Path $ProjectRoot "src/ChromeBookmarksManager/MainWindow.xaml"
$mainWindowCode = Join-Path $ProjectRoot "src/ChromeBookmarksManager/MainWindow.xaml.cs"
$dialogXaml = Join-Path $ProjectRoot "src/ChromeBookmarksManager/MoveNodeDialog.xaml"
$dialogCode = Join-Path $ProjectRoot "src/ChromeBookmarksManager/MoveNodeDialog.xaml.cs"

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

    Require-Text $xaml 'Header="Move to..."' "Move to... must be exposed from context menus."
    Require-Text $xaml 'IsEnabled="{Binding CanMoveSelectedFolder}"' "Folder Move to... must follow CanMoveSelectedFolder."
    Require-Text $xaml 'IsEnabled="{Binding CanMoveSelectedBookmark}"' "Bookmark Move to... must follow CanMoveSelectedBookmark."
    Require-Text $xaml 'Click="MoveFolder_Click"' "Folder context menu must route Move to... through MoveFolder_Click."
    Require-Text $xaml 'Click="MoveBookmark_Click"' "Bookmark context menu must route Move to... through MoveBookmark_Click."
    Require-Text $xaml 'VirtualizingPanel.IsVirtualizing="True"' "V0.6 Move UI must preserve virtualization."
    Require-Text $xaml 'VirtualizingPanel.VirtualizationMode="Recycling"' "V0.6 Move UI must preserve recycling virtualization."
}

if (Require-File $mainWindowCode) {
    $code = Get-Content -LiteralPath $mainWindowCode -Raw

    Require-Text $code 'MoveNodeDialog' "MainWindow must use the shared MoveNodeDialog."
    Require-Text $code 'MoveBookmarkToEndAsync' "Bookmark Move to... must call MainViewModel.MoveBookmarkToEndAsync."
    Require-Text $code 'MoveFolderToEndAsync' "Folder Move to... must call MainViewModel.MoveFolderToEndAsync."
    Require-Text $code 'BookmarkMoveException' "Move validation failures must be surfaced to the user."

    foreach ($forbidden in @(
        ".RemoveChildAt(",
        ".InsertChild(",
        ".AddChild("
    )) {
        if ($code.Contains($forbidden, [System.StringComparison]::Ordinal)) {
            $errors.Add("MainWindow code-behind must not mutate bookmark hierarchy directly: $forbidden")
        }
    }
}

if (Require-File $dialogXaml) {
    $dialog = Get-Content -LiteralPath $dialogXaml -Raw

    Require-Text $dialog 'x:Name="TargetTree"' "Move dialog must contain a stable TargetTree."
    Require-Text $dialog 'ItemsSource="{Binding Roots}"' "Move dialog TreeView must bind to destination roots."
    Require-Text $dialog 'IsEnabled="{Binding IsValidTarget}"' "Invalid move destinations must be disabled."
    Require-Text $dialog 'IsDefault="True"' "Move dialog must provide an Enter/default action."
    Require-Text $dialog 'IsCancel="True"' "Move dialog must provide an Escape/cancel action."
}

if (Require-File $dialogCode) {
    $dialogCodeContent = Get-Content -LiteralPath $dialogCode -Raw

    Require-Text $dialogCodeContent 'SelectedTarget' "Move dialog must expose its selected BookmarkFolder."
    Require-Text $dialogCodeContent 'MoveTargetTreeItemViewModel.BuildRoots' "Move dialog must build targets from the tested target-tree model."
    Require-Text $dialogCodeContent 'IsValidTarget' "Move dialog must reject an invalid selected target."
}

if ($errors.Count -gt 0) {
    foreach ($errorMessage in $errors) {
        Write-Error $errorMessage
    }

    exit 1
}

Write-Host "V0.6 Move UI contract verified."
