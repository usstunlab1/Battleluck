using Unity.Entities;
using Unity.Mathematics;
using BattleLuck.Models;

namespace BattleLuck.Services.Teleport;

/// <summary>
/// Canonical player teleport boundary.
/// Supports position, player-to-player, and zone-based teleportation with validation.
/// </summary>
public sealed class TeleportService
{
    public OperationResult Teleport(Entity player, float3 destination)
    {
        // Backward-compatible wrapper used by existing services (e.g., PortalService)
        return TeleportToPosition(player, destination);
    }

    public OperationResult TeleportToPosition(Entity player, float3 destination)
    {
        if (!ValidatePlayer(player, out var error)) return OperationResult.Fail(error);
        if (!math.all(math.isfinite(destination))) return OperationResult.Fail("Teleport destination contains non-finite coordinates.");

        player.SetPosition(destination);
        return OperationResult.Ok();
    }

    public OperationResult TeleportToPlayer(Entity player, Entity targetPlayer)
    {
        if (!ValidatePlayer(player, out var error)) return OperationResult.Fail(error);
        if (!ValidatePlayer(targetPlayer, out var targetError)) return OperationResult.Fail($"Target: {targetError}");

        var destination = targetPlayer.GetPosition();
        player.SetPosition(destination);
        return OperationResult.Ok();
    }

    public OperationResult TeleportToZone(Entity player, int zoneHash)
    {
        if (!ValidatePlayer(player, out var error)) return OperationResult.Fail(error);

        // Zone lookup requires SessionController or ZoneDetectionSystem
        // For now, we delegate to SessionController's helper if available
        var session = BattleLuckPlugin.Session;
        if (session == null) return OperationResult.Fail("Session controller is not initialized.");

        var zone = session.GetZoneByHash(zoneHash);
        if (zone == null) return OperationResult.Fail($"Zone with hash {zoneHash} was not found.");

        player.SetPosition(zone.Position.ToFloat3());
        return OperationResult.Ok();
    }

    private static bool ValidatePlayer(Entity player, out string error)
    {
        error = "";
        if (!player.Exists())
        {
            error = "Entity does not exist.";
            return false;
        }
        if (!player.IsPlayer())
        {
            error = "Entity is not a player.";
            return false;
        }
        return true;
    }
}
