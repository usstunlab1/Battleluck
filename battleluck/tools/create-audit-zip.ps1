$repo = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $repo "release\BattleLuck-Full-Audit.zip"

$items = Get-ChildItem -LiteralPath $repo -Force |
    Where-Object {
        $_.Name -notin @(".git", "bin", "obj", ".vs", "BattleLuck-Full-Audit.zip")
    }

Compress-Archive `
    -LiteralPath $items.FullName `
    -DestinationPath $dest `
    -CompressionLevel Optimal `
    -Force

Get-Item $dest | Select-Object FullName, Length, LastWriteTime
