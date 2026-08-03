using System;
using System.Collections.Generic;

namespace BattleLuck.Models.Castles;

/// <summary>
/// Immutable castle snapshot captured from live ProjectM state. Never
/// persists raw Unity Entity handles — only stable primitive values.
/// </summary>
public sealed class CastleSnapshot
{
    public string SnapshotId { get; init; } = "";

    public ulong OwnerSteamId { get; init; }

    public int TerritoryIndex { get; init; }

    public int CastleLevel { get; init; }

    public Vector3Dto Center { get; init; } = new();

    public IReadOnlyList<CastleSnapshotObject> Objects { get; init; }
        = Array.Empty<CastleSnapshotObject>();

    public string ContentHash { get; init; } = "";
}

/// <summary>
/// Immutable snapshot of a single castle object. All values are stable
/// primitives; no ECS handles are stored.
/// </summary>
public sealed class CastleSnapshotObject
{
    public int PrefabGuid { get; init; }

    public string PrefabName { get; init; } = "";

    public Vector3Dto Position { get; init; } = new();

    public float Rotation { get; init; }

    public float Scale { get; init; } = 1f;

    public string Category { get; init; } = "other";

    public int Floor { get; init; }

    public string StableObjectKey { get; init; } = "";

    public BoundsDto? Bounds { get; init; }

    public IReadOnlyList<string> DependencyReferences { get; init; }
        = Array.Empty<string>();
}

/// <summary>Immutable 3D vector (world or relative).</summary>
public sealed class Vector3Dto
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}

/// <summary>Immutable axis-aligned bounds.</summary>
public sealed class BoundsDto
{
    public Vector3Dto Min { get; init; } = new();
    public Vector3Dto Max { get; init; } = new();
}