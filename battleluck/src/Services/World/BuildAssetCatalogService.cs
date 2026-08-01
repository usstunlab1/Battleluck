using BattleLuck.Models;
using BattleLuck.Utilities;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace BattleLuck.Services.Runtime;

/// <summary>
/// BattleLuck's single, read-only source of truth for editor-visible native
/// build assets.  It combines the checked-in prefab archive with the live
/// server registry at placement time.  The archive makes both the legacy
/// palette and Blueprint editor search the same database; live validation
/// prevents an old archive entry from being spawned after a game update.
/// </summary>
public static class BuildAssetCatalogService
{
    public const int MaxTileFootprintCells = 64;

    public enum AssetKind
    {
        Unsupported,
        Tile,
        Chest
    }

    public sealed record Asset(
        string Name,
        int PrefabGuid,
        AssetKind Kind,
        string Category,
        int2 TileBoundsMin,
        int2 TileBoundsMax,
        bool IsArchived);

    /// <summary>
    /// Search the bundled prefab database.  Results are deliberately not
    /// marked executable: a live server validates each selected entry again.
    /// </summary>
    public static IReadOnlyList<Asset> Search(string? query, int limit = 50, bool includeChests = true)
    {
        var matches = PrefabHelper.SearchArchivedPrefabs(query, Math.Clamp(limit * 4, 1, 500));
        return matches
            .Select(pair => Describe(pair.Guid, pair.Entry.Name, pair.Entry, isArchived: true))
            .Where(asset => asset.Kind == AssetKind.Tile || (includeChests && asset.Kind == AssetKind.Chest))
            .OrderBy(asset => asset.Kind == AssetKind.Tile ? 0 : 1)
            .ThenBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();
    }

    /// <summary>
    /// Resolves an import against the live V Rising prefab map.  It accepts a
    /// stable GUID (preferred) or a canonical name, but never permits chunks,
    /// portals, hearts, or other unsafe world prefabs.
    /// </summary>
    public static OperationResult<Asset> ResolveForBlueprint(
        string? prefabName,
        int prefabGuid,
        string? requestedKind)
    {
        if (!VRisingCore.IsReady)
            return OperationResult<Asset>.Fail("Blueprint placement requires a ready live server prefab registry.");

        if (!TryResolve(prefabName, prefabGuid, out var guid, out var canonicalName))
            return OperationResult<Asset>.Fail("The requested prefab is absent from the BattleLuck database and live prefab registry.");

        if (!PrefabHelper.ValidatePrefab(guid))
            return OperationResult<Asset>.Fail($"Prefab '{canonicalName}' ({guid.GuidHash}) is not present in the live prefab registry.");

        PrefabArchiveEntry? archived = null;
        if (PrefabHelper.TryGetArchivedPrefab(guid, out var entry))
            archived = entry;

        var asset = Describe(guid, canonicalName, archived, isArchived: archived != null);
        var rawKind = requestedKind?.Trim() ?? string.Empty;
        var requested = ParseRequestedKind(rawKind);
        if (rawKind.Length > 0 && requested == AssetKind.Unsupported)
            return OperationResult<Asset>.Fail($"Unknown Blueprint asset kind '{requestedKind}'. Use tile/floor/wall/decor or chest.");
        if (requested != AssetKind.Unsupported && requested != asset.Kind)
            return OperationResult<Asset>.Fail(
                $"Prefab '{asset.Name}' is a {asset.Kind.ToString().ToLowerInvariant()} asset, not '{requestedKind}'.");

        if (asset.Kind == AssetKind.Unsupported)
            return OperationResult<Asset>.Fail(
                $"Prefab '{asset.Name}' is not an allowlisted BattleLuck tile or chest asset.");

        if (asset.Kind == AssetKind.Tile)
        {
            if (!EventTileSafety.TryResolveSafeTileModelPrefab(guid, out _, out var error))
                return OperationResult<Asset>.Fail(error);
        }
        else if (!EventTileSafety.TryResolveSafeSchematicPrefab(guid, out _, out var error))
        {
            return OperationResult<Asset>.Fail(error);
        }

        return OperationResult<Asset>.Ok(asset);
    }

