using System.Collections.Concurrent;
using System.Globalization;
using BattleLuck.Core;
using BattleLuck.Models;
using BattleLuck.Services.Flow;
using BattleLuck.Services.Geometry;
using BattleLuck.Utilities;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace BattleLuck.Services.Runtime;

/// <summary>
/// BattleLuck-owned action extensions. This is deliberately one in-project
/// registration and execution surface with no command or runtime dependency
/// on an external mod.
///
/// Every mutating action is server-authoritative, session-owned, main-thread
/// only, and uses native replication. The UI may describe these actions but
/// never invokes them directly.
/// </summary>
public static class BattleLuckNativeActionCatalog
{
    public sealed record Descriptor(
        string Name,
        string Category,
        string Description,
        string RiskLevel,
        bool IsMutating,
        string[] Required,
        string[] Optional,
        string[] Aliases,
        string[] Examples);

    sealed record NativeSignRecord(
        string SignId,
        string SessionId,
        string TrackingGroup,
        string Prefab,
        int ZoneHash,
        ulong OwnerSteamId,
        float3 Position,
        Entity Entity);

    static readonly IReadOnlyDictionary<string, string> BuffPresets =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["chaos"] = "Buff_General_Ignite",
            ["chill"] = "Buff_General_Chill",
            ["shock"] = "Buff_General_Shock",
            ["slow"] = "Buff_General_Slow",
            ["stun"] = "Buff_General_Stun",
            ["blood_mend"] = "Buff_BloodMend",
            ["combat"] = "Buff_InCombat"
        };

    static readonly IReadOnlyDictionary<string, string> SignPresets =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["basic"] = "TM_Sign01",
            ["basic_2"] = "TM_Sign02",
            ["basic_3"] = "TM_Sign03",
            ["snow"] = "TM_Sign_Snow_01",
            ["silver"] = "TM_Silverhills_Sign01",
            ["stable"] = "TM_Castle_ObjectDecor_StableSign01",
            ["banner"] = "TM_DraculasCastle_Banner_Clean01"
        };

    static readonly ConcurrentDictionary<string, NativeSignRecord> ActiveSigns =
        new(StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyDictionary<string, Descriptor> Entries =
        new Dictionary<string, Descriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["actions.search"] = View(
                "actions.search", "actions", "Search executable BattleLuck actions without executing one.",
                optional: new[] { "query", "limit" },
                aliases: new[] { "find actions", "list actions" },
                examples: new[] { "actions.search:query=loot" }),
            ["loot.status"] = View(
                "loot.status", "loot", "Show the active zone loot table and reward settings.",
                examples: new[] { "loot.status" }),
            ["buff.preset.list"] = View(
                "buff.preset.list", "buff", "List BattleLuck's allowlisted native buff presets.",
                aliases: new[] { "list buff presets" },
                examples: new[] { "buff.preset.list" }),
            ["sign.preset.list"] = View(
                "sign.preset.list", "sign", "List allowlisted native sign visuals.",
                aliases: new[] { "list sign presets" },
                examples: new[] { "sign.preset.list" }),
            ["sign.status"] = View(
                "sign.status", "sign", "Show tracked signs owned by the active session.",
                examples: new[] { "sign.status" }),
            ["weapon.search"] = View(
                "weapon.search", "weapon", "Search live native Item_Weapon prefabs.",
                optional: new[] { "query", "limit" },
                aliases: new[] { "find weapon", "search weapons" },
                examples: new[] { "weapon.search:query=greatsword" }),
            ["zone.status"] = View(
                "zone.status", "zone", "Show the active BattleLuck zone without changing it.",
                examples: new[] { "zone.status" }),
            ["geometry.status"] = View(
                "geometry.status", "geometry", "Show the active Blueprint geometry build state.",
                examples: new[] { "geometry.status" }),
            ["geometry.catalog.search"] = View(
                "geometry.catalog.search", "geometry", "Search safe native tile prefabs from BattleLuck's shared prefab database.",
                optional: new[] { "query", "limit" },
                examples: new[] { "geometry.catalog.search:query=wall|limit=10" }),
            ["build.asset.search"] = View(
                "build.asset.search", "geometry", "Search the same BattleLuck prefab database used by legacy palettes and Blueprint imports; returns native tile and chest assets.",
                optional: new[] { "query", "kind", "limit" },
                aliases: new[] { "search build assets", "find floor block", "find chest" },
                examples: new[] { "build.asset.search:query=floor|kind=tile|limit=10", "build.asset.search:query=worldchest|kind=chest" }),

            ["loot.grant"] = Mutation(
                "loot.grant", "loot", "Grant one validated item or apply one validated buff reward.",
                required: new[] { "prefab", "rewardId" },
                optional: new[] { "amount", "durationSeconds" },
                aliases: new[] { "grant loot", "give reward" },
                examples: new[] { "loot.grant:prefab=Item_Consumable_GlassBottle_BloodRosePotion_T01|amount=1|rewardId=round_1" }),
            ["loot.roll"] = Mutation(
                "loot.roll", "loot", "Deterministically roll the active zone's weighted loot table once per reward id.",
                required: new[] { "rewardId" },
                aliases: new[] { "roll loot", "roll reward" },
                examples: new[] { "loot.roll:rewardId=wave_3_clear" }),
            ["loot.kit.grant"] = Mutation(
                "loot.kit.grant", "loot", "Grant a configured kit as an additive reward without clearing the current loadout.",
                required: new[] { "kitId", "rewardId" },
                aliases: new[] { "grant reward kit" },
                examples: new[] { "loot.kit.grant:kitId=trials|rewardId=trial_complete" }),
            ["buff.preset.apply"] = Mutation(
                "buff.preset.apply", "buff", "Apply an allowlisted BattleLuck buff preset to the acting player.",
                required: new[] { "preset" },
                optional: new[] { "durationSeconds" },
                aliases: new[] { "apply preset buff", "apply chaos buff" },
                examples: new[] { "buff.preset.apply:preset=chaos|durationSeconds=20" }),
            ["buff.preset.remove"] = Mutation(
                "buff.preset.remove", "buff", "Remove an allowlisted BattleLuck buff preset from the acting player.",
                required: new[] { "preset" },
                aliases: new[] { "remove preset buff" },
                examples: new[] { "buff.preset.remove:preset=chaos" }),
            ["zone.buff.preset.apply"] = Mutation(
                "zone.buff.preset.apply", "zone", "Apply an allowlisted buff to active session participants only.",
                required: new[] { "preset" },
                optional: new[] { "durationSeconds" },
                aliases: new[] { "buff event players" },
                examples: new[] { "zone.buff.preset.apply:preset=chill|durationSeconds=15" }),
            ["zone.buff.preset.remove"] = Mutation(
                "zone.buff.preset.remove", "zone", "Remove an allowlisted buff from active session participants only.",
                required: new[] { "preset" },
                aliases: new[] { "clear event player buff" },
                examples: new[] { "zone.buff.preset.remove:preset=chill" }),
            ["sign.spawn"] = Mutation(
                "sign.spawn", "sign", "Spawn one allowlisted, session-tracked native sign visual inside the active zone.",
                required: new[] { "signId" },
                optional: new[] { "preset", "position", "offset", "rotation" },
                aliases: new[] { "place sign", "spawn sign" },
                examples: new[] { "sign.spawn:signId=entrance|preset=banner|position=zone" },
                risk: "controlled"),
            ["sign.clear"] = Mutation(
                "sign.clear", "sign", "Remove one sign tracking group owned by the active session.",
                required: new[] { "signId" },
                aliases: new[] { "remove sign", "clear sign" },
                examples: new[] { "sign.clear:signId=entrance" },
                risk: "controlled"),
            ["weapon.grant"] = Mutation(
                "weapon.grant", "weapon", "Grant a validated native Item_Weapon prefab.",
                required: new[] { "prefab", "rewardId" },
                optional: new[] { "amount" },
                aliases: new[] { "give weapon", "grant weapon" },
                examples: new[] { "weapon.grant:prefab=Item_Weapon_GreatSword_T08_Sanguine|amount=1|rewardId=arena_weapon" }),
            ["weapon.kit.grant"] = Mutation(
                "weapon.kit.grant", "weapon", "Grant only the weapon entries from a configured BattleLuck kit.",
                required: new[] { "kitId", "rewardId" },
                aliases: new[] { "give weapon kit" },
                examples: new[] { "weapon.kit.grant:kitId=bloodbath|rewardId=bloodbath_weapons" }),
            ["zone.marker.spawn"] = Mutation(
                "zone.marker.spawn", "zone", "Spawn one allowlisted sign visual at the active zone center.",
                optional: new[] { "preset", "rotation" },
                aliases: new[] { "mark zone", "place zone sign" },
                examples: new[] { "zone.marker.spawn:preset=banner" },
                risk: "controlled"),
            ["zone.marker.clear"] = Mutation(
                "zone.marker.clear", "zone", "Remove the active zone's BattleLuck-owned marker.",
                aliases: new[] { "clear zone marker" },
                examples: new[] { "zone.marker.clear" },
                risk: "controlled"),
            ["geometry.adjust"] = Mutation(
                "geometry.adjust", "geometry", "Move or rotate one existing Blueprint geometry entity; never spawns a missing entity.",
                required: new[] { "id" }, optional: new[] { "offset", "rotation" },
                examples: new[] { "geometry.adjust:id=north_gate|offset=2,0,-8|rotation=90" }),
            ["geometry.delete"] = Mutation(
                "geometry.delete", "geometry", "Delete one existing Blueprint geometry entity and its persisted placement.",
                required: new[] { "id" }, examples: new[] { "geometry.delete:id=north_gate" }),
            ["geometry.keep"] = Mutation(
                "geometry.keep", "geometry", "Mark one existing geometry entity to survive reconcile deletion.",
                required: new[] { "id" }, examples: new[] { "geometry.keep:id=north_gate" }),
            ["geometry.reconcile"] = Mutation(
                "geometry.reconcile", "geometry", "Reload active-blueprint.json and adjust/keep/delete existing entities only; never spawn.",
                examples: new[] { "geometry.reconcile" })
        };

    public static bool IsNativeAction(string? actionName) =>
        !string.IsNullOrWhiteSpace(actionName) && Entries.ContainsKey(actionName.Trim());

    public static void InjectEntries(IDictionary<string, ActionManifestEntry> entries)
    {
        foreach (var descriptor in Entries.Values)
        {
            if (entries.ContainsKey(descriptor.Name))
                continue;

            var entry = new ActionManifestEntry
            {
                Name = descriptor.Name,
                Category = descriptor.Category,
                Description = descriptor.Description,
                RiskLevel = descriptor.RiskLevel,
                RequiresApproval = descriptor.IsMutating,
                HandlerAvailable = true,
                Executable = true,
                IsMutating = descriptor.IsMutating,
                IsReversible = descriptor.Name.EndsWith(".remove", StringComparison.OrdinalIgnoreCase) ||
                               descriptor.Name.EndsWith(".clear", StringComparison.OrdinalIgnoreCase),
                IsIdempotent = descriptor.Name.EndsWith(".status", StringComparison.OrdinalIgnoreCase) ||
                               descriptor.Name.EndsWith(".list", StringComparison.OrdinalIgnoreCase) ||
                               descriptor.Name.EndsWith(".search", StringComparison.OrdinalIgnoreCase),
                MainThreadRequired = true,
                Availability = "server_only",
                ClientRequired = false,
                UsesNativeReplication = true,
                AllowedSources = descriptor.IsMutating
                    ? ActionSourcePermissions.Admin | ActionSourcePermissions.EventRuntime | ActionSourcePermissions.System
                    : ActionSourcePermissions.Admin | ActionSourcePermissions.Player | ActionSourcePermissions.AI |
                      ActionSourcePermissions.EventRuntime | ActionSourcePermissions.System,
                ConfirmationRequiredSources = descriptor.IsMutating
                    ? ActionSourcePermissions.AI | ActionSourcePermissions.Webhook | ActionSourcePermissions.MCP
                    : ActionSourcePermissions.None,
                AllowedPhases = descriptor.IsMutating
                    ? SessionPhaseAllowance.AnySession
                    : SessionPhaseAllowance.Any
            };
            entry.Required.AddRange(descriptor.Required);
            entry.Optional.AddRange(descriptor.Optional);
            entry.Aliases.AddRange(descriptor.Aliases);
            entry.Examples.AddRange(descriptor.Examples);
            entries[descriptor.Name] = entry;
        }
    }

    public static OperationResult Execute(
        string actionName,
        Dictionary<string, string> parameters,
        FlowActionContext context)
    {
        if (!Entries.TryGetValue(actionName, out var descriptor))
            return OperationResult.Fail($"Unknown BattleLuck native action '{actionName}'.");

        if (descriptor.IsMutating && context.GameContext == null)
            return OperationResult.Fail($"Action '{actionName}' requires an active BattleLuck session owner.");

        return actionName switch
        {
            "actions.search" => SearchActions(parameters, context),
            "loot.status" => LootStatus(context),
            "buff.preset.list" => Notify(context, $"Buff presets: {string.Join(", ", BuffPresets.Keys)}"),
            "sign.preset.list" => Notify(context, $"Sign presets: {string.Join(", ", SignPresets.Keys)}"),
            "sign.status" => SignStatus(context),
            "weapon.search" => SearchWeapons(parameters, context),
            "zone.status" => ZoneStatus(context),
            "geometry.status" => GeometryStatus(context),
            "geometry.catalog.search" => SearchGeometryCatalog(parameters, context),
            "build.asset.search" => SearchBuildAssets(parameters, context),
            "loot.grant" => GrantLoot(parameters, context),
            "loot.roll" => RollLoot(parameters, context),
            "loot.kit.grant" => GrantLootKit(parameters, context),
            "buff.preset.apply" => ApplyBuffPreset(parameters, context.PlayerCharacter, apply: true),
            "buff.preset.remove" => ApplyBuffPreset(parameters, context.PlayerCharacter, apply: false),
            "zone.buff.preset.apply" => ApplyZoneBuffPreset(parameters, context, apply: true),
            "zone.buff.preset.remove" => ApplyZoneBuffPreset(parameters, context, apply: false),
            "sign.spawn" => SpawnSign(parameters, context),
            "sign.clear" => ClearSign(parameters, context),
            "weapon.grant" => GrantWeapon(parameters, context),
            "weapon.kit.grant" => GrantWeaponKit(parameters, context),
            "zone.marker.spawn" => SpawnZoneMarker(parameters, context),
            "zone.marker.clear" => ClearZoneMarker(context),
            "geometry.adjust" => AdjustGeometry(parameters, context),
            "geometry.delete" => Geometry(context, service => service.Delete(RequireIdentifier(Required(parameters, "id"), "id"))),
            "geometry.keep" => Geometry(context, service => service.Keep(RequireIdentifier(Required(parameters, "id"), "id"))),
            "geometry.reconcile" => Geometry(context, service => service.Reconcile()),
            _ => OperationResult.Fail($"No handler exists for BattleLuck native action '{actionName}'.")
        };
    }

    /// <summary>Apply a reward from the physical LootCrateController.</summary>
    public static OperationResult GrantCrateReward(
        Entity player,
        CrateTypeConfig crate,
        GameModeContext context,
        ZoneDefinition? zone)
    {
        if (!player.Exists() || !player.IsPlayer())
            return OperationResult.Fail("Loot recipient is not a live player.");
        if (crate == null || string.IsNullOrWhiteSpace(crate.Prefab))
            return OperationResult.Fail("Loot crate has no reward prefab.");

        var result = GrantPrefabReward(
            player,
            crate.Prefab,
            Math.Clamp(crate.Amount, 1, 100),
            durationSeconds: 20f,
            zone: zone);
        if (result.Success)
            BattleLuckPlugin.LogInfo(
                $"[NativeActions] Loot crate '{crate.Type}' granted '{crate.Prefab}' x{Math.Max(1, crate.Amount)} " +
                $"to {player.GetSteamId()} in session {context.SessionId}.");
        return result;
    }

    public static bool IsInventoryLootPrefab(string? prefab) =>
        !string.IsNullOrWhiteSpace(prefab) &&
        prefab.StartsWith("Item_", StringComparison.OrdinalIgnoreCase);

    static Descriptor View(
        string name,
        string category,
        string description,
        string[]? required = null,
        string[]? optional = null,
        string[]? aliases = null,
        string[]? examples = null) =>
        new(name, category, description, "safe", false,
            required ?? Array.Empty<string>(),
            optional ?? Array.Empty<string>(),
            aliases ?? Array.Empty<string>(),
            examples ?? new[] { name });

    static Descriptor Mutation(
        string name,
        string category,
        string description,
        string[]? required = null,
        string[]? optional = null,
        string[]? aliases = null,
        string[]? examples = null,
        string risk = "controlled") =>
        new(name, category, description, risk, true,
            required ?? Array.Empty<string>(),
            optional ?? Array.Empty<string>(),
            aliases ?? Array.Empty<string>(),
            examples ?? new[] { name });

    static OperationResult SearchActions(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var query = Text(parameters, "query", "");
        var limit = Math.Clamp(Int(parameters, "limit", 10), 1, 20);
        var manifest = ActionManifestService.Instance;
        var matches = manifest.Search(query, manifest.Entries.Count)
            .Where(result =>
                manifest.TryGetAction(result.Name, out var entry) &&
                entry?.HandlerAvailable == true &&
                entry.Executable &&
                entry.IsServerContractValid &&
                FlowActionExecutor.IsRuntimeHandledAction(result.Name))
            .Take(limit)
            .Select(result => $"{result.Name} [{result.RiskLevel}]")
            .ToArray();
        return Notify(context, matches.Length == 0
            ? "No executable BattleLuck actions matched."
            : $"Actions: {string.Join(", ", matches)}");
    }

    static OperationResult LootStatus(FlowActionContext context)
    {
        var loot = context.Zone?.LootCrates;
        if (loot == null || loot.CrateTypes.Count == 0)
            return Notify(context, "This zone has no configured loot table.");

        var table = string.Join(", ", loot.CrateTypes
            .Where(item => !string.IsNullOrWhiteSpace(item.Prefab))
            .Select(item => $"{item.Type}:{item.Weight}"));
        return Notify(context,
            $"Loot enabled={loot.Enabled}, winnerOnly={loot.WinnerOnly}, lockedUntilKills={loot.LockedUntilKills}, table={table}");
    }

    static OperationResult ZoneStatus(FlowActionContext context)
    {
        if (context.Zone == null)
            return Notify(context, $"No active zone definition. zoneHash={context.ZoneHash}.");

        return Notify(context,
            $"Zone {context.Zone.Name} hash={context.Zone.Hash} radius={context.Zone.Radius:F1} " +
            $"exitRadius={context.Zone.ExitRadius:F1} session={context.GameContext?.SessionId ?? "none"}.");
    }

    static OperationResult SearchWeapons(Dictionary<string, string> parameters, FlowActionContext context)
    {
        if (!VRisingCore.IsReady)
            return OperationResult.Fail("Weapon search requires the live server prefab registry.");

        var query = Text(parameters, "query", "Item_Weapon_");
        var limit = Math.Clamp(Int(parameters, "limit", 10), 1, 20);
        PrefabHelper.ScanLivePrefabs();
        var names = PrefabHelper.FindLive(query)
            .Select(pair => pair.Key)
            .Where(IsGrantableWeaponName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
        return Notify(context, names.Length == 0
            ? $"No native weapons matched '{query}'."
            : $"Weapons: {string.Join(", ", names)}");
    }

    static OperationResult GeometryStatus(FlowActionContext context)
    {
        if (!TryGeometry(context, out var service))
            return Notify(context, "No active session owns Blueprint geometry.");
        return Notify(context, service.Status());
    }

    static OperationResult SearchGeometryCatalog(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var query = Text(parameters, "query", "TM_");
        var limit = Math.Clamp(Int(parameters, "limit", 10), 1, 20);
        var names = BuildAssetCatalogService.Search(query, limit, includeChests: false)
            .Select(asset => $"{asset.Name}({asset.PrefabGuid})")
            .ToArray();
        return Notify(context, names.Length == 0 ? $"No safe TM_ prefabs matched '{query}'." : $"Geometry prefabs: {string.Join(", ", names)}");
    }

    static OperationResult SearchBuildAssets(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var query = Text(parameters, "query", "");
        var kind = Text(parameters, "kind", "");
        var limit = Math.Clamp(Int(parameters, "limit", 10), 1, 20);
        var includeChests = !kind.Equals("tile", StringComparison.OrdinalIgnoreCase) &&
                            !kind.Equals("floor", StringComparison.OrdinalIgnoreCase) &&
                            !kind.Equals("wall", StringComparison.OrdinalIgnoreCase);
        var assets = BuildAssetCatalogService.Search(query, limit * 3, includeChests)
            .Where(asset => string.IsNullOrWhiteSpace(kind) ||
                            kind.Equals("tile", StringComparison.OrdinalIgnoreCase) && asset.Kind == BuildAssetCatalogService.AssetKind.Tile ||
                            kind.Equals("floor", StringComparison.OrdinalIgnoreCase) && asset.Kind == BuildAssetCatalogService.AssetKind.Tile ||
                            kind.Equals("wall", StringComparison.OrdinalIgnoreCase) && asset.Kind == BuildAssetCatalogService.AssetKind.Tile ||
                            kind.Equals("chest", StringComparison.OrdinalIgnoreCase) && asset.Kind == BuildAssetCatalogService.AssetKind.Chest)
            .Take(limit)
            .Select(asset => $"{asset.Name}({asset.PrefabGuid},{asset.Kind.ToString().ToLowerInvariant()})")
            .ToArray();
        return Notify(context, assets.Length == 0
            ? $"No shared BattleLuck build assets matched '{query}'."
            : $"Build assets: {string.Join(", ", assets)}");
    }

    static OperationResult AdjustGeometry(Dictionary<string, string> parameters, FlowActionContext context)
    {
        Vec3Config? offset = null;
        var rawOffset = Text(parameters, "offset", "");
        if (!string.IsNullOrWhiteSpace(rawOffset))
        {
            if (!TryParseFloat3(rawOffset, out var parsed))
                return OperationResult.Fail("offset must be x,y,z.");
            offset = Vec3Config.FromFloat3(parsed);
        }
        float? rotation = parameters.ContainsKey("rotation") ? Float(parameters, "rotation", 0f) : null;
        return Geometry(context, service =>
            service.Adjust(RequireIdentifier(Required(parameters, "id"), "id"), offset, rotation));
    }

    static OperationResult Geometry(FlowActionContext context, Func<BlueprintGeometryService, OperationResult> action) =>
        TryGeometry(context, out var service)
            ? action(service)
            : OperationResult.Fail("No active session owns Blueprint geometry.");

    static bool TryGeometry(FlowActionContext context, out BlueprintGeometryService service)
    {
        if (context.GameContext?.State.TryGetValue(BlueprintGeometryService.ContextKey, out var value) == true &&
            value is BlueprintGeometryService activeService)
        {
            service = activeService;
            return true;
        }

        service = null!;
        return false;
    }

    static OperationResult GrantLoot(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var rewardId = Required(parameters, "rewardId");
        if (IsClaimed(context, rewardId))
            return OperationResult.Ok();

        var result = GrantPrefabReward(
            context.PlayerCharacter,
            Required(parameters, "prefab"),
            Math.Clamp(Int(parameters, "amount", 1), 1, 100),
            Math.Clamp(Float(parameters, "durationSeconds", 20f), 1f, 300f),
            context.Zone);
        if (result.Success)
            MarkClaimed(context, rewardId);
        return result;
    }

    static OperationResult RollLoot(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var rewardId = Required(parameters, "rewardId");
        if (IsClaimed(context, rewardId))
            return OperationResult.Ok();

        var candidates = context.Zone?.LootCrates?.CrateTypes
            .Where(item => item.Weight > 0 && !string.IsNullOrWhiteSpace(item.Prefab))
            .ToArray() ?? Array.Empty<CrateTypeConfig>();
        if (candidates.Length == 0)
            return OperationResult.Fail("The active zone has no weighted loot entries.");

        var totalWeight = candidates.Sum(item => item.Weight);
        var seed = StableHash($"{context.GameContext!.SessionId}:{context.PlayerCharacter.GetSteamId()}:{rewardId}");
        var roll = (int)(seed % (uint)totalWeight);
        var cumulative = 0;
        var selected = candidates[^1];
        foreach (var candidate in candidates)
        {
            cumulative += candidate.Weight;
            if (roll < cumulative)
            {
                selected = candidate;
                break;
            }
        }

        var result = GrantPrefabReward(
            context.PlayerCharacter,
            selected.Prefab,
            Math.Clamp(selected.Amount, 1, 100),
            Math.Clamp(Float(parameters, "durationSeconds", 20f), 1f, 300f),
            context.Zone);
        if (result.Success)
        {
            MarkClaimed(context, rewardId);
            Notify(context, $"Loot roll '{rewardId}' granted {selected.Type}.");
        }
        return result;
    }

    static OperationResult GrantLootKit(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var rewardId = Required(parameters, "rewardId");
        if (IsClaimed(context, rewardId))
            return OperationResult.Ok();

        var result = KitController.ApplyKit(
            context.PlayerCharacter,
            Required(parameters, "kitId"),
            KitApplyMode.AdditiveReward);
        if (result.Success)
            MarkClaimed(context, rewardId);
        return result;
    }

    static OperationResult GrantPrefabReward(
        Entity player,
        string prefabName,
        int amount,
        float durationSeconds,
        ZoneDefinition? zone)
    {
        if (prefabName.StartsWith("Buff_", StringComparison.OrdinalIgnoreCase) &&
            !IsAllowedBuffReward(prefabName, zone))
        {
            return OperationResult.Fail(
                $"Buff reward '{prefabName}' is not a BattleLuck preset or part of the active zone loot table.");
        }

        var prefab = PrefabHelper.GetValidPrefabGuidDeep(prefabName) ??
                     PrefabHelper.GetPrefabGuidDeep(prefabName);
        if (!prefab.HasValue || prefab.Value == PrefabGUID.Empty)
            return OperationResult.Fail($"Unknown loot prefab '{prefabName}'.");

        if (prefabName.StartsWith("Buff_", StringComparison.OrdinalIgnoreCase))
        {
            return player.BuffEntity(prefab.Value, out _, durationSeconds)
                ? OperationResult.Ok()
                : OperationResult.Fail($"Buff reward '{prefabName}' could not be applied.");
        }

        if (!IsInventoryLootPrefab(prefabName))
            return OperationResult.Fail($"Loot prefab '{prefabName}' is neither an Item_ nor an allowlisted Buff_.");

        return player.TryGiveItem(prefab.Value, amount)
            ? OperationResult.Ok()
            : OperationResult.Fail($"Item reward '{prefabName}' x{amount} could not be granted.");
    }

    internal static bool IsAllowedBuffReward(string? prefabName, ZoneDefinition? zone)
    {
        if (string.IsNullOrWhiteSpace(prefabName) ||
            !prefabName.StartsWith("Buff_", StringComparison.OrdinalIgnoreCase))
            return false;

        if (BuffPresets.Values.Contains(prefabName, StringComparer.OrdinalIgnoreCase))
            return true;

        return zone?.LootCrates?.CrateTypes.Any(reward =>
            reward.Prefab.Equals(prefabName, StringComparison.OrdinalIgnoreCase)) == true;
    }

    static OperationResult ApplyBuffPreset(
        Dictionary<string, string> parameters,
        Entity target,
        bool apply)
    {
        var preset = Required(parameters, "preset");
        if (!BuffPresets.TryGetValue(preset, out var prefabName))
            return OperationResult.Fail($"Unknown buff preset '{preset}'. Known: {string.Join(", ", BuffPresets.Keys)}.");

        var prefab = PrefabHelper.GetPrefabGuidDeep(prefabName);
        if (!prefab.HasValue)
            return OperationResult.Fail($"Buff preset '{preset}' is unavailable on this server build.");

        if (!apply)
        {
            target.TryRemoveBuff(prefab.Value);
            return OperationResult.Ok();
        }

        var duration = Math.Clamp(Float(parameters, "durationSeconds", 20f), 1f, 300f);
        return target.BuffEntity(prefab.Value, out _, duration)
            ? OperationResult.Ok()
            : OperationResult.Fail($"Buff preset '{preset}' could not be applied.");
    }

    static OperationResult ApplyZoneBuffPreset(
        Dictionary<string, string> parameters,
        FlowActionContext context,
        bool apply)
    {
        var participants = VRisingCore.GetOnlinePlayers()
            .Where(player => player.Exists() && player.IsPlayer() &&
                             context.GameContext!.Players.Contains(player.GetSteamId()))
            .ToArray();
        if (participants.Length == 0)
            return OperationResult.Fail("No online participants belong to the active session.");

        var failures = 0;
        foreach (var participant in participants)
        {
            if (!ApplyBuffPreset(parameters, participant, apply).Success)
                failures++;
        }
        return failures == 0
            ? OperationResult.Ok()
            : OperationResult.Fail($"Buff preset failed for {failures}/{participants.Length} participant(s).");
    }

    static OperationResult SpawnSign(Dictionary<string, string> parameters, FlowActionContext context)
    {
        if (context.Zone == null)
            return OperationResult.Fail("sign.spawn requires an active zone definition.");

        var signId = RequireIdentifier(Required(parameters, "signId"), "signId");
        var preset = Text(parameters, "preset", "basic");
        if (!SignPresets.TryGetValue(preset, out var prefab))
            return OperationResult.Fail($"Unknown sign preset '{preset}'. Known: {string.Join(", ", SignPresets.Keys)}.");

        var position = ResolvePosition(parameters, context);
        var center = ZoneCenter(context.Zone);
        var radius = Math.Max(1f, context.Zone.Radius);
        if (math.distance(position.xz, center.xz) > radius)
            return OperationResult.Fail("sign.spawn position must be inside the active zone.");

        var key = SignKey(context, signId);
        var group = context.GameContext!.SessionId;
        if (ActiveSigns.ContainsKey(key))
            return OperationResult.Fail($"Sign '{signId}' already exists in this session; clear it before replacing it.");

        var spawned = SchematicLoader.SpawnPrefabAt(
            prefab,
            position,
            Math.Clamp(Float(parameters, "rotation", 0f), -360f, 360f),
            "sign",
            group);
        if (!spawned.Success || spawned.Value == null)
            return OperationResult.Fail(spawned.Error ?? $"Sign '{signId}' could not be spawned.");

        ActiveSigns[key] = new NativeSignRecord(
            signId,
            context.GameContext.SessionId,
            group,
            prefab,
            context.ZoneHash,
            context.PlayerCharacter.GetSteamId(),
            position,
            spawned.Value.Entity);
        return OperationResult.Ok();
    }

    static OperationResult ClearSign(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var signId = RequireIdentifier(Required(parameters, "signId"), "signId");
        var key = SignKey(context, signId);
        if (!ActiveSigns.TryGetValue(key, out var sign))
            return OperationResult.Ok();

        var cleared = SchematicLoader.ClearTrackedEntity(sign.TrackingGroup, sign.Entity);
        if (!cleared.Success)
            return OperationResult.Fail(cleared.Error ?? $"Sign '{signId}' could not be cleared.");
        ActiveSigns.TryRemove(key, out _);
        return OperationResult.Ok();
    }

    static OperationResult SignStatus(FlowActionContext context)
    {
        if (context.GameContext == null)
            return Notify(context, "No active session owns signs.");
        var sessionId = context.GameContext.SessionId;
        foreach (var pair in ActiveSigns.Where(pair =>
                     pair.Value.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase) &&
                     !IsLiveSign(pair.Value)).ToArray())
            ActiveSigns.TryRemove(pair.Key, out _);

        var signs = ActiveSigns.Values
            .Where(sign => sign.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase))
            .Select(sign => $"{sign.SignId}:{sign.Prefab}")
            .ToArray();
        return Notify(context, signs.Length == 0
            ? "No BattleLuck-owned signs are active in this session."
            : $"Signs: {string.Join(", ", signs)}");
    }

    static OperationResult SpawnZoneMarker(Dictionary<string, string> parameters, FlowActionContext context)
    {
        if (context.Zone == null)
            return OperationResult.Fail("zone.marker.spawn requires an active zone.");
        var mapped = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase)
        {
            ["signId"] = $"zone_{context.Zone.Hash}",
            ["position"] = "zone"
        };
        if (!mapped.ContainsKey("preset"))
            mapped["preset"] = "banner";
        return SpawnSign(mapped, context);
    }

    static OperationResult ClearZoneMarker(FlowActionContext context)
    {
        if (context.Zone == null)
            return OperationResult.Fail("zone.marker.clear requires an active zone.");
        return ClearSign(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["signId"] = $"zone_{context.Zone.Hash}"
            },
            context);
    }

    static OperationResult GrantWeapon(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var rewardId = Required(parameters, "rewardId");
        if (IsClaimed(context, rewardId))
            return OperationResult.Ok();

        var result = GrantWeaponPrefab(
            context.PlayerCharacter,
            Required(parameters, "prefab"),
            Math.Clamp(Int(parameters, "amount", 1), 1, 4));
        if (result.Success)
            MarkClaimed(context, rewardId);
        return result;
    }

    static OperationResult GrantWeaponKit(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var rewardId = Required(parameters, "rewardId");
        if (IsClaimed(context, rewardId))
            return OperationResult.Ok();

        var kitId = Required(parameters, "kitId");
        var kit = KitController.LoadKit(kitId);
        if (kit == null || kit.Weapons.Count == 0)
            return OperationResult.Fail($"Kit '{kitId}' has no weapon entries.");

        var granted = 0;
        var failures = new List<string>();
        foreach (var weapon in kit.Weapons)
        {
            var result = GrantWeaponPrefab(
                context.PlayerCharacter,
                weapon.Prefab,
                Math.Clamp(weapon.Amount, 1, 4));
            if (result.Success)
                granted++;
            else
                failures.Add(weapon.Prefab);
        }

        if (failures.Count > 0)
            return OperationResult.Fail(
                $"Weapon kit '{kitId}' granted {granted}/{kit.Weapons.Count}; failed: {string.Join(", ", failures.Take(5))}.");

        MarkClaimed(context, rewardId);
        return OperationResult.Ok();
    }

    static OperationResult GrantWeaponPrefab(Entity player, string prefabName, int amount)
    {
        var prefab = PrefabHelper.GetValidPrefabGuidDeep(prefabName) ??
                     PrefabHelper.GetPrefabGuidDeep(prefabName);
        if (!prefab.HasValue || prefab.Value == PrefabGUID.Empty)
            return OperationResult.Fail($"Unknown weapon prefab '{prefabName}'.");

        var liveName = PrefabHelper.GetLivePrefabName(prefab.Value) ?? prefabName;
        if (!IsGrantableWeaponName(liveName))
            return OperationResult.Fail($"Prefab '{prefabName}' is not a grantable native weapon item.");

        return player.TryGiveItem(prefab.Value, amount)
            ? OperationResult.Ok()
            : OperationResult.Fail($"Weapon '{liveName}' x{amount} could not be granted.");
    }

    static bool IsGrantableWeaponName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            !name.StartsWith("Item_Weapon_", StringComparison.OrdinalIgnoreCase))
            return false;

        return !name.Contains("Shattered", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("Template", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("NameGenerator", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("Recipe", StringComparison.OrdinalIgnoreCase);
    }

    static OperationResult Notify(FlowActionContext context, string message) =>
        BattleLuckPlugin.TryNotifyPlayerBySteamId(context.PlayerCharacter.GetSteamId(), message)
            ? OperationResult.Ok()
            : OperationResult.Fail("Player is not online to receive the view.");

    static bool IsClaimed(FlowActionContext context, string rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId) || context.GameContext == null)
            return false;
        return Claims(context.GameContext).Contains(ClaimKey(context, rewardId));
    }

    static void MarkClaimed(FlowActionContext context, string rewardId)
    {
        if (string.IsNullOrWhiteSpace(rewardId) || context.GameContext == null)
            return;
        Claims(context.GameContext).Add(ClaimKey(context, rewardId));
    }

    static HashSet<string> Claims(GameModeContext context)
    {
        const string key = "battleluck.native.rewardClaims";
        if (!context.State.TryGetValue(key, out var value) || value is not HashSet<string> claims)
        {
            claims = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            context.State[key] = claims;
        }
        return claims;
    }

    static string ClaimKey(FlowActionContext context, string rewardId) =>
        $"{context.PlayerCharacter.GetSteamId()}:{RequireIdentifier(rewardId, "rewardId")}";

    static string SignKey(FlowActionContext context, string signId) =>
        $"{Sanitize(context.GameContext!.SessionId)}:{Sanitize(signId)}";

    static bool IsLiveSign(NativeSignRecord sign)
    {
        try { return sign.Entity.Exists(); }
        catch { return false; }
    }

    public static void CleanupSession(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        foreach (var pair in ActiveSigns.Where(pair =>
                     pair.Value.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase)).ToArray())
            ActiveSigns.TryRemove(pair.Key, out _);
    }

    static float3 ResolvePosition(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var value = Text(parameters, "position", "");
        float3 position;
        if (value.Equals("zone", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("zone_center", StringComparison.OrdinalIgnoreCase))
        {
            position = ZoneCenter(context.Zone!);
        }
        else if (!TryParseFloat3(value, out position))
        {
            position = context.PlayerCharacter.GetPosition();
        }

        if (TryParseFloat3(Text(parameters, "offset", ""), out var offset))
        {
            offset = math.clamp(offset, new float3(-100f), new float3(100f));
            position += offset;
        }
        return position;
    }

    static float3 ZoneCenter(ZoneDefinition zone)
    {
        var center = zone.Position.ToFloat3();
        return math.lengthsq(center) > 0.0001f ? center : zone.TeleportSpawn.ToFloat3();
    }

    static bool TryParseFloat3(string value, out float3 result)
    {
        result = float3.zero;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var parts = value.Split(new[] { ',', ' ' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 ||
            !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out result.x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out result.y) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out result.z))
            return false;
        return math.all(math.isfinite(result));
    }

    static uint StableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var ch in value)
        {
            hash ^= ch;
            hash *= prime;
        }
        return hash;
    }

    static string Required(Dictionary<string, string> parameters, string key)
    {
        var value = Text(parameters, key, "");
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required parameter '{key}'.");
        return value;
    }

    static string RequireIdentifier(string value, string parameter)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 64 ||
            trimmed.Any(ch => !char.IsLetterOrDigit(ch) && ch is not '_' and not '-'))
            throw new InvalidOperationException(
                $"{parameter} must be 1-64 letters, numbers, '-' or '_'.");
        return trimmed;
    }

    static string Sanitize(string value)
    {
        var clean = new string((value ?? string.Empty)
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .Take(80)
            .ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "unknown" : clean;
    }

    static string Text(Dictionary<string, string> parameters, string key, string fallback) =>
        parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    static int Int(Dictionary<string, string> parameters, string key, int fallback) =>
        parameters.TryGetValue(key, out var value) &&
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    static float Float(Dictionary<string, string> parameters, string key, float fallback) =>
        parameters.TryGetValue(key, out var value) &&
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
        float.IsFinite(parsed)
            ? parsed
            : fallback;
}
