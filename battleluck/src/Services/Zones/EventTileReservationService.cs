using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace BattleLuck.Services.Zone;

/// <summary>
/// Cross-controller tile reservations for BattleLuck-spawned geometry.
/// Visual event tiles can retain native TilePosition/TileBounds, but a local
/// reservation is still required while queued work is being spawned. The
/// reservation API supports an entire native prefab footprint atomically.
/// </summary>
public static class EventTileReservationService
{
    static readonly object Sync = new();
    static readonly Dictionary<long, string> Reservations = new();
    static readonly Dictionary<string, HashSet<long>> ByOwner = new(StringComparer.Ordinal);

    public static bool TryReserve(string owner, int2 tile, out long key, out string? conflictingOwner)
    {
        key = ToKey(tile);
        conflictingOwner = null;
        if (string.IsNullOrWhiteSpace(owner))
            return false;

        lock (Sync)
        {
            if (Reservations.TryGetValue(key, out conflictingOwner))
                return false;

            Reservations[key] = owner;
            if (!ByOwner.TryGetValue(owner, out var keys))
            {
                keys = new HashSet<long>();
                ByOwner[owner] = keys;
            }
            keys.Add(key);
            return true;
        }
    }

    /// <summary>
    /// Atomically reserve every cell in a native tile footprint. Existing
    /// reservations, including ones owned by the same tracking group, are a
    /// conflict so two separately tracked structures cannot overlap.
    /// </summary>
    public static bool TryReserveMany(
        string owner,
        IEnumerable<int2> tiles,
        out IReadOnlyList<long> keys,
        out string? conflictingOwner)
    {
        keys = Array.Empty<long>();
        conflictingOwner = null;
        if (string.IsNullOrWhiteSpace(owner))
            return false;

        var requested = tiles.Select(ToKey).Distinct().ToArray();
        if (requested.Length == 0)
            return false;

        lock (Sync)
        {
            foreach (var key in requested)
            {
                if (Reservations.TryGetValue(key, out conflictingOwner))
                    return false;
            }

            if (!ByOwner.TryGetValue(owner, out var owned))
            {
                owned = new HashSet<long>();
                ByOwner[owner] = owned;
            }

            foreach (var key in requested)
            {
                Reservations[key] = owner;
                owned.Add(key);
            }

            keys = requested;
            return true;
        }
    }

    public static bool IsReserved(int2 tile, out string? owner)
    {
        lock (Sync)
            return Reservations.TryGetValue(ToKey(tile), out owner);
    }

    public static void Release(string owner, long key)
    {
        lock (Sync)
        {
            if (!Reservations.TryGetValue(key, out var currentOwner) ||
                !currentOwner.Equals(owner, StringComparison.Ordinal))
            {
                return;
            }

            Reservations.Remove(key);
            if (!ByOwner.TryGetValue(owner, out var keys))
                return;
            keys.Remove(key);
            if (keys.Count == 0)
                ByOwner.Remove(owner);
        }
    }

    public static int ReleaseOwner(string owner)
    {
        lock (Sync)
        {
            if (!ByOwner.Remove(owner, out var keys))
                return 0;
            foreach (var key in keys)
            {
                if (Reservations.TryGetValue(key, out var currentOwner) &&
                    currentOwner.Equals(owner, StringComparison.Ordinal))
                {
                    Reservations.Remove(key);
                }
            }
            return keys.Count;
        }
    }

    public static bool TryReplaceOwnerReservations(
        string owner,
        IEnumerable<int2> tiles,
        out string? conflictingOwner)
    {
        conflictingOwner = null;
        var desired = tiles.Select(ToKey).ToHashSet();
        lock (Sync)
        {
            foreach (var key in desired)
            {
                if (Reservations.TryGetValue(key, out var existing) &&
                    !existing.Equals(owner, StringComparison.Ordinal))
                {
                    conflictingOwner = existing;
                    return false;
                }
            }

            if (ByOwner.TryGetValue(owner, out var previous))
            {
                foreach (var key in previous)
                {
                    if (!desired.Contains(key) &&
                        Reservations.TryGetValue(key, out var existing) &&
                        existing.Equals(owner, StringComparison.Ordinal))
                    {
                        Reservations.Remove(key);
                    }
                }
            }

            ByOwner[owner] = desired;
            foreach (var key in desired)
                Reservations[key] = owner;
            return true;
        }
    }

