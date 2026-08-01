using System.Text.RegularExpressions;
using BattleLuck.Core;
using BattleLuck.Models;
using BattleLuck.Services.Runtime;
using BattleLuck.Utilities;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BattleLuck.Services.Geometry;

/// <summary>
/// Session-owned blueprint geometry sourced only from blueprints/active-blueprint.json.
/// It never disables native building restrictions and never executes editor
/// action metadata.
/// </summary>
public sealed class BlueprintGeometryService
{
    public const string ContextKey = "blueprintGeometryRuntime";
    const int MaxPlacements = 400;
    static readonly Regex SafeId = new("^[A-Za-z0-9_.-]{1,64}$", RegexOptions.Compiled);
    readonly Dictionary<string, RuntimePlacement> _active = new(StringComparer.OrdinalIgnoreCase);
    readonly Queue<BlueprintGeometryPlacement> _pending = new();
    readonly Dictionary<string, string> _missing = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string> _globalFailures = new();
    BlueprintGeometryConfig _config = new();
    GameModeContext? _context;
    ZoneDefinition? _zone;

    sealed record RuntimePlacement(BlueprintGeometryPlacement Definition, Entity Entity);
    sealed record NativePlacementPlan(
        BuildAssetCatalogService.Asset Asset,
        float3 WorldPosition,
        IReadOnlyList<int2> FootprintTiles);

    public bool HasPendingBuildWork => _pending.Count > 0;
    public bool Failed => _globalFailures.Count > 0 || _missing.Count > 0;
    public string LastError => _globalFailures.LastOrDefault() ?? _missing.Values.LastOrDefault() ?? "";
    public int ActiveCount => _active.Count;
    public int MissingCount => _missing.Count;
    /// <summary>
    /// Placements which could not be made live in this session. The value is a
    /// repair reason suitable for the admin/editor, not an executable action.
    /// </summary>
    public IReadOnlyDictionary<string, string> MissingPlacements => _missing;
    public static string ConfigPath => Path.Combine(ConfigLoader.ConfigRoot, "blueprints", "active-blueprint.json");

    public OperationResult Start(GameModeContext context, ZoneDefinition zone)
    {
        ResetDiagnostics();
        _pending.Clear();
        _context = context;
        _zone = zone;
        context.State[ContextKey] = this;
        var loaded = LoadConfig();
        if (!loaded.Success || loaded.Value == null)
            return Fail(loaded.Error ?? "Active blueprint could not be loaded.");

        _config = loaded.Value;
        NormalizeConfig(_config);
        if (!_config.Enabled || (_config.ZoneHash != 0 && _config.ZoneHash != zone.Hash))
            return OperationResult.Ok();

        var deduplicated = DeduplicateFirst(_config.Placements).ToList();
        if (deduplicated.Count != _config.Placements.Count)
        {
            BattleLuckPlugin.LogWarning("[BlueprintGeometry] Duplicate placement ids removed; the first placement was kept.");
            _config.Placements = deduplicated;
            var saved = SaveConfig(_config);
            if (!saved.Success) return Fail(saved.Error ?? "Deduplicated blueprint could not be saved.");
        }
        var validation = Validate(_config, zone.Radius);
        if (!validation.Success)
            return Fail(validation.Error ?? "Active blueprint validation failed.");

        foreach (var placement in DeduplicateFirst(_config.Placements))
            _pending.Enqueue(placement);
        return OperationResult.Ok();
    }

    public bool Tick()
    {
        if (_context == null || _zone == null)
            return false;

        for (var i = 0; i < Math.Clamp(_config.BatchSize, 1, 2) && _pending.TryDequeue(out var placement); i++)
        {
            if (_active.ContainsKey(placement.Id))
            {
                ClearMissing(placement.Id);
                continue;
            }

            var plan = PlanLivePlacement(placement, _zone, _config.BorderMargin);
            if (!plan.Success || plan.Value == null)
            {
                RecordMissing(placement.Id, plan.Error ?? $"Prefab for '{placement.Id}' is unavailable.");
                continue;
            }

            var spawnKind = plan.Value.Asset.Kind == BuildAssetCatalogService.AssetKind.Tile
                ? "blueprint"
                : "blueprint_chest";
            var spawned = SchematicLoader.SpawnPrefabAt(
                plan.Value.Asset.Name,
                plan.Value.WorldPosition,
                placement.Rotation,
                spawnKind,
                _context.SessionId,
                plan.Value.FootprintTiles);
            if (!spawned.Success || spawned.Value == null)
            {
                RecordMissing(placement.Id, spawned.Error ?? "The native prefab could not be spawned.");
                continue;
            }
            _active[placement.Id] = new RuntimePlacement(Clone(placement), spawned.Value.Entity);
            ClearMissing(placement.Id);
        }
        return HasPendingBuildWork;
    }

