using System.Text.Json.Serialization;

namespace BattleLuck.Models;

public sealed class AdaptiveDrillCatalog
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("events")] public Dictionary<string, AdaptiveEventCatalog> Events { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AdaptiveEventCatalog
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("maximumNpcCount")] public int MaximumNpcCount { get; set; } = 8;
    [JsonPropertyName("baseThreat")] public float BaseThreat { get; set; } = 12;
    [JsonPropertyName("npcs")] public List<AdaptiveNpcCatalogEntry> Npcs { get; set; } = new();
    [JsonPropertyName("drills")] public List<CombatDrillDefinition> Drills { get; set; } = new();
}

public sealed class AdaptiveNpcCatalogEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("prefab")] public string Prefab { get; set; } = "";
    [JsonPropertyName("threatCost")] public float ThreatCost { get; set; } = 5;
    [JsonPropertyName("minimumStrength")] public float MinimumStrength { get; set; }
    [JsonPropertyName("maximumStrength")] public float MaximumStrength { get; set; } = 999;
    [JsonPropertyName("behavior")] public string Behavior { get; set; } = "attack";
}

public sealed class CombatDrillDefinition
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("reaction")] public string Reaction { get; set; } = "counter";
}

public sealed record PlayerCombatProfile(ulong SteamId, int Level, float HealthRatio, float CombatStrength);
public sealed record EventParticipantProfile(IReadOnlyList<PlayerCombatProfile> Players, float AverageStrength);
public sealed record SpawnNpcPlan(string CatalogId, string Prefab, int Count, string Behavior);
public sealed record AdaptiveSpawnPlan(string EventId, float ThreatBudget, IReadOnlyList<SpawnNpcPlan> Npcs);
