param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [int]$ObservationSeconds = 5
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExecutablePath -PathType Leaf)) {
    throw "Executable not found: $ExecutablePath"
}

$process = Start-Process -FilePath $ExecutablePath -PassThru

try {
    Start-Sleep -Seconds $ObservationSeconds
    $process.Refresh()

    if ($process.HasExited) {
        throw "Application exited during smoke-test startup. ExitCode=$($process.ExitCode)"
    }

    Write-Host "Smoke test passed. Process stayed alive for $ObservationSeconds seconds."
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}