    public OperationResult Adjust(string id, Vec3Config? offset, float? rotation)
    {
        if (!_active.TryGetValue(id, out var current) || !current.Entity.Exists())
            return OperationResult.Fail($"Geometry '{id}' does not exist; adjust never spawns missing geometry.");
        var zone = _zone;
        if (zone == null)
            return OperationResult.Fail("Active blueprint zone is unavailable.");
        var next = Clone(current.Definition);
        if (offset != null) next.Offset = offset;
        if (rotation.HasValue) next.Rotation = NormalizeRotation(rotation.Value);
        var valid = ValidatePlacement(next, zone.Radius, _config.BorderMargin);
        if (!valid.Success) return valid;
        var plan = PlanLivePlacement(next, zone, _config.BorderMargin);
        if (!plan.Success || plan.Value == null)
            return OperationResult.Fail(plan.Error ?? $"Geometry '{id}' could not be prepared for native placement.");

        var updatedConfig = ReplaceConfigCopy(_config, next);
        if (updatedConfig == null)
            return OperationResult.Fail($"Geometry '{id}' is no longer present in the active blueprint configuration.");
        var saved = SaveConfig(updatedConfig);
        if (!saved.Success)
            return saved;

        var moved = ApplyRuntimeTransform(current.Entity, _context?.SessionId ?? "", next, plan.Value);
        if (!moved.Success)
        {
            var rollback = SaveConfig(_config);
            var rollbackNote = rollback.Success ? " Configuration was restored." : $" Configuration rollback also failed: {rollback.Error}";
            return OperationResult.Fail($"Geometry '{id}' was not moved: {moved.Error}.{rollbackNote}");
        }

        _config = updatedConfig;
        _active[id] = new RuntimePlacement(next, current.Entity);
        ClearMissing(id);
        return OperationResult.Ok();
    }

