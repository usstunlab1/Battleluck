# BattleLuck — Development Guidelines

## Code Quality Standards

### Naming Conventions
- **Classes**: PascalCase, descriptive nouns — `FlowActionExecutor`, `BorderWallController`, `SessionController`
- **Interfaces**: `I` prefix — `IActionRuntime`, `IRuntimeCapabilities`, `IActionValidationPipeline`
- **Private fields**: `_camelCase` prefix — `_playerState`, `_activeSessions`, `_mainThreadQueue`
- **Static readonly fields**: PascalCase — `PenaltySpawn`, `DefaultModeDurationSeconds`, `MAX_WALLS`
- **Constants**: PascalCase for named constants, ALL_CAPS only for numeric limits — `const int MAX_WALLS = 256`
- **Action name strings**: `dot.separated.lowercase` — `"npc.follow"`, `"zone.buff.apply"`, `"snapshot.restore"`
- **Config/state keys**: `camelCase` strings — `"arenaSpawningRequested"`, `"manualShrink"`, `"freeBuildEnabled"`
- **Prefab fields**: `Category_Name_Variant` — `Item_Weapon_Sword_T09`, `Buff_General_Stun`, `CHAR_Skeleton_Warrior`

### File & Class Structure
- One primary class per file; companion types (records, small helpers) may coexist in the same file
- `sealed` on all concrete service classes — `public sealed class FlowActionExecutor`, `public sealed class BorderWallController`
- `readonly record struct` for small value types — `readonly record struct TrapState(...)`, `readonly record struct PendingArenaRespawn(...)`
- `public sealed class ActiveSession` as a companion to `SessionController` in the same file
- Explicit `using` statements at the top of each file even when GlobalUsings covers them (for clarity in complex files)
- Namespace matches folder path: `BattleLuck.Services.Flow`, `BattleLuck.Services.AI`, `BattleLuck.Core`

### Nullable & Safety
- Nullable reference types enabled project-wide; use `?` on all potentially-null references
- Null-conditional operator `?.` preferred over explicit null checks for chained calls: `BattleLuckPlugin.NpcService?.Tick(deltaSeconds)`
- Null-coalescing `??` for fallbacks: `Text(p, "kitId", config?.KitId ?? "bloodbath")`
- Guard clauses at method entry: check `entity.Exists()`, `steamId == 0`, `string.IsNullOrWhiteSpace()`

---

## Architectural Patterns

### OperationResult Pattern (universal return type)
Every action and service method returns `OperationResult` or `OperationResult<T>`:
```csharp
// Success
return OperationResult.Ok();
return OperationResult<T>.Ok(value);

// Failure with user-facing message
return OperationResult.Fail("NPC control service is not initialized.");

// Failure with help text for troubleshooting
return OperationResult.FailWithHelp(
    $"Action '{actionName}' is not registered in actions_catalog.json.",
    "Action not cataloged",
    "Add the action to actions_catalog.json before using it.");
```
Never throw exceptions for expected failures; only use exceptions for truly unexpected states.

### Main-Thread Dispatch Pattern
All Unity ECS / ProjectM mutations must run on the server main thread. Async work dispatches back via:
```csharp
// Fire-and-forget async work
_ = Task.Run(async () =>
{
    var response = await BattleLuckPlugin.AIAssistant.HandleDirectQuery(steamId, prompt);
    MainThreadDispatcher.Enqueue(() => SendSystemReferenceResult(player, response, scope));
});

// Enqueue from any thread
MainThreadDispatcher.Enqueue(() =>
{
    try { /* ECS mutation */ }
    catch (Exception ex) { BattleLuckLogger.Warning($"...{ex.Message}"); }
});
```

### ECS EntityCommandBuffer Pattern
For ECS mutations inside action execution, use `EcbHelper`:
```csharp
var ecb = EcbHelper.GetEcb();
if (ecb.Equals(default(EntityCommandBuffer))) return;
var e = ecb.CreateEntity();
ecb.AddComponent(e, new ZoneBuffApplyAction
{
    TargetEntity = target,
    ZoneHash = zoneHash,
    BuffPrefab = ToFixed64(prefabName),
    Duration = duration,
    SessionEntity = Entity.Null
});
```
Always wrap ECB dispatch in try/catch and log warnings on failure.

### Action String Format
Actions use a canonical string format: `actionName:key=value|key2=value2`
```csharp
// Parse
var (actionName, parameters) = ParseActionString(actionString);

// Build
var action = $"player.buff.apply:buffPrefab=Buff_General_Stun|duration={MatchStartStunSeconds}";

// Parameter helpers (always use these, never raw dictionary access)
static string Text(Dictionary<string, string> p, string key, string fallback)
static int Int(Dictionary<string, string> p, string key, int fallback)
static float Float(Dictionary<string, string> p, string key, float fallback)
static bool Bool(Dictionary<string, string> p, string key, bool fallback)
static string Required(Dictionary<string, string> p, string key)  // throws if missing
```

