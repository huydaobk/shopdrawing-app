param(
    [string]$RunnerRoot = 'C:\actions-runner-shopdrawing',
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$ensureRunnerScript = Join-Path $repoRoot 'scripts\ensure-runner.ps1'
$guardScript = Join-Path $repoRoot 'scripts\run-runner-guard.ps1'
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runValueName = 'ShopDrawingRunnerGuard'

if (-not (Test-Path $ensureRunnerScript)) {
    throw "Missing script: $ensureRunnerScript"
}

if (-not (Test-Path $guardScript)) {
    throw "Missing script: $guardScript"
}

$taskCommand = "powershell.exe -WindowStyle Hidden -NoProfile -ExecutionPolicy Bypass -File `"$guardScript`" -RunnerRoot `"$RunnerRoot`""

if ($Remove) {
    if (Get-ItemProperty -Path $runKeyPath -Name $runValueName -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $runKeyPath -Name $runValueName
    }

    Write-Host 'Runner autostart removed from HKCU Run.'
    exit 0
}

New-Item -Path $runKeyPath -Force | Out-Null
Set-ItemProperty -Path $runKeyPath -Name $runValueName -Value $taskCommand

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $ensureRunnerScript -RunnerRoot $RunnerRoot
Start-Process -FilePath 'powershell.exe' -ArgumentList @(
    '-WindowStyle', 'Hidden',
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', $guardScript,
    '-RunnerRoot', $RunnerRoot
) -WindowStyle Hidden | Out-Null

Write-Host 'Runner autostart installed in HKCU Run.'
Write-Host "Run value: $runValueName"
Write-Host "Command:   $taskCommand"
