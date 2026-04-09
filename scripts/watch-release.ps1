param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

function Invoke-Gh {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Args
    )

    & gh @Args
    if ($LASTEXITCODE -ne 0) {
        throw "gh $($Args -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$tagName = "v$Version"

Write-Host "Watching release workflow for $tagName..." -ForegroundColor Cyan

$runsJson = & gh run list --workflow release --limit 20 --json databaseId,headBranch
if ($LASTEXITCODE -ne 0) {
    throw "Unable to list release workflow runs."
}

$runs = $runsJson | ConvertFrom-Json
$matchingRuns = @($runs | Where-Object { $_.headBranch -eq $tagName })
$runId = if ($matchingRuns.Count -gt 0) { [string]$matchingRuns[0].databaseId } else { "" }

if (-not $runId) {
    throw "No release workflow run found for $tagName yet."
}

Invoke-Gh run watch $runId --exit-status
Invoke-Gh release view $tagName
