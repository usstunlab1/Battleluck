using System;
using System.Collections.Generic;
using BattleLuck.Models.Maps;
using BattleLuck.Services.Castles;
using ProjectM.CastleBuilding;
using Unity.Entities;

namespace BattleLuck.Services.Maps;

/// <summary>
/// Builds an immutable castle map projection by resolving live CastleHeart
/// entities and re-deriving ownership through <see cref="CastleObjectResolver"/>.
/// The builder never persists ECS handles — only primitive values are emitted.
/// </summary>
public sealed class CastleMapProjectionBuilder
{
    readonly CastleObjectResolver _resolver;

    public CastleMapProjectionBuilder(CastleObjectResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>
    /// Enumerate all live castle hearts and produce an immutable castle map
    /// record list. Returns empty when the world is not ready.
    /// </summary>
    public IReadOnlyList<CastleMapRecord> Build()
    {
        if (_resolver == null || !_resolver.IsWorldReady || !VRisingCore.IsReady)
            return Array.Empty<CastleMapRecord>();

        var records = new List<CastleMapRecord>();
        var em = VRisingCore.EntityManager;

        try
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<CastleHeart>());
            var hearts = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            try
            {
                foreach (var heart in hearts)
                {
                    if (!em.Exists(heart)) continue;

                    var record = BuildRecord(heart);
                    if (record != null)
                        records.Add(record);
                }
            }
            finally
            {
                hearts.Dispose();
                query.Dispose();
            }
        }
        catch
        {
            // A projection failure must never crash the server tick.
            return Array.Empty<CastleMapRecord>();
        }

        return records;
    }

    CastleMapRecord? BuildRecord(Entity heart)
    {
        if (!heart.Exists()) return null;

        try
        {
            if (!heart.Has<Translation>()) return null;

            var position = heart.Read<Translation>().Value;
            var ownerSteamId = _resolver.ResolveOwnerOfHeart(heart);

            // Territory index and castle level are intentionally left at 0 until
            // the exact ProjectM component field names are verified from live
            // prefab/assembly inspection (corrected plan: verify before use).

            return new CastleMapRecord
            {
                CastleId = (ulong)heart.Index, // runtime index is NOT persisted; transient in-memory id only
                OwnerSteamId = ownerSteamId,
                TerritoryIndex = 0,
                CastleLevel = 0,
                CenterX = position.x,
                CenterZ = position.z
            };
        }
        catch
        {
            return null;
        }
    }
}