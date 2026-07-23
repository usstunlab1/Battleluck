using System.Text.Json;
using BattleLuck.Models;
using BattleLuck.Services.Npc;
using Unity.Entities;
using Unity.Mathematics;

namespace BattleLuck.Services.Adaptive;

public sealed class AdaptiveCombatDrillService
{
    public static AdaptiveCombatDrillService Instance { get; } = new();
    readonly SpawnController _spawner = new();
    AdaptiveDrillCatalog? _catalog;

    public void StartEvent(GameModeContext context, ZoneDefinition zone)
    {
        var catalog = LoadCatalog();
        if (!catalog.Events.TryGetValue(context.ModeId, out var definition) && !catalog.Events.TryGetValue("*", out definition)) return;
        if (!definition.Enabled) return;
        var players = VRisingCore.GetOnlinePlayers().Where(p => p.Exists() && p.IsPlayer() && context.Players.Contains(p.GetSteamId())).ToList();
        if (players.Count == 0 || BattleLuckPlugin.NpcService == null) return;
        var profile = new EventParticipantProfile(players.Select(p => new PlayerCombatProfile(p.GetSteamId(), p.GetUnitLevel(), 1f, Math.Clamp(p.GetUnitLevel(), 1, 100))).ToList(), players.Average(p => (float)Math.Clamp(p.GetUnitLevel(), 1, 100)));
        var budget = definition.BaseThreat * players.Count * Math.Clamp(0.75f + profile.AverageStrength / 100f, .75f, 1.5f);
        var selected = definition.Npcs.Where(n => n.ThreatCost > 0 && profile.AverageStrength >= n.MinimumStrength && profile.AverageStrength <= n.MaximumStrength && PrefabHelper.GetValidPrefabGuidDeep(n.Prefab).HasValue)
            .OrderBy(n => n.ThreatCost).ToList();
        var plans = new List<SpawnNpcPlan>(); var remaining = budget; var count = 0;
        while (selected.Count > 0 && count < Math.Clamp(definition.MaximumNpcCount, 1, 32))
        {
            var entry = selected.LastOrDefault(n => n.ThreatCost <= remaining) ?? selected.First();
            if (entry.ThreatCost > remaining && count > 0) break;
            plans.Add(new SpawnNpcPlan(entry.Id, entry.Prefab, 1, entry.Behavior)); remaining -= entry.ThreatCost; count++;
        }
        var plan = new AdaptiveSpawnPlan(context.ModeId, budget, plans);
        context.State["adaptiveSpawnPlan"] = plan;
        var target = players[0]; var center = zone.Position.ToFloat3();
        foreach (var (spawn, index) in plan.Npcs.Select((value, index) => (value, index)))
        {
            var prefab = PrefabHelper.GetValidPrefabGuidDeep(spawn.Prefab)!.Value;
            var pos = center + new float3(3 + index * 2, 0, 3);
            _spawner.SpawnNPC(prefab, pos, entity =>
            {
                var id = $"adaptive_{context.SessionId}_{index}";
                var registration = BattleLuckPlugin.NpcService.RegisterNpc(context.SessionId, id, spawn.Prefab, prefab, entity, pos, 80f);
                if (!registration.Success || registration.Value == null) return;
                if (spawn.Behavior.Equals("follow", StringComparison.OrdinalIgnoreCase)) BattleLuckPlugin.NpcService.Follow(id, target, 4f, 100f);
                else BattleLuckPlugin.NpcService.Aggro(id, target, 3f, 100f);
            });
        }
        BattleLuckPlugin.LogInfo($"[AdaptiveDrills] event={context.ModeId} players={players.Count} strength={profile.AverageStrength:F1} budget={budget:F1} spawns={plans.Count}.");
    }

    AdaptiveDrillCatalog LoadCatalog()
    {
        if (_catalog != null) return _catalog;
        var path = Path.Combine(ConfigLoader.ConfigRoot, "adaptive_drills.json");
        try { _catalog = File.Exists(path) ? JsonSerializer.Deserialize<AdaptiveDrillCatalog>(File.ReadAllText(path), ConfigLoader.JsonOptions) : null; }
        catch (Exception ex) { BattleLuckPlugin.LogWarning($"[AdaptiveDrills] Catalog load failed: {ex.Message}"); }
        return _catalog ??= new AdaptiveDrillCatalog();
    }
}
