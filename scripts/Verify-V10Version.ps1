param(
    [string]$ProjectPath = "src/ChromeBookmarksManager/ChromeBookmarksManager.csproj",
    [string]$ExpectedTag,
    [string]$ExecutablePath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProjectPath -PathType Leaf)) {
    throw "Project file not found: $ProjectPath"
}

[xml]$project = Get-Content -LiteralPath $ProjectPath -Raw -Encoding UTF8
$version = [string]$project.Project.PropertyGroup.VersionPrefix

if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "VersionPrefix must be semantic MAJOR.MINOR.PATCH, found '$version'."
}

if ($version -ne "1.1.1") {
    throw "V1.1.1 contract expected VersionPrefix 1.1.1, found $version."
}

if ($ExpectedTag) {
    $expected = "v$version"
    if ($ExpectedTag -cne $expected) {
        throw "Release tag '$ExpectedTag' does not match project version '$expected'."
    }
}

if ($ExecutablePath) {
    if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
        throw "Executable not found: $ExecutablePath"
    }

    $info = (Get-Item -LiteralPath $ExecutablePath).VersionInfo
    if ($info.FileVersion -cne "$version.0") {
        throw "EXE FileVersion '$($info.FileVersion)' does not equal '$version.0'."
    }

    if ($info.ProductVersion -cne $version) {
        throw "EXE ProductVersion '$($info.ProductVersion)' does not equal '$version'."
    }
}

Write-Host "V1.1.1 version contract verified: $version"