    /// <summary>
    /// Replaces the cells owned by one tracked entity while preserving other
    /// reservations belonging to the same session/group. This is the
    /// footprint-safe counterpart to moving a Blueprint placement.
    /// </summary>
    public static bool TryReplaceReservationSubset(
        string owner,
        IReadOnlyCollection<long> previousKeys,
        IEnumerable<int2> nextTiles,
        out IReadOnlyList<long> nextKeys,
        out string? conflictingOwner)
    {
        nextKeys = Array.Empty<long>();
        conflictingOwner = null;
        if (string.IsNullOrWhiteSpace(owner))
            return false;

        var previous = previousKeys.ToHashSet();
        var desired = nextTiles.Select(ToKey).Distinct().ToArray();
        if (desired.Length == 0)
            return false;

        lock (Sync)
        {
            foreach (var key in desired)
            {
                if (!Reservations.TryGetValue(key, out var existing))
                    continue;
                if (!existing.Equals(owner, StringComparison.Ordinal) || !previous.Contains(key))
                {
                    conflictingOwner = existing;
                    return false;
                }
            }

            if (!ByOwner.TryGetValue(owner, out var owned))
            {
                owned = new HashSet<long>();
                ByOwner[owner] = owned;
            }

            foreach (var key in previous)
            {
                if (Reservations.TryGetValue(key, out var existing) && existing.Equals(owner, StringComparison.Ordinal))
                {
                    Reservations.Remove(key);
                    owned.Remove(key);
                }
            }

            foreach (var key in desired)
            {
                Reservations[key] = owner;
                owned.Add(key);
            }

            if (owned.Count == 0)
                ByOwner.Remove(owner);
            nextKeys = desired;
            return true;
        }
    }

    public static HashSet<long> SnapshotWorldOccupiedTiles()
    {
        var occupied = new HashSet<long>();
        try
        {
            var em = VRisingCore.EntityManager;
            var query = em.CreateEntityQuery(new EntityQueryDesc
            {
                Any = new[] { ComponentType.ReadOnly<TilePosition>(), ComponentType.ReadOnly<TileBounds>() },
                None = new[] { ComponentType.ReadOnly<PlayerCharacter>() }
            });
            var entities = query.ToEntityArray(Allocator.Temp);
            try
            {
                foreach (var entity in entities)
                {
                    if (!em.Exists(entity))
                        continue;

                    if (em.HasComponent<TileBounds>(entity))
                    {
                        var bounds = em.GetComponentData<TileBounds>(entity).Value;
                        var width = (long)bounds.Max.x - bounds.Min.x + 1;
                        var height = (long)bounds.Max.y - bounds.Min.y + 1;
                        // A malformed native entity must not make a routine
                        // occupancy scan unbounded. Normal Blueprint assets
                        // are capped at 64 cells before reaching this point.
                        if (width > 0 && height > 0 && width * height <= 4096)
                        {
                            for (long x = bounds.Min.x; x <= bounds.Max.x; x++)
                            for (long y = bounds.Min.y; y <= bounds.Max.y; y++)
                                occupied.Add(ToKey(new int2((int)x, (int)y)));
                            continue;
                        }
                    }

                    if (em.HasComponent<TilePosition>(entity))
                        occupied.Add(ToKey(em.GetComponentData<TilePosition>(entity).Tile));
                }
            }
            finally
            {
                entities.Dispose();
                query.Dispose();
            }
        }
        catch (Exception ex)
        {
            BattleLuckPlugin.LogWarning($"[TileReservations] World occupancy scan failed: {ex.Message}");
        }

        return occupied;
    }

    public static long ToKey(int2 tile) => ((long)tile.x << 32) ^ (uint)tile.y;

    public static int2 FromKey(long key) => new((int)(key >> 32), unchecked((int)key));
}
