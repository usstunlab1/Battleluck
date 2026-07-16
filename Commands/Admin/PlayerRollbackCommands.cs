using BattleLuck.Services;
using BattleLuck.Services.Runtime;
using BattleLuck.Commands;
using Unity.Entities;
using VampireCommandFramework;

/// <summary>
/// Explicit player/event rollback surfaces. These restore BattleLuck's persisted
/// player snapshots; they do not claim to restore the dedicated server's native
/// world save, which remains owned by the V Rising SaveFileManager/host tooling.
/// </summary>
public static class PlayerRollbackCommands
{
    static readonly EventDeploymentAuditService Audit = new();

    public static void RollbackPlayer(ChatCommandContext ctx, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector) || selector.Equals("self", StringComparison.OrdinalIgnoreCase))
            selector = ctx.GetSenderCharacterEntity().GetSteamId().ToString();

        if (!TryResolveOnline(selector, out var player))
        {
            ctx.Reply($"❌ Player '{selector}' must be online for native rollback. Their persisted snapshot is not deleted.");
            return;
        }

        var result = RestoreOnlinePlayer(player, out var snapshot, out var wasActive);
        Audit.RecordStateRollback("player_rollback", player.GetSteamId().ToString(), result.Success, result.Error, restored: result.Success ? 1 : 0, skipped: 0);

        if (!result.Success)
        {
            ctx.Reply($"❌ Player rollback failed: {result.Error}");
            return;
        }

        ctx.Reply($"✅ Restored {player.GetPlayerName()}'s pre-event snapshot (zone {snapshot!.ZoneHash}, {(wasActive ? "active session exited" : "snapshot restored directly")}).");
        ctx.Reply("The consumed player snapshot is now cleared. A server/world backup is a separate operation.");
    }

    public static void RollbackAllEventPlayers(ChatCommandContext ctx, bool confirmed)
    {
        if (!confirmed)
        {
            ctx.Reply("⚠️ This restores every online player with a positive event snapshot. Offline snapshots remain untouched. Repeat `.ai rollback server players confirm` to proceed.");
            return;
        }

        var loadouts = BattleLuckPlugin.PlayerLoadouts;
        if (loadouts == null)
        {
            ctx.Reply("❌ Player loadout service is not initialized.");
            return;
        }

        var online = VRisingCore.GetOnlinePlayers()
            .Where(player => player.Exists() && player.IsPlayer())
            .ToDictionary(player => player.GetSteamId(), player => player);
        var eventSnapshots = loadouts.ListSnapshots()
            .Where(snapshot => snapshot.ZoneHash > 0)
            .ToList();

        var restored = 0;
        var failed = 0;
        var offline = 0;
        foreach (var snapshot in eventSnapshots)
        {
            if (!online.TryGetValue(ParseSteamId(snapshot.PlayerId), out var player) || !player.Exists())
            {
                offline++;
                continue;
            }

            var result = RestoreOnlinePlayer(player, out _, out _);
            if (result.Success)
                restored++;
            else
                failed++;
        }

        var success = failed == 0;
        Audit.RecordStateRollback("server_player_rollback", "all", success,
            failed == 0 ? null : $"{failed} player snapshot(s) failed to restore.", restored, offline + failed);
        ctx.Reply($"🛡️ Server player-state rollback: restored={restored}, offline/pending={offline}, failed={failed}.");
        ctx.Reply("This operation does not restore the V Rising world save. Offline player snapshots remain on disk for a later online rollback.");
    }

    public static void Status(ChatCommandContext ctx)
    {
        var loadouts = BattleLuckPlugin.PlayerLoadouts;
        if (loadouts == null)
        {
            ctx.Reply("❌ Player loadout service is not initialized.");
            return;
        }

        var snapshots = loadouts.ListSnapshots().Where(snapshot => snapshot.ZoneHash > 0).ToList();
        var onlineIds = VRisingCore.GetOnlinePlayers()
            .Where(player => player.Exists() && player.IsPlayer())
            .Select(player => player.GetSteamId())
            .ToHashSet();
        var online = snapshots.Count(snapshot => onlineIds.Contains(ParseSteamId(snapshot.PlayerId)));
        ctx.Reply($"📦 BattleLuck event snapshots: total={snapshots.Count}, online={online}, offline/pending={snapshots.Count - online}.");
        foreach (var snapshot in snapshots.Take(12))
            ctx.Reply($"- {snapshot.PlayerId} zone={snapshot.ZoneHash} saved={snapshot.Timestamp.ToLocalTime():g} name={snapshot.Name}");
        ctx.Reply("These are player snapshots, not full server/world save files.");
    }

    static OperationResult RestoreOnlinePlayer(Entity player, out PlayerSnapshot? snapshot, out bool wasActive)
    {
        snapshot = null;
        wasActive = false;
        var loadouts = BattleLuckPlugin.PlayerLoadouts;
        if (loadouts == null)
            return OperationResult.Fail("Player loadout service is not initialized.");

        var steamId = player.GetSteamId();
        snapshot = loadouts.GetSnapshot(steamId);
        if (snapshot == null)
            return OperationResult.Fail($"No persisted snapshot exists for player {steamId}.");
        if (snapshot.ZoneHash <= 0)
            return OperationResult.Fail($"Snapshot for player {steamId} is not an event snapshot (zone {snapshot.ZoneHash}).");

        wasActive = BattleLuckPlugin.Session?.ForceExitPlayer(steamId, player) == true;
        if (wasActive && loadouts.GetSnapshot(steamId) == null)
            return OperationResult.Ok();

        var restored = loadouts.Restore(player, snapshot.ZoneHash);
        if (!restored.Success)
            return restored;

        return OperationResult.Ok();
    }

    static bool TryResolveOnline(string selector, out Entity player)
    {
        player = Entity.Null;
        var online = VRisingCore.GetOnlinePlayers()
            .Where(candidate => candidate.Exists() && candidate.IsPlayer())
            .ToList();

        if (ulong.TryParse(selector, out var steamId))
        {
            player = online.FirstOrDefault(candidate => candidate.GetSteamId() == steamId);
            return player.Exists();
        }

        player = online.FirstOrDefault(candidate =>
            candidate.GetPlayerName().Equals(selector, StringComparison.OrdinalIgnoreCase) ||
            candidate.GetPlayerName().Contains(selector, StringComparison.OrdinalIgnoreCase));
        return player.Exists();
    }

    static ulong ParseSteamId(string value) => ulong.TryParse(value, out var id) ? id : 0;
}
