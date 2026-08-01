[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [Parameter(Mandatory = $true)][string]$SourcePackage
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$source = (Resolve-Path -LiteralPath $SourcePackage).Path
$document = Get-Content -LiteralPath $source -Raw | ConvertFrom-Json -Depth 100

foreach ($required in 'contractName','schemaVersion','producer','producerVersion','sourceVersion','targetGameVersion','createdAtUtc','sourceHash','contentHash','records','validationResult') {
    if ($document.PSObject.Properties.Name -notcontains $required) { throw "Missing required contract field '$required'." }
}
if ([string]$document.producer -ne 'Bluecotrex') { throw "Expected producer 'Bluecotrex', received '$($document.producer)'." }
$version = [Version]([string]$document.schemaVersion)
if ($version.Major -ne 1) { throw "Unsupported contract major version '$version'." }
if (-not $document.validationResult.valid) { throw 'Bluecotrex export reports failed validation.' }

$recordsJson = ConvertTo-Json -InputObject @($document.records) -Depth 100 -Compress
$actualHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($recordsJson))).ToLowerInvariant()
if ($actualHash -ne [string]$document.contentHash) { throw "Content hash mismatch: expected $($document.contentHash), actual $actualHash." }

$safeName = ([string]$document.contractName).ToLowerInvariant() -replace '[^a-z0-9.-]', '-'
if ([string]::IsNullOrWhiteSpace($safeName)) { throw 'Contract name does not produce a safe file name.' }
$destinationRoot = [IO.Path]::GetFullPath((Join-Path $root 'Data\Imports\Bluecotrex'))
$expectedRoot = [IO.Path]::GetFullPath((Join-Path $root 'Data'))
if (-not $destinationRoot.StartsWith($expectedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved import destination escaped BattleLuck Data ownership.'
}
$destination = Join-Path $destinationRoot ($safeName + '.json')

if ($PSCmdlet.ShouldProcess($destination, "Import validated Bluecotrex contract '$($document.contractName)'")) {
    [IO.Directory]::CreateDirectory($destinationRoot) | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    Write-Output "Imported validated contract to $destination"
} else {
    Write-Output "Validated contract '$($document.contractName)' for $destination"
}