    /// <summary>
    /// Converts an archive footprint to all tile cells occupied after a native
    /// grid rotation.  The origin is the requested TilePosition anchor.
    /// </summary>
    public static OperationResult<IReadOnlyList<int2>> GetOccupiedTiles(Asset asset, int2 anchor, float rotationDegrees)
    {
        if (asset.Kind != AssetKind.Tile)
            return OperationResult<IReadOnlyList<int2>>.Ok(Array.Empty<int2>());

        if (!IsQuarterTurn(rotationDegrees))
            return OperationResult<IReadOnlyList<int2>>.Fail("Native grid rotation must be a multiple of 90 degrees.");

        var min = asset.TileBoundsMin;
        var max = asset.TileBoundsMax;
        if (min.x > max.x || min.y > max.y)
            return OperationResult<IReadOnlyList<int2>>.Fail($"Prefab '{asset.Name}' has invalid tile bounds.");

        var cells = new List<int2>();
        var quarterTurns = ((int)MathF.Round(rotationDegrees / 90f) % 4 + 4) % 4;
        for (var x = min.x; x <= max.x; x++)
        for (var y = min.y; y <= max.y; y++)
        {
            var local = RotateQuarterTurn(new int2(x, y), quarterTurns);
            cells.Add(anchor + local);
            if (cells.Count > MaxTileFootprintCells)
                return OperationResult<IReadOnlyList<int2>>.Fail(
                    $"Prefab '{asset.Name}' occupies more than {MaxTileFootprintCells} grid cells.");
        }

        return OperationResult<IReadOnlyList<int2>>.Ok(cells);
    }

    public static bool IsQuarterTurn(float degrees)
    {
        if (!float.IsFinite(degrees))
            return false;
        var normalized = ((degrees % 90f) + 90f) % 90f;
        return normalized < 0.001f || normalized > 89.999f;
    }

    public static AssetKind Classify(string? prefabName, PrefabArchiveEntry? archived = null)
    {
        var name = prefabName?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return AssetKind.Unsupported;

        if (IsContainerChestName(name))
            return AssetKind.Chest;

        var archiveSaysTile = archived?.Flags.TryGetValue("tilePosition", out var tilePosition) == true && tilePosition;
        return name.StartsWith("TM_", StringComparison.OrdinalIgnoreCase) || archiveSaysTile
            ? AssetKind.Tile
            : AssetKind.Unsupported;
    }

    public static bool IsContainerChestName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (name.StartsWith("Chain_Container_", StringComparison.OrdinalIgnoreCase) ||
         name.StartsWith("TM_Chest_", StringComparison.OrdinalIgnoreCase));

    static Asset Describe(PrefabGUID guid, string name, PrefabArchiveEntry? archived, bool isArchived)
    {
        var min = ReadBounds(archived?.TileBoundsMin, int2.zero);
        var max = ReadBounds(archived?.TileBoundsMax, min);
        var kind = Classify(name, archived);
        return new Asset(
            name,
            guid.GuidHash,
            kind,
            archived?.Category ?? "live",
            min,
            max,
            isArchived);
    }

    static bool TryResolve(string? prefabName, int prefabGuid, out PrefabGUID guid, out string canonicalName)
    {
        guid = PrefabGUID.Empty;
        canonicalName = string.Empty;
        if (prefabGuid != 0)
        {
            guid = new PrefabGUID(prefabGuid);
            canonicalName = PrefabHelper.GetLivePrefabName(guid) ?? PrefabHelper.GetName(guid) ?? prefabName?.Trim() ?? string.Empty;
            if (canonicalName.Length == 0)
                return false;

            if (!string.IsNullOrWhiteSpace(prefabName) &&
                PrefabHelper.TryResolvePrefabReference(prefabName, out var namedGuid, out _) &&
                namedGuid.GuidHash != guid.GuidHash)
            {
                return false;
            }
            return true;
        }

        if (string.IsNullOrWhiteSpace(prefabName) ||
            !PrefabHelper.TryResolvePrefabReference(prefabName, out guid, out canonicalName))
            return false;
        return true;
    }

    static AssetKind ParseRequestedKind(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "visual" or "tile" or "floor" or "wall" or "decor" or "decoration" => AssetKind.Tile,
            "chest" or "loot_chest" or "container" => AssetKind.Chest,
            _ => AssetKind.Unsupported
        };
    }

    static int2 ReadBounds(int[]? input, int2 fallback) =>
        input is { Length: >= 2 } ? new int2(input[0], input[1]) : fallback;

    static int2 RotateQuarterTurn(int2 point, int quarterTurns) => quarterTurns switch
    {
        1 => new int2(point.y, -point.x),
        2 => new int2(-point.x, -point.y),
        3 => new int2(-point.y, point.x),
        _ => point
    };
}