    public OperationResult Delete(string id)
    {
        var hasActive = _active.TryGetValue(id, out var current);
        var hasPending = _pending.Any(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        var hasConfigured = _config.Placements.Any(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (!hasActive && !hasPending && !hasConfigured && !_missing.ContainsKey(id))
            return OperationResult.Fail($"Geometry '{id}' does not exist.");

        if (hasConfigured)
        {
            var updatedConfig = CloneConfig(_config);
            updatedConfig.Placements.RemoveAll(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            var saved = SaveConfig(updatedConfig);
            if (!saved.Success)
                return saved;
            _config = updatedConfig;
        }

        RemovePending(id);
        ClearMissing(id);

        if (!hasActive || current == null)
            return OperationResult.Ok();
        if (!current.Entity.Exists())
        {
            _active.Remove(id);
            return OperationResult.Ok();
        }

        var cleared = SchematicLoader.ClearTrackedEntity(_context?.SessionId ?? "", current.Entity);
        if (!cleared.Success)
        {
            RecordMissing(id, $"Deletion was saved, but the live entity could not be cleared: {cleared.Error ?? "unknown error"}");
            return OperationResult.Fail(cleared.Error ?? $"Geometry '{id}' could not be deleted.");
        }
        _active.Remove(id);
        return OperationResult.Ok();
    }

    public OperationResult Keep(string id)
    {
        if (!_active.TryGetValue(id, out var current) || !current.Entity.Exists())
            return OperationResult.Fail($"Geometry '{id}' does not exist.");
        var next = Clone(current.Definition);
        next.Keep = true;
        var updatedConfig = ReplaceConfigCopy(_config, next);
        if (updatedConfig == null)
            return OperationResult.Fail($"Geometry '{id}' is no longer present in the active blueprint configuration.");
        var saved = SaveConfig(updatedConfig);
        if (!saved.Success)
            return saved;
        _config = updatedConfig;
        _active[id] = new RuntimePlacement(next, current.Entity);
        ClearMissing(id);
        return OperationResult.Ok();
    }

    /// <summary>Adjusts/deletes existing entities only. It deliberately never spawns.</summary>
    public OperationResult Reconcile()
    {
        var loaded = LoadConfig();
        if (!loaded.Success || loaded.Value == null)
            return Fail(loaded.Error ?? "Active blueprint reload failed.");
        var next = loaded.Value;
        NormalizeConfig(next);
        var validation = Validate(next, _zone?.Radius ?? 0f);
        if (!validation.Success)
            return Fail(validation.Error ?? "Active blueprint validation failed.");

        _globalFailures.Clear();
        var byId = next.Placements.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (var id in _active.Keys.ToArray())
        {
            var current = _active[id];
            if (!current.Entity.Exists())
            {
                _active.Remove(id);
                if (byId.ContainsKey(id))
                    RecordMissing(id, "The tracked live entity no longer exists; reconcile never spawns missing geometry.");
                continue;
            }

            if (!byId.TryGetValue(id, out var desired))
            {
                if (!current.Definition.Keep)
                {
                    var cleared = SchematicLoader.ClearTrackedEntity(_context?.SessionId ?? "", current.Entity);
                    if (cleared.Success)
                    {
                        _active.Remove(id);
                        ClearMissing(id);
                    }
                    else
                    {
                        var error = cleared.Error ?? "The tracked live entity could not be cleared.";
                        RecordMissing(id, $"Removed from configuration but could not be cleared: {error}");
                        failures.Add($"{id}: {error}");
                    }
                }
                continue;
            }

            if (!HasSamePrefab(current.Definition, desired))
            {
                const string error = "The configured prefab changed; reconcile never replaces live geometry. Start a fresh session or repair it in Build Mode.";
                RecordMissing(id, error);
                failures.Add($"{id}: prefab changed");
                continue;
            }

            if (_zone == null)
            {
                const string error = "Active blueprint zone is unavailable.";
                RecordMissing(id, error);
                failures.Add($"{id}: {error}");
                continue;
            }

            var plan = PlanLivePlacement(desired, _zone, next.BorderMargin);
            if (!plan.Success || plan.Value == null)
            {
                var error = plan.Error ?? "The native placement could not be prepared.";
                RecordMissing(id, error);
                failures.Add($"{id}: {error}");
                continue;
            }

            var result = ApplyRuntimeTransform(current.Entity, _context?.SessionId ?? "", desired, plan.Value);
            if (!result.Success)
            {
                RecordMissing(id, result.Error ?? "The live entity could not be adjusted.");
                failures.Add($"{id}: {result.Error}");
                continue;
            }

            _active[id] = new RuntimePlacement(Clone(desired), current.Entity);
            ClearMissing(id);
        }

        // Reconcile is deliberately non-spawning. Existing queued work is
        // updated/cancelled against the newly loaded file, but a placement that
        // was added only to disk is reported for a later session or manual
        // build-mode repair rather than queued here.
        var pendingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var previousPending = _pending.ToArray();
        _pending.Clear();
        foreach (var pending in previousPending)
        {
            if (!byId.TryGetValue(pending.Id, out var desired) || _active.ContainsKey(pending.Id) || !pendingIds.Add(pending.Id))
                continue;
            _pending.Enqueue(Clone(desired));
        }

        foreach (var desired in byId.Values)
        {
            if (!_active.ContainsKey(desired.Id) && !pendingIds.Contains(desired.Id))
                RecordMissing(desired.Id, "Configured placement is not active; reconcile never spawns it. Start a fresh session or repair it in Build Mode.");
        }

        _config = next;
        return failures.Count == 0
            ? OperationResult.Ok()
            : OperationResult.Fail($"Blueprint reconcile completed with errors: {string.Join("; ", failures.Take(3))}");
    }

    public string Status()
    {
        var repair = _missing.Count == 0
            ? ""
            : $", repair={string.Join(" | ", _missing.Take(5).Select(x => $"{x.Key}: {x.Value}"))}";
        return $"blueprint geometry enabled={_config.Enabled}, active={ActiveCount}, pending={_pending.Count}, missing={MissingCount}, failed={Failed}" +
               (Failed ? $", error={LastError}" : "") + repair;
    }

    public void Cleanup()
    {
        _pending.Clear();
        _active.Clear();
        ResetDiagnostics();
        if (_context != null)
            _context.State.Remove(ContextKey);
        _context = null;
        _zone = null;
    }

    public static OperationResult<BlueprintGeometryConfig> LoadConfig()
    {
        try
        {
            EnsureDefaultConfig();
            var config = JsonSerializer.Deserialize<BlueprintGeometryConfig>(File.ReadAllText(ConfigPath), ConfigLoader.JsonOptions);
            return config == null
                ? OperationResult<BlueprintGeometryConfig>.Fail("blueprints/active-blueprint.json is empty.")
                : OperationResult<BlueprintGeometryConfig>.Ok(config);
        }
        catch (Exception ex) { return OperationResult<BlueprintGeometryConfig>.Fail(ex.Message); }
    }

    public static OperationResult SaveConfig(BlueprintGeometryConfig config)
    {
        var valid = Validate(config, radius: 0f);
        if (!valid.Success) return valid;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var temp = ConfigPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(config, new JsonSerializerOptions(ConfigLoader.JsonOptions) { WriteIndented = true }));
            File.Move(temp, ConfigPath, true);
            return OperationResult.Ok();
        }
        catch (Exception ex) { return OperationResult.Fail(ex.Message); }
    }

    public static OperationResult Validate(BlueprintGeometryConfig config, float radius)
    {
        if (config == null) return OperationResult.Fail("Blueprint configuration is required.");
        if (config.Placements == null) return OperationResult.Fail("Blueprint placements are required.");
        if (config.Placements.Count > MaxPlacements) return OperationResult.Fail($"Blueprint exceeds {MaxPlacements} placements.");
        if (config.BatchSize is < 1 or > 2) return OperationResult.Fail("batchSize must be 1 or 2.");
        if (!float.IsFinite(config.BorderMargin) || config.BorderMargin < 0f)
            return OperationResult.Fail("borderMargin must be a finite non-negative value.");
        var duplicate = config.Placements.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null) return OperationResult.Fail($"Duplicate geometry id '{duplicate.Key}'.");
        foreach (var placement in config.Placements)
        {
            var result = ValidatePlacement(placement, radius, config.BorderMargin);
            if (!result.Success) return result;
        }
        return OperationResult.Ok();
    }

