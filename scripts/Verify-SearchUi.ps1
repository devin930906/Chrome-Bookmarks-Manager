$ErrorActionPreference = "Stop"

$xamlPath = Join-Path $PSScriptRoot "..\src\ChromeBookmarksManager\MainWindow.xaml"
$xamlPath = [System.IO.Path]::GetFullPath($xamlPath)

if (-not (Test-Path -LiteralPath $xamlPath -PathType Leaf)) {
    throw "MainWindow.xaml was not found at: $xamlPath"
}

$content = Get-Content -LiteralPath $xamlPath -Raw

if ($content.Contains(
        'Search is implemented in V0.4.',
        [System.StringComparison]::Ordinal)) {
    throw "The disabled V0.4 search placeholder returned."
}

$listMatches = [regex]::Matches(
    $content,
    '<ListView(?=\s|>)(?<attributes>[^>]*)>',
    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

if ($listMatches.Count -ne 1) {
    throw "Expected exactly one production ListView, found $($listMatches.Count)."
}

$listAttributes = $listMatches[0].Groups["attributes"].Value

$requiredListTokens = @(
    'ItemsSource="{Binding DisplayedBookmarks}"',
    'VirtualizingPanel.IsVirtualizing="True"',
    'VirtualizingPanel.VirtualizationMode="Recycling"',
    'ScrollViewer.CanContentScroll="True"'
)

foreach ($token in $requiredListTokens) {
    if (-not $listAttributes.Contains(
            $token,
            [System.StringComparison]::Ordinal)) {
        throw "Search result ListView is missing required token: $token"
    }
}

$requiredUiTokens = @(
    'x:Name="SearchBox"',
    'Text="{Binding SearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"',
    'IsEnabled="{Binding CanSearchDocument}"',
    'SelectedValue="{Binding SearchScope, Mode=TwoWay}"',
    'Content="All bookmarks"',
    'Content="Current folder"',
    'Text="{Binding SearchSummaryText}"'
)

foreach ($token in $requiredUiTokens) {
    if (-not $content.Contains(
            $token,
            [System.StringComparison]::Ordinal)) {
        throw "Search UI contract is missing required token: $token"
    }
}

Write-Host "V0.4 search UI verification passed."
