$ErrorActionPreference = "Stop"

$xamlPath = Join-Path $PSScriptRoot "..\src\ChromeBookmarksManager\MainWindow.xaml"
$xamlPath = [System.IO.Path]::GetFullPath($xamlPath)

if (-not (Test-Path -LiteralPath $xamlPath -PathType Leaf)) {
    throw "MainWindow.xaml was not found at: $xamlPath"
}

$content = Get-Content -LiteralPath $xamlPath -Raw

function Get-BoundControlTag {
    param(
        [Parameter(Mandatory)]
        [string]$ControlName,

        [Parameter(Mandatory)]
        [string]$ItemsSourceBinding
    )

    $pattern = '<{0}\b(?=[^>]*ItemsSource="{1}")(?<attributes>[^>]*)>' -f [regex]::Escape($ControlName), [regex]::Escape($ItemsSourceBinding)
    $match = [regex]::Match(
        $content,
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if (-not $match.Success) {
        throw "Could not find the production $ControlName bound to $ItemsSourceBinding."
    }

    return $match.Groups["attributes"].Value
}

function Assert-VirtualizationContract {
    param(
        [Parameter(Mandatory)]
        [string]$ControlName,

        [Parameter(Mandatory)]
        [string]$Attributes
    )

    $required = @(
        'VirtualizingPanel.IsVirtualizing="True"',
        'VirtualizingPanel.VirtualizationMode="Recycling"',
        'ScrollViewer.CanContentScroll="True"'
    )

    foreach ($token in $required) {
        if (-not $Attributes.Contains($token, [System.StringComparison]::Ordinal)) {
            throw "$ControlName is missing required virtualization setting: $token"
        }
    }

    if ($Attributes.Contains(
            'ScrollViewer.CanContentScroll="False"',
            [System.StringComparison]::Ordinal)) {
        throw "$ControlName disables logical content scrolling and can break virtualization."
    }
}

$treeAttributes = Get-BoundControlTag -ControlName "TreeView" -ItemsSourceBinding '{Binding FolderRoots}'
$listAttributes = Get-BoundControlTag -ControlName "ListView" -ItemsSourceBinding '{Binding CurrentBookmarks}'

Assert-VirtualizationContract -ControlName "Folder TreeView" -Attributes $treeAttributes
Assert-VirtualizationContract -ControlName "Bookmark ListView" -Attributes $listAttributes

Write-Host "Browser virtualization verification passed."
