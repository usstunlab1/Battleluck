param(
    [string]$ReferencePath = "docs/reference/kindredextract-reference.json",
    [string]$TemplatePath = ".external/KindredExtract/SystemsQueryExtraction.tt",
    [string]$CsvPath = "docs/reference/kindredextract-systems.csv",
    [string]$PromptPath = "docs/reference/kindredextract-systems-prompt.md"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path

function Resolve-RepoPath([string]$path) {
    if ([System.IO.Path]::IsPathRooted($path)) { return $path }
    return Join-Path $repoRoot $path
}

$referenceFullPath = Resolve-RepoPath $ReferencePath
$templateFullPath = Resolve-RepoPath $TemplatePath
$csvFullPath = Resolve-RepoPath $CsvPath
$promptFullPath = Resolve-RepoPath $PromptPath

$sourceLabel = "docs/reference/kindredextract-reference.json"
$systems = @()
if (Test-Path $templateFullPath) {
    $template = Get-Content $templateFullPath -Raw
    $systemBlock = ([regex]::Match($template, 'string\[\] systemTypes\s*=\s*\{(?s)(.*?)\};')).Groups[1].Value
    $systems = @([regex]::Matches($systemBlock, '"([^"]+)"') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $sourceLabel = "KindredExtract/SystemsQueryExtraction.tt"
} elseif (Test-Path $referenceFullPath) {
    $snapshot = Get-Content $referenceFullPath -Raw | ConvertFrom-Json
    $systems = @($snapshot.systemTypes | ForEach-Object { [string]$_ } | Sort-Object -Unique)
} else {
    throw "Neither SystemsQueryExtraction.tt nor the reference snapshot exists. Clone KindredExtract or run the reference extractor first."
}

if ($systems.Count -eq 0) {
    throw "No systems were found in the KindredExtract template or reference snapshot."
}

function Get-PurposeHint([string]$name) {
    $value = $name.ToLowerInvariant()
    if ($value -match 'ability|cast|spell') { return "ability/casting" }
    if ($value -match 'combat|damage|attack|hit|projectile|weapon') { return "combat/damage" }
    if ($value -match 'ai|behaviour|behavior|path|move|navigation') { return "AI/navigation" }
    if ($value -match 'buff|debuff|status|effect') { return "buffs/effects" }
    if ($value -match 'inventory|item|equipment|loot|container|slot') { return "inventory/items" }
    if ($value -match 'castle|building|territory|door|tile|room|servant') { return "castle/building" }
    if ($value -match 'network|steam|eos|connection|user|serverbootstrap|teleport') { return "networking" }
    if ($value -match 'save|persist|serialize|deserialize|load') { return "persistence" }
    if ($value -match 'bake|conversion|transform') { return "baking/conversion" }
    if ($value -match 'render|presentation|camera|audio|visual|ui') { return "presentation" }
    if ($value -match 'spawn|prefab|vblood|blood|unit') { return "entities/spawn" }
    if ($value -match 'sequence|event|trigger|update|timer') { return "events/timing" }
    if ($value -match 'group|barrier') { return "scheduling/group boundary" }
    return "unknown; research required"
}

function Get-TickHint([string]$name) {
    $value = $name.ToLowerInvariant()
    if ($value -match 'fixedstep') { return "fixed-step hint" }
    if ($value -match 'presentation|render|camera|audio') { return "presentation hint" }
    if ($value -match 'initializ|bake|conversion') { return "initialization/baking hint" }
    if ($value -match 'destroy|cleanup|ondestroy') { return "destroy/cleanup hint" }
    if ($value -match 'spawn|oncreate') { return "spawn lifecycle hint" }
    if ($value -match 'server') { return "server-world hint" }
    if ($value -match 'client') { return "client-world hint" }
    if ($value -match 'group|barrier') { return "system-group boundary hint" }
    if ($value -match 'update|simulation|tick|event') { return "simulation/update hint" }
    return "unknown; inspect UpdateInGroup/runtime schedule"
}

$rows = foreach ($system in $systems) {
    $lastDot = $system.LastIndexOf('.')
    $namespace = if ($lastDot -gt 0) { $system.Substring(0, $lastDot) } else { "" }
    $typeName = if ($lastDot -gt 0) { $system.Substring($lastDot + 1) } else { $system }
    $lower = $system.ToLowerInvariant()
    $side = if ($lower -match '(^|[_\.])server($|[_\.])|server$') { "server" }
        elseif ($lower -match '(^|[_\.])client($|[_\.])|client$') { "client" }
        else { "shared/unknown" }
    $kind = if ($typeName -match 'Group$') { "group" }
        elseif ($typeName -match 'Barrier$') { "barrier" }
        elseif ($typeName -match 'System$') { "system" }
        else { "type/reference" }

    [pscustomobject]@{
        system_name = $system
        namespace = $namespace
        type_name = $typeName
        system_kind = $kind
        side_hint = $side
        purpose_hint = Get-PurposeHint $system
        tick_hint = Get-TickHint $system
        evidence = "type name heuristic only; verify Unity UpdateInGroup/order and live world"
        needs_runtime_verification = $true
        source = "https://github.com/Odjit/KindredExtract/blob/main/SystemsQueryExtraction.tt"
    }
}

$csvDirectory = Split-Path $csvFullPath -Parent
$promptDirectory = Split-Path $promptFullPath -Parent
New-Item -ItemType Directory -Force -Path $csvDirectory, $promptDirectory | Out-Null
$rows | Export-Csv -Path $csvFullPath -NoTypeInformation -Encoding UTF8

$prompt = @'
# KindredExtract Unity system/tick research prompt

You are reviewing `kindredextract-systems.csv`, generated from the
[Odjit/KindredExtract](https://github.com/Odjit/KindredExtract) reference list
for a V Rising Unity ECS server. The CSV contains SYSTEM_COUNT_PLACEHOLDER system/type names and
name-based hints only; it is not proof of runtime scheduling.

For each system, research and verify:

1. The full Unity/ProjectM type and whether it exists in the server world.
2. The actual purpose and the components/queries it reads or writes.
3. The real update group (`InitializationSystemGroup`, simulation group,
   fixed-step group, presentation group, or a ProjectM group), ordering, and
   whether it runs server, client, or shared.
4. Whether it is safe to observe from a server plugin and the correct tick/main
   thread boundary for an approved BattleLuck action.

Do not infer an exact tick rate from a name. Mark unknown values as `unknown` and
cite the assembly/source or a live KindredExtract dump. Keep the output bounded:
return a corrected CSV with the original `system_name`, verified purpose,
`world`, `update_group`, `order`, `tick_semantics`, `evidence`, and
`confidence` columns. Never propose arbitrary reflection or direct mutation of
an unverified native system.

Timing for BattleLuck sequences must use validated `wait:<seconds>` and
`tick:<event-second>` markers; the server main-thread dispatcher remains the
mutation boundary.
'@
$prompt = $prompt.Replace("SYSTEM_COUNT_PLACEHOLDER", $systems.Count.ToString())
Set-Content -Path $promptFullPath -Value $prompt -Encoding UTF8

Write-Output "Wrote $csvFullPath ($($rows.Count) systems from $sourceLabel)"
Write-Output "Wrote $promptFullPath"
