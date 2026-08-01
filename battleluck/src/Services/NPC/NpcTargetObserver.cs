using BattleLuck.Models;
using BattleLuck.Services.Runtime;
using Unity.Entities;

namespace BattleLuck.Services.Npc;

/// <summary>
/// Reads server-visible player state from ECS components and produces a
/// normalized PlayerObservation for the adaptive AI. Only depends on state
/// available to the server — no client input, animations, or player controls.
/// </summary>
public sealed class NpcTargetObserver
{
    public static NpcTargetObserver Instance { get; } = new();

    /// <summary>
    /// Build a PlayerObservation snapshot from the player entity, relative to the NPC entity.
    /// </summary>
    public PlayerObservation Observe(Entity playerEntity, Entity npcEntity)
    {
        if (!playerEntity.Exists())
            return new PlayerObservation();

        var steamId = playerEntity.GetSteamId();
        var npcPos = npcEntity.Exists() ? npcEntity.GetPosition() : float3.zero;
        var playerPos = playerEntity.GetPosition();
        var distance = npcEntity.Exists() ? math.distance(npcPos, playerPos) : 0f;

        var healthRatio = 1f;
        var isInCombat = false;
        var isCasting = false;
        var isDashing = false;
        var equippedWeapon = PrefabGUID.Empty;
        var weaponCategory = WeaponCategory.None;

        try
        {
            if (playerEntity.Has<Health>())
            {
                var health = playerEntity.Read<Health>();
                healthRatio = health.MaxHealth._Value > 0
                    ? health.Value / health.MaxHealth._Value
                    : 1f;
            }
        }
        catch { }

        // Weapon observation — route through WeaponSlot → server entity → prefab GUID
        try
        {
            if (playerEntity.Has<Equipment>())
            {
                var equipment = playerEntity.Read<Equipment>();
                var weaponSlotEntity = equipment.WeaponSlot.SlotEntity.GetEntityOnServer();
                equippedWeapon = weaponSlotEntity.Exists() ? weaponSlotEntity.GetPrefabGuid() : PrefabGUID.Empty;
                weaponCategory = ClassifyWeapon(equippedWeapon);
            }
        }
        catch { }

        // CastState is not available in the current reference assemblies.
        // Default to false until a version adapter provides this signal.

        // Dashing is not available in the current reference assemblies.
        // Default to false until a version adapter provides this signal.

        // ActiveAggroTargets is not available in the current reference assemblies.
        // Default to false until a version adapter provides this signal.

        // Determine movement direction relative to NPC
        var isMovingTowardNpc = false;
        var isMovingAwayFromNpc = false;

        try
        {
            if (playerEntity.Has<Velocity>())
            {
                var velocity = playerEntity.Read<Velocity>();
                var vel = velocity.Value;
                if (math.lengthsq(vel) > 0.01f)
                {
                    var dirToNpc = math.normalizesafe(npcPos - playerPos);
                    var dot = math.dot(math.normalizesafe(vel), dirToNpc);
                    isMovingTowardNpc = dot > 0.3f;
                    isMovingAwayFromNpc = dot < -0.3f;
                }
            }
        }
        catch { }

        // Read active buffs using EntityManager buffer API
        // BuffBuffer in this build exposes a `PrefabGuid` field (lowercase 'd' on Guid).
        var activeBuffs = new HashSet<PrefabGUID>();
        try
        {
            var em = VRisingCore.EntityManager;
            if (em.HasComponent<BuffBuffer>(playerEntity))
            {
                var buffBuffer = em.GetBuffer<BuffBuffer>(playerEntity);
                for (int i = 0; i < buffBuffer.Length && i < 16; i++)
                {
                    var buff = buffBuffer[i];
                    if (buff.PrefabGuid != PrefabGUID.Empty)
                        activeBuffs.Add(buff.PrefabGuid);
                }
            }
        }
        catch { }

        // AbilityEffectsBuffer is not available in the current reference assemblies.
        // Return empty recent-effects set until a version adapter provides this signal.
        var recentEffects = new HashSet<PrefabGUID>();

        return new PlayerObservation
        {
            SteamId = steamId,
            Position = playerPos,
            Velocity = playerPos - (npcEntity.Exists() ? playerEntity.GetPosition() : playerPos),
            DistanceToNpc = distance,
            HealthRatio = healthRatio,
            EquippedWeapon = equippedWeapon,
            WeaponCategory = weaponCategory,
            IsInCombat = isInCombat,
            IsCasting = isCasting,
            IsDashing = isDashing,
            IsMovingTowardNpc = isMovingTowardNpc,
            IsMovingAwayFromNpc = isMovingAwayFromNpc,
            ActiveBuffs = activeBuffs,
            RecentAbilityEffects = recentEffects
        };
    }

    static WeaponCategory ClassifyWeapon(PrefabGUID weapon)
    {
        var name = PrefabHelper.GetName(weapon) ?? "";
        if (name.Contains("bow", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("crossbow", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("pistol", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("rifle", StringComparison.OrdinalIgnoreCase))
            return WeaponCategory.Ranged;
        if (name.Contains("staff", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("wand", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("scepter", StringComparison.OrdinalIgnoreCase))
            return WeaponCategory.Magic;
        if (name.Contains("sword", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("axe", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("mace", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("spear", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("scythe", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("hammer", StringComparison.OrdinalIgnoreCase))
            return WeaponCategory.Melee;
        return WeaponCategory.None;
    }
}
