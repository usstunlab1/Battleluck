using BattleLuck.Services;
using BattleLuck.Commands;
using Unity.Entities;
using VampireCommandFramework;

/// <summary>
/// Per-player event rollback commands. These invoke the existing authoritative
/// rollback flow in PlayerStateController, which handles:
/// - Rollback via snapshot restore
/// - Snapshot deletion on success
/// - Snapshot retention on failure
/// </summary>
public static class PlayerRollbackCommands
{
    public static void RollbackPlayer(ChatCommandContext ctx, string selector)
    {
        if (string.IsNullOrWhiteSpace(selector) || selector.Equals("self", StringComparison.OrdinalIgnoreCase))
            selector = ctx.GetSenderCharacterEntity().GetSteamId().ToString();

        if (!TryResolveOnline(selector, out var player))
        {
            ctx.Reply($"❌ Player '{selector}' must be online for rollback.");
            return;
        }

        var steamId = player.GetSteamId();
        var playerState = BattleLuckPlugin.PlayerState;
        
        if (playerState == null)
        {
            ctx.Reply("❌ Player state controller is not initialized.");
            return;
        }

        if (!playerState.HasSnapshot(steamId))
        {
            ctx.Reply($"❌ No snapshot found for player {player.GetPlayerName()}.");
            return;
        }

        var restored = playerState.RestoreSnapshot(player, 0);
        if (!restored)
        {
            ctx.Reply($"❌ Player rollback failed: could not restore snapshot.");
            return;
        }

        playerState.ClearSnapshot(steamId);
        ctx.Reply($"✅ Rolled back {player.GetPlayerName()} and cleared the player snapshot.");
    }

    public static void RollbackAllEventPlayers(ChatCommandContext ctx, bool confirmed)
    {
        if (!confirmed)
        {
            ctx.Reply("⚠️ This restores every online player with an active event session. Repeat `.ai rollback server players confirm` to proceed.");
            return;
        }

        var playerState = BattleLuckPlugin.PlayerState;
        if (playerState == null)
        {
            ctx.Reply("❌ Player state controller is not initialized.");
            return;
        }

        var online = VRisingCore.GetOnlinePlayers()
            .Where(player => player.Exists() && player.IsPlayer())
            .ToList();

        var restored = 0;
        var failed = 0;

        foreach (var player in online)
        {
            var steamId = player.GetSteamId();
            if (!playerState.HasSnapshot(steamId))
            {
                failed++;
                continue;
            }

            var result = playerState.RestoreSnapshot(player, 0);
            if (result)
            {
                playerState.ClearSnapshot(steamId);
                restored++;
            }
            else
            {
                failed++;
            }
        }

        ctx.Reply($"🛡️ Server player-state rollback: restored={restored}, failed={failed}.");
    }

    public static void Status(ChatCommandContext ctx)
    {
        var playerState = BattleLuckPlugin.PlayerState;
        if (playerState == null)
        {
            ctx.Reply("❌ Player state controller is not initialized.");
            return;
        }

        var snapshots = playerState.ListSnapshots();
        if (snapshots.Count == 0)
        {
            ctx.Reply("No player snapshots are currently stored.");
            return;
        }

        ctx.Reply($"📊 Player snapshots stored: {snapshots.Count}");
        foreach (var snap in snapshots.Take(10))
        {
            var player = VRisingCore.GetOnlinePlayers()
                .FirstOrDefault(p => p.GetSteamId() == ulong.Parse(snap.PlayerId));
            var name = player.Exists() ? player.GetPlayerName() : snap.PlayerId;
            ctx.Reply($"- {name}: {snap.Inventory.Count} items, {snap.Buffs.Count} buffs, zone={snap.ZoneHash}");
        }
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
}
