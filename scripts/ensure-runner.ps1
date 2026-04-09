param(
    [string]$RunnerRoot = 'C:\actions-runner-shopdrawing'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $RunnerRoot)) {
    throw "Runner root not found: $RunnerRoot"
}

$runnerConfigPath = Join-Path $RunnerRoot '.runner'
if (-not (Test-Path $runnerConfigPath)) {
    throw "Runner is not configured at $RunnerRoot"
}

$listener = Get-Process Runner.Listener -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like (Join-Path $RunnerRoot 'bin\Runner.Listener.exe') }

if ($listener) {
    Write-Host "Runner already running at $RunnerRoot (PID $($listener.Id))."
    exit 0
}

$runCmd = Join-Path $RunnerRoot 'run.cmd'
if (-not (Test-Path $runCmd)) {
    throw "run.cmd not found at $runCmd"
}

Start-Process -FilePath $runCmd -WorkingDirectory $RunnerRoot -WindowStyle Minimized | Out-Null
Write-Host "Runner started from $RunnerRoot."
