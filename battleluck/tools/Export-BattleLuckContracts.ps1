[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$DestinationDirectory,
    [Parameter(Mandatory = $true)][string]$TargetGameVersion,
    [string]$ProducerVersion = '1.1.2'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$destination = [IO.Path]::GetFullPath($DestinationDirectory)
[IO.Directory]::CreateDirectory($destination) | Out-Null
$createdAt = [DateTime]::UtcNow.ToString('o')

function Relative([string]$Path) { [IO.Path]::GetRelativePath($root, $Path).Replace([IO.Path]::DirectorySeparatorChar, '/') }
function Text-Hash([string]$Text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text))).ToLowerInvariant() }
function Combined-Source-Hash([string[]]$Paths) {
    $basis = @($Paths | Sort-Object | ForEach-Object { "$(Relative $_)`t$((Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant())" }) -join "`n"
    Text-Hash $basis
}
function Write-Contract([string]$Name, [object[]]$Records, [string[]]$Sources, [string[]]$Warnings = @()) {
    $recordsJson = ConvertTo-Json -InputObject @($Records) -Depth 100 -Compress
    $document = [ordered]@{
        contractName = $Name
        schemaVersion = '1.0.0'
        producer = 'BattleLuck'
        producerVersion = $ProducerVersion
        sourceVersion = $ProducerVersion
        targetGameVersion = $TargetGameVersion
        createdAtUtc = $createdAt
        sourceHash = Combined-Source-Hash $Sources
        contentHash = Text-Hash $recordsJson
        sourceFiles = @($Sources | ForEach-Object { Relative $_ } | Sort-Object -Unique)
        records = @($Records)
        validationResult = [ordered]@{ valid = $true; warnings = @($Warnings); errors = @() }
    }
    $fileName = $Name.Replace('.', '-') + '.contract.json'
    $document | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $destination $fileName) -Encoding utf8NoBOM
    Write-Output $fileName
}

$prefabPath = Join-Path $root 'Data\Prefabs\render-prefabs.json'
$prefabDocument = Get-Content -LiteralPath $prefabPath -Raw | ConvertFrom-Json -Depth 100
$prefabRecords = @($prefabDocument.prefabs.PSObject.Properties | Sort-Object Name | ForEach-Object {
    [ordered]@{ prefabGuid = [int]$_.Name; data = $_.Value }
})

$actionPath = Join-Path $root 'config\BattleLuck\actions_catalog.json'
$actionDocument = Get-Content -LiteralPath $actionPath -Raw | ConvertFrom-Json -Depth 100
$legacyByTarget = @{}
foreach ($mapping in $actionDocument.llm_guidance.legacy_mappings.PSObject.Properties) {
    $target = ([string]$mapping.Value).ToLowerInvariant()
    if (-not $legacyByTarget.ContainsKey($target)) { $legacyByTarget[$target] = @() }
    $legacyByTarget[$target] += $mapping.Name
}
$actionRecords = @($actionDocument.metadata.PSObject.Properties | Sort-Object Name | ForEach-Object {
    $canonical = $_.Name.ToLowerInvariant()
    [ordered]@{ canonicalActionId = $canonical; legacyAliases = @($legacyByTarget[$canonical] | Sort-Object -Unique); metadata = $_.Value }
})

$inventoryTool = Join-Path $root 'tools\Export-CommandActionInventory.ps1'
$inventoryPath = Join-Path $destination '.command-action-inventory.tmp.json'
& $inventoryTool -ProjectRoot $root -OutputPath $inventoryPath | Out-Null
$commandInventory = Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json -Depth 100
$commandRecords = @($commandInventory.commands.records | Sort-Object surface, name, source)

$eventPaths = @(Get-ChildItem -LiteralPath (Join-Path $root 'config\BattleLuck\events') -File -Filter *.json | Sort-Object Name)
$eventRecords = @($eventPaths | ForEach-Object {
    [ordered]@{ eventId = $_.BaseName; document = (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json -Depth 100) }
})

$schemaPaths = @(Get-ChildItem -LiteralPath (Join-Path $root 'Data\Contracts') -File -Filter *.schema.json | Sort-Object Name)
$schemaRecords = @($schemaPaths | ForEach-Object {
    [ordered]@{ name = $_.Name; document = (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json -Depth 100) }
})

$written = @()
$written += Write-Contract 'battleluck.prefabs' $prefabRecords @($prefabPath)
$written += Write-Contract 'battleluck.actions' $actionRecords @($actionPath)
$written += Write-Contract 'battleluck.commands' $commandRecords @($inventoryTool)
$written += Write-Contract 'battleluck.events' $eventRecords @($eventPaths.FullName)
$written += Write-Contract 'battleluck.schemas' $schemaRecords @($schemaPaths.FullName)
$written += Write-Contract 'battleluck.localization' @() @($prefabPath) @('No standalone localization source is present; prefab identity exports retain internal names only.')

Remove-Item -LiteralPath $inventoryPath -Force
Write-Output "Exported $($written.Count) contracts to $destination"
