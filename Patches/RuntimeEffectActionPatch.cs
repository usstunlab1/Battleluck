using BattleLuck.Models;
using BattleLuck.Services.Flow;
using BattleLuck.Services.Runtime;
using HarmonyLib;

namespace BattleLuck.Patches;

/// <summary>
/// Extends the existing action runtime without adding another executor. The
/// original FlowActionExecutor still owns parsing, logging, policy, and reports;
/// this patch supplies handlers for the request-scoped effect action family.
/// </summary>
[HarmonyPatch]
public static class RuntimeEffectActionPatch
{
    [HarmonyPatch(typeof(FlowActionExecutor), "IsRegisteredAction")]
    [HarmonyPostfix]
    static void IsRegisteredActionPostfix(string actionName, ref bool __result)
    {
        if (!__result && RuntimeEffectActionCatalog.IsRuntimeEffectAction(actionName))
            __result = true;
    }

    [HarmonyPatch(typeof(FlowActionExecutor), "ExecuteParsed")]
    [HarmonyPrefix]
    static bool ExecuteParsedPrefix(
        string actionName,
        Dictionary<string, string> p,
        FlowActionContext c,
        ref OperationResult __result)
    {
        if (!RuntimeEffectActionCatalog.IsRuntimeEffectAction(actionName))
            return true;

        __result = RuntimeEffectActionService.Execute(actionName, p, c);
        return false;
    }
}

/// <summary>
/// Keeps ActionManifestService aware of the runtime effect family so mode JSON
/// validation, AI action discovery, aliases, and approval metadata stay aligned
/// with the actual executor handler.
/// </summary>
[HarmonyPatch]
public static class RuntimeEffectCatalogPatch
{
    [HarmonyPatch(typeof(ActionManifestService), nameof(ActionManifestService.Reload))]
    [HarmonyPostfix]
    static void ReloadPostfix(ActionManifestService __instance) =>
        RuntimeEffectActionCatalog.EnsureInjected(__instance);

    [HarmonyPatch(typeof(ActionManifestService), nameof(ActionManifestService.Validate))]
    [HarmonyPrefix]
    static void ValidatePrefix(ActionManifestService __instance) =>
        RuntimeEffectActionCatalog.EnsureInjected(__instance);

    [HarmonyPatch(typeof(ActionManifestService), nameof(ActionManifestService.TryGetAction))]
    [HarmonyPrefix]
    static void TryGetActionPrefix(ActionManifestService __instance) =>
        RuntimeEffectActionCatalog.EnsureInjected(__instance);

    [HarmonyPatch(typeof(ActionManifestService), "get_Entries")]
    [HarmonyPrefix]
    static void EntriesPrefix(ActionManifestService __instance) =>
        RuntimeEffectActionCatalog.EnsureInjected(__instance);
}
