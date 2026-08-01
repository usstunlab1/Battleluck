using BattleLuck.Models;
using Unity.Entities;
using Unity.Mathematics;

namespace BattleLuck.Services.Boss;

/// <summary>
/// Manages boss stat scaling, baseline capture, and restoration.
/// All boss.scale.* actions dispatch through this service.
/// </summary>
public sealed class BossScalingService
{
    readonly object _lock = new();

    // Captured baselines: entityId -> baseline stats
    readonly Dictionary<string, BossBaseline> _baselines = new(StringComparer.OrdinalIgnoreCase);
    // Active scaling: entityId -> scaling config
    readonly Dictionary<string, BossScalingConfig> _activeScaling = new(StringComparer.OrdinalIgnoreCase);

    public sealed record BossBaseline(
        float MaxHealth,
        float AttackPower,
        float SpellPower,
        float PhysicalPower,
        int Level);

    public sealed record BossScalingConfig(
        int PlayerCount,
        float HealthPerAdditionalPlayer,
        float PowerPerAdditionalPlayer,
        float LevelPerAdditionalPlayer,
        int MaximumPlayersCounted);

    /// <summary>
    /// Capture a boss's current stats as a baseline for later restoration.
    /// </summary>
    public OperationResult CaptureBaseline(string entityId, Entity entity)
    {
        if (!entity.Exists())
            return OperationResult.Fail($"Entity '{entityId}' does not exist.");

        var health = entity.GetMaxHealth();
        var attackPower = entity.GetAttackPower();
        var spellPower = entity.GetSpellPower();
        var physicalPower = entity.GetPhysicalPower();
        var level = entity.GetLevel();

        lock (_lock)
        {
            _baselines[entityId] = new BossBaseline(health, attackPower, spellPower, physicalPower, level);
        }

        return OperationResult.Ok();
    }

    /// <summary>
    /// Scale a boss's stats based on player count.
    /// </summary>
    public OperationResult ApplyScaling(string entityId, Entity entity, int playerCount,
        float healthPerAdditionalPlayer, float powerPerAdditionalPlayer,
        float levelPerAdditionalPlayer, int maximumPlayersCounted)
    {
        if (!entity.Exists())
            return OperationResult.Fail($"Entity '{entityId}' does not exist.");

        // Capture baseline if not already captured
        if (!_baselines.ContainsKey(entityId))
        {
            var captureResult = CaptureBaseline(entityId, entity);
            if (!captureResult.Success)
                return captureResult;
        }

        var config = new BossScalingConfig(
            playerCount,
            healthPerAdditionalPlayer,
            powerPerAdditionalPlayer,
            levelPerAdditionalPlayer,
            maximumPlayersCounted > 0 ? maximumPlayersCounted : playerCount);

        lock (_lock)
        {
            _activeScaling[entityId] = config;
        }

        var baseline = _baselines[entityId];
        var effectivePlayers = Math.Min(playerCount, config.MaximumPlayersCounted);
        var additionalPlayers = Math.Max(0, effectivePlayers - 1);

        // Apply scaled stats
        var healthMultiplier = 1f + (healthPerAdditionalPlayer * additionalPlayers);
        var powerMultiplier = 1f + (powerPerAdditionalPlayer * additionalPlayers);
        var levelBonus = (int)(levelPerAdditionalPlayer * additionalPlayers);

        entity.SetMaxHealth(baseline.MaxHealth * healthMultiplier);
        entity.SetAttackPower(baseline.AttackPower * powerMultiplier);
        entity.SetSpellPower(baseline.SpellPower * powerMultiplier);
        entity.SetPhysicalPower(baseline.PhysicalPower * powerMultiplier);
        entity.SetLevel(baseline.Level + levelBonus);

        return OperationResult.Ok();
    }

    /// <summary>
    /// Recalculate boss scaling when active attacker count changes.
    /// </summary>
    public OperationResult RefreshScaling(string entityId, Entity entity, int currentPlayerCount)
    {
        lock (_lock)
        {
            if (!_activeScaling.TryGetValue(entityId, out var config))
                return OperationResult.Fail($"No active scaling config for '{entityId}'.");

            return ApplyScaling(entityId, entity, currentPlayerCount,
                config.HealthPerAdditionalPlayer, config.PowerPerAdditionalPlayer,
                config.LevelPerAdditionalPlayer, config.MaximumPlayersCounted);
        }
    }

    /// <summary>
    /// Restore a boss's original stats from captured baseline.
    /// </summary>
    public OperationResult ResetScaling(string entityId, Entity entity)
    {
        if (!entity.Exists())
            return OperationResult.Fail($"Entity '{entityId}' does not exist.");

        lock (_lock)
        {
            if (!_baselines.TryGetValue(entityId, out var baseline))
                return OperationResult.Fail($"No baseline captured for '{entityId}'.");

            entity.SetMaxHealth(baseline.MaxHealth);
            entity.SetAttackPower(baseline.AttackPower);
            entity.SetSpellPower(baseline.SpellPower);
            entity.SetPhysicalPower(baseline.PhysicalPower);
            entity.SetLevel(baseline.Level);

            _activeScaling.Remove(entityId);
            _baselines.Remove(entityId);
        }

        return OperationResult.Ok();
    }
}