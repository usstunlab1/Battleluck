using System;
using System.Collections.Generic;

namespace BattleLuck.Models.Maps;

/// <summary>
/// Immutable read-only projection of the live world that is produced on the
/// server tick and consumed by CotrexBL. Never contains mutable ECS handles.
/// </summary>
public sealed class MapSnapshot
{
    public long Revision { get; init; }

    public DateTime CapturedAtUtc { get; init; }

    public IReadOnlyList<CastleMapRecord> Castles { get; init; }
        = Array.Empty<CastleMapRecord>();

    public IReadOnlyList<ZoneMapRecord> Zones { get; init; }
        = Array.Empty<ZoneMapRecord>();

    public IReadOnlyList<ResourceMapRecord> Resources { get; init; }
        = Array.Empty<ResourceMapRecord>();

    public IReadOnlyList<WaygateMapRecord> Waygates { get; init; }
        = Array.Empty<WaygateMapRecord>();
}