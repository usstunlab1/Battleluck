using Unity.Entities;

namespace BattleLuck.Models.Effects;

public enum RuntimeEffectKind
{
    Buff,
    Glow,
    AttachedVfx,
    WorldVfx,
    BorderVfx
}

public enum RuntimeEffectCleanupPolicy
{
    Manual,
    Timed,
    OnStageExit,
    OnZoneExit,
    OnEventExit,
    OnEntityDestroyed
}

/// <summary>
/// Runtime-only effect assignment. These records are never owned by kits,
/// weapons, or ability loadouts. Persistence is an explicit higher-level action.
/// </summary>
public sealed class RuntimeEffectAssignment
{
    public string AssignmentId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string ModeId { get; init; } = string.Empty;
    public string TrackingGroup { get; init; } = string.Empty;
    public string NativeTrackingGroup { get; init; } = string.Empty;
    public string SourceAction { get; init; } = string.Empty;
    public string TargetType { get; init; } = string.Empty;
    public string PrefabName { get; init; } = string.Empty;
    public int PrefabGuidHash { get; init; }
    public int ZoneHash { get; init; }
    public ulong OwnerSteamId { get; init; }
    public RuntimeEffectKind Kind { get; init; }
    public RuntimeEffectCleanupPolicy CleanupPolicy { get; init; }
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ExpiresUtc { get; init; }

    public HashSet<ulong> TargetSteamIds { get; } = new();
    public List<Entity> TargetEntities { get; } = new();
    public List<Entity> BuffEntities { get; } = new();
    public List<Entity> SpawnedWorldEntities { get; } = new();
}
