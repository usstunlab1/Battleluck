using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using BattleLuck.Models;
using BattleLuck.Models.Effects;
using BattleLuck.Services.Flow;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace BattleLuck.Services.Runtime;

/// <summary>
/// Executes temporary request-scoped effects. This is deliberately separate from
/// kit, weapon, and ability ownership. Effects exist only for their requested
/// lifetime or cleanup scope unless another explicit action persists a definition.
/// </summary>
public static class RuntimeEffectActionService
{
    static readonly ConcurrentDictionary<string, RuntimeEffectAssignment> Active =
        new(StringComparer.OrdinalIgnoreCase);

    public static OperationResult Execute(
        string actionName,
        Dictionary<string, string> parameters,
        FlowActionContext context)
    {
        if (!RuntimeEffectActionCatalog.IsRuntimeEffectAction(actionName))
            return OperationResult.Fail($"Unknown runtime effect action '{actionName}'.");

        if (actionName.EndsWith(".status", StringComparison.OrdinalIgnoreCase) ||
            actionName.Equals("effect.status", StringComparison.OrdinalIgnoreCase))
        {
            return Status(parameters, context);
        }

        if (actionName.Equals("effect.clear_group", StringComparison.OrdinalIgnoreCase))
            return ClearGroup(Required(parameters, "trackingGroup"), context);

        var remove = actionName.EndsWith(".remove", StringComparison.OrdinalIgnoreCase) ||
                     actionName.Equals("effect.remove", StringComparison.OrdinalIgnoreCase);
        var replace = actionName.EndsWith(".replace", StringComparison.OrdinalIgnoreCase) ||
                      actionName.Equals("effect.replace", StringComparison.OrdinalIgnoreCase);

        if (remove || replace)
        {
            var removed = RemoveMatching(parameters, context);
            if (remove)
                return removed;
        }

        ApplyActionDefaults(actionName, parameters);
        return Assign(actionName, parameters, context);
    }

