[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$destination = [System.IO.Path]::GetFullPath($OutputPath, (Get-Location).Path)
$destinationDirectory = Split-Path -Parent $destination
[System.IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null

function Relative-Path([string]$Path) {
    return [System.IO.Path]::GetRelativePath($root, $Path).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
}

function Get-TextFiles([string]$Filter) {
    return @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter $Filter |
        Where-Object { $_.FullName -notmatch '[\\/](artifacts|bin|obj|release|dist|node_modules|Library|Logs|Temp|\.external)[\\/]' })
}

function Get-DictionaryKeys([string]$Path, [string]$StartMarker, [string]$EndMarker) {
    if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return @() }
    $source = Get-Content -LiteralPath $Path -Raw
    $start = $source.IndexOf($StartMarker, [StringComparison]::Ordinal)
    if ($start -lt 0) { return @() }
    $end = $source.IndexOf($EndMarker, $start, [StringComparison]::Ordinal)
    if ($end -lt 0) { $end = $source.Length }
    return @([regex]::Matches($source.Substring($start, $end - $start), '\["([^"]+)"\]\s*=') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}

function Get-Sha256([string]$Text) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $hash = [Security.Cryptography.SHA256]::HashData($bytes)
    return [Convert]::ToHexString($hash).ToLowerInvariant()
}

$commandDeclarations = [Collections.Generic.List[object]]::new()
foreach ($file in Get-TextFiles '*.cs') {
    $source = Get-Content -LiteralPath $file.FullName -Raw
    if ($null -eq $source) { $source = '' }
    foreach ($surface in @(
        @{ Attribute = 'BattleLuckCommand'; Name = 'battleluck' },
        @{ Attribute = 'Command'; Name = 'vcf' }
    )) {
        $pattern = '\[' + [regex]::Escape($surface.Attribute) + '\s*\(\s*"([^"]+)"([^\]]*)\)\s*\]'
        foreach ($match in [regex]::Matches($source, $pattern, [Text.RegularExpressions.RegexOptions]::Singleline)) {
            $tail = $match.Groups[2].Value
            $commandDeclarations.Add([ordered]@{
                name = $match.Groups[1].Value.Trim().ToLowerInvariant()
                surface = $surface.Name
                adminOnly = $tail -match 'adminOnly\s*:\s*true'
                source = Relative-Path $file.FullName
            })
        }
    }
}

$dispatcherFile = Get-TextFiles 'BattleLuckCommandDispatcher.cs' | Select-Object -First 1
$dispatcherSource = if ($dispatcherFile) { [string](Get-Content -LiteralPath $dispatcherFile.FullName -Raw) } else { '' }
$dispatcherAllowlist = @([regex]::Matches($dispatcherSource, 'attr\.Name\.Equals\("([^"]+)"') |
    ForEach-Object { $_.Groups[1].Value.ToLowerInvariant() } | Sort-Object -Unique)

$pluginFile = Get-TextFiles 'BattleLuckPlugin.cs' | Select-Object -First 1
$pluginSource = if ($pluginFile) { [string](Get-Content -LiteralPath $pluginFile.FullName -Raw) } else { '' }
$vcfTypes = @([regex]::Matches($pluginSource, 'CommandRegistry\.RegisterCommandType\(typeof\(([^\)]+)\)\)') |
    ForEach-Object { ($_.Groups[1].Value -split '\.')[-1] } | Sort-Object -Unique)

$commands = @($commandDeclarations | ForEach-Object {
    $isReachable = if ($_.surface -eq 'battleluck') {
        $dispatcherAllowlist -contains $_.name
    } else {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($_.source)
        $vcfTypes -contains $baseName
    }
    [ordered]@{
        name = $_.name
        surface = $_.surface
        adminOnly = $_.adminOnly
        reachable = $isReachable
        source = $_.source
    }
} | Sort-Object surface, name, source)