### State Dictionary Pattern
Session state is stored in `GameModeContext.State` as `Dictionary<string, object?>`. Use typed accessors:
```csharp
// Typed lazy-init state
static T GetState<T>(GameModeContext? ctx, string key, Func<T> factory)
{
    if (ctx == null) return factory();
    if (!ctx.State.TryGetValue(key, out var value) || value is not T typed)
    {
        typed = factory();
        ctx.State[key] = typed;
    }
    return typed;
}

// Usage
static Dictionary<string, DateTime> Timers(GameModeContext? ctx) =>
    GetState<Dictionary<string, DateTime>>(ctx, "timers", () => new(StringComparer.OrdinalIgnoreCase));
```

### Service Locator (Core.cs)
Services are accessed via `BattleLuckPlugin.*` static properties (forwarding to `Core.*`):
```csharp
// Access services
BattleLuckPlugin.NpcService?.Tick(deltaSeconds);
BattleLuckPlugin.PlayerLoadouts?.Apply(player, kitId);
BattleLuckPlugin.DeathPrevention?.Arm(player, charges, ...);

// Never instantiate services directly in action handlers; always use the plugin locator
```

### Staggered Spawning Pattern (BorderWallController)
For operations that could spike the server, use a queue + tick pattern:
```csharp
// Queue work
_pendingWalls.Enqueue(slot);

// Drain N items per tick
public bool TickSpawn()
{
    int toSpawn = Math.Min(Math.Max(1, _batchSize), _pendingWalls.Count);
    for (int i = 0; i < toSpawn; i++)
    {
        var pending = _pendingWalls.Dequeue();
        // ... spawn one entity
    }
    return _pendingWalls.Count > 0; // true = still spawning
}
```

### Tile Deduplication Pattern
Before spawning any tile entity, check occupancy and dedup:
```csharp
var tileKey = ToTileKey(position);
if (_knownOccupiedTiles.Contains(tileKey)) { skipped++; return; }
if (!_queuedTiles.Add(tileKey)) { deduped++; return; }
if (!EventTileReservationService.TryReserve(owner, ToTilePosition(pos), out _, out _))
{
    _queuedTiles.Remove(tileKey);
    skipped++;
    return;
}
```

---

## Semantic Patterns

### Prefab Resolution
Always use `PrefabHelper` for prefab lookups; never hardcode GUIDs in action handlers:
```csharp
// Preferred: deep lookup (name → live map → static catalog)
var prefab = PrefabHelper.GetPrefabGuidDeep(prefabName);
if (!prefab.HasValue)
    return OperationResult.Fail("Unknown prefab.");

// For boundary prefabs
if (PrefabHelper.TryGetValidPrefabGuidDeep(prefabName, out var guid))
    return guid;

// Numeric GUID fallback
if (int.TryParse(name, out var guid))
    return new PrefabGUID(guid);
```

### Prefabs.cs — Static Registry
`Prefabs` is the single source of truth for all PrefabGUID constants. Some GUIDs are resolved at runtime:
```csharp
// Hardcoded (stable across game versions)
public static readonly PrefabGUID Buff_General_Stun = new(-508086356);

// Runtime-resolved (call ResolveLiveBuffGuids() after PrefabHelper.ScanLivePrefabs())
public static PrefabGUID Buff_General_Ignite = new(-1576592033);
public static PrefabGUID Admin_Invulnerable_Buff = new(532440764);

// Placeholder (must be resolved; negative -123456789 family = invalid)
public static PrefabGUID Item_Research_Scroll_Tier1 = new(-123456789);
```
Always check `!= PrefabGUID.Empty` before using a runtime-resolved GUID.

### Entity Safety Checks
Always validate entities before use:
```csharp
if (!entity.Exists()) return OperationResult.Fail("Entity does not exist.");
if (!entity.Has<PlayerCharacter>()) return; // not a player
if (entity == Entity.Null) return;

// ECS component access pattern
if (entity.Has<Health>())
{
    entity.With((ref Health health) => { health.Value = health.MaxHealth._Value; });
}
```

### AI Provider Failover Pattern
AI calls use a priority chain: Llama → Cloudflare → Google → local fallback:
```csharp
private async Task<string?> GetChatCompletionWithFailoverAsync(List<ChatMessage> messages, float temperature, int maxTokens)
{
    // Try primary, then fallback providers
    var llamaResponse = await TryLlamaAsync(messages, temperature, maxTokens, "context");
    if (!string.IsNullOrWhiteSpace(llamaResponse)) return llamaResponse;
    // ... try next provider
    return BuildLocalFallbackResponse(query); // never return null to callers
}
```
Always detect and handle provider scope refusals with `IsProviderScopeRefusal()`.

