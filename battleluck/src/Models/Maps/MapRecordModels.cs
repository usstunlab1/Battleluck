using System;

namespace BattleLuck.Models.Maps;

/// <summary>
/// Immutable castle map record. Ownership and position are resolved from live
/// ProjectM heart state at capture time; never persisted entity handles.
/// </summary>
public sealed class CastleMapRecord
{
    public ulong CastleId { get; init; }

    public ulong OwnerSteamId { get; init; }

    public int TerritoryIndex { get; init; }

    public int CastleLevel { get; init; }

    public float CenterX { get; init; }

    public float CenterZ { get; init; }
}

/// <summary>
/// Immutable zone map record describing an event zone boundary.
/// </summary>
public sealed class ZoneMapRecord
{
    public int ZoneHash { get; init; }

    public string Name { get; init; } = "";

    public float CenterX { get; init; }

    public float CenterZ { get; init; }

    public float Radius { get; init; }

    public bool IsActive { get; init; }
}

/// <summary>
/// Immutable resource map record (e.g., ore veins, quartz nodes).
/// </summary>
public sealed class ResourceMapRecord
{
    public string ResourceId { get; init; } = "";

    public string Name { get; init; } = "";

    public float WorldX { get; init; }

    public float WorldZ { get; init; }

    public float Radius { get; init; }

    public string Category { get; init; } = "general";
}

/// <summary>
/// Immutable waygate map record.
/// </summary>
public sealed class WaygateMapRecord
{
    public ulong WaygateId { get; init; }

    public string Name { get; init; } = "";

    public float WorldX { get; init; }

    public float WorldZ { get; init; }

    public bool IsLinked { get; init; }
}