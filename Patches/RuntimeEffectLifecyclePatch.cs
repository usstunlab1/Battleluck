using BattleLuck.Services.Runtime;
using HarmonyLib;

namespace BattleLuck.Patches;

/// <summary>
/// Connects request-scoped effects to the existing SessionController lifecycle.
/// No second timer, session manager, or ECS system is created.
/// </summary>
[HarmonyPatch]
public static class RuntimeEffectLifecyclePatch
{
    [HarmonyPatch(typeof(global::SessionController), nameof(global::SessionController.Tick))]
    [HarmonyPostfix]
    static void TickPostfix()
    {
        RuntimeEffectActionService.TickAll();
    }

    [HarmonyPatch(typeof(global::SessionController), "ExecuteLeaveFlow")]
    [HarmonyPrefix]
    static void ExecuteLeaveFlowPrefix(
        ulong steamId,
        int zoneHash,
        object ____activeSessions)
    {
        if (RuntimeEffectActionService.TryGetSessionContext(____activeSessions, zoneHash, out var context) && context != null)
            RuntimeEffectActionService.CleanupPlayer(steamId, context.SessionId);
    }

    [HarmonyPatch(typeof(global::SessionController), "CleanupPlayerState")]
    [HarmonyPrefix]
    static void CleanupPlayerStatePrefix(ulong steamId, object session)
    {
        var context = session?.GetType().GetProperty("Context")?.GetValue(session) as BattleLuck.Models.GameModeContext;
        RuntimeEffectActionService.CleanupPlayer(steamId, context?.SessionId);
    }

    [HarmonyPatch(typeof(global::SessionController), "EndSession")]
    [HarmonyPrefix]
    static void EndSessionPrefix(int zoneHash, object ____activeSessions)
    {
        if (RuntimeEffectActionService.TryGetSessionContext(____activeSessions, zoneHash, out var context) && context != null)
            RuntimeEffectActionService.CleanupSession(context.SessionId);
    }

    [HarmonyPatch(typeof(global::SessionController), nameof(global::SessionController.Shutdown))]
    [HarmonyPrefix]
    static void ShutdownPrefix()
    {
        RuntimeEffectActionService.CleanupAll();
    }
}
