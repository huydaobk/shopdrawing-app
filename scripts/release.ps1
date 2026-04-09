param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [switch]$PushLatestCommit,

    [switch]$SkipTagPush,

    [switch]$AllowDirty
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Git {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Args
    )

    & git @Args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Args -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$tagName = "v$Version"

Write-Step "Checking git state"
$currentBranch = (git branch --show-current).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to detect current branch."
}

if ([string]::IsNullOrWhiteSpace($currentBranch)) {
    throw "Detached HEAD is not supported for release. Checkout the release branch first."
}

$statusOutput = git status --short
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read git status."
}

if (-not $AllowDirty -and -not [string]::IsNullOrWhiteSpace(($statusOutput | Out-String))) {
    throw "Working tree is dirty. Commit or stash changes before releasing, or rerun with -AllowDirty if that is intentional."
}

Write-Host "Branch: $currentBranch"
Write-Host "Tag:    $tagName"

Write-Step "Checking remote state"
Invoke-Git fetch origin --tags

$localHead = (git rev-parse HEAD).Trim()
$remoteHead = (git rev-parse "origin/$currentBranch").Trim()

if ($localHead -ne $remoteHead) {
    if ($PushLatestCommit) {
        Write-Step "Pushing branch $currentBranch"
        Invoke-Git push origin $currentBranch
    }
    else {
        throw "Local branch is ahead/behind origin/$currentBranch. Push or sync first, or rerun with -PushLatestCommit."
    }
}

$existingLocalTag = git tag --list $tagName
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect local tags."
}

if (-not [string]::IsNullOrWhiteSpace(($existingLocalTag | Out-String))) {
    throw "Tag $tagName already exists locally."
}

$existingRemoteTag = git ls-remote --tags origin $tagName
if ($LASTEXITCODE -ne 0) {
    throw "Unable to inspect remote tags."
}

if (-not [string]::IsNullOrWhiteSpace(($existingRemoteTag | Out-String))) {
    throw "Tag $tagName already exists on origin."
}

Write-Step "Creating tag $tagName"
Invoke-Git tag $tagName

if (-not $SkipTagPush) {
    Write-Step "Pushing tag $tagName"
    Invoke-Git push origin $tagName
    Write-Host "Release trigger sent. GitHub Actions will build and publish assets for $tagName."
}
else {
    Write-Host "Tag created locally only. Push it later with: git push origin $tagName"
}

