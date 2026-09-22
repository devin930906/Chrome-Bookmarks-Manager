param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Executable not found: $ExecutablePath"
}

$name = Split-Path -Leaf $ExecutablePath
if ($name -cne "ChromeBookmarksManager.exe") {
    throw "Unexpected executable name: $name"
}

$hash = (Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash.ToLowerInvariant()
$outputPath = "$ExecutablePath.sha256"
Set-Content -LiteralPath $outputPath -Value "$hash  $name" -Encoding ascii

Write-Host "SHA256 created: $outputPath"
