param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "artifacts/win-x64"
)

$ErrorActionPreference = "Stop"
$project = "src/ChromeBookmarksManager/ChromeBookmarksManager.csproj"

if (Test-Path $OutputDirectory) {
    Remove-Item -Recurse -Force $OutputDirectory
}

New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

dotnet publish $project --configuration $Configuration --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false --output $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}