    public static void TickAll()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in Active.ToArray())
        {
            var assignment = pair.Value;
            if (assignment.ExpiresUtc.HasValue && assignment.ExpiresUtc.Value <= now)
                Cleanup(pair.Key, assignment, "timed expiration");
            else if (assignment.CleanupPolicy == RuntimeEffectCleanupPolicy.OnEntityDestroyed &&
                     assignment.TargetEntities.Count > 0 &&
                     assignment.TargetEntities.All(entity => !entity.Exists()))
                Cleanup(pair.Key, assignment, "target entities destroyed");
        }
    }

    public static void CleanupPlayer(ulong steamId, string? sessionId = null)
    {
        foreach (var pair in Active.ToArray())
        {
            var assignment = pair.Value;
            if (!string.IsNullOrWhiteSpace(sessionId) &&
                !assignment.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (assignment.TargetSteamIds.Contains(steamId) || assignment.OwnerSteamId == steamId)
                Cleanup(pair.Key, assignment, "player exit");
        }
    }

    public static void CleanupSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        foreach (var pair in Active.ToArray())
        {
            if (pair.Value.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase))
                Cleanup(pair.Key, pair.Value, "session end");
        }
    }

    public static void CleanupAll()
    {
        foreach (var pair in Active.ToArray())
            Cleanup(pair.Key, pair.Value, "server shutdown");
    }

    public static bool TryGetSessionContext(object activeSessions, int zoneHash, out GameModeContext? context)
    {
        context = null;
        try
        {
            if (activeSessions is not IDictionary dictionary || !dictionary.Contains(zoneHash))
                return false;

            var session = dictionary[zoneHash];
            if (session == null)
                return false;

            context = session.GetType().GetProperty("Context")?.GetValue(session) as GameModeContext;
            return context != null;
        }
        catch
        {
            return false;
        }
    }

    public static IEnumerable<GameModeContext> GetSessionContexts(object activeSessions)
    {
        if (activeSessions is not IDictionary dictionary)
            yield break;

        foreach (DictionaryEntry entry in dictionary)
        {
            var session = entry.Value;
            if (session == null)
                continue;
            if (session.GetType().GetProperty("Context")?.GetValue(session) is GameModeContext context)
                yield return context;
        }
    }

    static OperationResult Assign(
        string actionName,
        Dictionary<string, string> parameters,
        FlowActionContext context)
    {
        var prefabName = First(parameters, "prefab", "buffPrefab", "effectPrefab", "glowPrefab");
        if (string.IsNullOrWhiteSpace(prefabName))
            return OperationResult.Fail("Runtime effect assignment requires prefab/buffPrefab/effectPrefab/glowPrefab.");

        var prefab = ResolvePrefab(prefabName);
        if (prefab == PrefabGUID.Empty)
            return OperationResult.Fail($"Unknown effect prefab '{prefabName}'.");

        var kind = ParseKind(First(parameters, "kind", "effectKind", "type"));
        var targetType = NormalizeTarget(First(parameters, "targetType", "target", "scope"));
        var ownerSteamId = context.PlayerCharacter.GetSteamId();
        var sessionId = context.GameContext?.SessionId ?? $"manual:{ownerSteamId}";
        var modeId = context.ModeId;
        var assignmentId = First(parameters, "effectId", "assignmentId", "id");
        if (string.IsNullOrWhiteSpace(assignmentId))
            assignmentId = $"{targetType}_{kind}_{prefab.GuidHash}";

        var trackingGroup = First(parameters, "trackingGroup", "spawnGroup", "group");
        if (string.IsNullOrWhiteSpace(trackingGroup))
            trackingGroup = assignmentId;

        var cleanup = ParseCleanup(First(parameters, "cleanup", "cleanupPolicy"));
        var duration = Float(parameters, "durationSeconds", Float(parameters, "duration", -1f));
        DateTime? expires = duration > 0f ? DateTime.UtcNow.AddSeconds(duration) : null;
        if (cleanup == RuntimeEffectCleanupPolicy.Timed && !expires.HasValue)
            return OperationResult.Fail("cleanup=timed requires durationSeconds > 0.");

        var key = MakeKey(sessionId, assignmentId);
        if (Active.TryRemove(key, out var previous))
            Cleanup(key, previous, "replacement");

        var assignment = new RuntimeEffectAssignment
        {
            AssignmentId = assignmentId,
            SessionId = sessionId,
            ModeId = modeId,
            TrackingGroup = trackingGroup,
            NativeTrackingGroup = $"runtime_effect_{Sanitize(sessionId)}_{Sanitize(trackingGroup)}",
            SourceAction = actionName,
            TargetType = targetType,
            PrefabName = prefabName,
            PrefabGuidHash = prefab.GuidHash,
            ZoneHash = Int(parameters, "zoneHash", context.ZoneHash),
            OwnerSteamId = ownerSteamId,
            Kind = kind,
            CleanupPolicy = cleanup,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = expires
        };

        OperationResult result;
        if (targetType.Equals("zone_border", StringComparison.OrdinalIgnoreCase) || kind == RuntimeEffectKind.BorderVfx)
            result = SpawnZoneBorder(assignment, parameters, context);
        else if (targetType.Equals("position", StringComparison.OrdinalIgnoreCase) ||
                 targetType.Equals("point", StringComparison.OrdinalIgnoreCase) ||
                 kind == RuntimeEffectKind.WorldVfx)
            result = SpawnAtPosition(assignment, parameters, context);
        else
            result = ApplyToTargets(assignment, parameters, context, prefab, duration);

        if (!result.Success)
        {
            CleanupCreatedOnly(assignment);
            return result;
        }

        Active[key] = assignment;
        RememberInContext(context.GameContext, key);
        BattleLuckPlugin.LogInfo(
            $"[RuntimeEffects] Assigned '{assignment.AssignmentId}' kind={assignment.Kind} prefab={assignment.PrefabName} " +
            $"target={assignment.TargetType} targets={assignment.TargetEntities.Count} world={assignment.SpawnedWorldEntities.Count} " +
            $"cleanup={assignment.CleanupPolicy} session={assignment.SessionId}.");
        return OperationResult.Ok();
    }

    static OperationResult ApplyToTargets(
        RuntimeEffectAssignment assignment,
        Dictionary<string, string> parameters,
        FlowActionContext context,
        PrefabGUID prefab,
        float duration)
    {
        var targets = ResolveTargets(assignment.TargetType, parameters, context).Distinct().ToList();
        if (targets.Count == 0)
            return OperationResult.Fail($"No live entities matched targetType '{assignment.TargetType}'.");

        var applied = 0;
        foreach (var target in targets)
        {
            if (!target.Exists())
                continue;

            if (!target.BuffEntity(prefab, out var buffEntity, duration < 0f ? 0f : duration))
                continue;

            applied++;
            assignment.TargetEntities.Add(target);
            var steamId = target.GetSteamId();
            if (steamId != 0)
                assignment.TargetSteamIds.Add(steamId);
            if (buffEntity.Exists())
                assignment.BuffEntities.Add(buffEntity);
        }

        return applied > 0
            ? OperationResult.Ok()
            : OperationResult.Fail($"Effect prefab '{assignment.PrefabName}' could not be applied to matched targets.");
    }

    static OperationResult SpawnZoneBorder(
        RuntimeEffectAssignment assignment,
        Dictionary<string, string> parameters,
        FlowActionContext context)
    {
        if (context.Zone == null)
            return OperationResult.Fail("zone_border effect requires an active zone definition.");

        var center = ZoneCenter(context.Zone);
        var radius = Float(parameters, "radius", context.Zone.Radius);
        if (radius <= 0f)
            return OperationResult.Fail("zone_border effect requires radius > 0.");

        var spacing = Math.Clamp(Float(parameters, "spacing", 5f), 1f, 25f);
        var estimated = Math.Max(4, (int)Math.Ceiling((2f * Math.PI * radius) / spacing));
        var maxPoints = Math.Clamp(Int(parameters, "maxPoints", Math.Min(estimated, 96)), 4, 128);
        var pointCount = Math.Min(estimated, maxPoints);
        var heightOffset = Float(parameters, "heightOffset", 0.2f);
        var rotation = Float(parameters, "rotation", 0f);

        for (var index = 0; index < pointCount; index++)
        {
            var angle = (2f * math.PI * index) / pointCount;
            var position = new float3(
                center.x + math.cos(angle) * radius,
                center.y + heightOffset,
                center.z + math.sin(angle) * radius);

            var spawned = SchematicLoader.SpawnPrefabAt(
                assignment.PrefabName,
                position,
                rotation,
                "effect",
                assignment.NativeTrackingGroup);

            if (spawned.Success && spawned.Value != null && spawned.Value.Entity.Exists())
                assignment.SpawnedWorldEntities.Add(spawned.Value.Entity);
        }

        return assignment.SpawnedWorldEntities.Count > 0
            ? OperationResult.Ok()
            : OperationResult.Fail($"No border effect entities could be spawned from '{assignment.PrefabName}'.");
    }

    static OperationResult SpawnAtPosition(
        RuntimeEffectAssignment assignment,
        Dictionary<string, string> parameters,
        FlowActionContext context)
    {
        var position = ResolvePosition(parameters, context);
        var count = Math.Clamp(Int(parameters, "count", 1), 1, 32);
        var spacing = Math.Clamp(Float(parameters, "spacing", 2f), 0f, 20f);
        var rotation = Float(parameters, "rotation", 0f);

        for (var index = 0; index < count; index++)
        {
            var spawnPosition = position + new float3(index * spacing, 0f, 0f);
            var spawned = SchematicLoader.SpawnPrefabAt(
                assignment.PrefabName,
                spawnPosition,
                rotation,
                "effect",
                assignment.NativeTrackingGroup);
            if (spawned.Success && spawned.Value != null && spawned.Value.Entity.Exists())
                assignment.SpawnedWorldEntities.Add(spawned.Value.Entity);
        }

        return assignment.SpawnedWorldEntities.Count > 0
            ? OperationResult.Ok()
            : OperationResult.Fail($"Effect prefab '{assignment.PrefabName}' could not be spawned at the requested position.");
    }

    static OperationResult RemoveMatching(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var effectId = First(parameters, "effectId", "assignmentId", "id");
        var group = First(parameters, "trackingGroup", "spawnGroup", "group");
        var prefab = First(parameters, "prefab", "buffPrefab", "effectPrefab", "glowPrefab");
        var sessionId = context.GameContext?.SessionId ?? $"manual:{context.PlayerCharacter.GetSteamId()}";
        var matches = Active.ToArray().Where(pair =>
            pair.Value.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(effectId) || pair.Value.AssignmentId.Equals(effectId, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(group) || pair.Value.TrackingGroup.Equals(group, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(prefab) || pair.Value.PrefabName.Equals(prefab, StringComparison.OrdinalIgnoreCase) ||
             pair.Value.PrefabGuidHash.ToString(CultureInfo.InvariantCulture).Equals(prefab, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var pair in matches)
            Cleanup(pair.Key, pair.Value, "explicit removal");

        return matches.Count > 0
            ? OperationResult.Ok()
            : OperationResult.Fail("No matching runtime effect assignment was found.");
    }

    static OperationResult ClearGroup(string group, FlowActionContext context)
    {
        var sessionId = context.GameContext?.SessionId ?? $"manual:{context.PlayerCharacter.GetSteamId()}";
        var matches = Active.ToArray()
            .Where(pair => pair.Value.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase) &&
                           pair.Value.TrackingGroup.Equals(group, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var pair in matches)
            Cleanup(pair.Key, pair.Value, "group cleanup");
        return matches.Count > 0 ? OperationResult.Ok() : OperationResult.Fail($"No runtime effects found in tracking group '{group}'.");
    }

    static OperationResult Status(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var sessionId = context.GameContext?.SessionId ?? $"manual:{context.PlayerCharacter.GetSteamId()}";
        var effectId = First(parameters, "effectId", "assignmentId", "id");
        var group = First(parameters, "trackingGroup", "group");
        var entries = Active.Values.Where(assignment =>
            assignment.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(effectId) || assignment.AssignmentId.Equals(effectId, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(group) || assignment.TrackingGroup.Equals(group, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var summary = entries.Count == 0
            ? "No active runtime effects."
            : string.Join("; ", entries.Take(12).Select(entry =>
                $"{entry.AssignmentId}:{entry.Kind}:{entry.PrefabName}:targets={entry.TargetEntities.Count}:world={entry.SpawnedWorldEntities.Count}:cleanup={entry.CleanupPolicy}"));
        BattleLuckPlugin.LogInfo($"[RuntimeEffects] {summary}");
        return OperationResult.Ok();
    }

    static void Cleanup(string key, RuntimeEffectAssignment assignment, string reason)
    {
        Active.TryRemove(key, out _);
        var prefab = new PrefabGUID(assignment.PrefabGuidHash);

        foreach (var target in assignment.TargetEntities.Distinct())
        {
            if (!target.Exists())
                continue;
            try { target.TryRemoveBuff(prefab); } catch { }
        }

        foreach (var entity in assignment.SpawnedWorldEntities.Distinct())
        {
            if (!entity.Exists())
                continue;
            try { entity.DestroyWithReason(); } catch { }
        }

        try { SchematicLoader.ClearTrackingGroup(assignment.NativeTrackingGroup); } catch { }
        BattleLuckPlugin.LogInfo($"[RuntimeEffects] Removed '{assignment.AssignmentId}' ({reason}).");
    }

    static void CleanupCreatedOnly(RuntimeEffectAssignment assignment)
    {
        foreach (var entity in assignment.SpawnedWorldEntities.Distinct())
        {
            if (!entity.Exists())
                continue;
            try { entity.DestroyWithReason(); } catch { }
        }
        try { SchematicLoader.ClearTrackingGroup(assignment.NativeTrackingGroup); } catch { }
    }

    static IEnumerable<Entity> ResolveTargets(
        string targetType,
        Dictionary<string, string> parameters,
        FlowActionContext context)
    {
        switch (targetType)
        {
            case "all_players":
                return VRisingCore.GetOnlinePlayers().Where(entity => entity.Exists() && entity.IsPlayer()).ToList();
            case "session_players":
            case "zone_players":
                if (context.GameContext == null)
                    return new[] { context.PlayerCharacter };
                return VRisingCore.GetOnlinePlayers()
                    .Where(entity => entity.Exists() && entity.IsPlayer() && context.GameContext.Players.Contains(entity.GetSteamId()))
                    .ToList();
            case "tracking_group":
            case "spawn_group":
                return ResolveTrackingGroup(context.GameContext, First(parameters, "trackingGroup", "spawnGroup", "group"));
            case "boss":
                return ResolveTrackingGroup(context.GameContext, "bosses");
            case "npc":
                return ResolveTrackingGroup(context.GameContext, "npcs");
            case "object":
                return ResolveTrackingGroup(context.GameContext, "structures");
            case "spawned_entity":
                return ResolveTrackingGroup(context.GameContext, "spawned");
            default:
                return new[] { context.PlayerCharacter };
        }
    }

    static IEnumerable<Entity> ResolveTrackingGroup(GameModeContext? context, string group)
    {
        if (context == null || string.IsNullOrWhiteSpace(group))
            return Enumerable.Empty<Entity>();

        if (context.State.TryGetValue(group, out var value))
        {
            if (value is IEnumerable<Entity> entities)
                return entities.Where(entity => entity.Exists()).ToList();
            if (value is Entity single && single.Exists())
                return new[] { single };
        }

        return Enumerable.Empty<Entity>();
    }

    static float3 ResolvePosition(Dictionary<string, string> parameters, FlowActionContext context)
    {
        var positionText = First(parameters, "position", "worldPosition");
        if (TryParseFloat3(positionText, out var parsed))
            return parsed;

        var pointId = First(parameters, "pointId", "point");
        if (!string.IsNullOrWhiteSpace(pointId) && context.GameContext != null &&
            context.GameContext.State.TryGetValue("spatialPoints", out var pointsValue) &&
            pointsValue is Dictionary<string, float3> points &&
            points.TryGetValue(pointId, out var point))
            return point;

        if (positionText.Equals("zone", StringComparison.OrdinalIgnoreCase) ||
            positionText.Equals("zone_center", StringComparison.OrdinalIgnoreCase))
            return context.Zone != null ? ZoneCenter(context.Zone) : context.PlayerCharacter.GetPosition();

        return context.PlayerCharacter.GetPosition();
    }

    static float3 ZoneCenter(ZoneDefinition zone)
    {
        var center = zone.Position.ToFloat3();
        return math.lengthsq(center) > 0.0001f ? center : zone.TeleportSpawn.ToFloat3();
    }

    static PrefabGUID ResolvePrefab(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hash))
            return new PrefabGUID(hash);
        return PrefabHelper.GetPrefabGuidDeep(value) ?? PrefabHelper.GetLivePrefabGuid(value) ?? PrefabGUID.Empty;
    }

    static RuntimeEffectKind ParseKind(string value)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "glow": return RuntimeEffectKind.Glow;
            case "attached_vfx":
            case "attachedvfx": return RuntimeEffectKind.AttachedVfx;
            case "world_vfx":
            case "worldvfx": return RuntimeEffectKind.WorldVfx;
            case "border_vfx":
            case "bordervfx": return RuntimeEffectKind.BorderVfx;
            default: return RuntimeEffectKind.Buff;
        }
    }

    static RuntimeEffectCleanupPolicy ParseCleanup(string value)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "timed": return RuntimeEffectCleanupPolicy.Timed;
            case "on_stage_exit": return RuntimeEffectCleanupPolicy.OnStageExit;
            case "on_zone_exit": return RuntimeEffectCleanupPolicy.OnZoneExit;
            case "manual": return RuntimeEffectCleanupPolicy.Manual;
            case "on_entity_destroyed": return RuntimeEffectCleanupPolicy.OnEntityDestroyed;
            default: return RuntimeEffectCleanupPolicy.OnEventExit;
        }
    }

    static string NormalizeTarget(string value)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "all":
            case "allplayers":
            case "all_players": return "all_players";
            case "session":
            case "players":
            case "session_players": return "session_players";
            case "zone":
            case "zone_players": return "zone_players";
            case "zoneborder":
            case "zone_border":
            case "border": return "zone_border";
            case "trackinggroup":
            case "tracking_group": return "tracking_group";
            case "spawngroup":
            case "spawn_group": return "spawn_group";
            case "boss": return "boss";
            case "npc": return "npc";
            case "object": return "object";
            case "spawned":
            case "spawned_entity": return "spawned_entity";
            case "position": return "position";
            case "point": return "point";
            default: return "self";
        }
    }

    static void ApplyActionDefaults(string actionName, Dictionary<string, string> parameters)
    {
        if (actionName.StartsWith("zone.border.effect.", StringComparison.OrdinalIgnoreCase))
        {
            parameters["targetType"] = "zone_border";
            if (!parameters.ContainsKey("kind")) parameters["kind"] = "border_vfx";
        }
        else if (actionName.StartsWith("spawn.effect.", StringComparison.OrdinalIgnoreCase))
        {
            if (!parameters.ContainsKey("targetType")) parameters["targetType"] = "spawn_group";
            if (!parameters.ContainsKey("trackingGroup")) parameters["trackingGroup"] = "spawned";
        }
        else if (actionName.StartsWith("tracking.group.effect.", StringComparison.OrdinalIgnoreCase))
        {
            parameters["targetType"] = "tracking_group";
        }
    }

    static void RememberInContext(GameModeContext? context, string key)
    {
        if (context == null)
            return;
        const string stateKey = "runtime_effect_assignment_ids";
        if (!context.State.TryGetValue(stateKey, out var value) || value is not HashSet<string> keys)
        {
            keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            context.State[stateKey] = keys;
        }
        keys.Add(key);
    }

    static string MakeKey(string sessionId, string assignmentId) => $"{sessionId}:{assignmentId}";

    static string Sanitize(string value)
    {
        var chars = (value ?? string.Empty).Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        return new string(chars);
    }

    static string First(Dictionary<string, string> parameters, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return string.Empty;
    }

    static string Required(Dictionary<string, string> parameters, string key)
    {
        var value = First(parameters, key);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required parameter '{key}'.");
        return value;
    }

    static int Int(Dictionary<string, string> parameters, string key, int fallback) =>
        parameters.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    static float Float(Dictionary<string, string> parameters, string key, float fallback) =>
        parameters.TryGetValue(key, out var value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    static bool TryParseFloat3(string value, out float3 result)
    {
        result = float3.zero;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var parts = value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 3 &&
               float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out result.x) &&
               float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out result.y) &&
               float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out result.z);
    }
}