$commandDuplicateGroups = @($commands | Group-Object surface, name | Where-Object Count -gt 1 | ForEach-Object {
    [ordered]@{ key = $_.Name; count = $_.Count; sources = @($_.Group.source | Sort-Object -Unique) }
})

$catalogFile = Get-ChildItem -LiteralPath $root -Recurse -File -Filter actions_catalog.json |
    Where-Object { $_.FullName.Replace([System.IO.Path]::DirectorySeparatorChar, '/') -match '/config/BattleLuck/actions_catalog\.json$' } |
    Select-Object -First 1
if (-not $catalogFile) { throw "Could not find config/BattleLuck/actions_catalog.json below $root." }
$catalog = Get-Content -LiteralPath $catalogFile.FullName -Raw | ConvertFrom-Json -Depth 100
$registered = @($catalog.registered | ForEach-Object { ([string]$_).Trim().ToLowerInvariant() } | Sort-Object -Unique)
$metadataNames = @($catalog.metadata.PSObject.Properties.Name | ForEach-Object { $_.ToLowerInvariant() } | Sort-Object -Unique)

$executorFile = Get-TextFiles 'FlowActionExecutor.cs' | Select-Object -First 1
$executorSource = if ($executorFile) { [string](Get-Content -LiteralPath $executorFile.FullName -Raw) } else { '' }
$executorCases = @([regex]::Matches($executorSource, 'case\s+"([^"]+)"') |
    ForEach-Object { $_.Groups[1].Value.ToLowerInvariant() } |
    Where-Object { $registered -contains $_ } | Sort-Object -Unique)

$manifestFile = Get-TextFiles 'ActionManifestService.cs' | Select-Object -First 1
$catalogOnly = @()
if ($manifestFile) {
    $manifestSource = Get-Content -LiteralPath $manifestFile.FullName -Raw
    $start = $manifestSource.IndexOf('public static readonly IReadOnlySet<string> CatalogOnlyActions', [StringComparison]::Ordinal)
    if ($start -ge 0) {
        $end = $manifestSource.IndexOf('};', $start, [StringComparison]::Ordinal)
        if ($end -gt $start) {
            $catalogOnly = @([regex]::Matches($manifestSource.Substring($start, $end - $start), '"([^"]+)"') |
                ForEach-Object { $_.Groups[1].Value.ToLowerInvariant() } | Sort-Object -Unique)
        }
    }
}

$nativeFile = Get-TextFiles 'BattleLuckNativeActionCatalog.cs' | Select-Object -First 1
$runtimeFile = Get-TextFiles 'RuntimeEffectActionCatalog.cs' | Select-Object -First 1
$native = @(Get-DictionaryKeys $nativeFile.FullName 'public static readonly IReadOnlyDictionary<string, Descriptor> Entries' 'public static bool IsNativeAction')
$runtime = @(Get-DictionaryKeys $runtimeFile.FullName 'public static readonly IReadOnlyDictionary<string, Descriptor> Entries' 'public static void InjectEntries')

$actions = [Collections.Generic.List[object]]::new()
foreach ($name in $registered) {
    $property = $catalog.metadata.PSObject.Properties | Where-Object Name -IEQ $name | Select-Object -First 1
    $entry = $property.Value
    $actions.Add([ordered]@{
        name = $name
        source = 'configured'
        handlerAvailable = $executorCases -contains $name
        catalogOnly = $catalogOnly -contains $name
        metadataPresent = $null -ne $property
        riskLevel = if ($entry -and $entry.PSObject.Properties.Name -contains 'riskLevel') { [string]$entry.riskLevel } else { '' }
        requiresApproval = if ($entry -and $entry.PSObject.Properties.Name -contains 'requiresApproval') { [bool]$entry.requiresApproval } else { $false }
    })
}
foreach ($name in $native) {
    $actions.Add([ordered]@{ name = $name; source = 'native'; handlerAvailable = $true; catalogOnly = $false; metadataPresent = $true; riskLevel = 'source-defined'; requiresApproval = $null })
}
foreach ($name in $runtime) {
    $actions.Add([ordered]@{ name = $name; source = 'runtime-effect'; handlerAvailable = $true; catalogOnly = $false; metadataPresent = $true; riskLevel = 'source-defined'; requiresApproval = $null })
}
$actions = @($actions | Sort-Object source, name)

