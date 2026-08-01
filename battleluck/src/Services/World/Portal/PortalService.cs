using BattleLuck.Models;
using Unity.Entities;
using Unity.Mathematics;

namespace BattleLuck.Services.Portal;

/// <summary>
/// Manages teleport portals between markers using the existing teleport API.
/// All portal.* actions dispatch through this service.
/// </summary>
public sealed class PortalService
{
    readonly object _lock = new();

    // Registered portals: portalId -> portal data
    readonly Dictionary<string, PortalEntry> _portals = new(StringComparer.OrdinalIgnoreCase);
    // Player return points: steamId -> position
    readonly Dictionary<ulong, ReturnPoint> _returnPoints = new();

    public sealed record PortalEntry(
        string PortalId,
        float3 SourcePosition,
        float3 DestinationPosition,
        string VisualPrefab,
        float InteractionRadius,
        string AccessPolicy,
        bool Enabled);

    public sealed record ReturnPoint(
        float3 Position,
        int ZoneHash,
        DateTime BoundAt);

    /// <summary>
    /// Create a teleport portal between two markers.
    /// </summary>
    public OperationResult Create(string portalId, float3 sourcePosition, float3 destinationPosition,
        string visualPrefab = "", float interactionRadius = 3f, string accessPolicy = "all")
    {
        lock (_lock)
        {
            if (_portals.ContainsKey(portalId))
                return OperationResult.Fail($"Portal '{portalId}' already exists.");

            _portals[portalId] = new PortalEntry(
                portalId, sourcePosition, destinationPosition,
                visualPrefab, interactionRadius, accessPolicy, true);

            return OperationResult.Ok();
        }
    }

    /// <summary>
    /// Enable a portal for use.
    /// </summary>
    public OperationResult Enable(string portalId)
    {
        return SetEnabled(portalId, true);
    }

    /// <summary>
    /// Disable a portal.
    /// </summary>
    public OperationResult Disable(string portalId)
    {
        return SetEnabled(portalId, false);
    }

    /// <summary>
    /// Teleport a player through a portal.
    /// </summary>
    public OperationResult Teleport(string portalId, ulong steamId, bool preserveVelocity = false, bool requireOutOfCombat = true)
    {
        lock (_lock)
        {
            if (!_portals.TryGetValue(portalId, out var portal))
                return OperationResult.Fail($"Portal '{portalId}' not found.");

            if (!portal.Enabled)
                return OperationResult.Fail($"Portal '{portalId}' is disabled.");

            var player = PlayerEntityHelper.GetEntityBySteamId(steamId);
            if (!player.Exists())
                return OperationResult.Fail($"Player {steamId} not found.");

            if (requireOutOfCombat && player.IsInCombat())
                return OperationResult.Fail("Player is in combat.");

            var teleportService = BattleLuckPlugin.Teleports;
            if (teleportService == null)
                return OperationResult.Fail("Teleport service is not initialized.");

            return teleportService.Teleport(player, portal.DestinationPosition);
        }
    }

    /// <summary>
    /// Store the player's return point for the current event run.
    /// </summary>
    public OperationResult BindReturnPoint(ulong steamId, float3 position, int zoneHash)
    {
        lock (_lock)
        {
            _returnPoints[steamId] = new ReturnPoint(position, zoneHash, DateTime.UtcNow);
            return OperationResult.Ok();
        }
    }

    /// <summary>
    /// Teleport the player back to their bound return point.
    /// </summary>
    public OperationResult ExecuteReturn(ulong steamId)
    {
        lock (_lock)
        {
            if (!_returnPoints.TryGetValue(steamId, out var point))
                return OperationResult.Fail("No return point bound.");

            var player = PlayerEntityHelper.GetEntityBySteamId(steamId);
            if (!player.Exists())
                return OperationResult.Fail($"Player {steamId} not found.");

            var teleportService = BattleLuckPlugin.Teleports;
            if (teleportService == null)
                return OperationResult.Fail("Teleport service is not initialized.");

            return teleportService.Teleport(player, point.Position);
        }
    }

    OperationResult SetEnabled(string portalId, bool enabled)
    {
        lock (_lock)
        {
            if (!_portals.TryGetValue(portalId, out var portal))
                return OperationResult.Fail($"Portal '{portalId}' not found.");

            _portals[portalId] = portal with { Enabled = enabled };
            return OperationResult.Ok();
        }
    }
}