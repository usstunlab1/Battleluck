[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$output = [System.IO.Path]::GetFullPath($OutputDirectory, $repository)
[System.IO.Directory]::CreateDirectory($output) | Out-Null

function Invoke-GitLines {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $lines = & git -C $repository @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }

    return @($lines)
}

$untracked = Invoke-GitLines @('ls-files', '--others', '--exclude-standard')
$manifest = foreach ($relativePath in $untracked) {
    $absolutePath = Join-Path $repository $relativePath
    if (-not [System.IO.File]::Exists($absolutePath)) {
        continue
    }

    $file = Get-Item -LiteralPath $absolutePath
    [ordered]@{
        path = $relativePath.Replace('\\', '/')
        length = $file.Length
        sha256 = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$snapshot = [ordered]@{
    schema = 'battleluck.workspace-audit/v1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    head = (Invoke-GitLines @('rev-parse', 'HEAD'))[0]
    branch = (Invoke-GitLines @('branch', '--show-current'))[0]
    remotes = @(Invoke-GitLines @('remote', '-v'))
    worktrees = @(Invoke-GitLines @('worktree', 'list', '--porcelain'))
    stashes = @(Invoke-GitLines @('stash', 'list'))
    status = @(Invoke-GitLines @('status', '--short', '--branch', '--untracked-files=normal'))
    untrackedCount = @($manifest).Count
    untrackedFiles = @($manifest)
}

$jsonPath = Join-Path $output 'workspace-snapshot.json'
$snapshot | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

$manifestPath = Join-Path $output 'untracked-manifest.txt'
@($manifest | ForEach-Object { "{0}  {1}  {2}" -f $_.sha256, $_.length, $_.path }) |
    Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM

Write-Output "Workspace snapshot: $jsonPath"
Write-Output "Untracked manifest: $manifestPath"
Write-Output "Untracked files: $(@($manifest).Count)"
