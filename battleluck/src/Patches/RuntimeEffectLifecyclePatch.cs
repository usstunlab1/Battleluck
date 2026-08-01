using BattleLuck.Services.Runtime;
using HarmonyLib;

namespace BattleLuck.Patches;

/// <summary>
/// Connects request-scoped effects to the existing SessionController lifecycle.
/// No second timer, session manager, or ECS system is created.
///
/// Player snapshot restoration already removes temporary player buffs. Session-
/// owned border/spawn/group effects must remain alive when one participant leaves,
/// so this patch cleans them only on timeout, explicit removal, session end, or
/// server shutdown.
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
