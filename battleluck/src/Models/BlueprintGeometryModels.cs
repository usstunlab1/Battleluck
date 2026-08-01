using System.Text.Json.Serialization;

namespace BattleLuck.Models;

/// <summary>Persistent server-side definition for an active native blueprint.</summary>
public sealed class BlueprintGeometryConfig
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("zoneHash")] public int ZoneHash { get; set; }
    [JsonPropertyName("borderMargin")] public float BorderMargin { get; set; } = 1f;
    [JsonPropertyName("batchSize")] public int BatchSize { get; set; } = 2;
    [JsonPropertyName("placements")] public List<BlueprintGeometryPlacement> Placements { get; set; } = new();
}

public sealed class BlueprintGeometryPlacement
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("prefab")] public string Prefab { get; set; } = "";
    [JsonPropertyName("prefabGuid")] public int PrefabGuid { get; set; }
    [JsonPropertyName("kind")] public string Kind { get; set; } = "visual";
    [JsonPropertyName("offset")] public Vec3Config Offset { get; set; } = new();
    [JsonPropertyName("rotation")] public float Rotation { get; set; }
    [JsonPropertyName("keep")] public bool Keep { get; set; }

    // Editor metadata only. The game runtime never executes these strings.
    [JsonPropertyName("actions")] public List<string> Actions { get; set; } = new();
}
