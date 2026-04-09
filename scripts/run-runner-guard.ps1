param(
    [string]$RunnerRoot = 'C:\actions-runner-shopdrawing',
    [int]$CheckIntervalSeconds = 60
)

$ErrorActionPreference = 'Stop'

$mutexName = 'Local\ShopDrawingRunnerGuard'
$createdNew = $false
$mutex = New-Object System.Threading.Mutex($true, $mutexName, [ref]$createdNew)

if (-not $createdNew) {
    Write-Host 'Runner guard is already active.'
    exit 0
}

try {
    $listenerPath = Join-Path $RunnerRoot 'bin\Runner.Listener.exe'
    $runCmd = Join-Path $RunnerRoot 'run.cmd'

    if (-not (Test-Path $listenerPath)) {
        throw "Runner listener not found: $listenerPath"
    }

    if (-not (Test-Path $runCmd)) {
        throw "Runner command not found: $runCmd"
    }

    while ($true) {
        $listener = Get-Process Runner.Listener -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -eq $listenerPath }

        if (-not $listener) {
            Start-Process -FilePath $runCmd -WorkingDirectory $RunnerRoot -WindowStyle Minimized | Out-Null
        }

        Start-Sleep -Seconds $CheckIntervalSeconds
    }
}
finally {
    $mutex.ReleaseMutex() | Out-Null
    $mutex.Dispose()
}
