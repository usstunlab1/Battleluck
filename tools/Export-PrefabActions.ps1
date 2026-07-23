param(
    [string]$ArchivePath = (Join-Path $PSScriptRoot "..\Data\render-prefabs.json"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\Data\prefab-actions.json")
)

$archive = Get-Content -LiteralPath $ArchivePath -Raw | ConvertFrom-Json
$actions = foreach ($property in $archive.prefabs.psobject.Properties) {
    $entry = $property.Value
    [ordered]@{
        action = "prefab.resolve"
        prefab = $entry.name
        prefabGuid = [int]$property.Name
        category = $entry.category
        description = "Resolve $($entry.name) to V Rising prefab GUID $($property.Name)."
    }
}

$actions | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Host "Exported $($actions.Count) prefab actions to $OutputPath"