### Logging Convention
Use `BattleLuckPlugin.Log*` for all plugin-level logging (also posts to Discord webhook):
```csharp
BattleLuckPlugin.LogInfo($"[FlowActionExecutor] {actionName} applied to {successes} NPC(s).");
BattleLuckPlugin.LogWarning($"[Session] Auto-enter failed for {EntityExtensions.FormatPlayer(steamId, playerEntity)}: {result.Error}");
BattleLuckPlugin.LogError($"[Session] TryInitializeCore failed: {ex}");

// For service-internal logging (no Discord forwarding)
BattleLuckLogger.Info("...");
BattleLuckLogger.Warning("...");
```
Log prefix format: `[ClassName] message`. Include relevant IDs (steamId, modeId, zoneHash, sessionId).

### Event Subscription Pattern
Subscribe in Initialize/SubscribeToEvents, unsubscribe in Shutdown/UnsubscribeFromEvents:
```csharp
public void Initialize()
{
    _zoneDetection.OnPlayerEnterZone += HandlePlayerWalkIntoZone;
    DeathHook.OnDeath += HandleDeath;
    GameEvents.OnModeEnded += HandleModeEndedCleanup;
}

public void Shutdown()
{
    _zoneDetection.OnPlayerEnterZone -= HandlePlayerWalkIntoZone;
    DeathHook.OnDeath -= HandleDeath;
    GameEvents.OnModeEnded -= HandleModeEndedCleanup;
}
```

### Tick Safety Pattern
Every service ticked from `ServerTick` is wrapped individually:
```csharp
try { Session.Tick(players, deltaSeconds); }
catch (Exception ex) { Log?.LogWarning($"[BattleLuck] Tick error in Session.Tick: {ex.Message}"); }

try { MainThreadDispatcher.ProcessQueue(); }
catch (Exception ex) { Log?.LogWarning($"[BattleLuck] Tick error in MainThreadDispatcher.ProcessQueue: {ex.Message}"); }
```
Never let one tick failure cascade to others.

### NativeArray Disposal Pattern
Always dispose Unity NativeArrays in a try/finally:
```csharp
var entities = query.ToEntityArray(Allocator.Temp);
try
{
    foreach (var entity in entities) { /* process */ }
}
finally
{
    if (entities.IsCreated) entities.Dispose();
    query.Dispose();
}
```

### Switch Expression for Enum/String Dispatch
Prefer switch expressions for clean dispatch:
```csharp
static NpcControlMode ParseNpcControlMode(string raw) =>
    (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "guard" or "defender" or "defend" => NpcControlMode.Guard,
        "chase" or "aggressive" or "attack" => NpcControlMode.Aggro,
        "follow" => NpcControlMode.Follow,
        _ => NpcControlMode.Idle
    };

// Tier lookup
public static PrefabGUID GetSwordForTier(int tier) => tier switch
{
    1 => Item_Weapon_Sword_T01,
    2 => Item_Weapon_Sword_T02,
    _ => Item_Weapon_Sword_T09,
};
```

### StringComparer.OrdinalIgnoreCase
All string dictionaries and comparisons use `OrdinalIgnoreCase`:
```csharp
static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
new HashSet<string>(StringComparer.OrdinalIgnoreCase)
selector.Equals("all", StringComparison.OrdinalIgnoreCase)
```

### float Parsing with InvariantCulture
All float parsing uses `InvariantCulture` to avoid locale issues:
```csharp
float.TryParse(value, System.Globalization.NumberStyles.Float,
    System.Globalization.CultureInfo.InvariantCulture, out var parsed)
```

---

## What NOT to Do

- Never call `entity.Destroy()` on entities you don't own; use `entity.DestroyWithReason()` for tracked event entities
- Never mutate ECS components from a background thread; always enqueue via `MainThreadDispatcher`
- Never add new actions without registering them in `actions_catalog.json` first
- Never bypass the approval pipeline for AI-proposed mutations
- Never use `Newtonsoft.Json`; the project uses `System.Text.Json` exclusively
- Never hardcode PrefabGUIDs in action handlers; use `PrefabHelper` or `Prefabs.*`
- Never overwrite existing config files on deploy; `ConfigLoader.EnsureDefaultsDeployed()` is additive only
- Never spawn castle geometry (walls, floors, tiles) without checking `EventGeometryMutationsEnabled` and `EventTileSafety`
- Never access `VRisingCore.EntityManager` before `VRisingCore.IsReady` is true
