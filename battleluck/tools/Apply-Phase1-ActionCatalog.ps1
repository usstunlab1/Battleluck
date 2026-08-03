# Apply Phase 1: Action Catalog Reconciliation
# Modifies blueluck-ai/resources/contracts/battleluck-actions.contract.json
# - Removes 11 deprecated duplicate actions
# - Adds 13 missing effect actions
# - Extends chest.spawn params
# - Resolves notify/notification/send_message
# - Adds heal aliases
# - Regenerates recordsJson + contentHash

param(
    [string]$ContractPath = [System.IO.Path]::Combine($PSScriptRoot, '..', '..', 'blueluck-ai', 'resources', 'contracts', 'battleluck-actions.contract.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ContractPath)) {
    throw "Contract not found: $ContractPath"
}

$doc = Get-Content -Raw -LiteralPath $ContractPath | ConvertFrom-Json -Depth 100

# Parse the records array from recordsJson
$records = $doc.recordsJson | ConvertFrom-Json -Depth 100 -NoEnumerate
if ($records -isnot [System.Array]) {
    throw 'recordsJson is not an array'
}

$recordsList = [System.Collections.Generic.List[object]]::new()
foreach ($r in $records) {
    $recordsList.Add($r)
}

# C-1: Remove 11 deprecated actions
$deprecated = @(
    'boss.spawn',
    'blood.set',
    'set_blood',
    'snapshot.restore_old',
    'snapshot.save_old',
    'warevent_end',
    'warevent_start',
    'spawn.npc',
    'disable_pvp',
    'enable_pvp',
    'spawnwave'
)

$removed = @()
$recordsList = [System.Collections.Generic.List[object]]::new(
    @($records | Where-Object { $id = $_.canonicalActionId; if ($deprecated -contains $id) { $removed += $id; $false } else { $true } })
)

Write-Output "Removed deprecated actions: $($removed -join ', ')"

# C-5: Resolve notify/notification/send_message - mark notify/send_message as legacyAliases of notification
foreach ($r in $recordsList) {
    if ($r.canonicalActionId -eq 'notify') {
        $r.legacyAliases = @('notify')
    }
    if ($r.canonicalActionId -eq 'send_message') {
        $r.legacyAliases = @('send_message')
    }
    if ($r.canonicalActionId -eq 'notification') {
        $existing = @($r.legacyAliases)
        $r.legacyAliases = @($existing + @('notify', 'send_message') | Select-Object -Unique)
        if (-not $r.metadata.aliases) { $r.metadata | Add-Member -NotePropertyName aliases -NotePropertyValue @() }
        $r.metadata.aliases = @($r.metadata.aliases + @('message') | Select-Object -Unique)
    }
}
Write-Output 'C-5: notify/send_message now legacyAliases of notification'

# C-6: Add heal aliases
foreach ($r in $recordsList) {
    if ($r.canonicalActionId -eq 'heal') {
        if (-not $r.metadata.aliases) { $r.metadata | Add-Member -NotePropertyName aliases -NotePropertyValue @() }
        $r.metadata.aliases = @($r.metadata.aliases + @('full health', 'restore health') | Select-Object -Unique)
    }
}
Write-Output 'C-6: heal aliases added'

# C-4: Extend chest.spawn params
foreach ($r in $recordsList) {
    if ($r.canonicalActionId -eq 'chest.spawn') {
        if (-not $r.metadata.optional) { $r.metadata | Add-Member -NotePropertyName optional -NotePropertyValue @() }
        $r.metadata.optional = @($r.metadata.optional + @('requiredKills', 'lootTable') | Select-Object -Unique)
    }
}
Write-Output 'C-4: chest.spawn params extended'

# C-2: Add 13 effect actions
$effectActions = @(
    @{ id = 'effect.assign'; category = 'effects'; requireApproval = $true; required = @('effectPrefab'); optional = @('target', 'durationSeconds'); desc = 'Assign a runtime visual or gameplay effect to a target.' },
    @{ id = 'effect.remove'; category = 'effects'; requireApproval = $false; required = @('effectPrefab'); optional = @('target'); desc = 'Remove a runtime visual or gameplay effect from a target.' },
    @{ id = 'effect.replace'; category = 'effects'; requireApproval = $true; required = @('oldPrefab', 'newPrefab'); optional = @('target'); desc = 'Replace one runtime effect with another.' },
    @{ id = 'effect.status'; category = 'effects'; requireApproval = $false; required = @(); optional = @('target'); desc = 'Query current runtime effect assignments.' },
    @{ id = 'effect.clear_group'; category = 'effects'; requireApproval = $true; required = @(); optional = @('group'); desc = 'Clear all runtime effects for a tracking group.' },
    @{ id = 'zone.border.effect.apply'; category = 'zone'; requireApproval = $true; required = @('zoneHash', 'effectPrefab'); optional = @(); desc = 'Apply a visual effect to a zone border.' },
    @{ id = 'zone.border.effect.remove'; category = 'zone'; requireApproval = $false; required = @('zoneHash'); optional = @('effectPrefab'); desc = 'Remove a visual effect from a zone border.' },
    @{ id = 'zone.border.effect.replace'; category = 'zone'; requireApproval = $true; required = @('zoneHash', 'oldPrefab', 'newPrefab'); optional = @(); desc = 'Replace a zone border visual effect.' },
    @{ id = 'zone.border.effect.status'; category = 'zone'; requireApproval = $false; required = @('zoneHash'); optional = @(); desc = 'Query zone border effects.' },
    @{ id = 'spawn.effect.assign'; category = 'effects'; requireApproval = $true; required = @('effectPrefab'); optional = @('spawnGroup'); desc = 'Assign a runtime effect to a spawned group.' },
    @{ id = 'spawn.effect.remove'; category = 'effects'; requireApproval = $false; required = @('effectPrefab'); optional = @('spawnGroup'); desc = 'Remove a runtime effect from a spawned group.' },
    @{ id = 'tracking.group.effect.apply'; category = 'effects'; requireApproval = $true; required = @('trackingGroup', 'effectPrefab'); optional = @(); desc = 'Apply an effect to all entities in a tracking group.' },
    @{ id = 'tracking.group.effect.remove'; category = 'effects'; requireApproval = $false; required = @('trackingGroup', 'effectPrefab'); optional = @(); desc = 'Remove an effect from all entities in a tracking group.' }
)

$existingIds = @($recordsList | ForEach-Object { $_.canonicalActionId })
$added = @()
foreach ($ea in $effectActions) {
    if ($existingIds -contains $ea.id) { continue }
    $action = [PSCustomObject]@{
        canonicalActionId = $ea.id
        legacyAliases     = @()
        metadata          = [PSCustomObject]@{
            category          = $ea.category
            riskLevel         = 'controlled'
            requiresApproval  = $ea.requireApproval
            aliases           = @()
            required          = $ea.required
            optional          = $ea.optional
            description       = $ea.desc
            handlerAvailable  = $true
        }
    }
    $recordsList.Add($action)
    $added += $ea.id
}
Write-Output "Added effect actions: $($added -join ', ')"

# Serialize records back to compact JSON (matching original formatter)
$newRecordsJson = $recordsList | ConvertTo-Json -Depth 100 -Compress

# Recompute contentHash = SHA256(recordsJson) lowercase hex
$sha = [System.Security.Cryptography.SHA256]::Create()
$hashBytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($newRecordsJson))
$newHash = [Convert]::ToHexString($hashBytes).ToLowerInvariant()

# Update the document
$doc.recordsJson = $newRecordsJson
$doc.contentHash = $newHash
$doc.sourceHash = $newHash  # source hash is deterministic of content in this export flow

# Write back with formatting matching the original file (2-space indent)
$doc | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $ContractPath -Encoding UTF8

Write-Output "Updated $ContractPath"
Write-Output "New contentHash: $newHash"
Write-Output "Records count: $($recordsList.Count)"
Write-Output 'Phase 1 (C-1,C-2,C-4,C-5,C-6) applied. Run validate-contracts.ps1 to verify.'