    public static OperationResult ValidatePlacement(BlueprintGeometryPlacement placement, float radius, float margin)
    {
        if (placement == null) return OperationResult.Fail("Blueprint placement is required.");
        if (!SafeId.IsMatch(placement.Id ?? "")) return OperationResult.Fail($"Invalid geometry id '{placement.Id}'.");
        if (string.IsNullOrWhiteSpace(placement.Prefab) && placement.PrefabGuid == 0)
            return OperationResult.Fail($"Geometry '{placement.Id}' needs prefab or prefabGuid.");
        var requestedKind = placement.Kind?.Trim().ToLowerInvariant() ?? "visual";
        var isChest = requestedKind is "chest" or "loot_chest" or "container";
        var isTile = requestedKind is "visual" or "tile" or "floor" or "wall" or "decor" or "decoration";
        if (!isChest && !isTile)
            return OperationResult.Fail($"Geometry '{placement.Id}' has unsupported kind '{placement.Kind}'. Use tile/floor/wall/decor or chest.");
        if (!string.IsNullOrWhiteSpace(placement.Prefab))
        {
            if (isChest && !BuildAssetCatalogService.IsContainerChestName(placement.Prefab))
                return OperationResult.Fail($"Chest geometry '{placement.Id}' must use a Chain_Container_ or TM_Chest prefab.");
            if (isTile && !placement.Prefab.StartsWith("TM_", StringComparison.OrdinalIgnoreCase))
                return OperationResult.Fail($"Geometry '{placement.Id}' must use a TM_ tile-model prefab.");
        }
        var o = placement.Offset;
        if (o == null) return OperationResult.Fail($"Geometry '{placement.Id}' needs an offset.");
        if (!float.IsFinite(o.X) || !float.IsFinite(o.Y) || !float.IsFinite(o.Z) || !float.IsFinite(placement.Rotation))
            return OperationResult.Fail($"Geometry '{placement.Id}' contains non-finite coordinates.");
        if (isTile && !BuildAssetCatalogService.IsQuarterTurn(placement.Rotation))
            return OperationResult.Fail($"Geometry '{placement.Id}' must use a native grid rotation that is a multiple of 90 degrees.");
        if (radius > 0f && math.length(new float2(o.X, o.Z)) > Math.Max(0f, radius - Math.Max(0f, margin)))
            return OperationResult.Fail($"Geometry '{placement.Id}' is outside the zone border.");
        if (placement.Actions == null || placement.Actions.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 80))
            return OperationResult.Fail($"Geometry '{placement.Id}' has invalid inert action metadata.");
        return OperationResult.Ok();
    }

    static OperationResult<NativePlacementPlan> PlanLivePlacement(
        BlueprintGeometryPlacement placement,
        ZoneDefinition zone,
        float borderMargin)
    {
        var asset = BuildAssetCatalogService.ResolveForBlueprint(placement.Prefab, placement.PrefabGuid, placement.Kind);
        if (!asset.Success || asset.Value == null)
            return OperationResult<NativePlacementPlan>.Fail(asset.Error ?? $"Prefab for '{placement.Id}' is unavailable.");

        var position = WorldPosition(zone, placement.Offset);
        var withinZone = ValidateWorldPointInZone(position, zone, borderMargin);
        if (!withinZone.Success)
            return OperationResult<NativePlacementPlan>.Fail($"Geometry '{placement.Id}' {withinZone.Error}");

        if (asset.Value.Kind == BuildAssetCatalogService.AssetKind.Chest)
            return OperationResult<NativePlacementPlan>.Ok(new NativePlacementPlan(asset.Value, position, Array.Empty<int2>()));

        var anchor = ToNativeTileAnchor(position);
        if (!anchor.Success)
            return OperationResult<NativePlacementPlan>.Fail($"Geometry '{placement.Id}' {anchor.Error}");
        var footprint = BuildAssetCatalogService.GetOccupiedTiles(asset.Value, anchor.Value, placement.Rotation);
        if (!footprint.Success || footprint.Value == null)
            return OperationResult<NativePlacementPlan>.Fail($"Geometry '{placement.Id}' {footprint.Error}");

        foreach (var tile in footprint.Value)
        {
            var contained = ValidateTileInZone(tile, zone, borderMargin);
            if (!contained.Success)
                return OperationResult<NativePlacementPlan>.Fail($"Geometry '{placement.Id}' {contained.Error}");
        }

        return OperationResult<NativePlacementPlan>.Ok(new NativePlacementPlan(asset.Value, position, footprint.Value));
    }

    static void EnsureDefaultConfig()
    {
        if (File.Exists(ConfigPath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new BlueprintGeometryConfig(), new JsonSerializerOptions(ConfigLoader.JsonOptions) { WriteIndented = true }));
    }

    static IEnumerable<BlueprintGeometryPlacement> DeduplicateFirst(IEnumerable<BlueprintGeometryPlacement> placements)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var placement in placements)
            if (seen.Add(placement.Id))
                yield return placement;
    }

    static float3 WorldPosition(ZoneDefinition zone, Vec3Config offset)
    {
        var center = zone.Center.ToFloat3();
        return center + new float3(offset.X, offset.Y, offset.Z);
    }

    static OperationResult<int2> ToNativeTileAnchor(float3 worldPosition)
    {
        var scaledX = worldPosition.x * 2f;
        var scaledZ = worldPosition.z * 2f;
        var roundedX = math.round(scaledX);
        var roundedZ = math.round(scaledZ);
        if (math.abs(scaledX - roundedX) > 0.001f || math.abs(scaledZ - roundedZ) > 0.001f)
            return OperationResult<int2>.Fail("must be aligned to the native 0.5-unit tile grid.");
        return OperationResult<int2>.Ok(new int2((int)roundedX, (int)roundedZ));
    }

    static OperationResult ValidateWorldPointInZone(float3 worldPosition, ZoneDefinition zone, float borderMargin)
    {
        var availableRadius = zone.Radius - Math.Max(0f, borderMargin);
        if (availableRadius <= 0f)
            return OperationResult.Fail("cannot fit inside the configured zone border.");

        var center = zone.Center.ToFloat3();
        var distance = math.length(new float2(worldPosition.x - center.x, worldPosition.z - center.z));
        return distance <= availableRadius
            ? OperationResult.Ok()
            : OperationResult.Fail("is outside the configured zone border.");
    }

    static OperationResult ValidateTileInZone(int2 tile, ZoneDefinition zone, float borderMargin)
    {
        // TilePosition is in half-world-unit cells. Include a cell's diagonal
        // radius so a multi-cell prefab is wholly inside the border, not only
        // its anchor point.
        const float CellCircumradius = 0.354f;
        var availableRadius = zone.Radius - Math.Max(0f, borderMargin);
        if (availableRadius <= CellCircumradius)
            return OperationResult.Fail("cannot fit inside the configured zone border.");

        var center = zone.Center.ToFloat3();
        var worldCell = new float2(tile.x * 0.5f, tile.y * 0.5f);
        var distance = math.length(worldCell - new float2(center.x, center.z));
        return distance + CellCircumradius <= availableRadius
            ? OperationResult.Ok()
            : OperationResult.Fail("has a native footprint cell outside the configured zone border.");
    }

    static float NormalizeRotation(float value) => ((value % 360f) + 360f) % 360f;
    static BlueprintGeometryPlacement Clone(BlueprintGeometryPlacement value) =>
        new() { Id = value.Id, Prefab = value.Prefab, PrefabGuid = value.PrefabGuid, Kind = value.Kind,
            Offset = new Vec3Config { X = value.Offset.X, Y = value.Offset.Y, Z = value.Offset.Z },
            Rotation = value.Rotation, Keep = value.Keep, Actions = value.Actions.ToList() };

    static BlueprintGeometryConfig CloneConfig(BlueprintGeometryConfig source) =>
        new()
        {
            Version = source.Version,
            Enabled = source.Enabled,
            ZoneHash = source.ZoneHash,
            BorderMargin = source.BorderMargin,
            BatchSize = source.BatchSize,
            Placements = source.Placements.Select(Clone).ToList()
        };

    static BlueprintGeometryConfig? ReplaceConfigCopy(BlueprintGeometryConfig source, BlueprintGeometryPlacement placement)
    {
        var copy = CloneConfig(source);
        var index = copy.Placements.FindIndex(x => x.Id.Equals(placement.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;
        copy.Placements[index] = Clone(placement);
        return copy;
    }

    static void NormalizeConfig(BlueprintGeometryConfig config)
    {
        config.Placements ??= new List<BlueprintGeometryPlacement>();
        foreach (var placement in config.Placements)
        {
            placement.Id ??= "";
            placement.Prefab ??= "";
            placement.Kind ??= "visual";
            placement.Offset ??= new Vec3Config();
            placement.Actions ??= new List<string>();
        }
    }

    static bool HasSamePrefab(BlueprintGeometryPlacement left, BlueprintGeometryPlacement right) =>
        (left.PrefabGuid != 0 && right.PrefabGuid != 0
            ? left.PrefabGuid == right.PrefabGuid
            : left.Prefab.Equals(right.Prefab, StringComparison.OrdinalIgnoreCase)) &&
        left.Kind.Equals(right.Kind, StringComparison.OrdinalIgnoreCase);

    static OperationResult ApplyRuntimeTransform(
        Entity entity,
        string trackingGroup,
        BlueprintGeometryPlacement placement,
        NativePlacementPlan plan) =>
        SchematicLoader.UpdateTrackedBlueprintEntity(
            trackingGroup,
            entity,
            plan.WorldPosition,
            placement.Rotation,
            plan.FootprintTiles);

    void RemovePending(string id)
    {
        if (_pending.Count == 0) return;
        var retained = _pending.Where(x => !x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToArray();
        _pending.Clear();
        foreach (var placement in retained)
            _pending.Enqueue(placement);
    }

    void RecordMissing(string id, string error)
    {
        _missing[id] = error;
        BattleLuckPlugin.LogError($"[BlueprintGeometry] Placement '{id}' requires repair: {error}");
    }

    void ClearMissing(string id) => _missing.Remove(id);

    void ResetDiagnostics()
    {
        _missing.Clear();
        _globalFailures.Clear();
    }

    OperationResult Fail(string error)
    {
        _globalFailures.Add(error);
        BattleLuckPlugin.LogError($"[BlueprintGeometry] {error}");
        return OperationResult.Fail(error);
    }
}
