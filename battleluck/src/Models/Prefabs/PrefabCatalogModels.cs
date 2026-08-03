using System;
using System.Collections.Generic;

namespace BattleLuck.Models.Prefabs;

/// <summary>
/// Native ProjectM prefab catalog. Describes real prefabs by PrefabGUID and
/// internal name. Bounds are extracted from live prefab scan, not invented.
/// </summary>
public sealed class PrefabCatalog
{
    public int SchemaVersion { get; init; } = 1;

    public string GameVersion { get; init; } = "detected-at-runtime";

    public IReadOnlyList<PrefabCatalogEntry> Prefabs { get; init; }
        = Array.Empty<PrefabCatalogEntry>();
}

/// <summary>
/// A single native prefab entry. Only real ProjectM prefab identity fields
/// are used; presentation-only assets (mesh/material/collider) are NOT
/// included here unless explicitly CotrexBL presentation assets.
/// </summary>
public sealed class PrefabCatalogEntry
{
    public long PrefabGuid { get; init; }

    public string InternalName { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string Category { get; init; } = "other";

    public PrefabBounds? Bounds { get; init; }

    public string Source { get; init; } = "live-prefab-scan";
}

/// <summary>Axis-aligned bounds of a native prefab.</summary>
public sealed class PrefabBounds
{
    public float[] Min { get; init; } = { 0f, 0f, 0f };
    public float[] Max { get; init; } = { 0f, 0f, 0f };
}