$allActionNames = @($actions.name | Sort-Object -Unique)
$eventReferences = [Collections.Generic.List[object]]::new()
$eventFiles = Get-TextFiles '*.json' | Where-Object {
    $_.FullName.Replace([System.IO.Path]::DirectorySeparatorChar, '/') -match '/config/BattleLuck/events/'
}
foreach ($file in $eventFiles) {
    try { $jsonText = Get-Content -LiteralPath $file.FullName -Raw; $null = $jsonText | ConvertFrom-Json -Depth 100 } catch { continue }
    foreach ($name in $allActionNames) {
        if ($jsonText -match '(?i)["'']' + [regex]::Escape($name) + '["'']') {
            $eventReferences.Add([ordered]@{ action = $name; source = Relative-Path $file.FullName })
        }
    }
}
$eventReferences = @($eventReferences | Sort-Object action, source -Unique)

$semantic = [ordered]@{
    commands = @($commands | ForEach-Object { [ordered]@{ name = $_.name; surface = $_.surface; adminOnly = $_.adminOnly; reachable = $_.reachable } })
    actions = @($actions | ForEach-Object { [ordered]@{ name = $_.name; source = $_.source; handlerAvailable = $_.handlerAvailable; catalogOnly = $_.catalogOnly; metadataPresent = $_.metadataPresent; riskLevel = $_.riskLevel; requiresApproval = $_.requiresApproval } })
    eventReferences = @($eventReferences)
}
$semanticJson = $semantic | ConvertTo-Json -Depth 12 -Compress

$inventory = [ordered]@{
    schema = 'battleluck.command-action-inventory/v1'
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    semanticSha256 = Get-Sha256 $semanticJson
    projectRootName = Split-Path -Leaf $root
    commands = [ordered]@{
        declarationCount = $commands.Count
        uniqueNames = @($commands.name | Sort-Object -Unique).Count
        reachableDeclarationCount = @($commands | Where-Object reachable).Count
        unreachableDeclarationCount = @($commands | Where-Object { -not $_.reachable }).Count
        duplicateGroups = $commandDuplicateGroups
        records = $commands
    }
    actions = [ordered]@{
        configuredCount = $registered.Count
        metadataCount = $metadataNames.Count
        executorHandlerCount = $executorCases.Count
        catalogOnlyCount = $catalogOnly.Count
        nativeCount = $native.Count
        runtimeEffectCount = $runtime.Count
        totalUniqueCount = $allActionNames.Count
        missingMetadata = @($registered | Where-Object { $metadataNames -notcontains $_ })
        orphanMetadata = @($metadataNames | Where-Object { $registered -notcontains $_ })
        missingConfiguredClassification = @($registered | Where-Object { ($executorCases -notcontains $_) -and ($catalogOnly -notcontains $_) })
        conflictingConfiguredClassification = @($registered | Where-Object { ($executorCases -contains $_) -and ($catalogOnly -contains $_) })
        crossSurfaceDuplicates = @($actions | Group-Object name | Where-Object Count -gt 1 | ForEach-Object Name)
        records = $actions
        eventReferences = $eventReferences
    }
}

$inventory | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $destination -Encoding utf8NoBOM
Write-Output "Inventory: $destination"
Write-Output "Semantic SHA-256: $($inventory.semanticSha256)"
Write-Output "Commands: declarations=$($commands.Count), reachable=$(@($commands | Where-Object reachable).Count), unique=$(@($commands.name | Sort-Object -Unique).Count)"
Write-Output "Actions: configured=$($registered.Count), handlers=$($executorCases.Count), catalogOnly=$($catalogOnly.Count), native=$($native.Count), runtime=$($runtime.Count), totalUnique=$($allActionNames.Count)"
